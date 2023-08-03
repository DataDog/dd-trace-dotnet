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
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.Debugger.Sink;
using Datadog.Trace.Debugger.Symbols.Model;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Debugger.Symbols
{
    internal class SymbolsUploader : ISymbolsUploader
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(SymbolsUploader));

        private readonly JsonSerializerSettings _jsonSerializerSettings;
        private readonly string _serviceName;
        private readonly string? _serviceVersion;
        private readonly string? _environment;
        private readonly IDiscoveryService _discoveryService;
        private readonly SemaphoreSlim _assemblySemaphore;
        private readonly SemaphoreSlim _discoveryServiceSemaphore;
        private readonly HashSet<string> _alreadyProcessed;
        private readonly long _sizeLimit;
        private readonly CancellationTokenSource _cancellationToken;
        private readonly IBatchUploadApi _api;
        private byte[]? _payload;
        private string? _isSymbolUploaderEnabled;

        private SymbolsUploader(IBatchUploadApi api, IDiscoveryService discoveryService, DebuggerSettings settings, ImmutableTracerSettings tracerSettings, string serviceName)
        {
            _isSymbolUploaderEnabled = null;
            _alreadyProcessed = new HashSet<string>();
            _environment = tracerSettings.EnvironmentInternal;
            _serviceVersion = tracerSettings.ServiceVersionInternal;
            _serviceName = serviceName;
            _discoveryService = discoveryService;
            _api = api;
            _assemblySemaphore = new SemaphoreSlim(1);
            _discoveryServiceSemaphore = new SemaphoreSlim(0);
            _sizeLimit = settings.SymbolBatchSizeInMb * 1024 * 1024;
            _cancellationToken = new CancellationTokenSource();
            _jsonSerializerSettings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
            _discoveryService.SubscribeToChanges(ConfigurationChanged);
        }

        private void ConfigurationChanged(AgentConfiguration configuration)
        {
            _isSymbolUploaderEnabled = configuration.SymbolDbEndpoint;
            _discoveryServiceSemaphore.Release(1);
            _discoveryService.RemoveSubscription(ConfigurationChanged);
        }

        public static ISymbolsUploader Create(IBatchUploadApi api, IDiscoveryService discoveryService, DebuggerSettings settings, ImmutableTracerSettings tracerSettings, string serviceName)
        {
            if (api is not NoOpSymbolBatchUploadApi &&
               (settings.SymbolDatabaseUploadEnabled ||
                (EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.Debugger.SymbolDatabaseUploadEnabledInternal, "false").ToBoolean() ?? false)))
            {
                return new SymbolsUploader(api, discoveryService, settings, tracerSettings, serviceName);
            }

            Log.Information("Symbol database uploading is disabled. To enable it, please set {EnvironmentVariable} environment variable to 'true'.", ConfigurationKeys.Debugger.SymbolDatabaseUploadEnabled);
            return new NoOpUploader();
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
            await _assemblySemaphore.WaitAsync(_cancellationToken.Token).ConfigureAwait(false);

            if (_cancellationToken.IsCancellationRequested)
            {
                return;
            }

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
            using var symbolsExtractor = SymbolExtractor.Create(assembly);
            if (symbolsExtractor == null)
            {
                return;
            }

            var assemblyScope = symbolsExtractor.GetAssemblySymbol();

            var root = new Root
            {
                Service = _serviceName,
                Env = _environment,
                Language = "dotnet",
                Version = _serviceVersion,
                Scopes = new[] { assemblyScope }
            };

            await UploadClasses(root, symbolsExtractor.GetClassSymbols()).ConfigureAwait(false);
        }

        private async Task UploadClasses(Root root, IEnumerable<Model.Scope?> classes)
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
            return await _api!.SendBatchAsync(new ArraySegment<byte>(_payload)).ConfigureAwait(false);
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
            if (await WaitForDiscoveryServiceAsync().ConfigureAwait(false) == false)
            {
                return;
            }

            RegisterToAssemblyLoadEvent();
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                await ProcessItemAsync(assemblies[i]).ConfigureAwait(false);
            }
        }

        private async Task<bool> WaitForDiscoveryServiceAsync()
        {
            await _discoveryServiceSemaphore.WaitAsync(TimeSpan.FromSeconds(10), _cancellationToken.Token).ConfigureAwait(false);
            _discoveryServiceSemaphore.Dispose();
            if (_cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(Volatile.Read(ref _isSymbolUploaderEnabled)))
            {
                return true;
            }
            else
            {
                Log.Warning("Upload symbol database is not supported");
            }

            return false;
        }

        public void Dispose()
        {
            _cancellationToken.Cancel();
            _cancellationToken.Dispose();
            _assemblySemaphore.Dispose();
        }
    }
}
