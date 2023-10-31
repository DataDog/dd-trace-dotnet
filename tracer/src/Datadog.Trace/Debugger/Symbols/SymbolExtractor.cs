// <copyright file="SymbolExtractor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Datadog.Trace.Debugger.Symbols.Model;
using Datadog.Trace.Logging;
using Datadog.Trace.Pdb;
using Datadog.Trace.VendoredMicrosoftCode.System.Buffers;
using Datadog.Trace.VendoredMicrosoftCode.System.Collections.Immutable;
using Datadog.Trace.VendoredMicrosoftCode.System.Reflection.Metadata;
using static Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.AdoNetConstants;

namespace Datadog.Trace.Debugger.Symbols
{
    internal class SymbolExtractor : IDisposable
    {
        private readonly string _assemblyPath;
        protected const string Unknown = "UNKNOWN";
        protected const int UnknownMethodStartLine = 0;
        protected const int UnknownMethodEndLine = 0;
        protected const int UnknownFieldAndArgLine = 0;
        protected const int UnknownLocalLine = int.MaxValue;
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

        protected SymbolExtractor(DatadogMetadataReader metadataReader, string assemblyPath)
        {
            _assemblyPath = assemblyPath;
            DatadogMetadataReader = metadataReader;
            MetadataReader = metadataReader.MetadataReader;
        }

        protected DatadogMetadataReader DatadogMetadataReader { get; }

        protected MetadataReader MetadataReader { get; }

        public static SymbolExtractor? Create(Assembly assembly)
        {
            try
            {
                var datadogMetadataReader = DatadogMetadataReader.CreatePdbReader(assembly);
                if (datadogMetadataReader.MetadataReader.TypeDefinitions.Count == 0)
                {
                    Log.Debug("Could not found any type in assembly {Assembly}", assembly.FullName);
                    return null;
                }

                return datadogMetadataReader.IsPdbExist ? new SymbolPdbExtractor(datadogMetadataReader, assembly.Location) : new SymbolExtractor(datadogMetadataReader, assembly.Location);
            }
            catch (Exception e)
            {
                Log.Error(e, "Error while creating instance of SymbolExtractor with {Assembly}", assembly.FullName);
                return null;
            }
        }

        internal Model.Scope GetAssemblySymbol()
        {
            var assemblyNameHandle = MetadataReader.GetAssemblyDefinition().Name;
            var assemblyName = assemblyNameHandle.IsNil ? Unknown : MetadataReader.GetString(assemblyNameHandle);
            var assemblyScope = new Model.Scope
            {
                Name = assemblyName,
                ScopeType = SymbolType.Assembly,
                SourceFile = string.IsNullOrEmpty(_assemblyPath) ? Unknown : _assemblyPath,
            };

            return assemblyScope;
        }

        internal IEnumerable<Model.Scope?> GetClassSymbols()
        {
            foreach (var typeDefinitionHandle in MetadataReader.TypeDefinitions)
            {
                if (typeDefinitionHandle.IsNil)
                {
                    continue;
                }

                var typeDefinition = MetadataReader.GetTypeDefinition(typeDefinitionHandle);
                if (!TryGetClassSymbols(typeDefinition, out var classScope))
                {
                    continue;
                }

                yield return classScope;
            }
        }

        /// <summary>
        /// For testing
        /// </summary>
        internal Model.Scope? GetClassSymbols(string typeToExtract)
        {
            foreach (var typeDefinitionHandle in MetadataReader.TypeDefinitions)
            {
                if (typeDefinitionHandle.IsNil)
                {
                    continue;
                }

                var type = MetadataReader.GetTypeDefinition(typeDefinitionHandle);
                if (!typeToExtract.Equals(type.FullName(MetadataReader)))
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
            Model.Scope[]? scopes = null;
            try
            {
                var fields = type.GetFields();
                if (fields.Count > 0)
                {
                    fieldSymbols = GetFieldSymbols(fields, type);
                }

                var methods = type.GetMethods();
                var nestedTypes = type.GetNestedTypes();
                int scopesBufferLength = methods.Count + nestedTypes.Length;
                scopes = ArrayPool<Model.Scope>.Shared.Rent(scopesBufferLength);
                int methodsScopeIndex = 0;
                int nestedClassesScopeIndex = 0;
                if (methods.Count > 0)
                {
                    PopulateMethodScopes(type, methods, scopes, ref methodsScopeIndex);
                }

                nestedClassesScopeIndex = methodsScopeIndex;
                if (nestedTypes.Length > 0)
                {
                    PopulateNestedNotCompileGeneratedClassScope(nestedTypes, scopes, ref nestedClassesScopeIndex);
                }

                var allScopes = nestedClassesScopeIndex == 0 ? null : new Model.Scope[nestedClassesScopeIndex];

                if (methodsScopeIndex > 0)
                {
                    Array.Copy(scopes!, 0, allScopes!, 0, methodsScopeIndex);
                }

                if ((nestedClassesScopeIndex - methodsScopeIndex) > 0)
                {
                    int destIndex = methodsScopeIndex == 0 ? 0 : methodsScopeIndex;
                    Array.Copy(scopes!, 0, allScopes!, destIndex, nestedClassesScopeIndex - methodsScopeIndex);
                }

                var classLanguageSpecifics = GetClassLanguageSpecifics(type);
                var linesAndSource = GetClassSourcePdbInfo(allScopes);
                classScope = new Model.Scope
                {
                    Name = type.FullName(MetadataReader),
                    ScopeType = SymbolType.Class,
                    Symbols = fieldSymbols,
                    Scopes = allScopes,
                    StartLine = linesAndSource.StartLine,
                    EndLine = linesAndSource.EndLine,
                    SourceFile = linesAndSource.Path,
                    LanguageSpecifics = classLanguageSpecifics
                };
            }
            catch (Exception e)
            {
                string? typeName = null;
                try
                {
                    typeName = type.FullName(MetadataReader);
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
                if (scopes != null)
                {
                    ArrayPool<Model.Scope>.Shared.Return(scopes);
                }
            }

            return true;
        }

        private SourcePdbInfo GetClassSourcePdbInfo(Model.Scope[]? allScopes)
        {
            int classStartLine = int.MaxValue;
            int classEndLine = 0;
            string? classSourceFile = null;
            if (allScopes == null)
            {
                return new SourcePdbInfo { StartLine = classStartLine, EndLine = classEndLine, Path = classSourceFile };
            }

            for (int i = 0; i < allScopes.Length; i++)
            {
                var scope = allScopes[i];
                if (classStartLine > scope.StartLine && scope.StartLine > 0)
                {
                    classStartLine = scope.StartLine;
                }

                if (classEndLine < scope.EndLine)
                {
                    classEndLine = scope.EndLine;
                }

                classSourceFile ??= scope.SourceFile;
            }

            if (classStartLine == int.MaxValue)
            {
                for (int i = 0; i < allScopes.Length; i++)
                {
                    var scope = allScopes[i];
                    if (scope.Scopes == null)
                    {
                        continue;
                    }

                    for (int j = 0; j < scope.Scopes.Count; j++)
                    {
                        var innerScope = scope.Scopes[j];
                        if (classStartLine > innerScope.StartLine)
                        {
                            classStartLine = innerScope.StartLine;
                        }

                        if (classEndLine < innerScope.EndLine)
                        {
                            classEndLine = innerScope.EndLine;
                        }

                        classSourceFile ??= innerScope.SourceFile;
                    }
                }
            }

            if (classStartLine == int.MaxValue)
            {
                classStartLine = UnknownMethodStartLine;
            }

            if (classEndLine == 0)
            {
                classEndLine = UnknownMethodEndLine;
            }

            return new SourcePdbInfo { StartLine = classStartLine, EndLine = classEndLine, Path = classSourceFile };
        }

        private void PopulateNestedNotCompileGeneratedClassScope(ImmutableArray<TypeDefinitionHandle> nestedTypes, Model.Scope[] nestedClassScopes, ref int index)
        {
            for (var i = 0; i < nestedTypes.Length; i++)
            {
                var typeHandle = nestedTypes[i];
                if (typeHandle.IsNil)
                {
                    continue;
                }

                var nestedType = MetadataReader.GetTypeDefinition(typeHandle);

                if (IsCompilerGeneratedAttributeDefined(nestedType.GetCustomAttributes()))
                {
                    continue;
                }

                if (!TryGetClassSymbols(nestedType, out var nestedClassScope))
                {
                    continue;
                }

                nestedClassScopes[index] = nestedClassScope.Value;
                index++;
            }
        }

        protected bool IsCompilerGeneratedAttributeDefined(CustomAttributeHandleCollection attributes)
        {
            const string compilerGeneratedAttributeName = "System.Runtime.CompilerServices.CompilerGeneratedAttribute";
            foreach (var attributeHandle in attributes)
            {
                if (attributeHandle.IsNil)
                {
                    continue;
                }

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
            if (constructorHandle.IsNil)
            {
                return null;
            }

            switch (constructorHandle.Kind)
            {
                case HandleKind.MemberReference:
                    {
                        var memberRef = MetadataReader.GetMemberReference((MemberReferenceHandle)constructorHandle);
                        var attributeTypeHandle = memberRef.Parent;
                        if (attributeTypeHandle.IsNil)
                        {
                            return null;
                        }

                        return GetTypeFullName(attributeTypeHandle);
                    }

                case HandleKind.MethodDefinition:
                    {
                        var constructorDefinition = MetadataReader.GetMethodDefinition((MethodDefinitionHandle)constructorHandle);
                        var attributeTypeHandle = constructorDefinition.GetDeclaringType();
                        if (attributeTypeHandle.IsNil)
                        {
                            return null;
                        }

                        var attributeType = MetadataReader.GetTypeDefinition(attributeTypeHandle);
                        return attributeType.FullName(MetadataReader);
                    }

                default:
                    return null;
            }
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

        private Symbol[] GetFieldSymbols(FieldDefinitionHandleCollection fieldsCollection, TypeDefinition parentType)
        {
            int index = 0;
            Model.Symbol[] fieldSymbols = new Model.Symbol[fieldsCollection.Count];
            foreach (var fieldDefHandle in fieldsCollection)
            {
                var fieldDef = MetadataReader.GetFieldDefinition(fieldDefHandle);
                var fieldType = fieldDef.DecodeSignature(new TypeProvider(), 0);
                var fieldName = fieldDef.Name.IsNil ? null : MetadataReader.GetString(fieldDef.Name);
                fieldSymbols[index] = new Symbol
                {
                    Name = fieldName,
                    Type = fieldType.Name,
                    SymbolType = ((fieldDef.Attributes & FieldAttributes.Static) != 0) ? SymbolType.StaticField : SymbolType.Field,
                    Line = UnknownFieldAndArgLine
                };
                index++;
            }

            return fieldSymbols;
        }

        private void PopulateMethodScopes(TypeDefinition type, MethodDefinitionHandleCollection methods, Model.Scope[] classMethods, ref int index)
        {
            foreach (var methodDefHandle in methods)
            {
                if (methodDefHandle.IsNil)
                {
                    continue;
                }

                var methodDef = MetadataReader.GetMethodDefinition(methodDefHandle);
                if (IsCompilerGeneratedAttributeDefined(methodDef.GetCustomAttributes()))
                {
                    continue;
                }

                var methodScope = CreateMethodScope(type, methodDef, default);
                if (methodScope.Scopes != null &&
                    methodScope is { StartLine: UnknownMethodStartLine, EndLine: UnknownMethodEndLine, SourceFile: null })
                {
                    var sourcePdbInfo = GetMethodSourcePdbInfo(methodScope);
                    methodScope.StartLine = sourcePdbInfo.StartLine;
                    methodScope.EndLine = sourcePdbInfo.EndLine;
                    methodScope.SourceFile = sourcePdbInfo.Path;
                    if (sourcePdbInfo.EndColumn > 0)
                    {
                        var ls = new LanguageSpecifics
                        {
                            Annotations = methodScope.LanguageSpecifics?.Annotations,
                            AccessModifiers = methodScope.LanguageSpecifics?.AccessModifiers,
                            ReturnType = methodScope.LanguageSpecifics?.ReturnType,
                            StartColumn = sourcePdbInfo.StartColumn,
                            EndColumn = sourcePdbInfo.EndColumn,
                        };

                        methodScope.LanguageSpecifics = ls;
                    }
                }

                classMethods[index] = methodScope;
                index++;
            }
        }

        private SourcePdbInfo GetMethodSourcePdbInfo(Model.Scope methodScope)
        {
            var startLine = int.MaxValue;
            var endLine = 0;
            int? startColumn = null;
            int? endColumn = null;
            string? sourceFile = null;
            for (int i = 0; i < methodScope.Scopes.Count; i++)
            {
                var scope = methodScope.Scopes[i];
                if (startLine > scope.StartLine && scope.StartLine > 0)
                {
                    startLine = scope.StartLine;
                }

                if (endLine < scope.EndLine)
                {
                    endLine = scope.EndLine;
                }

                sourceFile ??= scope.SourceFile;

                if (!scope.LanguageSpecifics.HasValue)
                {
                    continue;
                }

                if (startColumn == null || startColumn > scope.LanguageSpecifics.Value.StartColumn)
                {
                    startColumn = scope.LanguageSpecifics.Value.StartColumn;
                }

                if (endColumn == null || endColumn < scope.LanguageSpecifics.Value.EndColumn)
                {
                    endColumn = scope.LanguageSpecifics.Value.EndColumn;
                }
            }

            if (startLine == int.MaxValue)
            {
                startLine = UnknownMethodStartLine;
            }

            if (endLine == 0)
            {
                endLine = UnknownMethodEndLine;
            }

            return new SourcePdbInfo { StartLine = startLine, EndLine = endLine, Path = sourceFile, StartColumn = startColumn, EndColumn = endColumn };
        }

        protected virtual Model.Scope CreateMethodScope(TypeDefinition type, MethodDefinition method, DatadogMetadataReader.CustomDebugInfoAsyncAndClosure debugInfo)
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
                SourceFile = null,
                StartLine = UnknownMethodStartLine,
                EndLine = UnknownMethodEndLine
            };

            return methodScope;
        }

        private Model.Scope[]? GetClosureScopes(TypeDefinition typeDef, MethodDefinition methodDef)
        {
            Model.Scope[]? closureMethods = null;
            try
            {
                var nestedTypes = typeDef.GetNestedTypes();
                var methods = typeDef.GetMethods();
                closureMethods = ArrayPool<Model.Scope>.Shared.Rent(methods.Count + (nestedTypes.Length * 2));
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
                        if (generatedMethodHandle.IsNil)
                        {
                            continue;
                        }

                        var generatedMethodDef = MetadataReader.GetMethodDefinition(generatedMethodHandle);
                        PopulateClosureMethod(generatedMethodDef, nestedType);
                    }
                }

                foreach (var methodHandle in methods)
                {
                    if (methodHandle.IsNil || methodHandle == methodDef.Handle)
                    {
                        continue;
                    }

                    var currentMethod = MetadataReader.GetMethodDefinition(methodHandle);
                    if (!IsCompilerGeneratedAttributeDefined(currentMethod.GetCustomAttributes()))
                    {
                        continue;
                    }

                    PopulateClosureMethod(currentMethod, typeDef);
                }

                if (index == 0)
                {
                    return null;
                }

                var closureScopes = new Model.Scope[index];
                for (int i = 0; i < index; i++)
                {
                    closureScopes[i] = closureMethods[i];
                }

                return closureScopes;

                void PopulateClosureMethod(MethodDefinition generatedMethod, TypeDefinition ownerType)
                {
                    var closureMethodScope = CreateMethodScopeForGeneratedMethod(methodDef, generatedMethod, ownerType);
                    if (closureMethodScope.HasValue)
                    {
                        if (index < closureMethods.Length)
                        {
                            closureMethods[index] = closureMethodScope.Value;
                            index++;
                        }
                        else
                        {
                            Log.Warning("Not enough space for all closure methods");
                        }
                    }
                }
            }
            finally
            {
                if (closureMethods != null)
                {
                    ArrayPool<Model.Scope>.Shared.Return(closureMethods);
                }
            }
        }

        private Model.Scope? CreateMethodScopeForGeneratedMethod(MethodDefinition method, MethodDefinition generatedMethod, TypeDefinition nestedType)
        {
            if (method.Name.IsNil || generatedMethod.Name.IsNil)
            {
                return null;
            }

            var cdi = DatadogMetadataReader.GetAsyncAndClosureCustomDebugInfo(generatedMethod.Handle);
            if (cdi.IsNil)
            {
                return null;
            }

            string? methodName = null;
            if (!cdi.StateMachineHoistedLocal)
            {
                var generatedMethodName = MetadataReader.GetString(generatedMethod.Name);
                if (generatedMethodName[0] != '<')
                {
                    return null;
                }

                var notGeneratedMethodName = Datadog.Trace.VendoredMicrosoftCode.System.MemoryExtensions.AsSpan(generatedMethodName, 1, generatedMethodName.IndexOf('>') - 1);
                methodName = MetadataReader.GetString(method.Name);
                if (!methodName.Equals(notGeneratedMethodName.ToString()))
                {
                    return null;
                }
            }
            else if (method.Handle.RowId == cdi.StateMachineKickoffMethod.RowId)
            {
                var kickoffDef = MetadataReader.GetMethodDefinition(cdi.StateMachineKickoffMethod);
                methodName = MetadataReader.GetString(kickoffDef.Name);
            }
            else
            {
                return null;
            }

            var closureMethodScope = CreateMethodScope(nestedType, generatedMethod, cdi);
            closureMethodScope.Name = methodName;
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

            var argsSymbol = new Symbol[parameters.Count];
            int index = 0;

            foreach (var parameterHandle in parameters)
            {
                var parameterDef = MetadataReader.GetParameter(parameterHandle);
                argsSymbol[index] = new Symbol
                {
                    Name = MetadataReader.GetString(parameterDef.Name),
                    SymbolType = SymbolType.Arg,
                    Line = UnknownFieldAndArgLine
                };
                index++;
            }

            var methodSig = method.DecodeSignature(new TypeProvider(), 0);
            for (int i = 0; i < argsSymbol.Length; i++)
            {
                if (argsSymbol[i].Name == null)
                {
                    break;
                }

                argsSymbol[i].Type = methodSig.ParameterTypes[i].Name;
            }

            return argsSymbol;
        }

        protected Symbol[]? ConcatMethodSymbols(Symbol[]? argSymbols, Symbol[]? localSymbols)
        {
            var localSymbolsCount = localSymbols?.Length ?? 0;
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
            EntityHandle? baseType = type.BaseType;
            if (baseType.Value.IsNil)
            {
                return null;
            }

            var baseTypeName = GetTypeFullName(type.BaseType);
            var objectTypeFullName = typeof(object).FullName;
            if (baseTypeName.Equals(objectTypeFullName))
            {
                return new[] { baseTypeName };
            }

            var classNames = new List<string>();
            classNames.Add(baseTypeName);

            while ((baseType = GetBaseTypeHandle(baseType.Value)).HasValue && !baseTypeName.Equals(objectTypeFullName))
            {
                classNames.Add(baseTypeName);
                baseTypeName = GetTypeFullName(baseType.Value);
            }

            return classNames.ToArray();
        }

        private string GetTypeFullName(EntityHandle typeHandle)
        {
            if (typeHandle.IsNil)
            {
                return Unknown;
            }

            switch (typeHandle)
            {
                case { Kind: HandleKind.TypeDefinition }:
                    {
                        var typeDef = MetadataReader.GetTypeDefinition((TypeDefinitionHandle)typeHandle);
                        return typeDef.FullName(MetadataReader);
                    }

                case { Kind: HandleKind.TypeReference }:
                    {
                        var typeRef = MetadataReader.GetTypeReference((TypeReferenceHandle)typeHandle);
                        return typeRef.FullName(MetadataReader);
                    }

                case { Kind: HandleKind.TypeSpecification }:
                    {
                        var typeSpec = MetadataReader.GetTypeSpecification((TypeSpecificationHandle)typeHandle);
                        return typeSpec.FullName();
                    }

                default:
                    {
                        Log.Warning("Unknown type handle {Kind}", typeHandle.Kind);
                        return Unknown;
                    }
            }
        }

        private EntityHandle? GetBaseTypeHandle(EntityHandle typeHandle)
        {
            switch (typeHandle)
            {
                case { Kind: HandleKind.TypeDefinition }:
                    {
                        var typeDef = MetadataReader.GetTypeDefinition((TypeDefinitionHandle)typeHandle);
                        return typeDef.BaseType;
                    }

                case { Kind: HandleKind.TypeReference }:
                    {
                        var typeRef = MetadataReader.GetTypeReference((TypeReferenceHandle)typeHandle);
                        return typeRef.ResolutionScope.Kind == HandleKind.TypeReference ? typeRef.ResolutionScope : null;
                    }

                default:
                    {
                        return null;
                    }
            }
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
                interfaceNames[index] = GetTypeFullName(MetadataReader.GetInterfaceImplementation(interfaceHandle).Interface);
                index++;
            }

            return interfaceNames;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            DatadogMetadataReader.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        internal record struct SourcePdbInfo
        {
            internal int StartLine;
            internal int EndLine;
            internal string? Path;
            internal int? StartColumn;
            internal int? EndColumn;
        }
    }
}
