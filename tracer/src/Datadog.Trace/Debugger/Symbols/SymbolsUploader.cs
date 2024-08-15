// <copyright file="SymbolsUploader.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.ExceptionAutoInstrumentation.ThirdParty;
using Datadog.Trace.Debugger.Sink;
using Datadog.Trace.Debugger.Symbols.Model;
using Datadog.Trace.Debugger.Upload;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.Util;
using Datadog.Trace.VendoredMicrosoftCode.System.Collections.Immutable;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Debugger.Symbols
{
    internal class SymbolsUploader : IDebuggerUploader
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(SymbolsUploader));

        private readonly JsonSerializerSettings _jsonSerializerSettings;
        private readonly string _serviceName;
        private readonly string? _serviceVersion;
        private readonly string? _environment;
        private readonly SemaphoreSlim _assemblySemaphore;
        private readonly SemaphoreSlim _discoveryServiceSemaphore;
        private readonly SemaphoreSlim _enablementSemaphore;
        private readonly HashSet<string> _alreadyProcessed;
        private readonly ImmutableHashSet<string> _symDb3rdPartyIncludes;
        private readonly ImmutableHashSet<string> _symDb3rdPartyExcludes;
        private readonly long _thresholdInBytes;
        private readonly CancellationTokenSource _cancellationToken;
        private readonly IBatchUploadApi _api;
        private readonly IRcmSubscriptionManager _subscriptionManager;
        private readonly ISubscription _subscription;
        private IDiscoveryService? _discoveryService;
        private byte[]? _payload;
        private string? _symDbEndpoint;
        private bool _isSymDbEnabled;

        private SymbolsUploader(
            IBatchUploadApi api,
            IDiscoveryService discoveryService,
            IRcmSubscriptionManager remoteConfigurationManager,
            DebuggerSettings settings,
            ImmutableTracerSettings tracerSettings,
            string serviceName)
        {
            _symDbEndpoint = null;
            _alreadyProcessed = new HashSet<string>();
            _environment = tracerSettings.EnvironmentInternal;
            _serviceVersion = tracerSettings.ServiceVersionInternal;
            _serviceName = serviceName;
            _discoveryService = discoveryService;
            _api = api;
            _assemblySemaphore = new SemaphoreSlim(1);
            _discoveryServiceSemaphore = new SemaphoreSlim(0);
            _enablementSemaphore = new SemaphoreSlim(0);
            _thresholdInBytes = settings.SymbolDatabaseBatchSizeInBytes;
            _symDb3rdPartyIncludes = settings.SymDbThirdPartyDetectionIncludes;
            _symDb3rdPartyExcludes = settings.SymDbThirdPartyDetectionExcludes;
            _cancellationToken = new CancellationTokenSource();
            _jsonSerializerSettings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
            _discoveryService.SubscribeToChanges(ConfigurationChanged);
            _subscription = new Subscription(Callback, RcmProducts.LiveDebuggingSymbolDb);
            _subscriptionManager = remoteConfigurationManager;
            _subscriptionManager.SubscribeToChanges(_subscription);
        }

        private ApplyDetails[] Callback(Dictionary<string, List<RemoteConfiguration>> addedConfig, Dictionary<string, List<RemoteConfigurationPath>>? removedConfig)
        {
            var result =
                (from configByProduct in addedConfig
                 where configByProduct.Key == RcmProducts.LiveDebuggingSymbolDb
                 from remoteConfiguration in configByProduct.Value
                 where remoteConfiguration.Path.Id.StartsWith(DefinitionPaths.SymDB)
                 select new NamedRawFile(remoteConfiguration.Path, remoteConfiguration.Contents)
                 into rawFile
                 select rawFile.Deserialize<SymDbEnablement>()).FirstOrDefault();

            if (_isSymDbEnabled == false && result.TypedFile?.UploadSymbols == true)
            {
                _isSymDbEnabled = true;
                _enablementSemaphore.Release(1);
            }
            else if (_isSymDbEnabled && result.TypedFile?.UploadSymbols == false)
            {
                _isSymDbEnabled = false;
                UnRegisterToAssemblyLoadEvent();
                _cancellationToken.Cancel(false);
            }

            return Array.Empty<ApplyDetails>();
        }

        private void ConfigurationChanged(AgentConfiguration configuration)
        {
            if (string.IsNullOrEmpty(configuration.SymbolDbEndpoint))
            {
                Log.Debug("`SymbolDb endpoint` is null. This can happen if your datadog-agent version is lower than 7.45");
                return;
            }

            _symDbEndpoint = configuration.SymbolDbEndpoint;
            _discoveryServiceSemaphore.Release(1);
            _discoveryService!.RemoveSubscription(ConfigurationChanged);
            _discoveryService = null;
        }

        public static IDebuggerUploader Create(IBatchUploadApi api, IDiscoveryService discoveryService, IRcmSubscriptionManager remoteConfigurationManager, DebuggerSettings settings, ImmutableTracerSettings tracerSettings, string serviceName)
        {
            if (!settings.SymbolDatabaseUploadEnabled)
            {
                Log.Information("Symbol database uploading is disabled. To enable it, please set {EnvironmentVariable} environment variable to 'true'.", ConfigurationKeys.Debugger.SymbolDatabaseUploadEnabled);
                return new NoOpSymbolUploader();
            }

            if (!ThirdPartyModules.IsValid)
            {
                Log.Warning("Third party modules load has failed. Disabling Symbol Uploader.");
                return new NoOpSymbolUploader();
            }

            return new SymbolsUploader(api, discoveryService, remoteConfigurationManager, settings, tracerSettings, serviceName);
        }

        private void RegisterToAssemblyLoadEvent()
        {
            AppDomain.CurrentDomain.AssemblyLoad += CurrentDomain_AssemblyLoad;
        }

        private void UnRegisterToAssemblyLoadEvent()
        {
            AppDomain.CurrentDomain.AssemblyLoad -= CurrentDomain_AssemblyLoad;
        }

        private void CurrentDomain_AssemblyLoad(object? sender, AssemblyLoadEventArgs args)
        {
            _ = ProcessItemAsync(args.LoadedAssembly);
        }

        private async Task ProcessItemAsync(Assembly assembly)
        {
            if (!_isSymDbEnabled)
            {
                return;
            }

            await Task.Yield();
            await _assemblySemaphore.WaitAsync(_cancellationToken.Token).ConfigureAwait(false);

            if (!_isSymDbEnabled || _cancellationToken.IsCancellationRequested)
            {
                _assemblySemaphore.Release();
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
                var assemblyName = assembly.GetName().Name;
                if (string.IsNullOrEmpty(assemblyName))
                {
                    return;
                }

                if (AssemblyFilter.ShouldSkipAssembly(assembly, _symDb3rdPartyExcludes, _symDb3rdPartyIncludes))
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
            var accumulatedBytes = 0;
            var builder = StringBuilderCache.Acquire((int)_thresholdInBytes);

            try
            {
                foreach (var classSymbol in classes)
                {
                    if (classSymbol == null)
                    {
                        continue;
                    }

                    accumulatedBytes += SerializeClass(classSymbol.Value, builder);
                    if (accumulatedBytes < _thresholdInBytes)
                    {
                        continue;
                    }

                    await Upload(root, builder).ConfigureAwait(false);
                    builder.Clear();
                    accumulatedBytes = 0;
                }

                if (accumulatedBytes > 0)
                {
                    await Upload(root, builder).ConfigureAwait(false);
                }
            }
            finally
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
            await SendSymbol(builder.ToString()).ConfigureAwait(false);
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

            try
            {
                sb.Append(symbolAsString);
            }
            catch (ArgumentOutOfRangeException)
            {
                return 0;
            }

            return Encoding.UTF8.GetByteCount(symbolAsString);
        }

        private void FinalizeSymbolForSend(Root root, StringBuilder sb)
        {
            const string classScopeString = "\"scopes\":null";

            var rootAsString = JsonConvert.SerializeObject(root);

            var classesIndex = rootAsString.IndexOf(classScopeString, StringComparison.Ordinal);

            sb.Insert(0, rootAsString.Substring(0, classesIndex + classScopeString.Length - "null".Length) + "[");
            sb.Append("]");
            sb.Append(rootAsString.Substring(classesIndex + classScopeString.Length));
        }

        public async Task StartFlushingAsync()
        {
            if (await WaitForDiscoveryServiceAsync().ConfigureAwait(false) == false)
            {
                Log.Information("Dynamic Instrumentation won't upload symbols for auto-complete suggestions. This can happen if your datadog-agent version is lower than 7.45");
                return;
            }

            if (await WaitForEnablementAsync().ConfigureAwait(false) == false)
            {
                Log.Information("This can happen when the service is shut down");
                return;
            }

            RegisterToAssemblyLoadEvent();
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                await ProcessItemAsync(assembly).ConfigureAwait(false);
            }
        }

        private async Task<bool> WaitForDiscoveryServiceAsync()
        {
            if (!string.IsNullOrEmpty(_symDbEndpoint))
            {
                // if it is already set, return immediately.
                // theoretically, this can be reverted in case of version downgrade, but we not support that atm.
                return true;
            }

            await Task.Yield();
            await _discoveryServiceSemaphore.WaitAsync(_cancellationToken.Token).ConfigureAwait(false);
            _discoveryServiceSemaphore.Dispose();
            if (_cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(Volatile.Read(ref _symDbEndpoint)))
            {
                return true;
            }

            return false;
        }

        private async Task<bool> WaitForEnablementAsync()
        {
            await Task.Yield();
            await _enablementSemaphore.WaitAsync(_cancellationToken.Token).ConfigureAwait(false);
            if (_cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            return true;
        }

        public void Dispose()
        {
            _subscriptionManager.Unsubscribe(_subscription);
            _cancellationToken.Cancel();
            _cancellationToken.Dispose();
            _assemblySemaphore.Dispose();
            _enablementSemaphore.Dispose();
        }
    }
}
