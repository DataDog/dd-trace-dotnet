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
        private volatile DynamicInstrumentation? _dynamicInstrumentation;

        private DebuggerManager(DebuggerSettings debuggerSettings, ExceptionReplaySettings exceptionReplaySettings)
        {
            _initialized = 0;
            _isDebuggerEndpointAvailable = false;
            DebuggerSettings = debuggerSettings;
            ExceptionReplaySettings = exceptionReplaySettings;
            ServiceName = string.Empty;
            _syncLock = new();
            _diDebounceDelay = TimeSpan.FromMilliseconds(250);
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
                return instance?.IsDisposed == false ? instance : null;
            }

            private set => _dynamicInstrumentation = value;
        }

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
                var debuggerEndpointTimeout = TimeSpan.FromMinutes(5);
                var timeoutTask = Task.Delay(debuggerEndpointTimeout);
                var completedTask = await Task.WhenAny(debuggerEndpointTcs.Task, timeoutTask, processExitTask).ConfigureAwait(false);
                if (completedTask == timeoutTask)
                {
                    Log.Warning("Debugger endpoint is not available after waiting {Timeout} seconds.", debuggerEndpointTimeout.TotalSeconds);
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
                if (_processExit.Task.IsCompleted || !DebuggerSettings.SymbolDatabaseUploadEnabled)
                {
                    return;
                }

                if (!tracerSettings.IsRemoteConfigurationAvailable)
                {
                    Log.Debug("Remote configuration is not available in this environment, so we don't upload symbols.");
                    return;
                }

                var tracerManager = TracerManager.Instance;
                SymbolsUploader = DebuggerFactory.CreateSymbolsUploader(tracerManager.DiscoveryService, RcmSubscriptionManager.Instance, ServiceName, tracerSettings, DebuggerSettings, tracerManager.GitMetadataTagsProvider);

                _ = Task.Run(
                    async () =>
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
                        Log.Debug("Code Origin for Spans is disabled by remote enablement. To enable it, re-enable it via Datadog UI");
                    }
                    else
                    {
                        Log.Debug("Code Origin for Spans is disabled. To enable it, please set {CodeOriginForSpansEnabled} environment variable to '1'/'true'.", ConfigurationKeys.Debugger.CodeOriginForSpansEnabled);
                    }

                    return;
                }

                if (CodeOrigin != null)
                {
                    Log.Debug("Code Origin for Spans is already initialized");
                    return;
                }

                CodeOrigin = new SpanCodeOrigin.SpanCodeOrigin(debuggerSettings);
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
                        Log.Debug("Exception Replay is disabled by remote enablement. To enable it, re-enable it via Datadog UI");
                    }
                    else
                    {
                        Log.Debug("Exception Replay is disabled. To enable it, please set {ExceptionReplayEnabled} environment variable to '1'/'true'.", ConfigurationKeys.Debugger.ExceptionReplayEnabled);
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

            // Only proceed if state actually needs to change
            if (ShouldSkipUpdate(debuggerSettings.DynamicInstrumentationEnabled, DynamicInstrumentation != null))
            {
                return;
            }

            // Cancel any pending operation and start new one
            var cts = new CancellationTokenSource();
            var prevCts = Interlocked.Exchange(ref _diDebounceCts, cts);
            Log.Debug("Is previous Dynamic Instrumentation update request exist? {Value}", prevCts != null);

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
                Log.Debug("Waiting {Timeout}ms before updating Dynamic Instrumentation", _diDebounceDelay.TotalMilliseconds);
                await Task.Delay(_diDebounceDelay, cts.Token).ConfigureAwait(false);

                // Re-check if state change is still needed after debounce
                if (!cts.IsCancellationRequested)
                {
                    if (ShouldSkipUpdate(DebuggerSettings.DynamicInstrumentationEnabled, DynamicInstrumentation != null))
                    {
                        return;
                    }

                    await SetDynamicInstrumentationStateAsync(tracerSettings, DebuggerSettings, cts.Token).ConfigureAwait(false);
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

            bool ShouldSkipUpdate(bool requested, bool current)
            {
                if (requested == current)
                {
                    Log.Debug("Skip update Dynamic Instrumentation. Requested is {Requested}, Current is {Current}", requested, current);
                    return true;
                }

                return false;
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

                if (!requestedState)
                {
                    DisableDynamicInstrumentation();
                    return;
                }

                lock (_syncLock)
                {
                    if (DynamicInstrumentation != null)
                    {
                        Log.Debug("Dynamic Instrumentation is already initialized");
                        return;
                    }
                }

                var tracerManager = TracerManager.Instance;
                var discoveryService = tracerManager.DiscoveryService;
                if (!_isDebuggerEndpointAvailable)
                {
                    var isDiscoverySuccessful = await WaitForDebuggerEndpointAsync(discoveryService, _processExit.Task).ConfigureAwait(false);
                    if (!isDiscoverySuccessful)
                    {
                        Log.Information("Debugger endpoint is not available");
                        return;
                    }
                }

                if (cancellationToken.IsCancellationRequested || _processExit.Task.IsCompleted)
                {
                    return;
                }

                EnableDynamicInstrumentation(discoveryService, tracerManager);
            }
            catch (Exception ex)
            {
                if (cancellationToken.IsCancellationRequested || _processExit.Task.IsCompleted)
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

            void DisableDynamicInstrumentation()
            {
                Log.Debug("Disabling Dynamic Instrumentation");

                var disabledViaRemoteConfig = debuggerSettings.DynamicSettings.DynamicInstrumentationEnabled == false;

                lock (_syncLock)
                {
                    if (DynamicInstrumentation != null)
                    {
                        SafeDisposal.TryDispose(_dynamicInstrumentation);
                        _dynamicInstrumentation = null;
                        TracerManager.Instance.Telemetry.ProductChanged(TelemetryProductType.DynamicInstrumentation, enabled: false, error: null);
                    }
                }

                if (disabledViaRemoteConfig)
                {
                    Log.Debug("Dynamic Instrumentation is disabled by remote enablement. To enable it, re-enable it via Datadog UI");
                }
                else
                {
                    // we need this log for tests
                    Log.Debug("Dynamic Instrumentation is disabled. To enable it, please set {DynamicInstrumentationEnabled} environment variable to 'true'.", ConfigurationKeys.Debugger.DynamicInstrumentationEnabled);
                }
            }

            void EnableDynamicInstrumentation(IDiscoveryService discoveryService, TracerManager tracerManager)
            {
                DynamicInstrumentation? instance = null;
                var created = false;
                lock (_syncLock)
                {
                    if (DynamicInstrumentation == null)
                    {
                        _dynamicInstrumentation = DebuggerFactory.CreateDynamicInstrumentation(
                            discoveryService,
                            RcmSubscriptionManager.Instance,
                            tracerSettings,
                            ServiceName,
                            debuggerSettings,
                            tracerManager.GitMetadataTagsProvider);
                        created = true;
                    }

                    instance = _dynamicInstrumentation;
                }

                if (created && instance != null)
                {
                    Log.Debug("Dynamic Instrumentation has been created");
                    instance.Initialize();
                    tracerManager.Telemetry.ProductChanged(TelemetryProductType.DynamicInstrumentation, enabled: true, error: null);
                }
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
                        .Add(_dynamicInstrumentation)
                        .Add(ExceptionReplay)
                        .Add(SymbolsUploader)
                        .DisposeAll();
        }
    }
}
