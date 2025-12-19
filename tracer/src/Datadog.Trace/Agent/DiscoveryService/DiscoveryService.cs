// <copyright file="DiscoveryService.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.Logging;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.Agent.DiscoveryService
{
    /// <summary>
    /// Queries the Datadog Agent and discovers which version we are running against and which endpoints it supports.
    /// </summary>
    internal sealed class DiscoveryService : IDiscoveryService
    {
        private const string SupportedDebuggerEndpoint = "debugger/v1/input";
        private const string SupportedDebuggerV2Endpoint = "debugger/v2/input";
        private const string SupportedDiagnosticsEndpoint = "debugger/v1/diagnostics";
        private const string SupportedSymbolDbEndpoint = "symdb/v1/input";
        private const string SupportedConfigurationEndpoint = "v0.7/config";
        private const string SupportedStatsEndpoint = "v0.6/stats";
        private const string SupportedDataStreamsEndpoint = "v0.1/pipeline_stats";
        private const string SupportedEventPlatformProxyEndpointV2 = "evp_proxy/v2";
        private const string SupportedEventPlatformProxyEndpointV4 = "evp_proxy/v4";
        private const string SupportedTelemetryProxyEndpoint = "telemetry/proxy";
        private const string SupportedTracerFlareEndpoint = "tracer_flare/v1";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<DiscoveryService>();
        private readonly int _initialRetryDelayMs;
        private readonly int _maxRetryDelayMs;
        private readonly int _recheckIntervalMs;
        private readonly TaskCompletionSource<bool> _processExit = new();
        private readonly List<Action<AgentConfiguration>> _agentChangeCallbacks = new();
        private readonly object _lock = new();
        private readonly Task _discoveryTask;
        private readonly IDisposable? _settingSubscription;
        private IApiRequestFactory _apiRequestFactory;
        private AgentConfiguration? _configuration;
        private string? _configurationHash;

        public DiscoveryService(
            TracerSettings.SettingsManager settings,
            TimeSpan tcpTimeout,
            int initialRetryDelayMs,
            int maxRetryDelayMs,
            int recheckIntervalMs)
            : this(CreateApiRequestFactory(settings.InitialExporterSettings, tcpTimeout), initialRetryDelayMs, maxRetryDelayMs, recheckIntervalMs)
        {
            // Create as a "managed" service that can update the request factory
            _settingSubscription = settings.SubscribeToChanges(changes =>
            {
                if (changes.UpdatedExporter is { } exporter)
                {
                    var newFactory = CreateApiRequestFactory(exporter, tcpTimeout);
                    Interlocked.Exchange(ref _apiRequestFactory!, newFactory);
                }
            });
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DiscoveryService"/> class.
        /// Public for testing purposes
        /// </summary>
        public DiscoveryService(
            IApiRequestFactory apiRequestFactory,
            int initialRetryDelayMs,
            int maxRetryDelayMs,
            int recheckIntervalMs)
        {
            _apiRequestFactory = apiRequestFactory;
            _initialRetryDelayMs = initialRetryDelayMs;
            _maxRetryDelayMs = maxRetryDelayMs;
            _recheckIntervalMs = recheckIntervalMs;
            _discoveryTask = Task.Run(FetchConfigurationLoopAsync);
            _discoveryTask.ContinueWith(t => Log.Error(t.Exception, "Error in discovery task"), TaskContinuationOptions.OnlyOnFaulted);
        }

        /// <summary>
        /// Gets all the supported endpoints for testing purposes only
        /// </summary>
        public static string[] AllSupportedEndpoints =>
            new[]
            {
                SupportedDebuggerEndpoint,
                SupportedDebuggerV2Endpoint,
                SupportedDiagnosticsEndpoint,
                SupportedSymbolDbEndpoint,
                SupportedConfigurationEndpoint,
                SupportedStatsEndpoint,
                SupportedDataStreamsEndpoint,
                SupportedEventPlatformProxyEndpointV2,
                SupportedEventPlatformProxyEndpointV4,
                SupportedTelemetryProxyEndpoint,
                SupportedTracerFlareEndpoint,
            };

        [TestingOnly]
        internal string? ConfigStateHash => Volatile.Read(ref _configurationHash);

        /// <summary>
        /// Create a <see cref="DiscoveryService"/> instance that responds to runtime changes in settings
        /// </summary>
        public static DiscoveryService CreateManaged(TracerSettings settings)
            => new(
                settings.Manager,
                tcpTimeout: TimeSpan.FromSeconds(15),
                initialRetryDelayMs: 500,
                maxRetryDelayMs: 5_000,
                recheckIntervalMs: 30_000);

        /// <summary>
        /// Create a <see cref="DiscoveryService"/> instance that does _not_ respond to runtime changes in settings
        /// </summary>
        public static DiscoveryService CreateUnmanaged(ExporterSettings exporterSettings)
            => CreateUnmanaged(
                exporterSettings,
                tcpTimeout: TimeSpan.FromSeconds(15),
                initialRetryDelayMs: 500,
                maxRetryDelayMs: 5_000,
                recheckIntervalMs: 30_000);

        /// <summary>
        /// Create a <see cref="DiscoveryService"/> instance that does _not_ respond to runtime changes in settings
        /// </summary>
        public static DiscoveryService CreateUnmanaged(
            ExporterSettings exporterSettings,
            TimeSpan tcpTimeout,
            int initialRetryDelayMs,
            int maxRetryDelayMs,
            int recheckIntervalMs)
            => new(
                CreateApiRequestFactory(exporterSettings, tcpTimeout),
                initialRetryDelayMs,
                maxRetryDelayMs,
                recheckIntervalMs);

        /// <inheritdoc cref="IDiscoveryService.SubscribeToChanges"/>
        public void SubscribeToChanges(Action<AgentConfiguration> callback)
        {
            lock (_lock)
            {
                if (!_agentChangeCallbacks.Contains(callback))
                {
                    _agentChangeCallbacks.Add(callback);
                }
            }

            if (Volatile.Read(ref _configuration) is { } currentConfig)
            {
                try
                {
                    // If we already have fetched the config, call this immediately
                    callback(currentConfig);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error notifying subscriber of initial discovered configuration");
                }
            }
        }

        /// <inheritdoc cref="IDiscoveryService.RemoveSubscription"/>
        public void RemoveSubscription(Action<AgentConfiguration> callback)
        {
            lock (_lock)
            {
                _agentChangeCallbacks.Remove(callback);
            }
        }

        private void NotifySubscribers(AgentConfiguration newConfig)
        {
            List<Action<AgentConfiguration>> subscribers;
            lock (_lock)
            {
                subscribers = _agentChangeCallbacks.ToList();
                // Setting the configuration immediately after grabbing
                // the subscribers ensures subscribers receive the
                // notification exactly once
                _configuration = newConfig;
            }

            foreach (var subscriber in subscribers)
            {
                try
                {
                    subscriber(newConfig);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error notifying subscriber of configuration change");
                }
            }
        }

        private async Task FetchConfigurationLoopAsync()
        {
            var requestFactory = _apiRequestFactory;
            var uri = requestFactory.GetEndpoint("info");

            int? sleepDuration = null;

            while (!_processExit.Task.IsCompleted)
            {
                try
                {
                    // If the exporter settings have been updated, refresh the endpoint
                    var updatedFactory = Volatile.Read(ref _apiRequestFactory);
                    if (requestFactory != updatedFactory)
                    {
                        requestFactory = updatedFactory;
                        uri = requestFactory.GetEndpoint("info");
                    }

                    var api = requestFactory.Create(uri);

                    using var response = await api.GetAsync().ConfigureAwait(false);
                    if (response.StatusCode is >= 200 and < 300)
                    {
                        await ProcessDiscoveryResponse(response).ConfigureAwait(false);
                        sleepDuration = null;
                    }
                    else
                    {
                        Log.Warning("Error discovering available agent services");
                        sleepDuration = GetNextSleepDuration(sleepDuration);
                    }
                }
                catch (Exception exception)
                {
                    Log.Warning(exception, "Error discovering available agent services");
                    sleepDuration = GetNextSleepDuration(sleepDuration);
                }

                try
                {
                    await Task.WhenAny(_processExit.Task, Task.Delay(sleepDuration ?? _recheckIntervalMs)).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }

            Log.Debug("Discovery service exiting");

            int GetNextSleepDuration(int? previousDuration) =>
                previousDuration is null ? _initialRetryDelayMs : Math.Min(previousDuration.Value * 2, _maxRetryDelayMs);
        }

        private async Task ProcessDiscoveryResponse(IApiResponse response)
        {
            // Grab the original stream
            var stream = await response.GetStreamAsync().ConfigureAwait(false);

            // Create a hash of the utf-8 bytes while also deserializing
            JObject? jObject;
            using var sha256 = SHA256.Create();
            using (var cryptoStream = new CryptoStream(stream, sha256, CryptoStreamMode.Read))
            {
                jObject = response.ReadAsType<JObject>(cryptoStream);

                // Newtonsoft.JSON doesn't technically read to the end of the stream, it stops as soon
                // as it has something parseable, but for the sha256 we need to read to the end so that
                // it finalizes correctly, so just drain it down
#if NETCOREAPP3_1_OR_GREATER
                Span<byte> buffer = stackalloc byte[10];
                while (cryptoStream.Read(buffer) > 0) { }
#else
                var buffer = new byte[10];
                while (cryptoStream.Read(buffer, 0, 10) > 0) { }
#endif
            }

            if (jObject is null)
            {
                throw new Exception("Error deserializing discovery response: response was null");
            }

            var agentVersion = jObject["version"]?.Value<string>();
            var clientDropP0 = jObject["client_drop_p0s"]?.Value<bool>() ?? false;
            var spanMetaStructs = jObject["span_meta_structs"]?.Value<bool>() ?? false;
            var spanEvents = jObject["span_events"]?.Value<bool>() ?? false;

            var discoveredEndpoints = (jObject["endpoints"] as JArray)?.Values<string>().ToArray();
            string? configurationEndpoint = null;
            string? debuggerEndpoint = null;
            string? debuggerV2Endpoint = null;
            string? diagnosticsEndpoint = null;
            string? symbolDbEndpoint = null;
            string? statsEndpoint = null;
            string? dataStreamsMonitoringEndpoint = null;
            string? eventPlatformProxyEndpoint = null;
            string? telemetryProxyEndpoint = null;
            string? tracerFlareEndpoint = null;

            if (discoveredEndpoints is { Length: > 0 })
            {
                foreach (var discoveredEndpoint in discoveredEndpoints)
                {
                    var endpoint = discoveredEndpoint?.Trim('/');
                    if (endpoint is null)
                    {
                        continue;
                    }

                    // effectively a switch, but case insensitive
                    if (endpoint.Equals(SupportedDebuggerEndpoint, StringComparison.OrdinalIgnoreCase))
                    {
                        debuggerEndpoint = endpoint;
                    }
                    else if (endpoint.Equals(SupportedDebuggerV2Endpoint, StringComparison.OrdinalIgnoreCase))
                    {
                        debuggerV2Endpoint = endpoint;
                    }
                    else if (endpoint.Equals(SupportedDiagnosticsEndpoint, StringComparison.OrdinalIgnoreCase))
                    {
                        diagnosticsEndpoint = endpoint;
                    }
                    else if (endpoint.Equals(SupportedSymbolDbEndpoint, StringComparison.OrdinalIgnoreCase))
                    {
                        symbolDbEndpoint = endpoint;
                    }
                    else if (endpoint.Equals(SupportedConfigurationEndpoint, StringComparison.OrdinalIgnoreCase))
                    {
                        configurationEndpoint = endpoint;
                    }
                    else if (endpoint.Equals(SupportedStatsEndpoint, StringComparison.OrdinalIgnoreCase))
                    {
                        statsEndpoint = endpoint;
                    }
                    else if (endpoint.Equals(SupportedDataStreamsEndpoint, StringComparison.OrdinalIgnoreCase))
                    {
                        dataStreamsMonitoringEndpoint = endpoint;
                    }
                    else if (eventPlatformProxyEndpoint is null && endpoint.Equals(SupportedEventPlatformProxyEndpointV2, StringComparison.OrdinalIgnoreCase))
                    {
                        eventPlatformProxyEndpoint = endpoint;
                    }
                    else if (endpoint.Equals(SupportedEventPlatformProxyEndpointV4, StringComparison.OrdinalIgnoreCase))
                    {
                        eventPlatformProxyEndpoint = endpoint;
                    }
                    else if (endpoint.Equals(SupportedTelemetryProxyEndpoint, StringComparison.OrdinalIgnoreCase))
                    {
                        telemetryProxyEndpoint = endpoint;
                    }
                    else if (endpoint.Equals(SupportedTracerFlareEndpoint, StringComparison.OrdinalIgnoreCase))
                    {
                        tracerFlareEndpoint = endpoint;
                    }
                }
            }

            var existingConfiguration = _configuration;

            var newConfig = new AgentConfiguration(
                configurationEndpoint: configurationEndpoint,
                debuggerEndpoint: debuggerEndpoint,
                debuggerV2Endpoint: debuggerV2Endpoint ?? diagnosticsEndpoint,
                diagnosticsEndpoint: diagnosticsEndpoint,
                symbolDbEndpoint: symbolDbEndpoint,
                agentVersion: agentVersion,
                statsEndpoint: statsEndpoint,
                dataStreamsMonitoringEndpoint: dataStreamsMonitoringEndpoint,
                eventPlatformProxyEndpoint: eventPlatformProxyEndpoint,
                telemetryProxyEndpoint: telemetryProxyEndpoint,
                tracerFlareEndpoint: tracerFlareEndpoint,
                clientDropP0: clientDropP0,
                spanMetaStructs: spanMetaStructs,
                spanEvents: spanEvents);

            // Save the hash, whether the details we care about changed or not
            _configurationHash = HexString.ToHexString(sha256.Hash);

            // AgentConfiguration is a record, so this compares by value
            if (existingConfiguration is null || !newConfig.Equals(existingConfiguration))
            {
                Log.Debug("Discovery configuration updated, notifying subscribers: {Configuration}", newConfig);
                NotifySubscribers(newConfig);
            }
        }

        public Task DisposeAsync()
        {
            _settingSubscription?.Dispose();
            if (!_processExit.TrySetResult(true))
            {
                // Double dispose in prod shouldn't happen, and should be avoided, so logging for follow-up
                Log.Debug($"{nameof(DiscoveryService)} is already disposed, skipping further disposal.");
            }

            return _discoveryTask;
        }

        private static IApiRequestFactory CreateApiRequestFactory(ExporterSettings exporterSettings, TimeSpan tcpTimeout)
            => AgentTransportStrategy.Get(
                exporterSettings,
                productName: "discovery",
                tcpTimeout: tcpTimeout,
                AgentHttpHeaderNames.MinimalHeaders,
                () => new MinimalAgentHeaderHelper(),
                uri => uri);
    }
}
