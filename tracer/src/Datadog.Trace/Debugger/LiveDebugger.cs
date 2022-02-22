// <copyright file="LiveDebugger.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.Debugger.Configurations;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger
{
    internal class LiveDebugger
    {
        private static readonly Lazy<LiveDebugger> LazyInstance = new Lazy<LiveDebugger>(Create, true);
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
            var api = ProbeConfigurationApi.Create(_settings, apiFactory, _discoveryService);
            var updater = ConfigurationUpdater.Create(_settings);
            _configurationPoller = ConfigurationPoller.Create(api, updater, _settings);
        }

        public static LiveDebugger Instance => LazyInstance.Value;

        private static LiveDebugger Create()
        {
            var debugger = new LiveDebugger();
            debugger.Initialize();

            return debugger;
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
