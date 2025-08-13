// <copyright file="DebuggerManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
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
        private volatile bool _isDebuggerEndpointAvailable;
        private int _initialized;
        private CancellationTokenSource? _diDebounceCts;

        private DebuggerManager(DebuggerSettings debuggerSettings, ExceptionReplaySettings exceptionReplaySettings)
        {
            _initialized = 0;
            _isDebuggerEndpointAvailable = false;
            DebuggerSettings = debuggerSettings;
            ExceptionReplaySettings = exceptionReplaySettings;
            ServiceName = string.Empty;
            _syncLock = new();
            _diDebounceDelay = TimeSpan.FromMilliseconds(300);
            _processExit = new(TaskCreationOptions.RunContinuationsAsynchronously);
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

        private async Task<bool> WaitForDebuggerEndpointAsync(IDiscoveryService discoveryService, Task processExitTask, Task ongoingInitializationTask)
        {
            if (_isDebuggerEndpointAvailable)
            {
                return true;
            }

            var debuggerEndpointTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            discoveryService.SubscribeToChanges(Callback);

            try
            {
                var debuggerEndpointTimeout = TimeSpan.FromMinutes(5);
                var timeoutTask = Task.Delay(debuggerEndpointTimeout);
                var completedTask = await Task.WhenAny(debuggerEndpointTcs.Task, timeoutTask, processExitTask, ongoingInitializationTask).ConfigureAwait(false);
                if (completedTask == timeoutTask)
                {
                    Log.Warning("Debugger endpoint is not available after waiting {Timeout} seconds.", debuggerEndpointTimeout.TotalSeconds);
                    return false;
                }

                return completedTask == debuggerEndpointTcs.Task;
            }
            finally
            {
                discoveryService.RemoveSubscription(Callback);
            }

            void Callback(AgentConfiguration x)
            {
                _isDebuggerEndpointAvailable = !string.IsNullOrEmpty(x.DebuggerEndpoint);
                if (_isDebuggerEndpointAvailable)
                {
                    debuggerEndpointTcs.TrySetResult(true);
                }
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
            InitializeSymbolUploader(tracerSettings);
            if (tracerSettings.StartupDiagnosticLogEnabled)
            {
                _ = Task.Run(WriteStartupDebuggerDiagnosticLog);
            }
        }

        private void InitializeSymbolUploader(TracerSettings tracerSettings)
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
                SymbolsUploader = DebuggerFactory.CreateSymbolsUploader(tracerManager.DiscoveryService, RcmSubscriptionManager.Instance, ServiceName, tracerSettings, DebuggerSettings, tracerManager.GitMetadataTagsProvider);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await SymbolsUploader.StartFlushingAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Failed to initialize symbol uploader");
                    }
                });
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
                        Log.Information("Code Origin for Spans is disabled. To enable it, please set {CodeOriginForSpansEnabled} environment variable to '1'/'true'.", ConfigurationKeys.Debugger.CodeOriginForSpansEnabled);
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

        private async Task ScheduleStartDynamicInstrumentation(TracerSettings tracerSettings, DebuggerSettings debuggerSettings)
        {
            if (_processExit.Task.IsCompleted)
            {
                return;
            }

            if (!tracerSettings.IsRemoteConfigurationAvailable)
            {
                if (debuggerSettings.DynamicInstrumentationEnabled)
                {
                    Log.Warning("Remote configuration is not available in this environment, so Dynamic Instrumentation cannot be enabled.");
                }

                return;
            }

            var requestedState = debuggerSettings.DynamicInstrumentationEnabled;
            var currentState = DynamicInstrumentation is { IsDisposed: false };

            // Only proceed if state actually needs to change
            if (requestedState == currentState)
            {
                return;
            }

            // Cancel any pending operation and start new one
            var cts = new CancellationTokenSource();
            var prevCts = Interlocked.Exchange(ref _diDebounceCts, cts);

            try
            {
                prevCts?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Previous token was already disposed, ignore
            }

            try
            {
                await Task.Delay(_diDebounceDelay, cts.Token).ConfigureAwait(false);

                // Re-check if state change is still needed after debounce
                if (!cts.IsCancellationRequested)
                {
                    var finalRequestedState = DebuggerSettings.DynamicInstrumentationEnabled;
                    var finalCurrentState = DynamicInstrumentation is { IsDisposed: false };

                    if (finalRequestedState != finalCurrentState)
                    {
                        await SetDynamicInstrumentationStateAsync(tracerSettings, DebuggerSettings, cts.Token).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in DI debounce task");
            }
            finally
            {
                Interlocked.CompareExchange(ref _diDebounceCts, null, cts);
                SafeDisposal.TryDispose(cts);
                SafeDisposal.TryDispose(prevCts);
            }
        }

        private async Task SetDynamicInstrumentationStateAsync(TracerSettings tracerSettings, DebuggerSettings debuggerSettings, CancellationToken cancellationToken)
        {
            try
            {
                if (_processExit.Task.IsCompleted || cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                var requestedState = debuggerSettings.DynamicInstrumentationEnabled;
                var currentState = DynamicInstrumentation is { IsDisposed: false };

                if (!requestedState)
                {
                    DisableDynamicInstrumentation(currentState);
                    return;
                }

                if (currentState)
                {
                    Log.Debug("Dynamic Instrumentation is already initialized");
                    return;
                }

                var tracerManager = TracerManager.Instance;
                var discoveryService = tracerManager.DiscoveryService;
                if (!_isDebuggerEndpointAvailable)
                {
                    var isDiscoverySuccessful = await WaitForDebuggerEndpointAsync(discoveryService, _processExit.Task, Task.FromCanceled(cancellationToken)).ConfigureAwait(false);
                    if (!isDiscoverySuccessful)
                    {
                        return;
                    }
                }

                if (cancellationToken.IsCancellationRequested || _processExit.Task.IsCompleted)
                {
                    return;
                }

                EnableDynamicInstrumentation(discoveryService, tracerManager);
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

            void DisableDynamicInstrumentation(bool currentState)
            {
                var origin = debuggerSettings.DynamicSettings.DynamicInstrumentationEnabled == false
                                 ? ConfigurationOrigins.RemoteConfig
                                 : ConfigurationOrigins.AppConfig;

                if (currentState)
                {
                    SafeDisposal.TryDispose(DynamicInstrumentation);
                    DynamicInstrumentation = null;
                    TracerManager.Instance.Telemetry.ProductChanged(TelemetryProductType.DynamicInstrumentation, enabled: false, error: null);
                    tracerSettings.Telemetry.Record(ConfigurationKeys.Debugger.DynamicInstrumentationEnabled, false, origin);
                }

                if (origin == ConfigurationOrigins.RemoteConfig)
                {
                    Log.Information("Dynamic Instrumentation is disabled by remote enablement. To enable it, re-enable it via Datadog UI");
                }
                else
                {
                    Log.Information("Dynamic Instrumentation is disabled. To enable it, please set {DynamicInstrumentationEnabled} environment variable to 'true'.", ConfigurationKeys.Debugger.DynamicInstrumentationEnabled);
                }
            }

            void EnableDynamicInstrumentation(IDiscoveryService discoveryService, TracerManager tracerManager)
            {
                DynamicInstrumentation = DebuggerFactory.CreateDynamicInstrumentation(
                    discoveryService,
                    RcmSubscriptionManager.Instance,
                    tracerSettings,
                    ServiceName,
                    debuggerSettings,
                    tracerManager.GitMetadataTagsProvider);
                Log.Debug("Dynamic Instrumentation has been created");
                DynamicInstrumentation.Initialize();
                tracerManager.Telemetry.ProductChanged(TelemetryProductType.DynamicInstrumentation, enabled: true, error: null);
                tracerSettings.Telemetry.Record(ConfigurationKeys.Debugger.DynamicInstrumentationEnabled, true, debuggerSettings.DynamicSettings.DynamicInstrumentationEnabled == true ? ConfigurationOrigins.RemoteConfig : ConfigurationOrigins.AppConfig);
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
                    writer.WriteStartObject();
                    writer.WritePropertyName("dynamic_instrumentation_enabled");
                    writer.WriteValue(settings.DynamicInstrumentationEnabled);
                    writer.WritePropertyName("code_origin_for_spans_enabled");
                    writer.WriteValue(settings.CodeOriginForSpansEnabled);
                    writer.WritePropertyName("exception_replay_enabled");
                    writer.WriteValue(ExceptionReplaySettings.Enabled);
                    writer.WritePropertyName("symbol_database_upload_enabled");
                    writer.WriteValue(settings.SymbolDatabaseUploadEnabled);
                    writer.WriteEndObject();
                }

                Log.Information("DATADOG DEBUGGER CONFIGURATION - {Configuration}", stringWriter.ToString());
            }
            catch
            {
                // ignored
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
                        .DisposeAll();
        }
    }
}
