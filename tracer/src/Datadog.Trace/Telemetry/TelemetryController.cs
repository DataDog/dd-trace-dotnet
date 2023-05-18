// <copyright file="TelemetryController.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.Configuration;
using Datadog.Trace.ContinuousProfiler;
using Datadog.Trace.Iast.Settings;
using Datadog.Trace.Logging;
using Datadog.Trace.PlatformHelpers;

namespace Datadog.Trace.Telemetry
{
    internal class TelemetryController : ITelemetryController
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<TelemetryController>();
        private readonly ConfigurationTelemetryCollector _configuration;
        private readonly DependencyTelemetryCollector _dependencies;
        private readonly IntegrationTelemetryCollector _integrations;
        private readonly TelemetryDataBuilder _dataBuilder = new();
        private readonly TelemetryTransportManager _transportManager;
        private readonly TimeSpan _flushInterval;
        private readonly TimeSpan _heartBeatInterval;
        private readonly TaskCompletionSource<bool> _tracerInitialized = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _processExit = new();
        private readonly Task _flushTask;
        private readonly Task _heartbeatTask;
        private bool _fatalError;

        internal TelemetryController(
            ConfigurationTelemetryCollector configuration,
            DependencyTelemetryCollector dependencies,
            IntegrationTelemetryCollector integrations,
            TelemetryTransportManager transportManager,
            TimeSpan flushInterval,
            TimeSpan heartBeatInterval)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _dependencies = dependencies ?? throw new ArgumentNullException(nameof(dependencies));
            _integrations = integrations ?? throw new ArgumentNullException(nameof(integrations));
            _flushInterval = flushInterval;
            _heartBeatInterval = heartBeatInterval;
            _transportManager = transportManager ?? throw new ArgumentNullException(nameof(transportManager));

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

            _flushTask = Task.Run(PushTelemetryLoopAsync);
            _heartbeatTask = Task.Run(PushHeartbeatLoopAsync);
        }

        public bool FatalError => Volatile.Read(ref _fatalError);

        public void RecordTracerSettings(ImmutableTracerSettings settings, string defaultServiceName)
        {
            _configuration.RecordTracerSettings(settings, defaultServiceName);
            _integrations.RecordTracerSettings(settings);
        }

        public void Start()
        {
            _tracerInitialized.TrySetResult(true);
        }

        public void ProductChanged(TelemetryProductType product, bool enabled, ErrorData? error)
        {
            // Not implemented in V1 of telemetry
        }

        public void RecordSecuritySettings(SecuritySettings settings)
            => _configuration.RecordSecuritySettings(settings);

        public void RecordIastSettings(IastSettings settings)
            => _configuration.RecordIastSettings(settings);

        public void RecordProfilerSettings(Profiler profiler)
            => _configuration.RecordProfilerSettings(profiler);

        public void IntegrationRunning(IntegrationId integrationId)
            => _integrations.IntegrationRunning(integrationId);

        public void IntegrationGeneratedSpan(IntegrationId integrationId)
            => _integrations.IntegrationGeneratedSpan(integrationId);

        public void IntegrationDisabledDueToError(IntegrationId integrationId, string error)
            => _integrations.IntegrationDisabledDueToError(integrationId, error);

        public async Task DisposeAsync(bool sendAppClosingTelemetry)
        {
            TerminateLoop(sendAppClosingTelemetry);
            await _flushTask.ConfigureAwait(false);
            await _heartbeatTask.ConfigureAwait(false);
        }

        public Task DisposeAsync()
        {
            return DisposeAsync(sendAppClosingTelemetry: true);
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
                _dependencies.AssemblyLoaded(assembly);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error recording loaded assembly");
            }
        }

        private async Task PushHeartbeatLoopAsync()
        {
#if !NET5_0_OR_GREATER
            var tasks = new Task[2];
            tasks[0] = _tracerInitialized.Task;
            tasks[1] = _processExit.Task;

            // wait for initialization before trying to send first heartbeat
            // .NET 5.0 has an explicit overload for this
            await Task.WhenAny(tasks).ConfigureAwait(false);
#else
            await Task.WhenAny(_tracerInitialized.Task, _processExit.Task).ConfigureAwait(false);
#endif

            while (true)
            {
#if NET5_0_OR_GREATER
                // .NET 5.0 has an explicit overload for this
                await Task.WhenAny(Task.Delay(_heartBeatInterval), _processExit.Task).ConfigureAwait(false);
#else
                tasks[0] = Task.Delay(_heartBeatInterval);
                await Task.WhenAny(tasks).ConfigureAwait(false);
#endif

                if (_processExit.Task.IsCompleted)
                {
                    Log.Debug("Process exit requested, ending heartbeat loop");
                    return;
                }

                await PushHeartbeatAsync().ConfigureAwait(false);
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
                await Task.WhenAny(Task.Delay(_flushInterval), _processExit.Task).ConfigureAwait(false);
#else
                tasks[0] = Task.Delay(_flushInterval);
                await Task.WhenAny(tasks).ConfigureAwait(false);
#endif
            }
        }

        private async Task PushHeartbeatAsync()
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

                var heartbeatData = _dataBuilder.BuildHeartbeatData(application, host);
                var result = await _transportManager.TryPushTelemetry(heartbeatData).ConfigureAwait(false);
                if (!result)
                {
                    _fatalError = true;
                    Log.Debug("Unable to send heartbeat, ending heartbeat loop");
                    TerminateLoop(sendAppClosingTelemetry: false);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error pushing heartbeat");
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
                var success = await PushTelemetry(application, host).ConfigureAwait(false);
                if (!success)
                {
                    if (isFinalPush)
                    {
                        Log.Debug("Unable to send final telemetry, skipping app-closing telemetry ");
                        return;
                    }
                    else
                    {
                        _fatalError = true;
                        Log.Debug("Unable to send telemetry, ending telemetry loop");
                        TerminateLoop(sendAppClosingTelemetry: false);
                    }
                }

                if (isFinalPush)
                {
                    var closingTelemetryData = _dataBuilder.BuildAppClosingTelemetryData(application, host);

                    Log.Debug("Pushing app-closing telemetry");
                    await _transportManager.TryPushTelemetry(closingTelemetryData).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error pushing telemetry");
            }
        }

        private async Task<bool> PushTelemetry(ApplicationTelemetryData application, HostTelemetryData host)
        {
            // use values from previous failed attempt if necessary
            var configuration = _configuration.GetConfigurationData() ?? _transportManager.PreviousConfiguration;
            var newDependencies = _dependencies.GetData();
            if (newDependencies is not null && _transportManager.PreviousDependencies is { } previousDependencies)
            {
                newDependencies.AddRange(previousDependencies);
            }

            var aggregatedDependencies = newDependencies ?? _transportManager.PreviousDependencies;
            var integrations = _integrations.GetData() ?? _transportManager.PreviousIntegrations;

            var data = _dataBuilder.BuildTelemetryData(application, host, configuration, aggregatedDependencies, integrations);
            if (data.Length == 0)
            {
                return true;
            }

            Log.Debug("Pushing telemetry changes");
            foreach (var telemetryData in data)
            {
                var result = await _transportManager.TryPushTelemetry(telemetryData, configuration, aggregatedDependencies, integrations).ConfigureAwait(false);
                if (!result)
                {
                    // big problem, abandon hope
                    return false;
                }
            }

            return true;
        }
    }
}
