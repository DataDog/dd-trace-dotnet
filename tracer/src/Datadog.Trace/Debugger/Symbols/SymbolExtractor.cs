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
            var assemblyName = assemblyNameHandle.IsNil ? null : MetadataReader.GetString(assemblyNameHandle);
            var assemblyScope = new Model.Scope
            {
                Name = assemblyName,
                ScopeType = ScopeType.Assembly,
                SourceFile = string.IsNullOrEmpty(_assemblyPath) ? null : _assemblyPath,
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

        private bool TryGetClassSymbols(TypeDefinitionHandle typeDefinitionHandle, out Model.Scope? classScope)
        {
            classScope = null;
            Model.Symbol[]? fieldSymbols = null;
            Model.Scope[]? scopes = null;

            try
            {
                if (DatadogMetadataReader.IsCompilerGeneratedAttributeDefinedOnType(typeDefinitionHandle.RowId))
                {
                    return true;
                }

                var type = MetadataReader.GetTypeDefinition(typeDefinitionHandle);

                if (type.IsInterfaceType())
                {
                    return true;
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

                var allScopes = nestedClassesScopeIndex == 0 ? null : new Model.Scope[nestedClassesScopeIndex];
                if (allScopes == null)
                {
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

                var classLanguageSpecifics = GetClassLanguageSpecifics(type);
                var linesAndSource = GetClassSourceLocationInfo(allScopes);
                classScope = new Model.Scope
                {
                    Name = typeDefinitionHandle.FullName(MetadataReader),
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
                return new SourceLocationInfo { StartLine = UnknownMethodStartLine, EndLine = UnknownMethodEndLine, Path = classSourceFile };
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

            return new SourceLocationInfo { StartLine = classStartLine, EndLine = classEndLine, Path = classSourceFile };
        }

        private void PopulateNestedNotCompileGeneratedClassScope(ImmutableArray<TypeDefinitionHandle> nestedTypes, Model.Scope[] nestedClassScopes, ref int index)
        {
            for (var i = 0; i < nestedTypes.Length; i++)
            {
                Model.Scope? nestedClassScope = null;
                try
                {
                    var typeHandle = nestedTypes[i];
                    if (typeHandle.IsNil)
                    {
                        continue;
                    }

                    if (DatadogMetadataReader.IsCompilerGeneratedAttributeDefinedOnType(typeHandle.RowId))
                    {
                        continue;
                    }

                    if (!TryGetClassSymbols(typeHandle, out nestedClassScope) || nestedClassScope == null)
                    {
                        continue;
                    }

                    nestedClassScopes[index] = nestedClassScope.Value;
                    index++;
                }
                catch (Exception e)
                {
                    Log.Warning(e, "Symbol extractor fail to get information for nested class {NestedClass}", nestedClassScope?.Name ?? "Unknown");
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
                var fieldTypeName = fieldDef.DecodeSignature(new TypeProvider(false), 0);
                if (fieldDef.Name.IsNil)
                {
                    continue;
                }

                var fieldName = Datadog.Trace.VendoredMicrosoftCode.System.MemoryExtensions.AsSpan(MetadataReader.GetString(fieldDef.Name));
                if (fieldName[0] == '<')
                {
                    // properties
                    var endNameIndex = fieldName.IndexOf('>');
                    if (endNameIndex > 1)
                    {
                        fieldName = fieldName.Slice(1, endNameIndex - 1);
                    }
                }

                var accessModifiers = (fieldDef.Attributes & System.Reflection.FieldAttributes.FieldAccessMask) > 0 ? new[] { MethodAccess[Convert.ToUInt16(fieldDef.Attributes & System.Reflection.FieldAttributes.FieldAccessMask)] } : null;
                var fieldAttributes = ((int)fieldDef.Attributes & 0x0070) > 0 ? new[] { FieldAttributes[Convert.ToUInt16((int)fieldDef.Attributes & 0x0070)] } : null;
                var ls = new LanguageSpecifics() { AccessModifiers = accessModifiers, Annotations = fieldAttributes };

                fieldSymbols[index] = new Symbol
                {
                    Name = fieldName.ToString(),
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

                    if (DatadogMetadataReader.IsCompilerGeneratedAttributeDefinedOnMethod(methodDefHandle.RowId))
                    {
                        continue;
                    }

                    if (!DatadogMetadataReader.HasMethodBody(methodDefHandle.RowId))
                    {
                        continue;
                    }

                    var methodDef = MetadataReader.GetMethodDefinition(methodDefHandle);
                    methodScope = CreateMethodScope(type, methodDef);
                    if (methodScope == default)
                    {
                        continue;
                    }

                    if (methodScope.Scopes != null &&
                        methodScope is { StartLine: UnknownMethodStartLine, EndLine: UnknownMethodEndLine, SourceFile: null })
                    {
                        var sourcePdbInfo = GetMethodSourceLocationInfo(methodScope);
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
                    return name ??
                           MetadataReader.GetString(MetadataReader.GetMethodDefinition(handle).Name);
                }
                catch
                {
                    return "Unknown";
                }
            }
        }

        private SourceLocationInfo GetMethodSourceLocationInfo(Model.Scope methodScope)
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

            return new SourceLocationInfo { StartLine = startLine, EndLine = endLine, Path = sourceFile, StartColumn = startColumn, EndColumn = endColumn };
        }

        protected virtual Model.Scope CreateMethodScope(TypeDefinition type, MethodDefinition method)
        {
            if (method.Name.IsNil)
            {
                return default;
            }

            var methodName = MetadataReader.GetString(method.Name);
            if (string.IsNullOrEmpty(methodName))
            {
                return default;
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

            var methodScope = new Model.Scope
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

            return methodScope;
        }

        private Model.Scope[]? GetClosureScopes(TypeDefinition typeDef, MethodDefinition methodDef)
        {
            Model.Scope[]? closureMethods = null;
            int index = 0;
            try
            {
                var nestedTypes = typeDef.GetNestedTypes();
                var methods = typeDef.GetMethods();
                closureMethods = ArrayPool<Model.Scope>.Shared.Rent(methods.Count + (nestedTypes.Length * 2));

                for (int i = 0; i < nestedTypes.Length; i++)
                {
                    if (!DatadogMetadataReader.IsCompilerGeneratedAttributeDefinedOnType(nestedTypes[i].RowId))
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

                    if (!DatadogMetadataReader.IsCompilerGeneratedAttributeDefinedOnMethod(methodHandle.RowId))
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
                    ArrayPool<Model.Scope>.Shared.Return(closureMethods);
                }
            }

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

        protected virtual Model.Scope? CreateMethodScopeForGeneratedMethod(MethodDefinition method, MethodDefinition generatedMethod, TypeDefinition nestedType)
        {
            return null;
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

            var argsSymbol = CreateArgSymbolArray(method, parameters);
            int index = 0;
            var methodSig = method.DecodeSignature(new TypeProvider(false), 0);
            var paramTypesMatchArgSymbols = methodSig.ParameterTypes.Length == argsSymbol.Length || methodSig.ParameterTypes.Length == argsSymbol.Length - 1;
            foreach (var parameterHandle in parameters)
            {
                var parameterDef = MetadataReader.GetParameter(parameterHandle);
                if (index == 0 && !method.IsStaticMethod())
                {
                    argsSymbol[index] = new Symbol { Name = "this", SymbolType = SymbolType.Arg, Line = UnknownFieldAndArgLine, Type = method.GetDeclaringType().FullName(MetadataReader) };
                    index++;

                    if (parameterDef.IsHiddenThis())
                    {
                        continue;
                    }
                }

                argsSymbol[index] = new Symbol
                {
                    Name = MetadataReader.GetString(parameterDef.Name),
                    SymbolType = SymbolType.Arg,
                    Line = UnknownFieldAndArgLine,
                    Type = paramTypesMatchArgSymbols ? methodSig.ParameterTypes[parameterDef.IsHiddenThis() ? index : index - 1] : "Unknown"
                };
                index++;
            }

            return argsSymbol;
        }

        private Symbol[] CreateArgSymbolArray(MethodDefinition method, ParameterHandleCollection parameters)
        {
            // ReSharper disable once NotDisposedResource
            return method.IsStaticMethod() ?
                       new Symbol[parameters.Count] :
                       MetadataReader.GetParameter(parameters.GetEnumerator().Current).IsHiddenThis() ?
                           new Symbol[parameters.Count] : new Symbol[parameters.Count + 1];
        }

        private string[]? GetClassBaseClassNames(TypeDefinition type)
        {
            EntityHandle? baseType = type.BaseType;
            if (baseType.Value.IsNil)
            {
                return null;
            }

            var baseTypeName = type.BaseType.FullName(MetadataReader);
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
                baseTypeName = baseType.Value.FullName(MetadataReader);
            }

            return classNames.ToArray();
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

        internal record struct SourceLocationInfo
        {
            internal int StartLine;
            internal int EndLine;
            internal string? Path;
            internal int? StartColumn;
            internal int? EndColumn;
        }
    }
}
