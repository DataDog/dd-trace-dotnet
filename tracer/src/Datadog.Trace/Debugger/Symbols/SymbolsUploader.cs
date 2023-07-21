// <copyright file="SymbolsUploader.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.Sink;
using Datadog.Trace.Debugger.Symbols.Model;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Debugger.Symbols
{
    internal class SymbolsUploader : ISymbolsUploader
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(SymbolsUploader));

        private readonly JsonSerializerSettings _jsonSerializerSettings;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly string _serviceName;
        private readonly string? _serviceVersion;
        private readonly string? _environment;
        private readonly SymbolExtractor _symbolExtractor;
        private readonly SemaphoreSlim _assemblySemaphore;
        private readonly HashSet<string> _alreadyProcessed;
        private readonly IBatchUploadApi _api;
        private readonly long _sizeLimit;
        private byte[]? _payload;

        private SymbolsUploader(string? environment, string? serviceVersion, string serviceName, SymbolExtractor symbolExtractor, IBatchUploadApi api, int sizeLimit)
        {
            _alreadyProcessed = new HashSet<string>();
            _environment = environment;
            _serviceVersion = serviceVersion;
            _serviceName = serviceName;
            _symbolExtractor = symbolExtractor;
            _assemblySemaphore = new SemaphoreSlim(1);
            _cancellationTokenSource = new CancellationTokenSource();
            _sizeLimit = sizeLimit * 1024 * 1024;
            _api = api;
            _jsonSerializerSettings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
        }

        public static SymbolsUploader Create(string? environment, string? serviceVersion, string serviceName, SymbolExtractor symbolExtractor, IBatchUploadApi api, int sizeLimit)
        {
            return new SymbolsUploader(environment, serviceVersion, serviceName, symbolExtractor, api, sizeLimit);
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
            await Task.Yield();
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
                if (AssemblyFilter.ShouldSkipAssembly(assembly))
                {
                    return;
                }

                if (!_alreadyProcessed.Add(assembly.Location))
                {
                    return;
                }

                await UploadAssemblySymbols(assembly).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Log.Error(e, "Error while trying to extract assembly symbol {Assembly}", assembly);
            }
        }

        private async Task UploadAssemblySymbols(Assembly assembly)
        {
            var assemblyScope = _symbolExtractor.GetAssemblySymbol(assembly);

            var root = new Root
            {
                Service = _serviceName,
                Env = _environment,
                Language = "dotnet",
                Version = _serviceVersion,
                Scopes = new[] { assemblyScope }
            };

            await UploadClasses(root, _symbolExtractor.GetClassSymbols(assembly)).ConfigureAwait(false);
        }

        public async Task UploadClasses(Root root, IEnumerable<Model.Scope?> classes)
        {
            var count = 0;
            var builder = StringBuilderCache.Acquire(StringBuilderCache.MaxBuilderSize);

            try
            {
                foreach (var classSymbol in classes)
                {
                    if (classSymbol == null)
                    {
                        continue;
                    }

                    count += SerializeClass(classSymbol.Value, builder);
                    if (count < _sizeLimit)
                    {
                        continue;
                    }

                    await Upload(root, builder).ConfigureAwait(false);
                    count = 0;
                }

                if (count > 0)
                {
                    await Upload(root, builder).ConfigureAwait(false);
                }
            }
            catch (Exception)
            {
                if (builder != null)
                {
                    StringBuilderCache.Release(builder);
                }
            }
        }

        private async Task Upload(Root root, StringBuilder builder)
        {
            FinalizeSymbolForSend(root, builder);
            await SendSymbol(StringBuilderCache.GetStringAndRelease(builder)).ConfigureAwait(false);
            ResetPayload();
        }

        private void ResetPayload()
        {
            if (_payload != null)
            {
                Array.Clear(_payload, 0, _payload.Length);
            }
        }

        private async Task<bool> SendSymbol(string symbol)
        {
            var count = Encoding.UTF8.GetByteCount(symbol);
            if (_payload == null || count >= _payload.Length)
            {
                _payload = new byte[count];
            }

            Encoding.UTF8.GetBytes(symbol, 0, symbol.Length, _payload, 0);
            return await _api.SendBatchAsync(new ArraySegment<byte>(_payload)).ConfigureAwait(false);
        }

        private int SerializeClass(Model.Scope classScope, StringBuilder sb)
        {
            if (sb.Length != 0)
            {
                sb.Append(',');
            }

            var symbolAsString = JsonConvert.SerializeObject(classScope, _jsonSerializerSettings);
            sb.Append(symbolAsString);

            return Encoding.UTF8.GetByteCount(symbolAsString);
        }

        private void FinalizeSymbolForSend(Root root, StringBuilder sb)
        {
            const string classScopeString = "\"scopes\":[]";

            var rootAsString = JsonConvert.SerializeObject(root, _jsonSerializerSettings);

            var classesIndex = rootAsString.IndexOf(classScopeString, StringComparison.Ordinal);

            sb.Insert(0, rootAsString.Substring(0, classesIndex + classScopeString.Length - 1));
            sb.Append(rootAsString.Substring(classesIndex + classScopeString.Length - 1));
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
            _cancellationTokenSource.Dispose();
            _assemblySemaphore.Dispose();
        }
    }
}
