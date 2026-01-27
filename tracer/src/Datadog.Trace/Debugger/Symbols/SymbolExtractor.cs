// <copyright file="SymbolExtractor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using Datadog.Trace.Debugger.Symbols.Model;
using Datadog.Trace.Logging;
using Datadog.Trace.Pdb;
using Datadog.Trace.VendoredMicrosoftCode.System;
using Datadog.Trace.VendoredMicrosoftCode.System.Buffers;
using Datadog.Trace.VendoredMicrosoftCode.System.Collections.Immutable;
using Datadog.Trace.VendoredMicrosoftCode.System.Reflection.Metadata;
using Datadog.Trace.VendoredMicrosoftCode.System.Reflection.Metadata.Ecma335;

namespace Datadog.Trace.Debugger.Symbols
{
    internal class SymbolExtractor : IDisposable
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(SymbolExtractor));
        private readonly string _assemblyPath;
        private bool _disposed;

        protected const int UnknownMethodStartLine = 0;
        protected const int UnknownMethodEndLine = 0;
        protected const int UnknownFieldAndArgLine = 0;
        protected const MethodAttributes StaticFinalVirtualMethod = System.Reflection.MethodAttributes.Static | System.Reflection.MethodAttributes.Final | System.Reflection.MethodAttributes.Virtual;

        protected SymbolExtractor(DatadogMetadataReader metadataReader, string assemblyPath)
        {
            _assemblyPath = assemblyPath;
            DatadogMetadataReader = metadataReader;
            MetadataReader = metadataReader.MetadataReader;
        }

        protected DatadogMetadataReader DatadogMetadataReader { get; }

        protected MetadataReader MetadataReader { get; }

        protected Dictionary<int, string> MethodAccess { get; } = new()
        {
            { 0x0001, "private" },
            { 0x0002, "private protected" },
            { 0x0003, "internal" },
            { 0x0004, "protected" },
            { 0x0005, "protected internal" },
            { 0x0006, "public" }
        };

        protected Dictionary<int, string> MethodAttributes { get; } = new()
        {
            { 0x0010, "static" },
            { 0x0020, "final" },
            { 0x0040, "virtual" },
            { 0x0060, "final virtual" }
        };

        protected Dictionary<int, string> FieldAttributes { get; } = new()
        {
            { 0x0010, "static" },
            { 0x0020, "readonly" },
            { 0x0030, "static readonly" },
            { 0x0040, "const" },
            { 0x0050, "static const" },
        };

        public static SymbolExtractor? Create(Assembly assembly)
        {
            try
            {
                var datadogMetadataReader = DatadogMetadataReader.CreatePdbReader(assembly);
                if (datadogMetadataReader == null)
                {
                    Log.Debug("DatadogMetadataReader is null for assembly {Assembly}", assembly.FullName);
                    return null;
                }

                if (datadogMetadataReader.MetadataReader.TypeDefinitions.Count == 0)
                {
                    Log.Debug("Could not find any type in assembly {Assembly}", assembly.FullName);
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

        internal bool TryGetAssemblySymbol(out Model.Scope assemblyScope)
        {
            assemblyScope = default;
            var assemblyNameHandle = MetadataReader.GetAssemblyDefinition().Name;
            if (assemblyNameHandle.IsNil)
            {
                return false;
            }

            var assemblyName = assemblyNameHandle.IsNil ? null : MetadataReader.GetString(assemblyNameHandle);
            assemblyScope = new Model.Scope
            {
                Name = assemblyName,
                ScopeType = ScopeType.Assembly,
                SourceFile = _assemblyPath,
            };

            return true;
        }

        internal IEnumerable<Model.Scope> GetClassSymbols()
        {
            foreach (var typeDefinitionHandle in MetadataReader.TypeDefinitions)
            {
                if (typeDefinitionHandle.IsNil)
                {
                    continue;
                }

                if (!TryGetClassSymbols(typeDefinitionHandle, out var classScope))
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

                if (!typeToExtract.Equals(typeDefinitionHandle.FullName(MetadataReader)))
                {
                    continue;
                }

                return TryGetClassSymbols(typeDefinitionHandle, out var classScope) ? classScope : null;
            }

            return null;
        }

        private bool TryGetClassSymbols(TypeDefinitionHandle typeDefinitionHandle, out Model.Scope classScope)
        {
            classScope = default;
            Model.Symbol[]? fieldSymbols = null;
            Model.Scope[]? scopes = null;

            try
            {
                var typeName = typeDefinitionHandle.FullName(MetadataReader);
                if (string.IsNullOrEmpty(typeName))
                {
                    return false;
                }

                if (DatadogMetadataReader.IsCompilerGeneratedAttributeDefinedOnType(MetadataTokens.GetToken(typeDefinitionHandle)))
                {
                    return false;
                }

                var type = MetadataReader.GetTypeDefinition(typeDefinitionHandle);

                if (type.IsInterfaceType())
                {
                    return false;
                }

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

                var classLanguageSpecifics = GetClassLanguageSpecifics(type);
                SourceLocationInfo linesAndSource = default;
                var allScopes = nestedClassesScopeIndex == 0 ? null : new Model.Scope[nestedClassesScopeIndex];
                if (allScopes == null)
                {
                    linesAndSource = GetClassSourceLocationInfo(null);
                    classScope = new Model.Scope
                    {
                        Name = typeName,
                        ScopeType = ScopeType.Class,
                        Symbols = fieldSymbols,
                        Scopes = allScopes,
                        StartLine = linesAndSource.StartLine,
                        EndLine = linesAndSource.EndLine,
                        SourceFile = linesAndSource.Path,
                        LanguageSpecifics = classLanguageSpecifics
                    };
                    return true;
                }

                if (methodsScopeIndex > 0)
                {
                    Array.Copy(scopes!, 0, allScopes!, 0, methodsScopeIndex);
                }

                if ((nestedClassesScopeIndex - methodsScopeIndex) > 0)
                {
                    int destIndex = methodsScopeIndex == 0 ? 0 : methodsScopeIndex;
                    Array.Copy(scopes!, destIndex, allScopes!, destIndex, nestedClassesScopeIndex - methodsScopeIndex);
                }

                linesAndSource = GetClassSourceLocationInfo(allScopes);
                classScope = new Model.Scope
                {
                    Name = typeName,
                    ScopeType = ScopeType.Class,
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
                    typeName = typeDefinitionHandle.FullName(MetadataReader);
                }
                catch
                {
                    // ignored
                }

                Log.Warning(e, "Error while trying to extract symbol info for type {Type}", typeName ?? "NA");
                return false;
            }
            finally
            {
                if (scopes != null)
                {
                    ArrayPool<Model.Scope>.Shared.Return(scopes, true);
                }
            }

            return true;
        }

        private SourceLocationInfo GetClassSourceLocationInfo(Model.Scope[]? allScopes)
        {
            int classStartLine = int.MaxValue;
            int classEndLine = 0;
            string? classSourceFile = null;
            if (allScopes == null)
            {
                return new SourceLocationInfo(startLine: UnknownMethodStartLine, endLine: UnknownMethodEndLine, path: classSourceFile);
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

                    for (int j = 0; j < scope.Scopes.Length; j++)
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

            return new SourceLocationInfo(startLine: classStartLine, endLine: classEndLine, path: classSourceFile);
        }

        private void PopulateNestedNotCompileGeneratedClassScope(ImmutableArray<TypeDefinitionHandle> nestedTypes, Model.Scope[] nestedClassScopes, ref int index)
        {
            for (var i = 0; i < nestedTypes.Length; i++)
            {
                Model.Scope nestedClassScope = default;
                try
                {
                    var typeHandle = nestedTypes[i];
                    if (typeHandle.IsNil)
                    {
                        continue;
                    }

                    if (DatadogMetadataReader.IsCompilerGeneratedAttributeDefinedOnType(MetadataTokens.GetToken(typeHandle)))
                    {
                        continue;
                    }

                    if (!TryGetClassSymbols(typeHandle, out nestedClassScope))
                    {
                        continue;
                    }

                    nestedClassScopes[index] = nestedClassScope;
                    index++;
                }
                catch (Exception e)
                {
                    Log.Warning(e, "Symbol extractor fail to get information for nested class {NestedClass}", nestedClassScope.Name ?? "Unknown");
                }
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
                SuperClasses = baseClassNames,
                IsPdbExist = DatadogMetadataReader.IsPdbExist
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
                if (fieldDef.Name.IsNil)
                {
                    continue;
                }

                var fieldTypeName = fieldDef.DecodeSignature(new TypeProvider(false), 0);
                if (string.IsNullOrEmpty(fieldTypeName))
                {
                    continue;
                }

                var fieldName = Datadog.Trace.VendoredMicrosoftCode.System.MemoryExtensions.AsSpan(MetadataReader.GetString(fieldDef.Name));
                if (fieldName.IsEmpty)
                {
                    continue;
                }

                if (fieldName[0] == '<')
                {
                    // properties
                    var endNameIndex = fieldName.IndexOf('>');
                    if (endNameIndex > 1)
                    {
                        fieldName = fieldName.Slice(1, endNameIndex - 1);
                    }
                }

                var fieldNameAsString = fieldName.ToString();
                if (string.IsNullOrEmpty(fieldNameAsString))
                {
                    continue;
                }

                var accessModifiers = (fieldDef.Attributes & System.Reflection.FieldAttributes.FieldAccessMask) > 0 ? new[] { MethodAccess[Convert.ToUInt16(fieldDef.Attributes & System.Reflection.FieldAttributes.FieldAccessMask)] } : null;
                var fieldAttributes = ((int)fieldDef.Attributes & 0x0070) > 0 ? new[] { FieldAttributes[Convert.ToUInt16((int)fieldDef.Attributes & 0x0070)] } : null;
                var ls = new LanguageSpecifics() { AccessModifiers = accessModifiers, Annotations = fieldAttributes };

                fieldSymbols[index] = new Symbol
                {
                    Name = fieldNameAsString,
                    Type = fieldTypeName,
                    SymbolType = ((fieldDef.Attributes & System.Reflection.FieldAttributes.Static) != 0) ? SymbolType.StaticField : SymbolType.Field,
                    Line = UnknownFieldAndArgLine,
                    LanguageSpecifics = ls
                };
                index++;
            }

            return fieldSymbols;
        }

        private void PopulateMethodScopes(TypeDefinition type, MethodDefinitionHandleCollection methods, Model.Scope[] classMethods, ref int index)
        {
            foreach (var methodDefHandle in methods)
            {
                Model.Scope methodScope = default;
                try
                {
                    if (methodDefHandle.IsNil)
                    {
                        continue;
                    }

                    var methodToken = MetadataTokens.GetToken(methodDefHandle);

                    if (DatadogMetadataReader.IsCompilerGeneratedAttributeDefinedOnMethod(methodToken))
                    {
                        continue;
                    }

                    if (!DatadogMetadataReader.HasMethodBody(methodToken))
                    {
                        continue;
                    }

                    var methodDef = MetadataReader.GetMethodDefinition(methodDefHandle);
                    if (!TryCreateMethodScope(type, methodDef, out methodScope))
                    {
                        continue;
                    }

                    if (methodScope.Scopes != null &&
                        methodScope is { StartLine: UnknownMethodStartLine, EndLine: UnknownMethodEndLine, SourceFile: null })
                    {
                        var sourcePdbInfo = GetMethodSourceLocationInfo(ref methodScope);
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
                catch (Exception e)
                {
                    var name = GetMethodName(methodScope.Name, methodDefHandle);
                    Log.Warning(e, "Symbol extractor fail to get information for method {Method}", name);
                }
            }

            string GetMethodName(string? name, MethodDefinitionHandle handle)
            {
                try
                {
                    return name ?? MetadataReader.GetString(MetadataReader.GetMethodDefinition(handle).Name);
                }
                catch
                {
                    return "Unknown";
                }
            }
        }

        private SourceLocationInfo GetMethodSourceLocationInfo(ref Model.Scope methodScope)
        {
            var startLine = int.MaxValue;
            var endLine = 0;
            int? startColumn = null;
            int? endColumn = null;
            string? sourceFile = null;
            if (methodScope.Scopes != null)
            {
                for (int i = 0; i < methodScope.Scopes.Length; i++)
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
            }

            if (startLine == int.MaxValue)
            {
                startLine = UnknownMethodStartLine;
            }

            if (endLine == 0)
            {
                endLine = UnknownMethodEndLine;
            }

            return new SourceLocationInfo(startLine: startLine, endLine: endLine, path: sourceFile, startColumn: startColumn, endColumn: endColumn);
        }

        protected virtual bool TryCreateMethodScope(TypeDefinition type, MethodDefinition method, out Model.Scope methodScope)
        {
            methodScope = default;

            if (method.Name.IsNil)
            {
                return false;
            }

            var methodName = MetadataReader.GetString(method.Name);
            if (string.IsNullOrEmpty(methodName))
            {
                return false;
            }

            // arguments
            var argsSymbol = GetArgsSymbol(method);

            // closures
            var closureScopes = GetClosureScopes(type, method);
            var methodAttributes = method.Attributes & StaticFinalVirtualMethod;
            var isAsyncMethod = DatadogMetadataReader.IsAsyncMethod(method.GetCustomAttributes());
            var methodLanguageSpecifics = new LanguageSpecifics
            {
                ReturnType = method.DecodeSignature(new TypeProvider(false), 0).ReturnType,
                AccessModifiers = (method.Attributes & System.Reflection.MethodAttributes.MemberAccessMask) > 0 ? new[] { MethodAccess[Convert.ToUInt16(method.Attributes & System.Reflection.MethodAttributes.MemberAccessMask)] } : null,
                Annotations = methodAttributes > 0 && isAsyncMethod ? new[] { MethodAttributes[Convert.ToUInt16(methodAttributes)], "async" } :
                              methodAttributes > 0 ? new[] { MethodAttributes[Convert.ToUInt16(methodAttributes)] } :
                              isAsyncMethod ? new[] { "async" } : null
            };

            methodScope = new Model.Scope
            {
                ScopeType = ScopeType.Method,
                Name = methodName,
                LanguageSpecifics = methodLanguageSpecifics,
                Symbols = argsSymbol,
                Scopes = closureScopes,
                SourceFile = null,
                StartLine = UnknownMethodStartLine,
                EndLine = UnknownMethodEndLine
            };

            return true;
        }

        private Model.Scope[]? GetClosureScopes(TypeDefinition typeDef, MethodDefinition methodDef)
        {
            Model.Scope[]? closureMethods = null;
            int index = 0;
            try
            {
                var nestedTypes = typeDef.GetNestedTypes();
                var methods = typeDef.GetMethods();
                // Heuristic: closure/state-machine generated methods tend to scale with the number of methods (async methods, lambdas).
                // We intentionally over-estimate a bit to reduce pool growth.
                long estimatedCapacity = (methods.Count * 2L) + (nestedTypes.Length * 4L);
                if (estimatedCapacity < 16)
                {
                    estimatedCapacity = 16;
                }

                if (estimatedCapacity > int.MaxValue)
                {
                    estimatedCapacity = int.MaxValue;
                }

                closureMethods = ArrayPool<Model.Scope>.Shared.Rent((int)estimatedCapacity);

                for (int i = 0; i < nestedTypes.Length; i++)
                {
                    if (!DatadogMetadataReader.IsCompilerGeneratedAttributeDefinedOnType(MetadataTokens.GetToken(nestedTypes[i])))
                    {
                        continue;
                    }

                    var nestedType = MetadataReader.GetTypeDefinition(nestedTypes[i]);
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

                    if (!DatadogMetadataReader.IsCompilerGeneratedAttributeDefinedOnMethod(MetadataTokens.GetToken(methodHandle)))
                    {
                        continue;
                    }

                    var currentMethod = MetadataReader.GetMethodDefinition(methodHandle);
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
            }
            finally
            {
                if (closureMethods != null)
                {
                    // Clear only the populated portion to avoid keeping references alive via the pool
                    Array.Clear(closureMethods, 0, index);
                    ArrayPool<Model.Scope>.Shared.Return(closureMethods);
                }
            }

            void PopulateClosureMethod(MethodDefinition generatedMethod, TypeDefinition ownerType)
            {
                if (TryCreateMethodScopeForGeneratedMethod(methodDef, generatedMethod, ownerType, out var closureMethodScope))
                {
                    // This can exceed our initial heuristic, so grow the pooled buffer on-demand to avoid dropping closure methods.
                    if (index >= closureMethods!.Length)
                    {
                        var newBuffer = ArrayPool<Model.Scope>.Shared.Rent(closureMethods.Length * 2);
                        Array.Copy(closureMethods, 0, newBuffer, 0, index);

                        // Clear only the used portion
                        Array.Clear(closureMethods, 0, index);
                        ArrayPool<Model.Scope>.Shared.Return(closureMethods);

                        closureMethods = newBuffer;
                    }

                    closureMethods![index] = closureMethodScope;
                    index++;
                }
            }
        }

        protected virtual bool TryCreateMethodScopeForGeneratedMethod(MethodDefinition method, MethodDefinition generatedMethod, TypeDefinition nestedType, out Model.Scope methodScope)
        {
            methodScope = default;
            return false;
        }

        private Symbol[]? GetArgsSymbol(MethodDefinition method)
        {
            var parameters = method.GetParameters();
            if (parameters.Count == 0)
            {
                if (method.IsStaticMethod())
                {
                    return null;
                }

                return [new Symbol { Name = "this", SymbolType = SymbolType.Arg, Line = UnknownFieldAndArgLine, Type = method.GetDeclaringType().FullName(MetadataReader) }];
            }

            var argsSymbol = method.IsStaticMethod() ? new Symbol[parameters.Count] : new Symbol[parameters.Count + 1]; // 'this'
            int index = 0;
            var methodParameterTypes = method.DecodeSignature(new TypeProvider(false), 0).ParameterTypes;
            var typesIndex = 0;
            if (!method.IsStaticMethod())
            {
                var thisSymbol = new Symbol { Name = "this", SymbolType = SymbolType.Arg, Line = UnknownFieldAndArgLine, Type = method.GetDeclaringType().FullName(MetadataReader) };
                argsSymbol[0] = thisSymbol;
                index++;
            }

            foreach (var parameterHandle in parameters)
            {
                if (parameterHandle.IsNil)
                {
                    continue;
                }

                var parameterDef = MetadataReader.GetParameter(parameterHandle);
                var parameterName = MetadataReader.GetString(parameterDef.Name);
                if (string.IsNullOrEmpty(parameterName))
                {
                    continue;
                }

                if (parameterDef.IsHiddenThis())
                {
                    continue;
                }

                argsSymbol[index] = new Symbol
                {
                    Name = parameterName,
                    SymbolType = SymbolType.Arg,
                    Line = UnknownFieldAndArgLine,
                    Type = typesIndex < methodParameterTypes.Length ? methodParameterTypes[typesIndex] : "Unknown"
                };
                index++;
                typesIndex++;
            }

            return argsSymbol;
        }

        private string[]? GetClassBaseClassNames(TypeDefinition type)
        {
            EntityHandle baseType = type.BaseType;
            if (baseType.IsNil)
            {
                return null;
            }

            var baseTypeName = type.BaseType.FullName(MetadataReader);
            var objectTypeFullName = typeof(object).FullName;
            if (baseTypeName.Equals(objectTypeFullName))
            {
                return [baseTypeName];
            }

            var classNames = new List<string> { baseTypeName };

            while (!(baseType = baseType.GetBaseTypeHandle(MetadataReader)).IsNil && !baseTypeName.Equals(objectTypeFullName))
            {
                classNames.Add(baseTypeName);
                baseTypeName = baseType.FullName(MetadataReader);
            }

            return classNames.ToArray();
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
                interfaceNames[index] = MetadataReader.GetInterfaceImplementation(interfaceHandle).Interface.FullName(MetadataReader);
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

        internal readonly record struct SourceLocationInfo
        {
            internal readonly int StartLine;
            internal readonly int EndLine;
            internal readonly string? Path;
            internal readonly int? StartColumn;
            internal readonly int? EndColumn;

            public SourceLocationInfo(int startLine, int endLine, string? path, int? startColumn, int? endColumn)
            {
                StartLine = startLine;
                EndLine = endLine;
                Path = path;
                StartColumn = startColumn;
                EndColumn = endColumn;
            }

            public SourceLocationInfo(int startLine, int endLine, string? path)
                : this()
            {
                StartLine = startLine;
                EndLine = endLine;
                Path = path;
            }
        }
    }
}
