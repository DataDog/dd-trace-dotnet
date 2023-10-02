// <copyright file="SymbolExtractor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using Datadog.System.Buffers;
using Datadog.System.Reflection.Metadata;
using Datadog.System.Reflection.Metadata.Ecma335;
using Datadog.System.Reflection.PortableExecutable;
using Datadog.Trace.Debugger.Symbols.Model;
using Datadog.Trace.Logging;
using Datadog.Trace.Pdb;
using Datadog.Trace.Vendors.dnlib.DotNet.Pdb.Portable;
using Datadog.Trace.Vendors.dnlib.DotNet.Pdb.Symbols;
using FieldAttributes = System.Reflection.FieldAttributes;
using MethodAttributes = System.Reflection.MethodAttributes;
using TypeAttributes = System.Reflection.TypeAttributes;

namespace Datadog.Trace.Debugger.Symbols
{
    internal class SymbolExtractor : IDisposable
    {
        protected const string Unknown = "UNKNOWN";
        protected const int UnknownStartLine = 0;
        protected const int UnknownEndLine = 0;
        protected const int UnknownEndLineEntireScope = int.MaxValue;
        private const MethodAttributes StaticFinalVirtual = MethodAttributes.Static | MethodAttributes.Final | MethodAttributes.Virtual;
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(SymbolExtractor));
        private readonly Dictionary<int, string> _methodAccess = new()
        {
            { 0x0001, "Private" },
            { 0x0002, "FamANDAssem" },
            { 0x0003, "Assembly" },
            { 0x0004, "Family" },
            { 0x0005, "FamORAssem" },
            { 0x0006, "Public" }
        };

        private readonly Dictionary<int, string> _methodAttributes = new()
        {
            { 0x0010, "Static" },
            { 0x0020, "Final" },
            { 0x0040, "Virtual" },
            { 0x0060, "Final Virtual" },
        };

        private bool _disposed;

        protected SymbolExtractor(MetadataReader metadataReader)
        {
            MetadataReader = metadataReader;
        }

        ~SymbolExtractor() => Dispose(false);

        protected MetadataReader MetadataReader { get; }

        public static SymbolExtractor? Create(Assembly assembly)
        {
            try
            {
                MetadataReader metadataReader;
                var pdbReader = DatadogPdbReader.CreatePdbReader(assembly);
                if (pdbReader == null)
                {
                    Log.Warning("Could not create a PDB reader file for assembly {Assembly}", assembly.FullName);
                    var peReader = new Datadog.System.Reflection.PortableExecutable.PEReader(File.OpenRead(assembly.Location), PEStreamOptions.PrefetchMetadata);
                    metadataReader = peReader.GetMetadataReader(MetadataReaderOptions.Default, MetadataStringDecoder.DefaultUTF8);
                }
                else
                {
                    metadataReader = pdbReader.MetadataReader;
                }

                if (metadataReader.TypeDefinitions.Count == 0)
                {
                    Log.Debug("Could not found any type in assembly {Assembly}", assembly.FullName);
                    return null;
                }

                return pdbReader != null ? new SymbolPdbExtractor(pdbReader) : new SymbolExtractor(metadataReader);
            }
            catch (Exception e)
            {
                Log.Error(e, "Error while creating instance of SymbolExtractor with {Assembly}", assembly.FullName);
                return null;
            }
        }

        internal Model.Scope GetAssemblySymbol()
        {
            var assemblyName = MetadataReader.GetString(MetadataReader.GetAssemblyDefinition().Name);
            if (string.IsNullOrEmpty(assemblyName))
            {
                assemblyName = Unknown;
            }

            string? assemblyPath = null; // path??
            if (string.IsNullOrEmpty(assemblyPath))
            {
                assemblyPath = Unknown;
            }

            var assemblyScope = new Model.Scope
            {
                Name = assemblyName,
                ScopeType = SymbolType.Assembly,
                SourceFile = assemblyPath,
            };

            return assemblyScope;
        }

        internal IEnumerable<Model.Scope?> GetClassSymbols()
        {
            foreach (var typeDefinition in MetadataReader.TypeDefinitions)
            {
                var typeDef = MetadataReader.GetTypeDefinition(typeDefinition);
                if (!TryGetClassSymbols(typeDef, out var classScope))
                {
                    continue;
                }

                yield return classScope;
            }
        }

        internal Model.Scope? GetClassSymbols(string typeToExtract)
        {
            foreach (var typeDefinitionHandle in MetadataReader.TypeDefinitions)
            {
                var type = MetadataReader.GetTypeDefinition(typeDefinitionHandle);
                if (!typeToExtract.Equals(MetadataReader.GetString(type.Name))) // full name?
                {
                    continue;
                }

                return TryGetClassSymbols(type, out var classScope) ? classScope : null;
            }

            return null;
        }

        private bool TryGetClassSymbols(TypeDefinition type, [NotNullWhen(true)] out Model.Scope? classScope)
        {
            classScope = null;
            Model.Symbol[]? fieldSymbols = null;
            Model.Scope[]? methodScopes = null;
            Model.Scope[]? nestedClassScopes = null;
            Model.Scope[]? allScopes = null;
            try
            {
                var fields = type.GetFields();
                var methods = type.GetMethods();
                if (fields.Count == 0 && methods.Count == 0)
                {
                    return false;
                }

                fieldSymbols = GetFieldSymbols(fields, type);

                methodScopes = GetMethodScopes(type, methods, out var classStartLine, out var classEndLine, out var typeSourceFile);

                nestedClassScopes = GetNestedNotCompileGeneratedClassScope(type);

                var overallCount = (methodScopes?.Length ?? 0) + (nestedClassScopes?.Length ?? 0);

                allScopes = overallCount == 0 ? null : ArrayPool<Model.Scope>.Shared.Rent(overallCount);

                if (methodScopes?.Length > 0)
                {
                    Array.Copy(methodScopes, 0, allScopes!, 0, methodScopes.Length);
                }

                if (nestedClassScopes?.Length > 0)
                {
                    Array.Copy(nestedClassScopes, 0, allScopes!, (methodScopes?.Length - 1) ?? 0, nestedClassScopes.Length);
                }

                var classLanguageSpecifics = GetClassLanguageSpecifics(type);

                classScope = new Model.Scope
                {
                    Name = GetTypeFullName(type),
                    ScopeType = SymbolType.Class,
                    StartLine = classStartLine,
                    EndLine = classEndLine,
                    Symbols = fieldSymbols,
                    Scopes = allScopes, // null entries
                    SourceFile = typeSourceFile ?? Unknown,
                    LanguageSpecifics = classLanguageSpecifics
                };
            }
            catch (Exception e)
            {
                string? typeName = null;
                try
                {
                    typeName = GetTypeFullName(type);
                }
                catch
                {
                    // ignored
                }

                Log.Warning(e, "Error while trying to extract symbol info for type {Type}", typeName ?? Unknown);
                return false;
            }
            finally
            {
                if (fieldSymbols != null)
                {
                    ArrayPool<Model.Symbol>.Shared.Return(fieldSymbols);
                }

                if (methodScopes != null)
                {
                    ArrayPool<Model.Scope>.Shared.Return(methodScopes);
                }

                if (nestedClassScopes != null)
                {
                    ArrayPool<Model.Scope>.Shared.Return(nestedClassScopes);
                }

                if (allScopes != null)
                {
                    ArrayPool<Model.Scope>.Shared.Return(allScopes);
                }
            }

            return true;
        }

        private Model.Scope[]? GetNestedNotCompileGeneratedClassScope(TypeDefinition type)
        {
            var nestedTypes = type.GetNestedTypes();
            if (nestedTypes.Length <= 0)
            {
                return null;
            }

            var nestedClassScopes = ArrayPool<Model.Scope>.Shared.Rent(nestedTypes.Length);
            try
            {
                for (var i = 0; i < nestedTypes.Length; i++)
                {
                    var nestedType = MetadataReader.GetTypeDefinition(nestedTypes[i]);

                    if (IsCompilerGeneratedAttributeDefined(nestedType.GetCustomAttributes()))
                    {
                        continue;
                    }

                    if (!TryGetClassSymbols(nestedType, out var nestedClassScope))
                    {
                        continue;
                    }

                    nestedClassScopes[i] = nestedClassScope.Value;
                }

                return nestedClassScopes;
            }
            catch
            {
                if (nestedClassScopes != null)
                {
                    ArrayPool<Model.Scope>.Shared.Return(nestedClassScopes, true);
                }

                return null;
            }
        }

        protected bool IsCompilerGeneratedAttributeDefined(CustomAttributeHandleCollection attributes)
        {
            const string compilerGeneratedAttributeName = "System.Runtime.CompilerServices.CompilerGeneratedAttribute";
            foreach (var attributeHandle in attributes)
            {
                var attribute = MetadataReader.GetCustomAttribute(attributeHandle);
                var attributeName = GetAttributeName(attribute);
                if (attributeName?.Equals(compilerGeneratedAttributeName) == true)
                {
                    return true;
                }
            }

            return false;
        }

        private string? GetAttributeName(CustomAttribute customAttribute)
        {
            var constructorHandle = customAttribute.Constructor;
            if (constructorHandle.Kind != HandleKind.MethodDefinition)
            {
                return null;
            }

            var constructorDefHandle = (MethodDefinitionHandle)customAttribute.Constructor;
            var constructorDefinition = MetadataReader.GetMethodDefinition(constructorDefHandle);
            var attributeTypeHandle = constructorDefinition.GetDeclaringType();
            var attributeType = MetadataReader.GetTypeDefinition(attributeTypeHandle);
            return MetadataReader.GetString(attributeType.Name);
        }

        private LanguageSpecifics GetClassLanguageSpecifics(TypeDefinition type)
        {
            var interfaceNames = GetClassInterfaceNames(type);

            var baseClassNames = GetClassBaseClassNames(type);

            var accessModifiers = (type.Attributes & TypeAttributes.VisibilityMask);
            var classLanguageSpecifics = new LanguageSpecifics
            {
                AccessModifiers = accessModifiers > 0 ? new[] { accessModifiers.ToString() } : null,
                Interfaces = interfaceNames,
                SuperClasses = baseClassNames
            };
            return classLanguageSpecifics;
        }

        private Symbol[]? GetFieldSymbols(FieldDefinitionHandleCollection fieldsCollection, TypeDefinition parentType)
        {
            if (fieldsCollection is not { Count: > 0 })
            {
                return null;
            }

            var typeSymbols = ArrayPool<Model.Symbol>.Shared.Rent(fieldsCollection.Count);
            int index = 0;
            foreach (var fieldDefHandle in fieldsCollection)
            {
                var fieldDef = MetadataReader.GetFieldDefinition(fieldDefHandle);
                typeSymbols[index] = new Symbol
                {
                    Name = MetadataReader.GetString(fieldDef.Name),
                    SymbolType = ((fieldDef.Attributes & FieldAttributes.Static) != 0) ? SymbolType.StaticField : SymbolType.Field,
                    Type = MetadataReader.GetString(parentType.Name),
                    Line = UnknownStartLine
                };
                index++;
            }

            return typeSymbols;
        }

        private Model.Scope[]? GetMethodScopes(TypeDefinition type, MethodDefinitionHandleCollection methods, out int classStartLine, out int classEndLine, out string? typeSourceFile)
        {
            classStartLine = UnknownStartLine;
            classEndLine = UnknownEndLineEntireScope;
            typeSourceFile = null;

            if (methods is not { Count: > 0 })
            {
                return null;
            }

            var classMethods = ArrayPool<Model.Scope>.Shared.Rent(methods.Count);
            int index = 0;

            try
            {
                foreach (var methodDefHandle in methods)
                {
                    var methodDef = MetadataReader.GetMethodDefinition(methodDefHandle);
                    if (IsCompilerGeneratedAttributeDefined(methodDef.GetCustomAttributes()))
                    {
                        continue;
                    }

                    var methodScope = CreateMethodScope(type, methodDef);
                    index++;
                    typeSourceFile ??= methodScope.SourceFile;

                    if (methodScope.StartLine > UnknownStartLine &&
                        (classStartLine == UnknownStartLine || methodScope.StartLine < classStartLine))
                    {
                        // not really first line but good enough for inner scopes (fields doesn't has line number anyway)
                        classStartLine = methodScope.StartLine;
                    }

                    if (methodScope.EndLine is > UnknownEndLine and < UnknownEndLineEntireScope &&
                        (classEndLine == UnknownEndLineEntireScope || methodScope.EndLine > classEndLine))
                    {
                        classEndLine = methodScope.EndLine + 1;
                    }

                    classMethods[index] = methodScope;
                }

                return classMethods;
            }
            catch
            {
                if (classMethods != null)
                {
                    ArrayPool<Model.Scope>.Shared.Return(classMethods, true);
                }

                return null;
            }
        }

        protected virtual Model.Scope CreateMethodScope(TypeDefinition type, MethodDefinition method)
        {
            // arguments
            var argsSymbol = GetArgsSymbol(method);

            // closures
            var closureScopes = GetClosureScopes(type, method);
            var methodAttributes = method.Attributes & StaticFinalVirtual;
            var methodLanguageSpecifics = new LanguageSpecifics
            {
                ReturnType = method.DecodeSignature(new TypeProvider(), 0).ReturnType.Name,
                AccessModifiers = (method.Attributes & MethodAttributes.MemberAccessMask) > 0 ? new[] { _methodAccess[Convert.ToUInt16(method.Attributes & MethodAttributes.MemberAccessMask)] } : null,
                Annotations = methodAttributes > 0 ? new[] { _methodAttributes[Convert.ToUInt16(methodAttributes)] } : null
            };

            var methodScope = new Model.Scope
            {
                ScopeType = SymbolType.Method,
                Name = MetadataReader.GetString(method.Name),
                LanguageSpecifics = methodLanguageSpecifics,
                Symbols = argsSymbol,
                Scopes = closureScopes,
                SourceFile = Unknown,
                StartLine = UnknownStartLine,
                EndLine = UnknownEndLineEntireScope
            };

            return methodScope;
        }

        private Model.Scope[]? GetClosureScopes(TypeDefinition typeDef, MethodDefinition methodDef)
        {
            var nestedTypes = typeDef.GetNestedTypes();

            var closureMethods = ArrayPool<Model.Scope>.Shared.Rent(typeDef.GetMethods().Count * 2);
            int index = 0;

            for (int i = 0; i < nestedTypes.Length; i++)
            {
                var nestedType = MetadataReader.GetTypeDefinition(nestedTypes[i]);
                if (!IsCompilerGeneratedAttributeDefined(nestedType.GetCustomAttributes()))
                {
                    continue;
                }

                var generatedMethods = nestedType.GetMethods();
                foreach (var generatedMethodHandle in generatedMethods)
                {
                    var generatedMethodDef = MetadataReader.GetMethodDefinition(generatedMethodHandle);
                    PopulateClosureMethod(generatedMethodDef, nestedType);
                }
            }

            foreach (var methodHandle in typeDef.GetMethods())
            {
                var currentMethod = MetadataReader.GetMethodDefinition(methodHandle);
                if (!IsCompilerGeneratedAttributeDefined(currentMethod.GetCustomAttributes()))
                {
                    continue;
                }

                PopulateClosureMethod(currentMethod, typeDef);
            }

            return closureMethods;

            void PopulateClosureMethod(MethodDefinition generatedMethod, TypeDefinition ownerType)
            {
                var closureMethodScope = CreateMethodScopeForGeneratedMethod(methodDef, generatedMethod, ownerType);
                if (closureMethodScope.HasValue)
                {
                    closureMethods[index] = closureMethodScope.Value;
                    index++;
                    if (index >= closureMethods.Length)
                    {
                        // expand?
                    }
                }
            }
        }

        private CustomDebugInfoAsyncAndClosure GetAsyncAndClosureCustomDebugInfo(MethodDefinitionHandle methodHandle)
        {
            Guid encLocalSlotMap = new Guid("755F52A8-91C5-45BE-B4B8-209571E552BD");
            Guid asyncMethodSteppingInformationBlob = new Guid("54FD2AC5-E925-401A-9C2A-F94F171072F8");
            // Guid asyncMethodSteppingInformationBlob = new Guid("AsyncMethodSteppingInformationBlob");
            var debugInformationHandle = default(CustomDebugInformationHandle);
            CustomDebugInfoAsyncAndClosure cdiAsyncAndClosure = default;
            foreach (var handle in MetadataReader.GetCustomDebugInformation(methodHandle))
            {
                var customDebugInformation = MetadataReader.GetCustomDebugInformation(handle);
                var cdiGuid = MetadataReader.GetGuid(customDebugInformation.Kind);
                if (cdiGuid == encLocalSlotMap)
                {
                    cdiAsyncAndClosure.LocalSlot = true;
                }

                if (cdiGuid == asyncMethodSteppingInformationBlob)
                {
                    debugInformationHandle = handle;
                }
            }

            if (!debugInformationHandle.IsNil)
            {
                var debugInformation = MetadataReader.GetCustomDebugInformation(debugInformationHandle);
                var blobReader = MetadataReader.GetBlobReader(debugInformation.Value);

                // Read the blob to get the async method custom debug information.
                cdiAsyncAndClosure.KickoffMethodName = ReadKickoffMethodNameFromBlob(blobReader);
            }

            return cdiAsyncAndClosure;
        }

        private string ReadKickoffMethodNameFromBlob(BlobReader blobReader)
        {
            if (blobReader.RemainingBytes == 0)
            {
                return Unknown;
            }

            var skip = blobReader.ReadInt32();
            while (blobReader.RemainingBytes > 0)
            {
                int yieldOffset = blobReader.ReadInt32();
                int catchHandlerOffset = blobReader.ReadInt32();

                while (true)
                {
                    int catchHandlerIndex = blobReader.ReadInt32();
                    if (catchHandlerIndex < 0)
                    {
                        break; // end of this AsyncCatchHandlerMapping sequence
                    }
                }

                if (yieldOffset == -1)
                {
                    // Kick-off method
                    int methodToken = blobReader.ReadInt32();
                    EntityHandle kickOffMethodHandle = MetadataTokens.EntityHandle(methodToken);

                    if (kickOffMethodHandle.Kind == HandleKind.MethodDefinition)
                    {
                        var kickOffMethod = MetadataReader.GetMethodDefinition((MethodDefinitionHandle)kickOffMethodHandle);
                        var kickOffMethodName = MetadataReader.GetString(kickOffMethod.Name);

                        return kickOffMethodName;
                    }

                    if (kickOffMethodHandle.Kind == HandleKind.MemberReference)
                    {
                        var memberRef = MetadataReader.GetMemberReference((MemberReferenceHandle)kickOffMethodHandle);
                        var memberName = MetadataReader.GetString(memberRef.Name);

                        return memberName;
                    }

                    return Unknown;
                }
            }

            return Unknown;
        }

        private Model.Scope? CreateMethodScopeForGeneratedMethod(MethodDefinition method, MethodDefinition generatedMethod, TypeDefinition nestedType)
        {
            var cdi = GetAsyncAndClosureCustomDebugInfo(generatedMethod.Handle);
            if (cdi.IsNil)
            {
                return null;
            }

            // if (!generatedMethod.CustomDebugInfos.Any(di => di.Kind is PdbCustomDebugInfoKind.AsyncMethod or PdbCustomDebugInfoKind.EditAndContinueLocalSlotMap))
            // {
            //    return null;
            // }

            // if (generatedMethod.CustomDebugInfos.FirstOrDefault(cdi => cdi is PdbAsyncMethodCustomDebugInfo) is PdbAsyncMethodCustomDebugInfo asyncMethodCustomDebugInfo)
            if (cdi.KickoffMethodName != null)
            {
                if (!MetadataReader.GetString(method.Name).Equals(cdi.KickoffMethodName))
                {
                    return null;
                }
            }
            else
            {
                var generatedMethodName = MetadataReader.GetString(generatedMethod.Name);
                if (generatedMethodName[0] != '<')
                {
                    return null;
                }

                var notGeneratedMethodName = generatedMethodName.Substring(1, generatedMethodName.IndexOf('>') - 1);
                if (!MetadataReader.GetString(method.Name).Equals(notGeneratedMethodName))
                {
                    return null;
                }
            }

            var closureMethodScope = CreateMethodScope(nestedType, generatedMethod);
            closureMethodScope.Name = MetadataReader.GetString(method.Name);
            closureMethodScope.ScopeType = SymbolType.Closure;
            return closureMethodScope;
        }

        private Symbol[]? GetArgsSymbol(MethodDefinition method)
        {
            var parameters = method.GetParameters();
            if (parameters.Count <= 0 ||
                (parameters.Count == 1 && MetadataReader.GetParameter(parameters.First()).SequenceNumber == -2 /* hidden this */))
            {
                return null;
            }

            var argsSymbol = ArrayPool<Symbol>.Shared.Rent(parameters.Count);
            int index = 0;

            foreach (var parameterHandle in parameters)
            {
                var parameterDef = MetadataReader.GetParameter(parameterHandle);
                argsSymbol[index] = new Symbol
                {
                    Name = MetadataReader.GetString(parameterDef.Name),
                    SymbolType = SymbolType.Arg,
                    Line = UnknownStartLine
                };
                index++;
            }

            var methodSig = method.DecodeSignature(new TypeProvider(), 0);
            for (int i = 0; i < argsSymbol.Length; i++)
            {
                argsSymbol[i].Type = methodSig.ParameterTypes[i].Name;
            }

            return argsSymbol;
        }

        protected Symbol[]? ConcatMethodSymbols(Symbol[]? argSymbols, Symbol[]? localSymbols, int localSymbolsCount)
        {
            var argsCount = argSymbols?.Length ?? 0;
            var symbolsCount = argsCount + localSymbolsCount;
            if (symbolsCount <= 0)
            {
                return null;
            }

            var methodSymbols = new Symbol[symbolsCount];

            if (argsCount > 0)
            {
                Array.Copy(argSymbols!, 0, methodSymbols, 0, argsCount);
            }

            if (localSymbolsCount > 0)
            {
                Array.Copy(localSymbols!, 0, methodSymbols, argsCount, localSymbolsCount);
            }

            return methodSymbols;
        }

        private string[]? GetClassBaseClassNames(TypeDefinition type)
        {
            string[]? baseClassNames = null;
            var baseType = MetadataReader.GetTypeDefinition((TypeDefinitionHandle)type.BaseType); // can be also type ref or type spec
            var baseTypeName = GetTypeFullName(baseType);
            var objectTypeFullName = typeof(object).FullName;
            if (!baseTypeName.Equals(objectTypeFullName))
            {
                var classNames = ListCache<string>.AllocList();
                while (!baseTypeName.Equals(objectTypeFullName))
                {
                    classNames.Add(baseTypeName);
                    baseType = MetadataReader.GetTypeDefinition((TypeDefinitionHandle)baseType.BaseType); // can be also type ref or type spec
                }

                baseClassNames = ListCache<string>.FreeAndToArray(ref classNames);
            }

            return baseClassNames;
        }

        private string GetTypeFullName(TypeDefinition typeDef)
        {
            var typeName = MetadataReader.GetString(typeDef.Name);
            var @namespace = MetadataReader.GetString(typeDef.Namespace);
            return $"{@namespace}.{typeName}";
        }

        private string[]? GetClassInterfaceNames(TypeDefinition type)
        {
            var interfaces = type.GetInterfaceImplementations();
            if (interfaces.Count <= 0)
            {
                return null;
            }

            var interfaceNames = new string[interfaces.Count];
            int index = 0;
            foreach (var interfaceHandle in interfaces)
            {
                interfaceNames[index] =
                    MetadataReader.
                        GetString(
                            MetadataReader.GetTypeDefinition(
                                (TypeDefinitionHandle)MetadataReader.GetInterfaceImplementation(interfaceHandle).Interface).Name); // can be also type ref or type spec
                index++;
            }

            return interfaceNames;
        }

        protected IList<SymbolScope> GetAllScopes(SymbolMethod method)
        {
            var result = new List<SymbolScope>();
            RetrieveAllNestedScopes(method.RootScope, result);
            return result;
        }

        private void RetrieveAllNestedScopes(SymbolScope? scope, List<SymbolScope> result)
        {
            // Recursively extract all nested scopes in method
            if (scope == null)
            {
                return;
            }

            result.Add(scope);
            foreach (var innerScope in scope.Children)
            {
                RetrieveAllNestedScopes(innerScope, result);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                // _metadataReader?.Dispose();
            }

            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private record struct CustomDebugInfoAsyncAndClosure
        {
            public bool LocalSlot { get; set; }

            public string? KickoffMethodName { get; set; }

            public bool IsNil => LocalSlot == false && KickoffMethodName == null;
        }
    }
}
