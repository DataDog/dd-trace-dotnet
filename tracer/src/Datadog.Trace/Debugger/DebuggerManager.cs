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

        private readonly object _syncLock;
        private readonly TimeSpan _diDebounceDelay;
        private readonly TaskCompletionSource<bool> _processExit;
        private TaskCompletionSource<bool> _dynamicInstrumentationInitializationGate;
        private int _initialized;
        private volatile bool _discoveryServiceReady;
        private Task? _dynamicInstrumentationTask;
        private CancellationTokenSource? _diDebounceCts;

        private DebuggerManager(DebuggerSettings debuggerSettings, ExceptionReplaySettings exceptionReplaySettings)
        {
            _initialized = 0;
            _discoveryServiceReady = false;
            DebuggerSettings = debuggerSettings;
            ExceptionReplaySettings = exceptionReplaySettings;
            ServiceName = string.Empty;
            _syncLock = new();
            _diDebounceDelay = TimeSpan.FromMilliseconds(300);
            _processExit = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _dynamicInstrumentationInitializationGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        internal static DebuggerManager Instance => _lazyInstance.Value;

        internal DebuggerSettings DebuggerSettings { get; private set; }

        internal ExceptionReplaySettings ExceptionReplaySettings { get; }

        internal DynamicInstrumentation? DynamicInstrumentation { get; private set; }

        internal SpanCodeOrigin.SpanCodeOrigin? CodeOrigin { get; private set; }

        internal IDebuggerUploader? SymbolsUploader { get; private set; }

        internal ExceptionReplay? ExceptionReplay { get; private set; }

        internal string ServiceName { get; private set; }

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

        private async Task<bool> WaitForDiscoveryServiceAsync(IDiscoveryService discoveryService, Task processExitTask, Task ongoingInitializationTask)
        {
            if (_discoveryServiceReady)
            {
                return true;
            }

            var tc = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            discoveryService.SubscribeToChanges(Callback);
            var completedTask = await Task.WhenAny(tc.Task, processExitTask, ongoingInitializationTask).ConfigureAwait(false);
            discoveryService.RemoveSubscription(Callback);
            if (completedTask == tc.Task)
            {
                return _discoveryServiceReady;
            }

            _discoveryServiceReady = false;
            return _discoveryServiceReady;

            void Callback(AgentConfiguration x)
            {
                if (string.IsNullOrEmpty(x.DebuggerEndpoint))
                {
                    return;
                }

                tc.TrySetResult(true);
                _discoveryServiceReady = true;
                discoveryService.RemoveSubscription(Callback);
            }
        }

        private void SetGeneralConfig(TracerSettings tracerSettings, DebuggerSettings settings)
        {
            DebuggerSnapshotSerializer.SetConfig(settings);
            Redaction.Instance.SetConfig(settings.RedactedIdentifiers, settings.RedactedExcludedIdentifiers, settings.RedactedTypes);
            ServiceName = GetServiceName(tracerSettings);
        }

        internal Task UpdateConfiguration(TracerSettings tracerSettings, DebuggerSettings? newDebuggerSettings = null)
        {
            return UpdateProductsState(tracerSettings, newDebuggerSettings ?? DebuggerSettings);
        }

        private Task UpdateProductsState(TracerSettings tracerSettings, DebuggerSettings newDebuggerSettings)
        {
            if (_processExit.Task.IsCompleted)
            {
                return _processExit.Task;
            }

            OneTimeSetup(tracerSettings);

            // Handle sync operations immediately with simple lock
            lock (_syncLock)
            {
                if (_processExit.Task.IsCompleted)
                {
                    return _processExit.Task;
                }

                DebuggerSettings = newDebuggerSettings;
                SetCodeOriginState(tracerSettings, newDebuggerSettings);
                SetExceptionReplayState(tracerSettings, newDebuggerSettings);
            }

            // Handle async DI operation separately - cancel previous and start new
            return ScheduleStartDynamicInstrumentation(tracerSettings, newDebuggerSettings);
        }

        private void OneTimeSetup(TracerSettings tracerSettings)
        {
            if (Interlocked.CompareExchange(ref _initialized, 1, 0) != 0)
            {
                return;
            }

            LifetimeManager.Instance.AddShutdownTask(ShutdownTasks);
            SetGeneralConfig(tracerSettings, DebuggerSettings);
            Task.Run(
                async () =>
                {
                    try
                    {
                        await InitializeSymbolUploader(tracerSettings).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Failed to initialize symbol uploader");
                    }
                });

            if (tracerSettings.StartupDiagnosticLogEnabled)
            {
                _ = Task.Run(WriteStartupDebuggerDiagnosticLog);
            }
        }

        private async Task InitializeSymbolUploader(TracerSettings tracerSettings)
        {
            try
            {
                if (_processExit.Task.IsCompleted)
                {
                    return;
                }

                if (!tracerSettings.IsRemoteConfigurationAvailable)
                {
                    Log.Information("Remote configuration is not available in this environment, so we don't upload symbols.");
                    return;
                }

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

        private void SetCodeOriginState(TracerSettings tracerSettings, DebuggerSettings debuggerSettings)
        {
            try
            {
                if (_processExit.Task.IsCompleted)
                {
                    return;
                }

                if (!debuggerSettings.CodeOriginForSpansEnabled)
                {
                    CodeOrigin = null;
                    if (debuggerSettings.DynamicSettings.CodeOriginEnabled == false)
                    {
                        Log.Information("Code Origin for Spans is disabled by remote enablement. To enable it, re-enable it via Datadog UI");
                        tracerSettings.Telemetry.Record(ConfigurationKeys.Debugger.CodeOriginForSpansEnabled, false, ConfigurationOrigins.RemoteConfig);
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

                CodeOrigin = new SpanCodeOrigin.SpanCodeOrigin(debuggerSettings);
                tracerSettings.Telemetry.Record(ConfigurationKeys.Debugger.CodeOriginForSpansEnabled, true, debuggerSettings.DynamicSettings.CodeOriginEnabled == true ? ConfigurationOrigins.RemoteConfig : ConfigurationOrigins.AppConfig);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error initializing Code Origin for Spans.");
            }
        }

        private void SetExceptionReplayState(TracerSettings tracerSettings, DebuggerSettings debuggerSettings)
        {
            try
            {
                if (_processExit.Task.IsCompleted)
                {
                    return;
                }

                if (!ExceptionReplaySettings.Enabled)
                {
                    SafeDisposal.TryDispose(ExceptionReplay);
                    ExceptionReplay = null;
                    if (debuggerSettings.DynamicSettings.ExceptionReplayEnabled == false)
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

                tracerSettings.Telemetry.Record(ConfigurationKeys.Debugger.ExceptionReplayEnabled, true, debuggerSettings.DynamicSettings.ExceptionReplayEnabled == true ? ConfigurationOrigins.RemoteConfig : ConfigurationOrigins.AppConfig);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error initializing Exception Replay.");
            }
        }

        private Task ScheduleStartDynamicInstrumentation(TracerSettings tracerSettings, DebuggerSettings debuggerSettings)
        {
            if (_processExit.Task.IsCompleted)
            {
                return _processExit.Task;
            }

            // Coalesce: cancel any in-flight debounce delay
            var cts = new CancellationTokenSource();
            var prev = Interlocked.Exchange(ref _diDebounceCts, cts);
            if (prev is not null)
            {
                SafeDisposal.TryExecute(() => prev.Cancel(), "cancel debounce");
                SafeDisposal.TryDispose(prev);
            }

            // Fire-and-forget debounce
            _ = Task.Run(
                async () =>
                {
                    try
                    {
                        await Task.Delay(_diDebounceDelay, cts.Token).ConfigureAwait(false);
                        if (!cts.IsCancellationRequested && !_processExit.Task.IsCompleted)
                        {
                            await StartDynamicInstrumentationAsync(tracerSettings, debuggerSettings).ConfigureAwait(false);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // The task was cancelled, which is expected if a new DI request comes in before the delay completes.
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error in DI debounce task");
                    }
                },
                cts.Token);

            return Task.CompletedTask;
        }

        private async Task StartDynamicInstrumentationAsync(TracerSettings tracerSettings, DebuggerSettings debuggerSettings)
        {
            if (_processExit.Task.IsCompleted)
            {
                return;
            }

            // Cancel any ongoing DI operation
            var oldGate = Interlocked.Exchange(ref _dynamicInstrumentationInitializationGate, new(TaskCreationOptions.RunContinuationsAsynchronously));
            oldGate.TrySetResult(false);
            SafeDisposal.TryDispose(oldGate.Task);

            if (_dynamicInstrumentationInitializationGate.Task.IsCompleted)
            {
                return;
            }

            // Wait for previous operation to actually stop (with timeout)
            var previousTask = Interlocked.Exchange(ref _dynamicInstrumentationTask, null);
            if (previousTask != null)
            {
                try
                {
                    var delayTask = Task.Delay(TimeSpan.FromSeconds(15));
                    await Task.WhenAny(delayTask, oldGate.Task, previousTask).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    Log.Warning("Previous Dynamic Instrumentation operation didn't stop within timeout");
                }
            }

            // Start new operation
            var newTask = SetDynamicInstrumentationStateAsync(tracerSettings, debuggerSettings, _dynamicInstrumentationInitializationGate.Task);
            _ = Interlocked.Exchange(ref _dynamicInstrumentationTask, newTask);

            try
            {
                await newTask.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in Dynamic Instrumentation async operation");
            }
        }

        private async Task SetDynamicInstrumentationStateAsync(TracerSettings tracerSettings, DebuggerSettings debuggerSettings, Task ongoingInitializationGateTask)
        {
            try
            {
                if (_processExit.Task.IsCompleted || ongoingInitializationGateTask.IsCompleted)
                {
                    return;
                }

                if (!debuggerSettings.DynamicInstrumentationEnabled)
                {
                    SafeDisposal.TryDispose(DynamicInstrumentation);
                    DynamicInstrumentation = null;

                    if (debuggerSettings.DynamicSettings.DynamicInstrumentationEnabled == false)
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
                    if (debuggerSettings.DynamicInstrumentationEnabled)
                    {
                        Log.Warning("Dynamic Instrumentation is enabled by environment variable but remote configuration is not available in this environment, so Dynamic Instrumentation cannot be enabled.");
                    }

                    tracerManager.Telemetry.ProductChanged(TelemetryProductType.DynamicInstrumentation, enabled: false, error: null);
                    return;
                }

                var sw = Stopwatch.StartNew();
                var discoveryService = tracerManager.DiscoveryService;
                if (!_discoveryServiceReady)
                {
                    var isDiscoverySuccessful = await WaitForDiscoveryServiceAsync(discoveryService, _processExit.Task, ongoingInitializationGateTask).ConfigureAwait(false);
                    if (!isDiscoverySuccessful)
                    {
                        return;
                    }

                    TelemetryFactory.Metrics.RecordDistributionSharedInitTime(MetricTags.InitializationComponent.DiscoveryService, sw.ElapsedMilliseconds);
                }

                if (ongoingInitializationGateTask.IsCompleted || _processExit.Task.IsCompleted)
                {
                    return;
                }

                DynamicInstrumentation = DebuggerFactory.CreateDynamicInstrumentation(
                    discoveryService,
                    RcmSubscriptionManager.Instance,
                    tracerSettings,
                    ServiceName,
                    debuggerSettings,
                    tracerManager.GitMetadataTagsProvider);
                Log.Debug("Dynamic Instrumentation has been created");

                sw.Restart();
                DynamicInstrumentation.Initialize();
                TelemetryFactory.Metrics.RecordDistributionSharedInitTime(MetricTags.InitializationComponent.DynamicInstrumentation, sw.ElapsedMilliseconds);
                tracerManager.Telemetry.ProductChanged(TelemetryProductType.DynamicInstrumentation, enabled: true, error: null);
                tracerSettings.Telemetry.Record(ConfigurationKeys.Debugger.DynamicInstrumentationEnabled, true, debuggerSettings.DynamicSettings.DynamicInstrumentationEnabled == true ? ConfigurationOrigins.RemoteConfig : ConfigurationOrigins.AppConfig);
            }
            catch (OperationCanceledException)
            {
                SafeDisposal.TryDispose(DynamicInstrumentation);
                DynamicInstrumentation = null;
            }
            catch (Exception ex)
            {
                TracerManager.Instance.Telemetry.ProductChanged(TelemetryProductType.DynamicInstrumentation, enabled: false, error: null);
                SafeDisposal.TryDispose(DynamicInstrumentation);
                DynamicInstrumentation = null;
                Log.Error(ex, "Error initializing Dynamic Instrumentation");
            }
        }

        private void WriteStartupDebuggerDiagnosticLog()
        {
            try
            {
                var stringWriter = new StringWriter();
                var settings = DebuggerSettings;
                using (var writer = new JsonTextWriter(stringWriter))
                {
                    writer.WritePropertyName("dynamic_instrumentation_enabled");
                    writer.WriteValue(settings.DynamicInstrumentationEnabled);
                    writer.WritePropertyName("code_origin_for_spans_enabled");
                    writer.WriteValue(settings.CodeOriginForSpansEnabled);
                    writer.WritePropertyName("exception_replay_enabled");
                    writer.WriteValue(ExceptionReplaySettings.Enabled);
                    writer.WritePropertyName("symbol_database_upload_enabled");
                    writer.WriteValue(settings.SymbolDatabaseUploadEnabled);
                }

                Log.Information("DATADOG DEBUGGER CONFIGURATION - {Configuration}", stringWriter.ToString());
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "DATADOG DEBUGGER DIAGNOSTICS - Error fetching configuration");
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

            // Interlocked.Exchange(ref _dynamicInstrumentationCancellation, null);
            SafeDisposal.New()
                        .Add(DynamicInstrumentation)
                        .Add(ExceptionReplay)
                        .Add(SymbolsUploader)
                        .DisposeAll();
        }
    }
}
