// <copyright file="SymbolExtractor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Logging;
using Datadog.Trace.Pdb;
using Datadog.Trace.Vendors.dnlib.DotNet;
using Datadog.Trace.Vendors.dnlib.DotNet.Pdb.Portable;
using Datadog.Trace.Vendors.dnlib.DotNet.Pdb.Symbols;
using TypeAttributes = Datadog.Trace.Vendors.dnlib.DotNet.TypeAttributes;

namespace Datadog.Trace.Debugger.Symbols
{
    internal class SymbolExtractor : ISymbolExtractor
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(SymbolExtractor));

        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly BlockingCollection<Assembly> _assemblies;
        private readonly string _serviceName;
        private readonly SymbolUploader _symbolUploader;
        private Task _processTask;

        private SymbolExtractor(string serviceName, SymbolUploader uploader)
        {
            _assemblies = new BlockingCollection<Assembly>();
            _serviceName = serviceName;
            _symbolUploader = uploader;
            _cancellationTokenSource = new CancellationTokenSource();
            Process();
            RegisterToAssemblyLoadEvent();
            EnqueueAlreadyLoadedAssemblies();
        }

        public static SymbolExtractor Create(string serviceName, SymbolUploader uploader)
        {
            return new SymbolExtractor(serviceName, uploader);
        }

        private void RegisterToAssemblyLoadEvent()
        {
            AppDomain.CurrentDomain.AssemblyLoad += (_, args) => AddModule(args.LoadedAssembly);
        }

        private void EnqueueAlreadyLoadedAssemblies()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                AddModule(assemblies[i]);
            }
        }

        private void AddModule(Assembly assembly)
        {
            _assemblies.Add(assembly);
        }

        private void Process()
        {
            _processTask = Task.Run(async () =>
            {
                foreach (var assemblyPath in _assemblies.GetConsumingEnumerable(_cancellationTokenSource.Token))
                {
                    await ProcessItem(assemblyPath).ConfigureAwait(false);
                }
            });
        }

        private async Task ProcessItem(Assembly assembly)
        {
            try
            {
                await ExtractModuleSymbols(assembly).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Log.Error(e, "Error while trying to extract assembly symbol {Assembly}", assembly);
            }
        }

        private async Task ExtractModuleSymbols(Assembly assembly)
        {
            if (AssemblyFilter.ShouldSkipAssembly(assembly))
            {
                return;
            }

            var root = GetAssemblySymbol(assembly);

            using var pdbReader = DatadogPdbReader.CreatePdbReader(assembly);
            var module = pdbReader.Module;

            // types
            for (var i = 0; i < module.Types.Count; i++)
            {
                var type = module?.Types?[i];

                try
                {
                    if (type.Fields?.Count == 0 && type.Methods?.Count == 0)
                    {
                        continue;
                    }

                    // fields
                    var classSymbols = GetFieldSymbols(type.Fields);

                    var classScopes = GetMethodSymbols(type.Methods, pdbReader, out var classStartLine, out var classEndLine, out var typeSourceFile);

                    var interfaceNames = GetClassInterfaceNames(type, i);

                    var baseClassNames = GetClassBaseClassNames(type, module);

                    var classLanguageSpecifics = new LanguageSpecifics
                    {
                        AccessModifiers = new[] { (type.Attributes & TypeAttributes.VisibilityMask).ToString() },
                        Annotations = new[] { type.Attributes.ToString() },
                        Interfaces = interfaceNames,
                        SuperClasses = baseClassNames
                    };

                    var classScope = new Scope
                    {
                        Name = type.FullName,
                        SymbolType = SymbolType.Class,
                        Type = type.FullName,
                        StartLine = classStartLine,
                        EndLine = classEndLine,
                        Symbols = classSymbols,
                        Scopes = ListCache<Scope>.FreeAndToArray(ref classScopes),
                        SourceFile = typeSourceFile ?? "UNKNOWN",
                        LanguageSpecifics = classLanguageSpecifics
                    };

                    root.Scopes[0].Scopes.Add(classScope);
                    await _symbolUploader.SendSymbol(root).ConfigureAwait(false);
                    root.Scopes[0].Scopes.Clear();
                }
                catch (Exception e)
                {
                    Log.Warning(e, "Error while trying to extract symbol info for type {Type}", type?.FullName ?? "UNKNOWN");
                }
            }
        }

        private SymbolModel GetAssemblySymbol(Assembly assembly)
        {
            var assemblyScope = new Scope
            {
                Name = assembly.FullName,
                SymbolType = SymbolType.Assembly,
                SourceFile = assembly.Location,
                Scopes = new List<Scope>()
            };

            var root = new SymbolModel
            {
                Service = _serviceName,
                Env = string.Empty,
                Language = "dotnet",
                Version = string.Empty,
                Scopes = new[] { assemblyScope }
            };

            return root;
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
                    Name = field.FullName,
                    SymbolType = SymbolType.Field,
                    Type = field.FieldType.TypeName,
                };
            }

            return typeSymbols;
        }

        private List<Scope> GetMethodSymbols(IList<MethodDef> typeMethods, DatadogPdbReader pdbReader, out int classStartLine, out int classEndLine, out string typeSourceFile)
        {
            classStartLine = -1;
            classEndLine = -1;
            typeSourceFile = null;

            if (typeMethods.Count <= 0)
            {
                return null;
            }

            var classScopes = ListCache<Scope>.AllocList();

            // methods
            for (int j = 0; j < typeMethods.Count; j++)
            {
                var method = typeMethods[j];
                var symbolMethod = pdbReader.GetMethodSymbolInfo(method.MDToken.ToInt32());
                var firstSq = symbolMethod.SequencePoints.FirstOrDefault();
                typeSourceFile ??= firstSq.Document.URL;
                if (j == 0)
                {
                    // not really first line but good enough for inner scopes (fields doesn't has line number anyway)
                    classStartLine = firstSq.Line;
                }

                Symbol[] methodSymbols = null;

                // arguments
                if (method.Parameters.Count > 0)
                {
                    methodSymbols = new Symbol[method.Parameters.Count + method.Body.Variables.Count];
                    for (int k = 0; k < method.Parameters.Count; k++)
                    {
                        var parameter = method.Parameters[k];
                        methodSymbols[k] = new Symbol
                        {
                            Name = parameter.Name,
                            Type = parameter.Type.FullName,
                            SymbolType = SymbolType.Arg,
                            Line = firstSq.Line
                        };
                    }
                }

                // locals

                if (method.Body != null &&
                    method.Body.Variables.Count > 0)
                {
                    methodSymbols ??= new Symbol[method.Body.Variables.Count];
                    var methodLocals = method.Body.Variables;
                    var allMethodScopes = GetAllScopes(symbolMethod);
                    var methodParametersCount = method.Parameters.Count;
                    for (int k = 0; k < allMethodScopes.Count; k++)
                    {
                        var currentScope = allMethodScopes[k];
                        for (int l = 0; l < currentScope.Locals.Count; l++)
                        {
                            var localSymbol = currentScope.Locals[l];
                            if (localSymbol.Index > methodLocals.Count)
                            {
                                continue;
                            }

                            int line = -1;
                            for (int m = 0; m < symbolMethod.SequencePoints.Count; m++)
                            {
                                if (symbolMethod.SequencePoints[m].Offset >= currentScope.StartOffset)
                                {
                                    line = symbolMethod.SequencePoints[m].Line;
                                }
                            }

                            methodSymbols[methodParametersCount - 1 + l] = new Symbol
                            {
                                Name = localSymbol.Name,
                                Type = methodLocals[l].Type.FullName,
                                SymbolType = SymbolType.Local,
                                Line = line
                            };
                        }
                    }
                }

                var methodLanguageSpecifics = new LanguageSpecifics
                {
                    ReturnType = new[] { method.ReturnType.FullName },
                    AccessModifiers = new List<string> { method.Access.ToString() },
                    Annotations = new List<string>
                    {
                        method.Attributes.ToString(),
                        method.ImplAttributes.ToString(),
                        // todo: do we need custom attributes?
                    }
                };

                var endLine = symbolMethod.SequencePoints.LastOrDefault().EndLine;
                if (j == typeMethods.Count - 1)
                {
                    classEndLine = endLine;
                }

                classScopes.Add(
                    new Scope
                    {
                        SymbolType = SymbolType.Method,
                        Name = method.FullName,
                        LanguageSpecifics = methodLanguageSpecifics,
                        Symbols = methodSymbols,
                        StartLine = firstSq.Line,
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

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            try
            {
                _processTask.Wait();
            }
            catch (Exception ex) when (ex is AggregateException or OperationCanceledException or ObjectDisposedException) { } // Suppress exception due to cancellation

            _cancellationTokenSource?.Dispose();
            _assemblies?.Dispose();
            _processTask?.Dispose();
        }
    }
}
