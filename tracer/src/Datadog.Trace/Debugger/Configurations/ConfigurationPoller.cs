// <copyright file="ConfigurationPoller.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.Configurations
{
    internal class ConfigurationPoller : IConfigurationPoller
    {
        private const int MaxPollIntervalSeconds = 25;
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ConfigurationPoller>();

        private readonly ImmutableDebuggerSettings _settings;
        private readonly IProbesApi _probesApi;
        private readonly CancellationTokenSource _cancellationSource;

        public ConfigurationPoller(IProbesApi probesApi, ImmutableDebuggerSettings settings)
        {
            _settings = settings;
            _probesApi = probesApi;
            _cancellationSource = new CancellationTokenSource();
        }

        public async Task StartPollingAsync()
        {
            var retryCount = 1;
            while (!_cancellationSource.IsCancellationRequested)
            {
                try
                {
                    var configs = await _probesApi.GetConfigurationsAsync().ConfigureAwait(false);
                    if (configs != null)
                    {
                        retryCount = 1;
                        ApplySettings(configs);
                    }
                    else
                    {
                        retryCount++;
                    }

                    await Delay(retryCount).ConfigureAwait(false);
                }
                catch (ThreadAbortException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    Log.Error(e, "Failed to poll probes settings");
                    retryCount++;
                    await Delay(retryCount).ConfigureAwait(false);
                }
            }

            async Task Delay(int iteration)
            {
                try
                {
                    var delay = Math.Min(_settings.ProbeConfigurationsPollIntervalSeconds * iteration, MaxPollIntervalSeconds);
                    await Task.Delay(TimeSpan.FromSeconds(delay), _cancellationSource.Token).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    // We are shutting down, so don't do anything about it
                }
            }
        }

        private void ApplySettings(object configs)
        {
            // todo apply config settings
        }

        public void Dispose()
        {
            _cancellationSource.Cancel();
        }
    }
}
