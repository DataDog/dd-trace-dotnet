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
        private readonly TelemetryCircuitBreaker _circuitBreaker = new();

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

            try
            {
                // Registering for the AppDomain.AssemblyLoad event cannot be called by a security transparent method
                // This will only happen if the Tracer is not run full-trust
                AppDomain.CurrentDomain.AssemblyLoad += CurrentDomain_OnAssemblyLoad;
                var assembliesLoaded = AppDomain.CurrentDomain.GetAssemblies();

                foreach (var t in assembliesLoaded)
                {
                    RecordAssembly(t);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Unable to register a callback to the AppDomain.AssemblyLoad event. Telemetry collection of loaded assemblies will be disabled.");
            }

            _telemetryTask = Task.Run(PushTelemetryLoopAsync);
        }

        public bool FatalError { get; private set; }

        public void RecordTracerSettings(ImmutableTracerSettings settings, string defaultServiceName, AzureAppServices appServicesMetadata)
        {
            _configuration.RecordTracerSettings(settings, defaultServiceName, appServicesMetadata);
            _integrations.RecordTracerSettings(settings);
        }

        public void Start()
        {
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

        public void Dispose(bool sendAppClosingTelemetry)
        {
            TerminateLoop(sendAppClosingTelemetry);
            _telemetryTask.GetAwaiter().GetResult();
        }

        public void Dispose()
        {
            Dispose(sendAppClosingTelemetry: true);
        }

        private void TerminateLoop(bool sendAppClosingTelemetry)
        {
            // If there's a fatal error, TerminateLoop() may be called more than once
            // (at error-time, and at process end). The following are idempotent so that's safe.
            _processExit.TrySetResult(sendAppClosingTelemetry);
            _tracerInitialized.TrySetResult(true);
            AppDomain.CurrentDomain.AssemblyLoad -= CurrentDomain_OnAssemblyLoad;
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
            tasks[1] = _processExit.Task;

            // wait for initialization before trying to send first telemetry
            // .NET 5.0 has an explicit overload for this
            await Task.WhenAny(tasks).ConfigureAwait(false);
#else
            await Task.WhenAny(_tracerInitialized.Task, _processExit.Task).ConfigureAwait(false);
#endif

            while (true)
            {
                if (_processExit.Task.IsCompleted)
                {
                    Log.Debug("Process exit requested, ending telemetry loop");
                    var sendAppClosingTelemetry = _processExit.Task.Result;
                    if (sendAppClosingTelemetry)
                    {
                        await PushTelemetry(isFinalPush: true).ConfigureAwait(false);
                    }

                    return;
                }

                await PushTelemetry(isFinalPush: false).ConfigureAwait(false);

#if NET5_0_OR_GREATER
                // .NET 5.0 has an explicit overload for this
                await Task.WhenAny(Task.Delay(_sendFrequency), _processExit.Task).ConfigureAwait(false);
#else
                tasks[0] = Task.Delay(_sendFrequency);
                await Task.WhenAny(tasks).ConfigureAwait(false);
#endif
            }
        }

        private async Task PushTelemetry(bool isFinalPush)
        {
            try
            {
                var application = _configuration.GetApplicationData();
                var host = _configuration.GetHostData();
                if (application is null || host is null)
                {
                    Log.Debug("Telemetry not initialized, skipping");
                    return;
                }

                // These calls change the state of the collectors, so must use the data
                var success = await PushTelemetry(application, host, sendHeartbeat: !isFinalPush).ConfigureAwait(false);
                if (!success)
                {
                    if (isFinalPush)
                    {
                        Log.Debug("Unable to send final telemetry, skipping app-closing telemetry ");
                        return;
                    }
                    else
                    {
                        FatalError = true;
                        Log.Debug("Unable to send telemetry, ending telemetry loop");
                        TerminateLoop(sendAppClosingTelemetry: false);
                    }
                }

                if (isFinalPush)
                {
                    var closingTelemetryData = _dataBuilder.BuildAppClosingTelemetryData(application, host);

                    Log.Debug("Pushing app-closing telemetry");
                    await _transport.PushTelemetry(closingTelemetryData).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error pushing telemetry");
            }
        }

        private async Task<bool> PushTelemetry(ApplicationTelemetryData application, HostTelemetryData host, bool sendHeartbeat)
        {
            // use values from previous failed attempt if necessary
            var configuration = _configuration.GetConfigurationData() ?? _circuitBreaker.PreviousConfiguration;
            var dependencies = _dependencies.GetData() ?? _circuitBreaker.PreviousDependencies;
            var integrations = _integrations.GetData() ?? _circuitBreaker.PreviousIntegrations;

            var data = _dataBuilder.BuildTelemetryData(application, host, configuration, dependencies, integrations, sendHeartbeat);
            if (data.Length == 0)
            {
                return true;
            }

            Log.Debug("Pushing telemetry changes");
            foreach (var telemetryData in data)
            {
                var result = await _transport.PushTelemetry(telemetryData).ConfigureAwait(false);
                if (_circuitBreaker.Evaluate(result, configuration, dependencies, integrations) == TelemetryPushResult.FatalError)
                {
                    // big problem, abandon hope
                    return false;
                }
            }

            return true;
        }
    }
}
