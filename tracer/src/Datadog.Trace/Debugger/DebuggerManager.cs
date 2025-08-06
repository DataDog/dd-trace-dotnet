// <copyright file="DebuggerManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.Configuration;
using Datadog.Trace.Debugger.ExceptionAutoInstrumentation;
using Datadog.Trace.Debugger.Helpers;
using Datadog.Trace.Debugger.Sink;
using Datadog.Trace.Debugger.Snapshots;
using Datadog.Trace.Logging;
using Datadog.Trace.Processors;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

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
        private readonly CancellationTokenSource _cancellationToken;
        private volatile bool _isShuttingDown;
        private int _initialized;

        private DebuggerManager(DebuggerSettings debuggerSettings, ExceptionReplaySettings exceptionReplaySettings)
        {
            _initialized = 0;
            _discoveryServiceReady = false;
            _isShuttingDown = false;
            _semaphore = new SemaphoreSlim(1, 1);
            DebuggerSettings = debuggerSettings;
            ExceptionReplaySettings = exceptionReplaySettings;
            _cancellationToken = new CancellationTokenSource();
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

        private async Task<bool> WaitForDiscoveryServiceAsync(IDiscoveryService discoveryService, CancellationToken cancellationToken)
        {
            if (_discoveryServiceReady)
            {
                return true;
            }

            var tc = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            using var registration = cancellationToken.Register(() => tc.TrySetResult(false));

            discoveryService.SubscribeToChanges(Callback);
            _discoveryServiceReady = await tc.Task.ConfigureAwait(false);
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

        private void SetGeneralConfig(DebuggerSettings settings)
        {
            DebuggerSnapshotSerializer.SetConfig(settings);
            Redaction.Instance.SetConfig(settings.RedactedIdentifiers, settings.RedactedExcludedIdentifiers, settings.RedactedTypes);
            ServiceName = GetServiceName();
        }

        private string GetServiceName()
        {
            var tracerManager = TracerManager.Instance;
            try
            {
                return TraceUtil.NormalizeTag(tracerManager.Settings.ServiceName ?? tracerManager.DefaultServiceName);
            }
            catch (Exception e)
            {
                Log.Error(e, "Could not set `DynamicInstrumentationHelper.ServiceName`.");
                return tracerManager.DefaultServiceName;
            }
        }

        private void SetCodeOriginState()
        {
            try
            {
                if (DebuggerSettings.CodeOriginForSpansEnabled && CodeOrigin == null)
                {
                    CodeOrigin = new SpanCodeOrigin.SpanCodeOrigin(DebuggerSettings);
                }
                else
                {
                    Log.Information("Code Origin for Spans is disabled by. To enable it, please set {CodeOriginForSpans} environment variable to '1'/'true'.", ConfigurationKeys.Debugger.CodeOriginForSpansEnabled);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error initializing Code Origin for spans.");
            }
        }

        private void SetExceptionReplayState()
        {
            try
            {
                if (ExceptionReplaySettings.Enabled && ExceptionReplay == null)
                {
                    var exceptionReplay = new ExceptionReplay(ExceptionReplaySettings);
                    exceptionReplay.Initialize();
                    ExceptionReplay = exceptionReplay;
                }
                else
                {
                    Log.Information("Exception Replay is disabled. To enable it, please set {ExceptionReplayEnabled} environment variable to '1'/'true'.", ConfigurationKeys.Debugger.ExceptionReplayEnabled);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error initializing Exception Replay.");
            }
        }

        private async Task SetDynamicInstrumentationState()
        {
            try
            {
                if (DebuggerSettings.DynamicInstrumentationEnabled && DynamicInstrumentation == null)
                {
                    var tracerManager = TracerManager.Instance;
                    var settings = tracerManager.Settings;

                    if (!settings.IsRemoteConfigurationAvailable)
                    {
                        if (DebuggerSettings.DynamicInstrumentationEnabled)
                        {
                            Log.Warning("Dynamic Instrumentation is enabled by environment variable but remote configuration is not available in this environment, so Dynamic Instrumentation cannot be enabled.");
                        }

                        tracerManager.Telemetry.ProductChanged(TelemetryProductType.DynamicInstrumentation, enabled: false, error: null);
                        return;
                    }

                    var discoveryService = tracerManager.DiscoveryService;
                    DynamicInstrumentation = DebuggerFactory.CreateDynamicInstrumentation(discoveryService, RcmSubscriptionManager.Instance, settings, Instance.ServiceName, Instance.DebuggerSettings, tracerManager.GitMetadataTagsProvider);
                    Log.Debug("Dynamic Instrumentation has been created.");

                    if (!_discoveryServiceReady)
                    {
                        var sw = Stopwatch.StartNew();
                        var isDiscoverySuccessful = await WaitForDiscoveryServiceAsync(discoveryService, _cancellationToken.Token).ConfigureAwait(false);
                        if (isDiscoverySuccessful)
                        {
                            TelemetryFactory.Metrics.RecordDistributionSharedInitTime(MetricTags.InitializationComponent.DiscoveryService, sw.ElapsedMilliseconds);
                            sw.Restart();
                            DynamicInstrumentation.Initialize();
                            TelemetryFactory.Metrics.RecordDistributionSharedInitTime(MetricTags.InitializationComponent.DynamicInstrumentation, sw.ElapsedMilliseconds);
                            tracerManager.Telemetry.ProductChanged(TelemetryProductType.DynamicInstrumentation, enabled: true, error: null);
                        }
                    }
                }
                else
                {
                    Log.Information("Dynamic Instrumentation is disabled. To enable it, please set {DynamicInstrumentationEnabled} environment variable to 'true'.", ConfigurationKeys.Debugger.DynamicInstrumentationEnabled);
                }
            }
            catch (Exception ex)
            {
                TracerManager.Instance.Telemetry.ProductChanged(TelemetryProductType.DynamicInstrumentation, enabled: false, error: null);
                Log.Error(ex, "Error initializing Dynamic Instrumentation.");
            }
        }

        internal Task UpdateConfiguration(DebuggerSettings? newDebuggerSettings = null)
        {
            return UpdateProductsState(newDebuggerSettings ?? DebuggerSettings);
        }

        private async Task UpdateProductsState(DebuggerSettings newDebuggerSettings)
        {
            if (_isShuttingDown)
            {
                return;
            }

            OneTimeSetup();

            bool semaphoreAcquired = false;
            try
            {
                semaphoreAcquired = await _semaphore.WaitAsync(TimeSpan.FromSeconds(30), _cancellationToken.Token).ConfigureAwait(false);
                if (!semaphoreAcquired || _isShuttingDown)
                {
                    Log.Debug("Skipping update debugger state due to semaphore timed out");
                    return;
                }

                DebuggerSettings = newDebuggerSettings;
                SetCodeOriginState();
                SetExceptionReplayState();
                await SetDynamicInstrumentationState().ConfigureAwait(false);
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

        private void OneTimeSetup()
        {
            if (Interlocked.CompareExchange(ref _initialized, 1, 0) != 0)
            {
                return;
            }

            LifetimeManager.Instance.AddShutdownTask(ShutdownTasks);
            SetGeneralConfig(DebuggerSettings);
            _ = InitializeSymbolUploader();
        }

        private async Task InitializeSymbolUploader()
        {
            try
            {
                var tracerManager = TracerManager.Instance;
                SymbolsUploader = DebuggerFactory.CreateSymbolsUploader(tracerManager.DiscoveryService, RcmSubscriptionManager.Instance, Instance.ServiceName, Instance.DebuggerSettings, tracerManager.GitMetadataTagsProvider);
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
            _isShuttingDown = true;

            if (ex != null)
            {
                Log.Debug(ex, "Shutdown task for DebuggerManager is running with exception");
            }

            SafeDisposal.New()
                        .Execute(() => _cancellationToken.Cancel(), "cancelling DebuggerManager operations")
                        .Add(SymbolsUploader)
                        .Add(_cancellationToken)
                        .Add(_semaphore)
                        .DisposeAll();
        }
    }
}
