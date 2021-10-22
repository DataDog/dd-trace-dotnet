// <copyright file="TelemetryController.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.PlatformHelpers;

namespace Datadog.Trace.Telemetry
{
    internal class TelemetryController : ITelemetryController, IDisposable
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<TelemetryController>();
        private readonly ConfigurationTelemetryCollector _configuration;
        private readonly DependencyTelemetryCollector _dependencies;
        private readonly IntegrationTelemetryCollector _integrations;
        private readonly TelemetryDataBuilder _dataBuilder = new();
        private readonly ITelemetryTransport _transport;
        private readonly TimeSpan _sendFrequency;
        private readonly TaskCompletionSource<bool> _tracerInitialized = new();
        private readonly TaskCompletionSource<bool> _processExit = new();
        private readonly Task _telemetryTask;

        internal TelemetryController(
            ConfigurationTelemetryCollector configuration,
            DependencyTelemetryCollector dependencies,
            IntegrationTelemetryCollector integrations,
            ITelemetryTransport transport,
            TimeSpan sendFrequency)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _dependencies = dependencies ?? throw new ArgumentNullException(nameof(dependencies));
            _integrations = integrations ?? throw new ArgumentNullException(nameof(integrations));
            _sendFrequency = sendFrequency;
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));

            AppDomain.CurrentDomain.AssemblyLoad += CurrentDomain_OnAssemblyLoad;
            var assembliesLoaded = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var t in assembliesLoaded)
            {
                RecordAssembly(t);
            }

            _telemetryTask = Task.Run(PushTelemetryLoopAsync);
        }

        public void RecordTracerSettings(ImmutableTracerSettings settings, string defaultServiceName, AzureAppServices appServicesMetadata)
        {
            _configuration.RecordTracerSettings(settings, defaultServiceName, appServicesMetadata);
            _integrations.RecordTracerSettings(settings);
            _tracerInitialized.TrySetResult(true);
        }

        public void RecordSecuritySettings(SecuritySettings settings)
            => _configuration.RecordSecuritySettings(settings);

        public void IntegrationRunning(IntegrationId integrationId)
            => _integrations.IntegrationRunning(integrationId);

        public void IntegrationGeneratedSpan(IntegrationId integrationId)
            => _integrations.IntegrationGeneratedSpan(integrationId);

        public void IntegrationDisabledDueToError(IntegrationId integrationId, string error)
            => _integrations.IntegrationDisabledDueToError(integrationId, error);

        public void Dispose()
        {
            _processExit.TrySetResult(true);
            _tracerInitialized.TrySetResult(true);
            AppDomain.CurrentDomain.AssemblyLoad -= CurrentDomain_OnAssemblyLoad;
            _telemetryTask.GetAwaiter().GetResult();
        }

        private void CurrentDomain_OnAssemblyLoad(object sender, AssemblyLoadEventArgs e)
        {
            RecordAssembly(e.LoadedAssembly);
        }

        private void RecordAssembly(Assembly assembly)
        {
            try
            {
                _dependencies.AssemblyLoaded(assembly.GetName());
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error recording loaded assembly");
            }
        }

        private async Task PushTelemetryLoopAsync()
        {
#if !NET5_0_OR_GREATER
            var tasks = new Task[2];
            tasks[0] = _tracerInitialized.Task;
#endif
            while (true)
            {
#if !NET5_0_OR_GREATER
                if (_tracerInitialized.Task.IsCompleted)
                {
                    tasks[0] = _processExit.Task;
                }
#endif

                if (_processExit.Task.IsCompleted)
                {
                    Log.Debug("Process exit requested, ending telemetry loop");
                    await PushAppClosingTelemetry().ConfigureAwait(false);
                    return;
                }

                await PushTelemetry().ConfigureAwait(false);

#if NET5_0_OR_GREATER
                // .NET 5.0 has an explicit overload for this
                await Task.WhenAny(
                               Task.Delay(_sendFrequency),
                               _tracerInitialized.Task.IsCompleted
                                    ? _processExit.Task
                                    : _tracerInitialized.Task)
                          .ConfigureAwait(false);
#else
                tasks[1] = Task.Delay(_sendFrequency);
                await Task.WhenAny(tasks).ConfigureAwait(false);
#endif
            }
        }

        private async Task PushTelemetry()
        {
            try
            {
                var application = _configuration.GetApplicationData();
                if (application is null)
                {
                    Log.Debug("Telemetry not initialized, skipping");
                    return;
                }

                // These calls change the state of the collectors, so must use the data
                var configuration = _configuration.GetConfigurationData();
                var dependencies = _dependencies.GetData();
                var integrations = _integrations.GetData();

                var data = _dataBuilder.BuildTelemetryData(application, configuration, dependencies, integrations);
                if (data is null)
                {
                    Log.Debug("No telemetry data, skipping");
                    return;
                }

                Log.Debug("Pushing telemetry changes");
                foreach (var telemetryData in data)
                {
                    await _transport.PushTelemetry(telemetryData).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error pushing telemetry");
            }
        }

        private async Task PushAppClosingTelemetry()
        {
            try
            {
                var application = _configuration.GetApplicationData();
                var data = _dataBuilder.BuildAppClosingTelemetryData(application);

                if (data is null)
                {
                    Log.Debug("No telemetry data found, skipping");
                    return;
                }

                Log.Debug("Pushing telemetry changes");
                await _transport.PushTelemetry(data).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error sending app-closing telemetry");
            }
        }
    }
}
