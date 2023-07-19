// <copyright file="SymbolExtractor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Datadog.Trace.Debugger.Symbols.Model;
using Datadog.Trace.Logging;
using Datadog.Trace.Pdb;
using Datadog.Trace.Vendors.dnlib.DotNet;
using Datadog.Trace.Vendors.dnlib.DotNet.Emit;
using Datadog.Trace.Vendors.dnlib.DotNet.Pdb.Portable;
using Datadog.Trace.Vendors.dnlib.DotNet.Pdb.Symbols;
using TypeAttributes = Datadog.Trace.Vendors.dnlib.DotNet.TypeAttributes;

namespace Datadog.Trace.Debugger.Symbols
{
    internal class SymbolExtractor
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(SymbolExtractor));

        internal IEnumerable<Model.Scope> GetClassSymbols(Assembly assembly, string[] typeToExtract = null)
        {
            using var pdbReader = DatadogPdbReader.CreatePdbReader(assembly);
            var module = pdbReader?.Module;

            if (module?.Types == null)
            {
                yield break;
            }

            for (var i = 0; i < module.Types.Count; i++)
            {
                var type = module.Types[i];

                if (typeToExtract != null && !typeToExtract.Any(t => t.Equals(type.FullName)))
                {
                    continue;
                }

                Model.Scope classScope = default;
                try
                {
                    if (type.Fields?.Count == 0 && type.Methods?.Count == 0)
                    {
                        continue;
                    }

                    var classSymbols = GetFieldSymbols(type.Fields);

                    var classScopes = GetMethodSymbols(type.Methods, pdbReader, out var classStartLine, out var classEndLine, out var typeSourceFile);

                    var classLanguageSpecifics = GetClassLanguageSpecifics(type, i, module);

                    classScope = new Model.Scope
                    {
                        Name = type.FullName,
                        ScopeType = SymbolType.Class,
                        StartLine = classStartLine,
                        EndLine = classEndLine,
                        Symbols = classSymbols,
                        Scopes = ListCache<Model.Scope>.FreeAndToArray(ref classScopes),
                        SourceFile = typeSourceFile ?? "UNKNOWN",
                        LanguageSpecifics = classLanguageSpecifics
                    };
                }
                catch (Exception e)
                {
                    Log.Warning(e, "Error while trying to extract symbol info for type {Type}", type?.FullName ?? "UNKNOWN");
                }

                yield return classScope;
            }
        }

        private LanguageSpecifics GetClassLanguageSpecifics(TypeDef type, int i, ModuleDefMD module)
        {
            var interfaceNames = GetClassInterfaceNames(type, i);

            var baseClassNames = GetClassBaseClassNames(type, module);

            var classLanguageSpecifics = new LanguageSpecifics
            {
                AccessModifiers = new[] { (type.Attributes & TypeAttributes.VisibilityMask).ToString() },
                Annotations = new[] { type.Attributes.ToString() },
                Interfaces = interfaceNames,
                SuperClasses = baseClassNames
            };
            return classLanguageSpecifics;
        }

        internal Model.Scope GetAssemblySymbol(Assembly assembly)
        {
            var assemblyScope = new Model.Scope
            {
                Name = assembly.FullName,
                ScopeType = SymbolType.Assembly,
                SourceFile = assembly.Location,
                StartLine = -1,
                EndLine = int.MaxValue,
                LanguageSpecifics = null,
                Scopes = new List<Model.Scope>()
            };

            return assemblyScope;
        }

        private Symbol[] GetFieldSymbols(IList<FieldDef> typeFields)
        {
            if (typeFields.Count <= 0)
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
                    SymbolType = field.IsStatic ? SymbolType.Static_Field : SymbolType.Field,
                    Type = field.FieldType.TypeName,
                    Line = -1
                };
            }

            return typeSymbols;
        }

        private List<Model.Scope> GetMethodSymbols(IList<MethodDef> typeMethods, DatadogPdbReader pdbReader, out int classStartLine, out int classEndLine, out string typeSourceFile)
        {
            classStartLine = -1;
            classEndLine = -1;
            typeSourceFile = null;

            if (typeMethods.Count <= 0)
            {
                return null;
            }

            var classScopes = ListCache<Model.Scope>.AllocList();

            for (var j = 0; j < typeMethods.Count; j++)
            {
                var method = typeMethods[j];
                var symbolMethod = pdbReader.GetMethodSymbolInfo(method.MDToken.ToInt32());
                if (symbolMethod == null)
                {
                    continue;
                }

                var firstSq = symbolMethod.SequencePoints.FirstOrDefault(sq => sq.IsHidden() == false);
                typeSourceFile ??= firstSq.Document?.URL;
                var startLine = firstSq.Line == 0 ? -1 : firstSq.Line;
                if (j == 0)
                {
                    // not really first line but good enough for inner scopes (fields doesn't has line number anyway)
                    classStartLine = startLine;
                }

                Symbol[] argsSymbol;
                Symbol[] localsSymbol;
                Symbol[] methodSymbols;

                // arguments
                Symbol[] GetArgsSymbol()
                {
                    if (method.Parameters.Count <= 0)
                    {
                        return null;
                    }

                    argsSymbol = new Symbol[method.Parameters.Count];
                    for (var k = 0; k < method.Parameters.Count; k++)
                    {
                        var parameter = method.Parameters[k];
                        argsSymbol[k] = new Symbol
                        {
                            Name = parameter.IsHiddenThisParameter ? "this" : parameter.Name,
                            Type = parameter.Type.FullName,
                            SymbolType = SymbolType.Arg,
                            Line = startLine
                        };
                    }

                    return argsSymbol;
                }

                argsSymbol = GetArgsSymbol();

                // locals
                Symbol[] GetLocalsSymbol(out int localsCount1)
                {
                    localsCount1 = 0;
                    if (method.Body is not { Variables.Count: > 0 })
                    {
                        return null;
                    }

                    var methodLocals = method.Body.Variables;
                    localsSymbol = new Symbol[methodLocals.Count];
                    var allMethodScopes = GetAllScopes(symbolMethod);
                    for (var k = 0; k < allMethodScopes.Count; k++)
                    {
                        var currentScope = allMethodScopes[k];
                        for (var l = 0; l < currentScope.Locals.Count; l++)
                        {
                            var localSymbol = currentScope.Locals[l];
                            if (localSymbol.Index > methodLocals.Count || string.IsNullOrEmpty(localSymbol.Name))
                            {
                                continue;
                            }

                            var line = -1;
                            for (var m = 0; m < symbolMethod.SequencePoints.Count; m++)
                            {
                                if (symbolMethod.SequencePoints[m].Offset >= currentScope.StartOffset)
                                {
                                    line = symbolMethod.SequencePoints[m].Line;
                                }
                            }

                            Local local = null;
                            for (var i = 0; i < methodLocals.Count; i++)
                            {
                                if (methodLocals[i].Index != localSymbol.Index)
                                {
                                    continue;
                                }

                                local = methodLocals[i];
                                break;
                            }

                            localsSymbol[localsCount1] = new Symbol
                            {
                                Name = localSymbol.Name,
                                Type = local?.Type?.FullName,
                                SymbolType = SymbolType.Local,
                                Line = line
                            };

                            localsCount1++;
                        }
                    }

                    return localsSymbol;
                }

                localsSymbol = GetLocalsSymbol(out var localsCount);

                Symbol[] ConcatMethodSymbols()
                {
                    var argsCount = argsSymbol?.Length ?? 0;
                    var symbolsCount = argsCount + localsCount;
                    if (symbolsCount <= 0)
                    {
                        return null;
                    }

                    methodSymbols = new Symbol[symbolsCount];

                    if (argsCount > 0)
                    {
                        Array.Copy(argsSymbol!, 0, methodSymbols, 0, argsCount);
                    }

                    if (localsCount > 0)
                    {
                        Array.Copy(localsSymbol, 0, methodSymbols, argsCount, localsCount);
                    }

                    return methodSymbols;
                }

                methodSymbols = ConcatMethodSymbols();

                var endLine = symbolMethod.SequencePoints.LastOrDefault(sq => sq.IsHidden() == false).EndLine;

                if (endLine > classEndLine)
                {
                    classEndLine = endLine + 1;
                }

                var methodLanguageSpecifics = new LanguageSpecifics
                {
                    ReturnType = method.ReturnType.FullName,
                    AccessModifiers = new List<string> { method.Access.ToString() },
                    Annotations = new List<string>
                    {
                        method.Attributes.ToString(),
                        method.ImplAttributes.ToString(),
                    }
                };

                classScopes.Add(
                    new Model.Scope
                    {
                        ScopeType = SymbolType.Method,
                        Name = method.Name,
                        LanguageSpecifics = methodLanguageSpecifics,
                        Symbols = methodSymbols,
                        StartLine = startLine,
                        EndLine = endLine,
                        SourceFile = typeSourceFile
                    });
            }

            return classScopes;
        }

        private string[] GetClassBaseClassNames(TypeDef type, ModuleDefMD module)
        {
            string[] baseClassNames = null;
            var baseType = type.BaseType;
            var objectType = module.CorLibTypes.Object.TypeDefOrRef;
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

        private string[] GetClassInterfaceNames(TypeDef type, int i)
        {
            var interfaces = type.Interfaces;
            if (interfaces.Count <= 0)
            {
                return null;
            }

            var interfaceNames = new string[interfaces.Count];
            for (var j = 0; j < type.Interfaces.Count; j++)
            {
                interfaceNames[j] = interfaces[i].Interface.FullName;
            }

            return interfaceNames;
        }

        private IList<SymbolScope> GetAllScopes(SymbolMethod method)
        {
            var result = new List<SymbolScope>();
            RetrieveAllNestedScopes(method.RootScope, result);
            return result;
        }

        private void RetrieveAllNestedScopes(SymbolScope scope, List<SymbolScope> result)
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
    }
}
