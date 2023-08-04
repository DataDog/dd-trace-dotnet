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
using Datadog.Trace.Vendors.dnlib.DotNet;
using Datadog.Trace.Vendors.dnlib.DotNet.Pdb;
using Datadog.Trace.Vendors.dnlib.DotNet.Pdb.Portable;
using Datadog.Trace.Vendors.dnlib.DotNet.Pdb.Symbols;
using MethodAttributes = Datadog.Trace.Vendors.dnlib.DotNet.MethodAttributes;
using TypeAttributes = Datadog.Trace.Vendors.dnlib.DotNet.TypeAttributes;

namespace Datadog.Trace.Debugger.Symbols
{
    internal class SymbolExtractor : IDisposable
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(SymbolExtractor));

        private readonly ModuleDefMD _module;
        private bool _disposed;

        protected SymbolExtractor(ModuleDefMD module)
        {
            _module = module;
        }

        ~SymbolExtractor() => Dispose(false);

        public static SymbolExtractor? Create(Assembly assembly)
        {
            try
            {
                var pdbReader = DatadogPdbReader.CreatePdbReader(assembly);
                if (pdbReader == null)
                {
                    Log.Warning("Could not create a PDB reader file for assembly {Assembly}", assembly.FullName);
                }

                var module = pdbReader?.Module ??
                             ModuleDefMD.Load(assembly.ManifestModule, new ModuleCreationOptions { TryToLoadPdbFromDisk = false });

                if (module == null)
                {
                    Log.Debug("Could not load module for assembly {Assembly}", assembly.FullName);
                    return null;
                }

                if (module.Types?.Count == 0)
                {
                    Log.Debug("Could not found any type in assembly {Assembly}", assembly.FullName);
                    return null;
                }

                return pdbReader != null ? new SymbolPdbExtractor(pdbReader) : new SymbolExtractor(module);
            }
            catch (Exception e)
            {
                Log.Error(e, "Error while creating instance of SymbolExtractor with {Assembly}", assembly.FullName);
                return null;
            }
        }

        internal Model.Scope GetAssemblySymbol()
        {
            var assemblyScope = new Model.Scope
            {
                Name = _module.Assembly.FullName,
                ScopeType = SymbolType.Assembly,
                SourceFile = _module.Location,
            };

            return assemblyScope;
        }

        internal IEnumerable<Model.Scope?> GetClassSymbols()
        {
            for (var i = 0; i < _module.Types.Count; i++)
            {
                var type = _module.Types[i];

                if (!TryGetClassSymbols(type, out var classScope))
                {
                    continue;
                }

                yield return classScope;
            }
        }

        internal Model.Scope? GetClassSymbols(string typeToExtract)
        {
            for (var i = 0; i < _module.Types.Count; i++)
            {
                var type = _module.Types[i];
                if (type != null && !typeToExtract.Equals(type.FullName))
                {
                    continue;
                }

                if (!TryGetClassSymbols(type, out var classScope))
                {
                    return null;
                }

                return classScope;
            }

            return null;
        }

        private bool TryGetClassSymbols(TypeDef? type, [NotNullWhen(true)] out Model.Scope? classScope)
        {
            classScope = null;

            if (type == null)
            {
                return false;
            }

            try
            {
                if (type.Fields?.Count == 0 && type.Methods?.Count == 0)
                {
                    return false;
                }

                var fieldSymbols = GetFieldSymbols(type.Fields);

                var methodScopes = GetMethodScopes(type, out var classStartLine, out var classEndLine, out var typeSourceFile);

                var nestedClassScopes = GetNestedNotCompileGeneratedClassScope(type);

                var overallCount = (methodScopes?.Length ?? 0 + nestedClassScopes?.Length ?? 0);

                var allScopes = overallCount == 0 ? null : new Model.Scope[overallCount];

                if (methodScopes?.Length > 0)
                {
                    Array.Copy(methodScopes, 0, allScopes!, 0, methodScopes.Length);
                }

                if (nestedClassScopes?.Length > 0)
                {
                    Array.Copy(nestedClassScopes, 0, allScopes!, methodScopes?.Length ?? 0, nestedClassScopes.Length);
                }

                var classLanguageSpecifics = GetClassLanguageSpecifics(type);

                classScope = new Model.Scope
                {
                    Name = type.FullName,
                    ScopeType = SymbolType.Class,
                    StartLine = classStartLine,
                    EndLine = classEndLine,
                    Symbols = fieldSymbols,
                    Scopes = allScopes,
                    SourceFile = typeSourceFile ?? "UNKNOWN",
                    LanguageSpecifics = classLanguageSpecifics
                };
            }
            catch (Exception e)
            {
                Log.Warning(e, "Error while trying to extract symbol info for type {Type}", type.FullName ?? "UNKNOWN");
                return false;
            }

            return true;
        }

        private Model.Scope[]? GetNestedNotCompileGeneratedClassScope(TypeDef type)
        {
            if (type.NestedTypes.Count <= 0)
            {
                return null;
            }

            var nestedClassScopes = ListCache<Model.Scope>.AllocList();
            try
            {
                for (var i = 0; i < type.NestedTypes.Count; i++)
                {
                    var nestedType = type.NestedTypes[i];

                    if (IsCompilerGeneratedAttributeDefined(nestedType.CustomAttributes))
                    {
                        continue;
                    }

                    if (!TryGetClassSymbols(nestedType, out var nestedClassScope))
                    {
                        continue;
                    }

                    nestedClassScopes.Add(nestedClassScope.Value);
                }

                return ListCache<Model.Scope>.FreeAndToArray(ref nestedClassScopes);
            }
            catch
            {
                if (nestedClassScopes != null)
                {
                    ListCache<Model.Scope>.Free(ref nestedClassScopes);
                }

                return null;
            }
        }

        protected bool IsCompilerGeneratedAttributeDefined(CustomAttributeCollection attributes)
        {
            return attributes.IsDefined("System.Runtime.CompilerServices.CompilerGeneratedAttribute");
        }

        private LanguageSpecifics GetClassLanguageSpecifics(TypeDef type)
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

        private Symbol[]? GetFieldSymbols(IList<FieldDef>? typeFields)
        {
            if (typeFields is not { Count: > 0 })
            {
                return null;
            }

            var typeSymbols = new Symbol[typeFields.Count];
            for (var j = 0; j < typeSymbols.Length; j++)
            {
                var field = typeFields[j];
                typeSymbols[j] = new Symbol
                {
                    Name = field.Name,
                    SymbolType = field.IsStatic ? SymbolType.StaticField : SymbolType.Field,
                    Type = field.FieldType.TypeName,
                    Line = -1
                };
            }

            return typeSymbols;
        }

        private Model.Scope[]? GetMethodScopes(TypeDef type, out int classStartLine, out int classEndLine, out string? typeSourceFile)
        {
            classStartLine = -1;
            classEndLine = -1;
            typeSourceFile = null;

            if (type.Methods is not { Count: > 0 })
            {
                return null;
            }

            var methods = type.Methods;
            var classMethods = ListCache<Model.Scope>.AllocList();

            for (var j = 0; j < methods.Count; j++)
            {
                var method = methods[j];

                if (IsCompilerGeneratedAttributeDefined(method.CustomAttributes))
                {
                    continue;
                }

                var methodScope = CreateMethodScope(type, method);

                if (j == 0)
                {
                    // not really first line but good enough for inner scopes (fields doesn't has line number anyway)
                    classStartLine = methodScope.StartLine;
                }

                if (methodScope.EndLine > classEndLine)
                {
                    classEndLine = methodScope.EndLine + 1;
                }

                classMethods.Add(methodScope);
            }

            return ListCache<Model.Scope>.FreeAndToArray(ref classMethods);
        }

        protected virtual Model.Scope CreateMethodScope(TypeDef type, MethodDef method)
        {
            // arguments
            var argsSymbol = GetArgsSymbol(method, -1);

            // closures
            var closureScopes = GetClosureScopes(type, method);

            const MethodAttributes staticVirtualFinal = MethodAttributes.Static | MethodAttributes.Virtual | MethodAttributes.Final;
            var methodAttributes = method.Attributes & staticVirtualFinal;
            var methodLanguageSpecifics = new LanguageSpecifics
            {
                ReturnType = method.ReturnType.FullName,
                AccessModifiers = new[] { method.Access.ToString() },
                Annotations = methodAttributes > 0 ? new[] { methodAttributes.ToString() } : null
            };

            var methodScope = new Model.Scope
            {
                ScopeType = SymbolType.Method,
                Name = method.Name.String,
                LanguageSpecifics = methodLanguageSpecifics,
                Symbols = argsSymbol,
                Scopes = closureScopes
            };

            return methodScope;
        }

        private Model.Scope[]? GetClosureScopes(TypeDef type, MethodDef method)
        {
            var nestedTypes = type.NestedTypes;

            var closureMethods = new List<Model.Scope>();
            for (int i = 0; i < nestedTypes.Count; i++)
            {
                var nestedType = nestedTypes[i];
                if (!IsCompilerGeneratedAttributeDefined(nestedType.CustomAttributes))
                {
                    continue;
                }

                var generatedMethods = nestedType.Methods;
                for (int j = 0; j < generatedMethods.Count; j++)
                {
                    var generatedMethod = generatedMethods[j];

                    var closureMethodScope = CreateMethodScopeForGeneratedMethod(method, generatedMethod, nestedType);
                    if (closureMethodScope.HasValue)
                    {
                        closureMethods.Add(closureMethodScope.Value);
                    }
                }
            }

            var methods = type.Methods;
            for (int i = 0; i < methods.Count; i++)
            {
                if (!IsCompilerGeneratedAttributeDefined(methods[i].CustomAttributes))
                {
                    continue;
                }

                var closureMethodScope = CreateMethodScopeForGeneratedMethod(method, methods[i], type);
                if (closureMethodScope.HasValue)
                {
                    closureMethods.Add(closureMethodScope.Value);
                }
            }

            return closureMethods.ToArray();
        }

        private Model.Scope? CreateMethodScopeForGeneratedMethod(MethodDef method, MethodDef generatedMethod, TypeDef nestedType)
        {
            if (!generatedMethod.HasCustomDebugInfos)
            {
                return null;
            }

            if (!generatedMethod.CustomDebugInfos.Any(di => di.Kind is PdbCustomDebugInfoKind.AsyncMethod or PdbCustomDebugInfoKind.EditAndContinueLocalSlotMap))
            {
                return null;
            }

            var generatedMethodName = generatedMethod.Name.String;
            if (generatedMethodName[0] != '<')
            {
                return null;
            }

            var notGeneratedMethodName = generatedMethodName.Substring(1, generatedMethodName.IndexOf('>') - 1);
            if (!method.Name.String.Equals(notGeneratedMethodName))
            {
                return null;
            }

            // todo: check state machine to add fields even if there is not pdb?

            var closureMethodScope = CreateMethodScope(nestedType, generatedMethod);
            closureMethodScope.Name = method.Name.String;
            closureMethodScope.ScopeType = SymbolType.Closure;
            return closureMethodScope;
        }

        protected Symbol[]? GetArgsSymbol(MethodDef method, int startLine)
        {
            if (method.Parameters.Count <= 0 ||
                (method.Parameters.Count == 1 && method.Parameters[0].IsHiddenThisParameter))
            {
                return null;
            }

            var argsSymbol = new Symbol[method.Parameters.Count];
            for (var k = 0; k < method.Parameters.Count; k++)
            {
                var parameter = method.Parameters[k];
                argsSymbol[k] = new Symbol
                {
                    Name = parameter.Name,
                    Type = parameter.Type.FullName,
                    SymbolType = SymbolType.Arg,
                    Line = startLine
                };
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

        private string[]? GetClassBaseClassNames(TypeDef type)
        {
            string[]? baseClassNames = null;
            var baseType = type.BaseType;
            var objectType = _module.CorLibTypes.Object.TypeDefOrRef;
            if (objectType != null && baseType != null && baseType != objectType)
            {
                var classNames = ListCache<string>.AllocList();
                while (baseType != null && baseType != objectType)
                {
                    classNames.Add(baseType.FullName);
                    baseType = baseType.GetBaseType();
                }

                baseClassNames = ListCache<string>.FreeAndToArray(ref classNames);
            }

            return baseClassNames;
        }

        private string[]? GetClassInterfaceNames(TypeDef type)
        {
            var interfaces = type.Interfaces;
            if (interfaces.Count <= 0)
            {
                return null;
            }

            var interfaceNames = new string[interfaces.Count];
            for (var j = 0; j < interfaces.Count; j++)
            {
                interfaceNames[j] = interfaces[j].Interface.FullName;
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
                _module?.Dispose();
            }

            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
