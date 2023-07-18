// <copyright file="SymbolSink.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.Symbols
{
    internal class SymbolSink : ISymbolSink
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(SymbolSink));

        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly string _serviceName;
        private readonly SymbolExtractor _symbolExtractor;
        private readonly SymbolUploader _symbolUploader;
        private readonly SemaphoreSlim _assemblySemaphore;
        private readonly HashSet<string> _alreadyProcessed;

        private SymbolSink(string serviceName, SymbolUploader uploader, SymbolExtractor symbolExtractor)
        {
            _alreadyProcessed = new HashSet<string>();
            _serviceName = serviceName;
            _symbolUploader = uploader;
            _symbolExtractor = symbolExtractor;
            _assemblySemaphore = new SemaphoreSlim(1);
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public static SymbolSink Create(string serviceName, SymbolUploader uploader, SymbolExtractor symbolExtractor)
        {
            return new SymbolSink(serviceName, uploader, symbolExtractor);
        }

        private void RegisterToAssemblyLoadEvent()
        {
            AppDomain.CurrentDomain.AssemblyLoad += async (_, args) =>
            {
                await ProcessItemAsync(args.LoadedAssembly).ConfigureAwait(false);
            };
        }

        private async Task ProcessItemAsync(Assembly assembly)
        {
            await _assemblySemaphore.WaitAsync().ConfigureAwait(false);

            try
            {
                await ProcessItem(assembly).ConfigureAwait(false);
            }
            finally
            {
                _assemblySemaphore.Release();
            }
        }

        private async Task ProcessItem(Assembly assembly)
        {
            try
            {
                if (!_alreadyProcessed.Add(assembly.Location))
                {
                    return;
                }

                await GetAssemblySymbols(assembly).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Log.Error(e, "Error while trying to extract assembly symbol {Assembly}", assembly);
            }
        }

        private async Task GetAssemblySymbols(Assembly assembly)
        {
            if (AssemblyFilter.ShouldSkipAssembly(assembly))
            {
                return;
            }

            var root = _symbolExtractor.GetAssemblySymbol(assembly, _serviceName);

            foreach (var classSymbol in _symbolExtractor.GetClassSymbols(assembly))
            {
                if (classSymbol == default)
                {
                    continue;
                }

                root.Scopes[0].Scopes.Add(classSymbol);
                await _symbolUploader.SendSymbol(root).ConfigureAwait(false);
                root.Scopes[0].Scopes.Clear();
            }
        }

        public async Task StartExtractingAssemblySymbolsAsync()
        {
            RegisterToAssemblyLoadEvent();
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                await ProcessItemAsync(assemblies[i]).ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource?.Dispose();
            _assemblySemaphore.Dispose();
        }
    }
}
