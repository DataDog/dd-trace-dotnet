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
    internal sealed class DebuggerManager
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DebuggerManager));
        private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(250);
        private static readonly TimeSpan EndpointTimeout = TimeSpan.FromMinutes(5);
        internal static readonly Func<string> ServiceNameProvider = static () => Instance.ServiceName;

        private static readonly Lazy<DebuggerManager> _lazyInstance =
            new(
                () => new DebuggerManager(
                    DebuggerSettings.FromDefaultSource(),
                    ExceptionReplaySettings.FromDefaultSource()),
                LazyThreadSafetyMode.ExecutionAndPublication);

        private readonly object _syncLock;
        private readonly TimeSpan _diDebounceDelay;
        private readonly TaskCompletionSource<bool> _processExit;
        private string _serviceName;
        private volatile bool _isDebuggerEndpointAvailable;
        private int _initialized;
        private int _symDbInitialized;
        private volatile TaskCompletionSource<bool>? _diDebounceGate;
        private volatile DynamicInstrumentation? _dynamicInstrumentation;
        private int _diState; // 0 = disabled, 1 = initializing, 2 = initialized
        private TracerSettings.SettingsManager? _subscribedSettingsManager;
        private IDisposable? _tracerSettingsSubscription;

        private DebuggerManager(DebuggerSettings debuggerSettings, ExceptionReplaySettings exceptionReplaySettings)
        {
            _initialized = 0;
            _symDbInitialized = 0;
            _isDebuggerEndpointAvailable = false;
            DebuggerSettings = debuggerSettings;
            ExceptionReplaySettings = exceptionReplaySettings;
            _serviceName = string.Empty;
            _syncLock = new();
            _diDebounceDelay = DebounceDelay;
            _processExit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        internal static DebuggerManager Instance => _lazyInstance.Value;

        internal DebuggerSettings DebuggerSettings { get; private set; }

        internal ExceptionReplaySettings ExceptionReplaySettings { get; }

        internal DynamicInstrumentation? DynamicInstrumentation
        {
            get
            {
                var instance = _dynamicInstrumentation;
                return instance is { IsDisposed: false, IsInitialized: true } ? instance : null;
            }

            private set => _dynamicInstrumentation = value;
        }

        internal SpanCodeOrigin.SpanCodeOrigin? CodeOrigin { get; private set; }

        internal IDebuggerUploader? SymbolsUploader { get; private set; }

        internal ExceptionReplay? ExceptionReplay { get; private set; }

        internal string ServiceName
        {
            get => Volatile.Read(ref _serviceName);
            private set => Volatile.Write(ref _serviceName, value);
        }

        private string GetServiceName(MutableSettings mutableSettings)
        {
            try
            {
                return TraceUtil.NormalizeTag(mutableSettings.DefaultServiceName);
            }
            catch (Exception e)
            {
                Log.Error(e, "Could not set `DynamicInstrumentationHelper.ServiceName`.");
                return mutableSettings.DefaultServiceName;
            }
        }

        private async Task<bool> WaitForDebuggerEndpointAsync(IDiscoveryService discoveryService, Task processExitTask)
        {
            if (_isDebuggerEndpointAvailable)
            {
                return true;
            }

            var debuggerEndpointTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            discoveryService.SubscribeToChanges(Callback);

            try
            {
                var timeoutTask = Task.Delay(EndpointTimeout);
                var completedTask = await Task.WhenAny(debuggerEndpointTcs.Task, timeoutTask, processExitTask).ConfigureAwait(false);
                if (completedTask == timeoutTask)
                {
                    return false;
                }

                return completedTask == debuggerEndpointTcs.Task;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while waiting for discovery service");
                return false;
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
            ServiceName = GetServiceName(tracerSettings.Manager.InitialMutableSettings);
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
            EnsureTracerSettingsSubscription(tracerSettings);

            InitializeSymbolUploaderIfNeeded(tracerSettings, newDebuggerSettings);

            lock (_syncLock)
            {
                if (_processExit.Task.IsCompleted)
                {
                    return _processExit.Task;
                }

                DebuggerSettings = newDebuggerSettings;
                SetCodeOriginState(newDebuggerSettings);
                SetExceptionReplayState(newDebuggerSettings);
            }

            return DebouncedUpdateDynamicInstrumentationAsync(tracerSettings, newDebuggerSettings);
        }

        private void OneTimeSetup(TracerSettings tracerSettings)
        {
            if (Interlocked.CompareExchange(ref _initialized, 1, 0) != 0)
            {
                return;
            }

            LifetimeManager.Instance.AddShutdownTask(ShutdownTasks);
            SetGeneralConfig(tracerSettings, DebuggerSettings);
            if (tracerSettings.Manager.InitialMutableSettings.StartupDiagnosticLogEnabled)
            {
                _ = Task.Run(WriteStartupDebuggerDiagnosticLog);
            }
        }

        private void EnsureTracerSettingsSubscription(TracerSettings tracerSettings)
        {
            if (_processExit.Task.IsCompleted)
            {
                return;
            }

            // If the global manager replaced, UpdateConfiguration can be called with a different instance.
            var settingsManager = tracerSettings.Manager;
            if (settingsManager == _subscribedSettingsManager)
            {
                return;
            }

            lock (_syncLock)
            {
                if (_processExit.Task.IsCompleted)
                {
                    return;
                }

                if (settingsManager == _subscribedSettingsManager)
                {
                    return;
                }

                SafeDisposal.TryDispose(_tracerSettingsSubscription);
                _tracerSettingsSubscription = settingsManager.SubscribeToChanges(OnTracerSettingsChanged);
                _subscribedSettingsManager = settingsManager;
            }
        }

        private void OnTracerSettingsChanged(TracerSettings.SettingsManager.SettingChanges changes)
        {
            if (_processExit.Task.IsCompleted)
            {
                return;
            }

            if (changes.UpdatedMutable is not { } updatedMutable)
            {
                return;
            }

            ServiceName = GetServiceName(updatedMutable);

            // Note: `SymbolsUploader` captures the service name on first use (symbol extraction/upload) and then keeps it fixed for its lifetime,
            // to avoid mixing symbols across services. If the service name changes after that point, the correct behavior would be to stop and
            // recreate the uploader, but that is expensive (symbol extraction/upload) and is intentionally deferred until we have an explicit
            // requirement.
        }

        private void InitializeSymbolUploaderIfNeeded(TracerSettings tracerSettings, DebuggerSettings newDebuggerSettings)
        {
            try
            {
                if (_processExit.Task.IsCompleted)
                {
                    return;
                }

                if (ExceptionReplaySettings.AgentlessEnabled)
                {
                    Log.Information("Exception Replay agentless mode enabled; skipping symbol uploader initialization because it requires the Datadog Agent and Remote Configuration.");
                    return;
                }

                if (Interlocked.CompareExchange(ref _symDbInitialized, 1, 0) != 0)
                {
                    // Once created, the symbol uploader persists even if DI is later disabled
                    return;
                }

                if (!DebuggerSettings.SymbolDatabaseUploadEnabled
                 || !newDebuggerSettings.DynamicInstrumentationCanBeEnabled)
                {
                    // explicitly disabled via local env var or DI can not be enabled
                    return;
                }

                if (!tracerSettings.IsRemoteConfigurationAvailable)
                {
                    Log.Debug("Remote configuration is not available in this environment, so we don't upload symbols.");
                    return;
                }

                if (!newDebuggerSettings.DynamicInstrumentationEnabled
                 && newDebuggerSettings.DynamicSettings.DynamicInstrumentationEnabled != true)
                {
                    return;
                }

                // Initialize symbol database uploader only if DI is enabled locally or remotely.
                var tracerManager = TracerManager.Instance;
                this.SymbolsUploader = DebuggerFactory.CreateSymbolsUploader(tracerManager.DiscoveryService, RcmSubscriptionManager.Instance, () => ServiceName, tracerSettings, DebuggerSettings, tracerManager.GitMetadataTagsProvider);
                _ = this.SymbolsUploader.StartFlushingAsync()
                        .ContinueWith(
                             t => Log.Error(t?.Exception, "Failed to initialize symbol uploader"),
                             CancellationToken.None,
                             TaskContinuationOptions.OnlyOnFaulted,
                             TaskScheduler.Default);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error initializing Symbol Database.");
            }
        }

        private void SetCodeOriginState(DebuggerSettings debuggerSettings)
        {
            try
            {
                if (_processExit.Task.IsCompleted)
                {
                    return;
                }

                var disabled = !debuggerSettings.CodeOriginForSpansCanBeEnabled || debuggerSettings.DynamicSettings.CodeOriginEnabled == false;
                if (disabled)
                {
                    CodeOrigin = null;
                    if (debuggerSettings.DynamicSettings.CodeOriginEnabled == false)
                    {
                        Log.Information("Code Origin for Spans is disabled by remote enablement. To enable it, re-enable it via Datadog UI");
                    }
                    else
                    {
                        Log.Debug("Code Origin for Spans is disabled");
                    }

                    return;
                }

                if (CodeOrigin != null)
                {
                    Log.Debug("Code Origin for Spans is already initialized");
                    return;
                }

                if (debuggerSettings.CodeOriginForSpansEnabled || debuggerSettings.DynamicSettings.CodeOriginEnabled == true)
                {
                    CodeOrigin = new SpanCodeOrigin.SpanCodeOrigin(debuggerSettings);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error initializing Code Origin for Spans.");
            }
        }

        private void SetExceptionReplayState(DebuggerSettings debuggerSettings)
        {
            try
            {
                if (_processExit.Task.IsCompleted)
                {
                    return;
                }

                var disabled = !ExceptionReplaySettings.CanBeEnabled || debuggerSettings.DynamicSettings.ExceptionReplayEnabled == false;
                if (disabled)
                {
                    SafeDisposal.TryDispose(ExceptionReplay);
                    ExceptionReplay = null;
                    if (debuggerSettings.DynamicSettings.ExceptionReplayEnabled == false)
                    {
                        Log.Information("Exception Replay is disabled by remote enablement. To enable it, re-enable it via Datadog UI");
                    }
                    else
                    {
                        Log.Debug("Exception Replay is disabled");
                    }

                    return;
                }

                if (ExceptionReplay != null)
                {
                    Log.Debug("Exception Replay is already initialized");
                    return;
                }

                if (ExceptionReplaySettings.Enabled || debuggerSettings.DynamicSettings.ExceptionReplayEnabled == true)
                {
                    var exceptionReplay = ExceptionReplay.Create(ExceptionReplaySettings);
                    exceptionReplay.Initialize();
                    ExceptionReplay = exceptionReplay;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error initializing Exception Replay.");
            }
        }

        private async Task DebouncedUpdateDynamicInstrumentationAsync(TracerSettings tracerSettings, DebuggerSettings debuggerSettings)
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

            var requestedDiState = debuggerSettings.DynamicInstrumentationEnabled || debuggerSettings.DynamicSettings.DynamicInstrumentationEnabled == true;
            var currentDiState = DynamicInstrumentation != null;
            var state = Volatile.Read(ref _diState);

            if (!requestedDiState && (currentDiState || state == 1))
            {
                // cancel any pending enable and disable immediately
                var prevGate = Interlocked.Exchange(ref _diDebounceGate, null);
                prevGate?.TrySetResult(false);
                DisableDynamicInstrumentation(debuggerSettings.DynamicSettings.DynamicInstrumentationEnabled == false);
                return;
            }

            if (ShouldSkipDiUpdate(requestedDiState, currentDiState, debuggerSettings))
            {
                return;
            }

            // cancel any pending operation and start new one
            var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var prev = Interlocked.Exchange(ref _diDebounceGate, gate);
            prev?.TrySetResult(false);
            Log.Debug("Previous DI update request exists: {Value}", prev != null);

            try
            {
                Log.Debug("Waiting {Timeout}ms before updating Dynamic Instrumentation", _diDebounceDelay.TotalMilliseconds);
                var delayTask = Task.Delay(_diDebounceDelay);
                var completed = await Task.WhenAny(delayTask, gate.Task, _processExit.Task).ConfigureAwait(false);
                if (completed != delayTask || _processExit.Task.IsCompleted)
                {
                    return; // superseded or shutting down
                }

                if (_diDebounceGate != gate)
                {
                    return;
                }

                await SetDynamicInstrumentationStateAsync(tracerSettings, gate).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in DI debounce task");
            }
            finally
            {
                Interlocked.CompareExchange(ref _diDebounceGate, null, gate);
            }
        }

        private bool ShouldSkipDiUpdate(bool requested, bool current, DebuggerSettings debuggerSettings)
        {
            if (requested && !debuggerSettings.DynamicInstrumentationCanBeEnabled)
            {
                Log.Debug("Dynamic Instrumentation can't be enabled because the local environment variable is set to false");
                return true;
            }

            // no change required AND no init in progress
            if ((requested == current) && Volatile.Read(ref _diState) == 0)
            {
                Log.Debug("Skip update Dynamic Instrumentation. Requested is {Requested}, Current is {Current}", requested, current);
                return true;
            }

            return false;
        }

        private async Task SetDynamicInstrumentationStateAsync(TracerSettings tracerSettings, TaskCompletionSource<bool> debounceGate)
        {
            try
            {
                if (_processExit.Task.IsCompleted || _diDebounceGate != debounceGate)
                {
                    return;
                }

                var disabled = !DebuggerSettings.DynamicInstrumentationCanBeEnabled || DebuggerSettings.DynamicSettings.DynamicInstrumentationEnabled == false;

                if (disabled)
                {
                    DisableDynamicInstrumentation(DebuggerSettings.DynamicSettings.DynamicInstrumentationEnabled == false);
                    return;
                }

                if (Volatile.Read(ref _diState) != 0)
                {
                    Log.Debug("Dynamic Instrumentation is already initialized");
                    return;
                }

                await EnableDynamicInstrumentation(tracerSettings, debounceGate).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (_processExit.Task.IsCompleted)
                {
                    return;
                }

                TracerManager.Instance.Telemetry.ProductChanged(TelemetryProductType.DynamicInstrumentation, enabled: false, error: null);
                lock (_syncLock)
                {
                    SafeDisposal.TryDispose(_dynamicInstrumentation);
                    _dynamicInstrumentation = null;
                }

                Log.Error(ex, "Error initializing Dynamic Instrumentation");
            }
        }

        private async Task EnableDynamicInstrumentation(TracerSettings tracerSettings, TaskCompletionSource<bool> debounceGate)
        {
            if (Interlocked.CompareExchange(ref _diState, 1, 0) != 0)
            {
                return; // someone else is initializing or done
            }

            DynamicInstrumentation? di = null;

            try
            {
                var tracerManager = TracerManager.Instance;
                var discoveryService = tracerManager.DiscoveryService;
                di = DebuggerFactory.CreateDynamicInstrumentation(
                    discoveryService,
                    RcmSubscriptionManager.Instance,
                    tracerSettings,
                    () => ServiceName,
                    DebuggerSettings,
                    tracerManager.GitMetadataTagsProvider);

                if (!_isDebuggerEndpointAvailable)
                {
                    var isDiscoverySuccessful = await WaitForDebuggerEndpointAsync(discoveryService, _processExit.Task).ConfigureAwait(false);
                    if (!isDiscoverySuccessful)
                    {
                        Log.Information("Debugger endpoint is not available");
                        Volatile.Write(ref _diState, 0);
                        SafeDisposal.TryDispose(di);
                        return;
                    }
                }

                if (_processExit.Task.IsCompleted || _diDebounceGate != debounceGate)
                {
                    Volatile.Write(ref _diState, 0);
                    SafeDisposal.TryDispose(di);
                    return;
                }

                di.Initialize();

                lock (_syncLock)
                {
                    var initialized = _dynamicInstrumentation is { IsDisposed: false };
                    var state = _diState;

                    if (_processExit.Task.IsCompleted ||
                        _diDebounceGate != debounceGate ||
                        state != 1 ||
                        initialized)
                    {
                        if (state == 1)
                        {
                            _diState = 0;
                        }
                    }
                    else
                    {
                        _dynamicInstrumentation = di;
                        di = null;
                        _diState = 2; // initialized
                    }
                }

                if (di != null)
                {
                    SafeDisposal.TryDispose(di);
                    return;
                }

                tracerManager.Telemetry.ProductChanged(TelemetryProductType.DynamicInstrumentation, enabled: true, error: null);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Fail to initialize Dynamic Instrumentation");
                Interlocked.CompareExchange(ref _diState, 0, 1);
                SafeDisposal.TryDispose(di);
            }
        }

        private void DisableDynamicInstrumentation(bool dynamicallyDisabled)
        {
            Log.Debug("Disabling Dynamic Instrumentation");

            bool disabled = false;
            lock (_syncLock)
            {
                if (_dynamicInstrumentation != null)
                {
                    SafeDisposal.TryDispose(_dynamicInstrumentation);
                    _dynamicInstrumentation = null;
                    disabled = true;
                }

                _diState = 0;
            }

            if (disabled)
            {
                TracerManager.Instance.Telemetry.ProductChanged(TelemetryProductType.DynamicInstrumentation, enabled: false, error: null);
            }

            if (dynamicallyDisabled)
            {
                Log.Information("Dynamic Instrumentation is disabled by remote enablement. To enable it, re-enable it via Datadog UI");
            }
            else
            {
                Log.Debug("Dynamic Instrumentation is disabled. To enable it, please set {DynamicInstrumentationEnabled} environment variable to 'true'.", ConfigurationKeys.Debugger.DynamicInstrumentationEnabled);
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
            Volatile.Write(ref _diState, 0);

            if (ex != null)
            {
                Log.Debug(ex, "Shutdown task for DebuggerManager is running with exception");
            }

            SafeDisposal.New()
                        .Add(_tracerSettingsSubscription)
                        .Add(_dynamicInstrumentation)
                        .Add(ExceptionReplay)
                        .Add(SymbolsUploader)
                        .DisposeAll();
        }
    }
}
