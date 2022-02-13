// <copyright file="ConfigurationPoller.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.Configurations
{
    internal class ConfigurationPoller : IConfigurationPoller
    {
        private const int MaxPollIntervalSeconds = 25;
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ConfigurationPoller>();

        private readonly IProbesApi _probesApi;
        private readonly IConfigurationUpdater _configurationUpdater;
        private readonly ImmutableDebuggerSettings _settings;
        private readonly CancellationTokenSource _cancellationSource;

        public ConfigurationPoller(
            IProbesApi probesApi,
            IConfigurationUpdater configurationUpdater,
            ImmutableDebuggerSettings settings)
        {
            _configurationUpdater = configurationUpdater;
            _settings = settings;
            _probesApi = probesApi;
            _cancellationSource = new CancellationTokenSource();
        }

        public async Task StartPollingAsync()
        {
            var retryCount = 1;
            ProbeConfiguration probeConfiguration = null;

            while (!_cancellationSource.IsCancellationRequested)
            {
                try
                {
                    probeConfiguration = await _probesApi.GetConfigurationsAsync().ConfigureAwait(false);
                    if (probeConfiguration != null)
                    {
                        retryCount = 1;
                        ApplySettings(probeConfiguration);
                    }
                    else
                    {
                        retryCount++;
                    }

                    await Delay(retryCount, probeConfiguration).ConfigureAwait(false);
                }
                catch (ThreadAbortException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    Log.Error(e, "Failed to poll probes settings");
                    retryCount++;
                    await Delay(retryCount, probeConfiguration).ConfigureAwait(false);
                }
            }

            async Task Delay(int count, ProbeConfiguration config)
            {
                if (_cancellationSource.IsCancellationRequested)
                {
                    return;
                }

                var seconds = config?.OpsConfiguration?.PollInterval ?? _settings.ProbeConfigurationsPollIntervalSeconds;

                try
                {
                    var delay = Math.Min(seconds * count, MaxPollIntervalSeconds);
                    await Task.Delay(TimeSpan.FromSeconds(delay), _cancellationSource.Token).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    // We are shutting down, so don't do anything about it
                }
            }
        }

        private void ApplySettings(ProbeConfiguration configuration)
        {
            _configurationUpdater.Accept(configuration);
        }

        public void Dispose()
        {
            _cancellationSource.Cancel();
        }
    }
}
