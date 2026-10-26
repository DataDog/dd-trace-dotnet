// <copyright file="SymbolExtractor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Datadog.Trace.Debugger.Symbols.Model;
using Datadog.Trace.Logging;
using Datadog.Trace.Pdb;
using Datadog.Trace.SourceGenerators;

#if NETCOREAPP
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
#else
using Datadog.Trace.VendoredMicrosoftCode.System.Reflection.Metadata;
using Datadog.Trace.VendoredMicrosoftCode.System.Reflection.Metadata.Ecma335;
#endif

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
        private const string LocalFunctionMarker = ">g__";
        private const string ClosureThisFieldName = "<>4__this";
        private const string ParentClosureFieldNamePrefix = "CS$<>8__locals";
        private const int MaxClosureNestingDepth = 16;

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

        /// <summary>
        /// Roslyn emits a local function declared in source as <c>Name</c> as a method named
        /// <c>&lt;{containing member}&gt;g__Name|{id}</c>, e.g. <c>&lt;&lt;Main&gt;$&gt;g__GetProduct|0_0</c> for a local function declared in top-level statements.
        /// </summary>
        [TestingAndPrivateOnly]
        internal static bool TryGetLocalFunctionName(string? methodName, [NotNullWhen(true)] out string? localFunctionName)
        {
            if (methodName is null || !TryGetLocalFunctionNameRange(methodName.AsSpan(), out var start, out var length))
            {
                localFunctionName = null;
                return false;
            }

            localFunctionName = methodName.Substring(start, length);
            return true;
        }

        internal static bool IsLocalFunctionName(string? methodName) => TryGetLocalFunctionNameRange(methodName.AsSpan(), out _, out _);

        private static bool TryGetLocalFunctionNameRange(ReadOnlySpan<char> methodName, out int start, out int length)
        {
            start = 0;
            length = 0;
            if (methodName.IsEmpty || methodName[0] != '<')
            {
                return false;
            }

            var idSeparator = methodName.LastIndexOf('|');
            if (idSeparator < 0)
            {
                return false;
            }

            var markerIndex = methodName.Slice(0, idSeparator).LastIndexOf(LocalFunctionMarker.AsSpan());
            // the containing member name between '<' and the marker can't be empty
            if (markerIndex < 2)
            {
                return false;
            }

            start = markerIndex + LocalFunctionMarker.Length;
            length = idSeparator - start;
            return length > 0 && methodName.Slice(start, length).IndexOfAny('<', '>') < 0;
        }

        private static void AddScope(ref Model.Scope[] scopes, ref int index, in Model.Scope scope)
        {
            if (index >= scopes.Length)
            {
                var newScopes = ArrayPool<Model.Scope>.Shared.Rent(Math.Max(scopes.Length * 2, 4));
                Array.Copy(scopes, 0, newScopes, 0, index);

                // Clear only the used portion to avoid keeping references alive via the pool
                Array.Clear(scopes, 0, index);
                ArrayPool<Model.Scope>.Shared.Return(scopes);
                scopes = newScopes;
            }

            scopes[index] = scope;
            index++;
        }

        /// <summary>
        /// The 'this' of a generated method (closure, local function) is a compiler-generated instance.
        /// Shows the user's 'this' instead: typed as <paramref name="userTypeName"/>, or removed when there is no 'this' in the user's code.
        /// </summary>
        protected static Symbol[]? ReplaceGeneratedThis(Symbol[] symbols, string? userTypeName)
        {
            var thisIndex = -1;
            for (var i = 0; i < symbols.Length; i++)
            {
                if (symbols[i].SymbolType == SymbolType.Arg && symbols[i].Name == "this")
                {
                    thisIndex = i;
                    break;
                }
            }

            if (thisIndex < 0)
            {
                return symbols;
            }

            if (userTypeName is not null)
            {
                symbols[thisIndex].Type = userTypeName;
                return symbols;
            }

            if (symbols.Length == 1)
            {
                return null;
            }

            var symbolsWithoutThis = new Symbol[symbols.Length - 1];
            Array.Copy(symbols, 0, symbolsWithoutThis, 0, thisIndex);
            Array.Copy(symbols, thisIndex + 1, symbolsWithoutThis, thisIndex, symbols.Length - thisIndex - 1);
            return symbolsWithoutThis;
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

                var type = MetadataReader.GetTypeDefinition(typeDefinitionHandle);

                // Compiler-generated types are not user types, but the synthesized type of top-level statements
                // (e.g. Program) holds the local functions declared there. Nested compiler-generated types
                // (closure classes, state machines) are surfaced through the user type that declares them.
                var isCompilerGeneratedType = DatadogMetadataReader.IsCompilerGeneratedAttributeDefinedOnType(MetadataTokens.GetToken(typeDefinitionHandle));
                if (type.IsNested && (isCompilerGeneratedType || IsNestedInCompilerGeneratedType(type)))
                {
                    return false;
                }

                if (type.IsInterfaceType())
                {
                    return false;
                }

                var fields = type.GetFields();
                if (fields.Count > 0 && !isCompilerGeneratedType)
                {
                    fieldSymbols = GetFieldSymbols(fields, type);
                }

                var methods = type.GetMethods();
                var nestedTypes = type.GetNestedTypes();
                scopes = ArrayPool<Model.Scope>.Shared.Rent(methods.Count + nestedTypes.Length);
                int scopesIndex = 0;
                if (methods.Count > 0)
                {
                    PopulateMethodScopes(type, methods, ref scopes, ref scopesIndex, localFunctionsOnly: isCompilerGeneratedType);
                }

                if (nestedTypes.Length > 0)
                {
                    PopulateNestedTypeScopes(nestedTypes, ref scopes, ref scopesIndex);
                }

                if (isCompilerGeneratedType && scopesIndex == 0)
                {
                    return false;
                }

                var classLanguageSpecifics = GetClassLanguageSpecifics(type);
                SourceLocationInfo linesAndSource = default;
                var allScopes = scopesIndex == 0 ? null : new Model.Scope[scopesIndex];
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

                Array.Copy(scopes, 0, allScopes, 0, scopesIndex);

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
                Log.Warning(e, "Error while trying to extract symbol info for type {Type}", GetTypeNameOrUnknown(typeDefinitionHandle));
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

        /// <summary>
        /// Nested user types are surfaced as nested class scopes. Nested compiler-generated types are not user types, but
        /// local functions that share captured variables with a lambda are emitted as instance methods of such a closure class
        /// (e.g. &lt;&gt;c__DisplayClass0_0), so we surface these local functions as methods of the type declaring them.
        /// </summary>
        private void PopulateNestedTypeScopes(ImmutableArray<TypeDefinitionHandle> nestedTypes, ref Model.Scope[] scopes, ref int index)
        {
            for (var i = 0; i < nestedTypes.Length; i++)
            {
                var typeHandle = nestedTypes[i];
                try
                {
                    if (typeHandle.IsNil)
                    {
                        continue;
                    }

                    if (DatadogMetadataReader.IsCompilerGeneratedAttributeDefinedOnType(MetadataTokens.GetToken(typeHandle)))
                    {
                        PopulateClosureClassLocalFunctionScopes(MetadataReader.GetTypeDefinition(typeHandle), ref scopes, ref index);
                        continue;
                    }

                    // types nested in a compiler-generated type are rejected by TryGetClassSymbols
                    if (TryGetClassSymbols(typeHandle, out var nestedClassScope))
                    {
                        AddScope(ref scopes, ref index, in nestedClassScope);
                    }
                }
                catch (Exception e)
                {
                    Log.Warning(e, "Symbol extractor fail to get information for nested class {NestedClass}", GetTypeNameOrUnknown(typeHandle));
                }
            }
        }

        private void PopulateClosureClassLocalFunctionScopes(TypeDefinition closureType, ref Model.Scope[] scopes, ref int index)
        {
            foreach (var methodHandle in closureType.GetMethods())
            {
                try
                {
                    if (methodHandle.IsNil)
                    {
                        continue;
                    }

                    var method = MetadataReader.GetMethodDefinition(methodHandle);
                    if (TryGetLocalFunctionScope(closureType, methodHandle, method, declaredOnClosureClass: true, out var localFunctionScope))
                    {
                        AddScope(ref scopes, ref index, in localFunctionScope);
                    }
                }
                catch (Exception e)
                {
                    Log.Warning(e, "Symbol extractor fail to get information for local function {Method}", GetMethodNameOrUnknown(methodHandle));
                }
            }
        }

        /// <summary>
        /// Users can't declare a type inside a compiler-generated type. Such types are compiler-generated even when they
        /// are not marked as such, e.g. the state machine of an async lambda nested in its closure class (&lt;&gt;c__DisplayClass0_0+&lt;&lt;Main&gt;b__0&gt;d).
        /// </summary>
        protected bool IsNestedInCompilerGeneratedType(TypeDefinition type)
        {
            var declaringType = type.GetDeclaringType();
            while (!declaringType.IsNil)
            {
                if (DatadogMetadataReader.IsCompilerGeneratedAttributeDefinedOnType(MetadataTokens.GetToken(declaringType)))
                {
                    return true;
                }

                declaringType = MetadataReader.GetTypeDefinition(declaringType).GetDeclaringType();
            }

            return false;
        }

        private string GetMethodNameOrUnknown(MethodDefinitionHandle handle)
        {
            try
            {
                return MetadataReader.GetString(MetadataReader.GetMethodDefinition(handle).Name);
            }
            catch
            {
                return "Unknown";
            }
        }

        private string GetTypeNameOrUnknown(TypeDefinitionHandle handle)
        {
            try
            {
                return handle.FullName(MetadataReader);
            }
            catch
            {
                return "Unknown";
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

                var fieldName = MetadataReader.GetString(fieldDef.Name).AsSpan();
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

        private void PopulateMethodScopes(TypeDefinition type, MethodDefinitionHandleCollection methods, ref Model.Scope[] classMethods, ref int index, bool localFunctionsOnly)
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

                    var methodDef = MetadataReader.GetMethodDefinition(methodDefHandle);
                    if (methodDef.Name.IsNil)
                    {
                        continue;
                    }

                    // Names starting with '<' are synthesized by the compiler (e.g. <Main>$ for top-level statements)
                    // and are never shown to the user. Local functions are the only ones among them the user declared by name.
                    if (MetadataReader.StringComparer.StartsWith(methodDef.Name, "<"))
                    {
                        if (TryGetLocalFunctionScope(type, methodDefHandle, methodDef, declaredOnClosureClass: false, out methodScope))
                        {
                            AddScope(ref classMethods, ref index, in methodScope);
                        }

                        continue;
                    }

                    if (localFunctionsOnly)
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

                    if (!TryCreateMethodScope(type, methodDefHandle, methodDef, out methodScope))
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

                    AddScope(ref classMethods, ref index, in methodScope);
                }
                catch (Exception e)
                {
                    var name = methodScope.Name ?? GetMethodNameOrUnknown(methodDefHandle);
                    Log.Warning(e, "Symbol extractor fail to get information for method {Method}", name);
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

        protected virtual bool TryCreateMethodScope(TypeDefinition type, MethodDefinitionHandle methodHandle, MethodDefinition method, out Model.Scope methodScope)
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
            var closureScopes = GetClosureScopes(type, methodHandle, method);

            methodScope = new Model.Scope
            {
                ScopeType = ScopeType.Method,
                Name = methodName,
                LanguageSpecifics = GetMethodLanguageSpecifics(method),
                HasInjectibleLines = false,
                Symbols = argsSymbol,
                Scopes = closureScopes,
                SourceFile = null,
                StartLine = UnknownMethodStartLine,
                EndLine = UnknownMethodEndLine
            };

            return true;
        }

        private LanguageSpecifics GetMethodLanguageSpecifics(MethodDefinition method)
        {
            var methodAttributes = method.Attributes & StaticFinalVirtualMethod;
            var isAsyncMethod = DatadogMetadataReader.IsAsyncMethod(method.GetCustomAttributes());
            return new LanguageSpecifics
            {
                ReturnType = method.DecodeSignature(new TypeProvider(false), 0).ReturnType,
                AccessModifiers = (method.Attributes & System.Reflection.MethodAttributes.MemberAccessMask) > 0 ? new[] { MethodAccess[Convert.ToUInt16(method.Attributes & System.Reflection.MethodAttributes.MemberAccessMask)] } : null,
                Annotations = methodAttributes > 0 && isAsyncMethod ? new[] { MethodAttributes[Convert.ToUInt16(methodAttributes)], "async" } :
                              methodAttributes > 0 ? new[] { MethodAttributes[Convert.ToUInt16(methodAttributes)] } :
                              isAsyncMethod ? new[] { "async" } : null
            };
        }

        private bool TryGetLocalFunctionScope(TypeDefinition ownerType, MethodDefinitionHandle methodHandle, MethodDefinition method, bool declaredOnClosureClass, out Model.Scope methodScope)
        {
            methodScope = default;
            if (method.Name.IsNil
             || !MetadataReader.StringComparer.StartsWith(method.Name, "<")
             || !TryGetLocalFunctionName(MetadataReader.GetString(method.Name), out var localFunctionName)
             || !DatadogMetadataReader.HasMethodBody(MetadataTokens.GetToken(methodHandle)))
            {
                return false;
            }

            return TryCreateLocalFunctionScope(ownerType, methodHandle, method, localFunctionName, declaredOnClosureClass, out methodScope);
        }

        /// <summary>
        /// Creates the method scope of a local function. The scope uses the name the user wrote, not the generated metadata name,
        /// and its source location is the one of the method that runs, so line probes on it resolve to the generated method.
        /// </summary>
        protected virtual bool TryCreateLocalFunctionScope(TypeDefinition ownerType, MethodDefinitionHandle methodHandle, MethodDefinition method, string localFunctionName, bool declaredOnClosureClass, out Model.Scope methodScope)
        {
            methodScope = new Model.Scope
            {
                ScopeType = ScopeType.Method,
                Name = localFunctionName,
                LanguageSpecifics = GetMethodLanguageSpecifics(method),
                HasInjectibleLines = false,
                Symbols = GetLocalFunctionArgsSymbol(ownerType, method, declaredOnClosureClass),
                SourceFile = null,
                StartLine = UnknownMethodStartLine,
                EndLine = UnknownMethodEndLine
            };

            return true;
        }

        private Symbol[]? GetLocalFunctionArgsSymbol(TypeDefinition ownerType, MethodDefinition method, bool declaredOnClosureClass)
        {
            var argsSymbol = GetArgsSymbol(method);
            if (!declaredOnClosureClass || method.IsStaticMethod() || argsSymbol is not { Length: > 0 })
            {
                return argsSymbol;
            }

            // 'this' is the closure instance. When the closure captured the user's 'this',
            // expose 'this' with the user's type, otherwise there is no 'this' in the user's code.
            var userType = ownerType.GetDeclaringType();
            var userTypeName = !userType.IsNil && ClosureCapturesThis(ownerType, MaxClosureNestingDepth) ? userType.FullName(MetadataReader) : null;
            return ReplaceGeneratedThis(argsSymbol, userTypeName);
        }

        /// <summary>
        /// A closure captures the user's 'this' in a &lt;&gt;4__this field. The closure of a nested scope (e.g. a loop body)
        /// reaches it through the closure of its parent scope, stored in a CS$&lt;&gt;8__locals field.
        /// </summary>
        private bool ClosureCapturesThis(TypeDefinition closureType, int remainingDepth)
        {
            foreach (var fieldHandle in closureType.GetFields())
            {
                var field = MetadataReader.GetFieldDefinition(fieldHandle);
                if (MetadataReader.StringComparer.Equals(field.Name, ClosureThisFieldName))
                {
                    return true;
                }

                if (remainingDepth > 0
                 && MetadataReader.StringComparer.StartsWith(field.Name, ParentClosureFieldNamePrefix)
                 && TryGetFieldTypeDefinition(field, out var parentClosureType)
                 && ClosureCapturesThis(MetadataReader.GetTypeDefinition(parentClosureType), remainingDepth - 1))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryGetFieldTypeDefinition(FieldDefinition field, out TypeDefinitionHandle typeHandle)
        {
            typeHandle = default;
            var signature = MetadataReader.GetBlobReader(field.Signature);
            if (signature.ReadSignatureHeader().Kind != SignatureKind.Field)
            {
                return false;
            }

            var typeCode = signature.ReadSignatureTypeCode();
            if (typeCode == SignatureTypeCode.GenericTypeInstance)
            {
                // closure of a generic method or type
                typeCode = signature.ReadSignatureTypeCode();
            }

            if (typeCode != SignatureTypeCode.TypeHandle)
            {
                return false;
            }

            var handle = signature.ReadTypeHandle();
            if (handle.Kind != HandleKind.TypeDefinition)
            {
                return false;
            }

            typeHandle = (TypeDefinitionHandle)handle;
            return true;
        }

        private Model.Scope[]? GetClosureScopes(TypeDefinition typeDef, MethodDefinitionHandle methodDefHandle, MethodDefinition methodDef)
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
                        PopulateClosureMethod(generatedMethodHandle, generatedMethodDef, nestedType);
                    }
                }

                foreach (var methodHandle in methods)
                {
                    if (methodHandle.IsNil || methodHandle == methodDefHandle)
                    {
                        continue;
                    }

                    if (!DatadogMetadataReader.IsCompilerGeneratedAttributeDefinedOnMethod(MetadataTokens.GetToken(methodHandle)))
                    {
                        continue;
                    }

                    var currentMethod = MetadataReader.GetMethodDefinition(methodHandle);
                    PopulateClosureMethod(methodHandle, currentMethod, typeDef);
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

            void PopulateClosureMethod(MethodDefinitionHandle generatedMethodHandle, MethodDefinition generatedMethod, TypeDefinition ownerType)
            {
                if (TryCreateMethodScopeForGeneratedMethod(methodDefHandle, methodDef, generatedMethodHandle, generatedMethod, ownerType, out var closureMethodScope))
                {
                    // This can exceed our initial heuristic, so the pooled buffer grows on-demand to avoid dropping closure methods.
                    AddScope(ref closureMethods!, ref index, in closureMethodScope);
                }
            }
        }

        protected virtual bool TryCreateMethodScopeForGeneratedMethod(MethodDefinitionHandle methodHandle, MethodDefinition method, MethodDefinitionHandle generatedMethodHandle, MethodDefinition generatedMethod, TypeDefinition nestedType, out Model.Scope methodScope)
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
