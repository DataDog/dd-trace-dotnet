// <copyright file="DebuggerManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Debugger.ExceptionAutoInstrumentation;
using Datadog.Trace.Debugger.Helpers;
using Datadog.Trace.Debugger.Sink;
using Datadog.Trace.Debugger.Snapshots;
using Datadog.Trace.Logging;
using Datadog.Trace.Processors;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Vendors.Newtonsoft.Json;

#nullable enable

namespace Datadog.Trace.Debugger
{
    internal class DebuggerManager
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DebuggerManager));

        private static readonly Lazy<DebuggerManager> _lazyInstance =
            new(
                () => new DebuggerManager(
                    DebuggerSettings.FromDefaultSource(),
                    ExceptionReplaySettings.FromDefaultSource()),
                LazyThreadSafetyMode.ExecutionAndPublication);

        private static volatile bool _discoveryServiceReady;
        private readonly SemaphoreSlim _semaphore;
        private readonly TaskCompletionSource<bool> _processExit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _initialized;

        private DebuggerManager(DebuggerSettings debuggerSettings, ExceptionReplaySettings exceptionReplaySettings)
        {
            _initialized = 0;
            _discoveryServiceReady = false;
            _semaphore = new SemaphoreSlim(1, 1);
            DebuggerSettings = debuggerSettings;
            ExceptionReplaySettings = exceptionReplaySettings;
            ServiceName = string.Empty;
        }

        internal static DebuggerManager Instance => _lazyInstance.Value;

        internal DebuggerSettings DebuggerSettings { get; private set; }

        internal ExceptionReplaySettings ExceptionReplaySettings { get; }

        internal DynamicInstrumentation? DynamicInstrumentation { get; private set; }

        internal SpanCodeOrigin.SpanCodeOrigin? CodeOrigin { get; private set; }

        internal IDebuggerUploader? SymbolsUploader { get; private set; }

        internal ExceptionReplay? ExceptionReplay { get; private set; }

        internal string ServiceName { get; private set; }

        private async Task<bool> WaitForDiscoveryServiceAsync(IDiscoveryService discoveryService, Task shutdownTask)
        {
            if (_discoveryServiceReady)
            {
                return true;
            }

            var tc = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            discoveryService.SubscribeToChanges(Callback);
            var completedTask = await Task.WhenAny(shutdownTask, tc.Task).ConfigureAwait(false);

            _discoveryServiceReady = completedTask == tc.Task;
            return _discoveryServiceReady;

            void Callback(AgentConfiguration x)
            {
                if (string.IsNullOrEmpty(x.DebuggerEndpoint))
                {
                    Log.Debug("`Debugger endpoint` is null.");
                    return;
                }

                tc.TrySetResult(true);
                discoveryService.RemoveSubscription(Callback);
            }
        }

        private void SetGeneralConfig(TracerSettings tracerSettings, DebuggerSettings settings)
        {
            DebuggerSnapshotSerializer.SetConfig(settings);
            Redaction.Instance.SetConfig(settings.RedactedIdentifiers, settings.RedactedExcludedIdentifiers, settings.RedactedTypes);
            ServiceName = GetServiceName(tracerSettings);
        }

        private string GetServiceName(TracerSettings tracerSettings)
        {
            try
            {
                return TraceUtil.NormalizeTag(tracerSettings.ServiceName ?? TracerManager.Instance.DefaultServiceName);
            }
            catch (Exception e)
            {
                Log.Error(e, "Could not set `DynamicInstrumentationHelper.ServiceName`.");
                return TracerManager.Instance.DefaultServiceName;
            }
        }

        private void SetCodeOriginState(TracerSettings tracerSettings)
        {
            try
            {
                if (!DebuggerSettings.CodeOriginForSpansEnabled)
                {
                    CodeOrigin = null;
                    if (DebuggerSettings.DynamicSettings.CodeOriginEnabled == false)
                    {
                        Log.Information("Code Origin for Spans is disabled by remote enablement. To enable it, re-enable it via Datadog UI");
                        tracerSettings.Telemetry.Record(ConfigurationKeys.Debugger.DynamicInstrumentationEnabled, false, ConfigurationOrigins.RemoteConfig);
                    }
                    else
                    {
                        Log.Information("Code Origin for Spans is disabled by. To enable it, please set {CodeOriginForSpansEnabled} environment variable to '1'/'true'.", ConfigurationKeys.Debugger.CodeOriginForSpansEnabled);
                    }

                    return;
                }

                if (CodeOrigin != null)
                {
                    Log.Debug("Code Origin for Spans is already initialized");
                    return;
                }

                CodeOrigin = new SpanCodeOrigin.SpanCodeOrigin(DebuggerSettings);
                tracerSettings.Telemetry.Record(ConfigurationKeys.Debugger.CodeOriginForSpansEnabled, true, DebuggerSettings.DynamicSettings.CodeOriginEnabled == true ? ConfigurationOrigins.RemoteConfig : ConfigurationOrigins.AppConfig);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error initializing Code Origin for Spans.");
            }
        }

        private void SetExceptionReplayState(TracerSettings tracerSettings)
        {
            try
            {
                if (!ExceptionReplaySettings.Enabled)
                {
                    SafeDisposal.TryDispose(ExceptionReplay);
                    ExceptionReplay = null;
                    if (DebuggerSettings.DynamicSettings.ExceptionReplayEnabled == false)
                    {
                        Log.Information("Exception Replay is disabled by remote enablement. To enable it, re-enable it via Datadog UI");
                        tracerSettings.Telemetry.Record(ConfigurationKeys.Debugger.ExceptionReplayEnabled, false, ConfigurationOrigins.RemoteConfig);
                    }
                    else
                    {
                        Log.Information("Exception Replay is disabled. To enable it, please set {ExceptionReplayEnabled} environment variable to '1'/'true'.", ConfigurationKeys.Debugger.ExceptionReplayEnabled);
                    }

                    return;
                }

                if (ExceptionReplay != null)
                {
                    Log.Debug("Exception Replay is already initialized");
                    return;
                }

                var exceptionReplay = ExceptionReplay.Create(ExceptionReplaySettings);
                exceptionReplay.Initialize();
                ExceptionReplay = exceptionReplay;

                tracerSettings.Telemetry.Record(ConfigurationKeys.Debugger.ExceptionReplayEnabled, true, DebuggerSettings.DynamicSettings.ExceptionReplayEnabled == true ? ConfigurationOrigins.RemoteConfig : ConfigurationOrigins.AppConfig);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error initializing Exception Replay.");
            }
        }

        private async Task SetDynamicInstrumentationState(TracerSettings tracerSettings)
        {
            try
            {
                if (!DebuggerSettings.DynamicInstrumentationEnabled)
                {
                    SafeDisposal.TryDispose(DynamicInstrumentation);
                    DynamicInstrumentation = null;

                    if (DebuggerSettings.DynamicSettings.DynamicInstrumentationEnabled == false)
                    {
                        Log.Information("Dynamic Instrumentation is disabled by remote enablement. To enable it, re-enable it via Datadog UI");
                        TracerManager.Instance.Telemetry.ProductChanged(TelemetryProductType.DynamicInstrumentation, enabled: false, error: null);
                        tracerSettings.Telemetry.Record(ConfigurationKeys.Debugger.DynamicInstrumentationEnabled, false, ConfigurationOrigins.RemoteConfig);
                    }
                    else
                    {
                        Log.Information("Dynamic Instrumentation is disabled. To enable it, please set {DynamicInstrumentationEnabled} environment variable to 'true'.", ConfigurationKeys.Debugger.DynamicInstrumentationEnabled);
                    }

                    return;
                }

                if (DynamicInstrumentation != null)
                {
                    Log.Debug("Dynamic Instrumentation is already initialized");
                    return;
                }

                var tracerManager = TracerManager.Instance;

                if (!tracerSettings.IsRemoteConfigurationAvailable)
                {
                    if (DebuggerSettings.DynamicInstrumentationEnabled)
                    {
                        Log.Warning("Dynamic Instrumentation is enabled by environment variable but remote configuration is not available in this environment, so Dynamic Instrumentation cannot be enabled.");
                    }

                    tracerManager.Telemetry.ProductChanged(TelemetryProductType.DynamicInstrumentation, enabled: false, error: null);
                    return;
                }

                var discoveryService = tracerManager.DiscoveryService;
                DynamicInstrumentation = DebuggerFactory.CreateDynamicInstrumentation(
                    discoveryService,
                    RcmSubscriptionManager.Instance,
                    tracerSettings,
                    ServiceName,
                    DebuggerSettings,
                    tracerManager.GitMetadataTagsProvider);
                Log.Debug("Dynamic Instrumentation has been created.");

                var sw = Stopwatch.StartNew();
                if (!_discoveryServiceReady)
                {
                    var isDiscoverySuccessful = await WaitForDiscoveryServiceAsync(discoveryService, _processExit.Task).ConfigureAwait(false);
                    if (!isDiscoverySuccessful)
                    {
                        Log.Warning("Discovery service is not ready, Dynamic Instrumentation will not be initialized.");
                        return;
                    }

                    TelemetryFactory.Metrics.RecordDistributionSharedInitTime(MetricTags.InitializationComponent.DiscoveryService, sw.ElapsedMilliseconds);
                }

                sw.Restart();
                DynamicInstrumentation.Initialize();
                TelemetryFactory.Metrics.RecordDistributionSharedInitTime(MetricTags.InitializationComponent.DynamicInstrumentation, sw.ElapsedMilliseconds);
                tracerManager.Telemetry.ProductChanged(TelemetryProductType.DynamicInstrumentation, enabled: true, error: null);
                tracerSettings.Telemetry.Record(ConfigurationKeys.Debugger.DynamicInstrumentationEnabled, true, DebuggerSettings.DynamicSettings.DynamicInstrumentationEnabled == true ? ConfigurationOrigins.RemoteConfig : ConfigurationOrigins.AppConfig);
            }
            catch (Exception ex)
            {
                TracerManager.Instance.Telemetry.ProductChanged(TelemetryProductType.DynamicInstrumentation, enabled: false, error: null);
                Log.Error(ex, "Error initializing Dynamic Instrumentation.");
            }
        }

        internal Task UpdateConfiguration(TracerSettings tracerSettings, DebuggerSettings? newDebuggerSettings = null)
        {
            return UpdateProductsState(tracerSettings, newDebuggerSettings ?? DebuggerSettings);
        }

        private async Task UpdateProductsState(TracerSettings tracerSettings, DebuggerSettings newDebuggerSettings)
        {
            if (_processExit.Task.IsCompleted)
            {
                return;
            }

            OneTimeSetup(tracerSettings);

            bool semaphoreAcquired = false;
            try
            {
                var attemptsRemaining = 6; // 5*6 = 30s timeout
                while (!_processExit.Task.IsCompleted && !semaphoreAcquired && attemptsRemaining > 0)
                {
                    attemptsRemaining--;
                    semaphoreAcquired = await _semaphore.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                }

                if (_processExit.Task.IsCompleted)
                {
                    Log.Debug("Skipping update debugger state due to process exit");
                    return;
                }

                if (!semaphoreAcquired)
                {
                    Log.Debug("Skipping update debugger state due to semaphore timed out");
                    return;
                }

                DebuggerSettings = newDebuggerSettings;
                SetCodeOriginState(tracerSettings);
                SetExceptionReplayState(tracerSettings);
                await SetDynamicInstrumentationState(tracerSettings).ConfigureAwait(false);
                if (tracerSettings.StartupDiagnosticLogEnabled)
                {
                    _ = Task.Run(WriteDebuggerDiagnosticLog);
                }
            }
            catch (OperationCanceledException)
            {
                // Handle cancellation gracefully
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error updating debugger state");
            }
            finally
            {
                if (semaphoreAcquired)
                {
                    try
                    {
                        _semaphore.Release();
                    }
                    catch
                    {
                        // ignore ObjectDisposedException or SemaphoreFullException
                    }
                }
            }
        }

        private void WriteDebuggerDiagnosticLog()
        {
            try
            {
                var stringWriter = new StringWriter();
                using (var writer = new JsonTextWriter(stringWriter))
                {
                    var debuggerSettings = DebuggerSettings;
                    writer.WritePropertyName("dynamic_instrumentation_enabled");
                    writer.WriteValue(debuggerSettings.DynamicInstrumentationEnabled);
                    writer.WritePropertyName("symbol_database_upload_enabled");
                    writer.WriteValue(debuggerSettings.SymbolDatabaseUploadEnabled);
                    writer.WritePropertyName("code_origin_for_spans_enabled");
                    writer.WriteValue(debuggerSettings.CodeOriginForSpansEnabled);
                    writer.WritePropertyName("symbol_database_upload_enabled");
                    writer.WriteValue(DebuggerSettings.SymbolDatabaseUploadEnabled);
                    writer.WritePropertyName("exception_replay_enabled");
                    writer.WriteValue(ExceptionReplaySettings.Enabled);
                }

                Log.Information("DATADOG DEBUGGER CONFIGURATION - {Configuration}", stringWriter.ToString());
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "DATADOG DEBUGGER DIAGNOSTICS - Error fetching configuration");
            }
        }

        private void OneTimeSetup(TracerSettings tracerSettings)
        {
            if (Interlocked.CompareExchange(ref _initialized, 1, 0) != 0)
            {
                return;
            }

            LifetimeManager.Instance.AddShutdownTask(ShutdownTasks);
            SetGeneralConfig(tracerSettings, DebuggerSettings);
            _ = InitializeSymbolUploader(tracerSettings);
        }

        private async Task InitializeSymbolUploader(TracerSettings tracerSettings)
        {
            try
            {
                var tracerManager = TracerManager.Instance;
                SymbolsUploader = DebuggerFactory.CreateSymbolsUploader(tracerManager.DiscoveryService, RcmSubscriptionManager.Instance, Instance.ServiceName, tracerSettings, DebuggerSettings, tracerManager.GitMetadataTagsProvider);
                // it will do nothing if it is an instance of NoOpSymbolUploader
                await SymbolsUploader.StartFlushingAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error initializing Symbol Database.");
            }
        }

        private void ShutdownTasks(Exception? ex)
        {
            if (_processExit.Task.IsCompleted)
            {
                return;
            }

            _processExit.TrySetResult(true);

            if (ex != null)
            {
                Log.Debug(ex, "Shutdown task for DebuggerManager is running with exception");
            }

            SafeDisposal.New()
                        .Add(DynamicInstrumentation)
                        .Add(ExceptionReplay)
                        .Add(SymbolsUploader)
                        .Add(_semaphore)
                        .DisposeAll();
        }
    }
}
