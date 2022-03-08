// <copyright file="LiveDebugger.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.Debugger.Configurations;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.Helpers;
using Datadog.Trace.Debugger.PInvoke;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger
{
    internal class LiveDebugger
    {
        private static readonly Lazy<LiveDebugger> LazyInstance = new Lazy<LiveDebugger>(Create, isThreadSafe: true);
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(LiveDebugger));

        private readonly ImmutableDebuggerSettings _settings;
        private readonly DiscoveryService _discoveryService;
        private readonly ConfigurationPoller _configurationPoller;

        private LiveDebugger()
        {
            var source = GlobalSettings.CreateDefaultConfigurationSource();

            var apiFactory = DebuggerTransportStrategy.Get();
            _discoveryService = DiscoveryService.Create(source, apiFactory);

            _settings = ImmutableDebuggerSettings.Create(DebuggerSettings.FromSource(source));
            var api = ProbeConfigurationFactory.Create(_settings, apiFactory, _discoveryService);
            var updater = ConfigurationUpdater.Create(_settings);
            _configurationPoller = ConfigurationPoller.Create(api, updater, _settings);
            AgentWriter = CreateAgentWriter(_settings);
        }

        public static LiveDebugger Instance => LazyInstance.Value;

        public DebuggerAgentWriter AgentWriter { get; }

        private static LiveDebugger Create()
        {
            var debugger = new LiveDebugger();
            debugger.Initialize();
            return debugger;
        }

        private static DebuggerAgentWriter CreateAgentWriter(ImmutableDebuggerSettings settings)
        {
            var apiRequestFactory = DebuggerTransportStrategy.Get();
            var api = DebuggerApi.Create(settings.AgentUri, apiRequestFactory);
            return DebuggerAgentWriter.Create(api);
        }

        internal void InstrumentProbes(IReadOnlyList<ProbeDefinition> probeDefinitions)
        {
            if (probeDefinitions.Count == 0)
            {
                return;
            }

            Log.Information($"Live Debugger.InstrumentProbes: Request to instrument {probeDefinitions.Count} probes definitions");
            var probes = probeDefinitions.Select(pd => new NativeMethodProbeDefinition("Samples.Probes", pd.Where.TypeName, pd.Where.MethodName, pd.Where.Signature.Split(','))).ToArray();
            using var disposable = new DisposableEnumerable<NativeMethodProbeDefinition>(probes);
            DebuggerNativeMethods.InstrumentProbes("1", probes);
        }

        private void Initialize()
        {
            if (!_settings.Enabled)
            {
                Log.Information("Live Debugger is disabled. To enable it, please set DD_DEBUGGER_ENABLED environment variable to 'true'.");
                return;
            }

            Log.Information("Initializing Live Debugger");
            Task.Run(async () => await InitializeAsync().ConfigureAwait(false));
        }

        private async Task InitializeAsync()
        {
            try
            {
                if (_settings.ProbeMode == ProbeMode.Agent)
                {
                    var isDiscoverySuccessful = await _discoveryService.DiscoverAsync().ConfigureAwait(false);
                    var isProbeConfigurationSupported = isDiscoverySuccessful && !string.IsNullOrWhiteSpace(_discoveryService.ProbeConfigurationEndpoint);
                    if (isProbeConfigurationSupported)
                    {
                        await StartPollingLoop().ConfigureAwait(false);
                    }
                    else
                    {
                        Log.Warning("You must upgrade datadog-agent in order to leverage the Live Debugger. All debugging features will be disabled.");
                    }
                }
                else
                {
                    await StartPollingLoop().ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "Initializing Live Debugger failed.");
            }

            Task StartPollingLoop()
            {
                LifetimeManager.Instance.AddShutdownTask(() => _configurationPoller.Dispose());
                return _configurationPoller.StartPollingAsync();
            }
        }
    }
}
