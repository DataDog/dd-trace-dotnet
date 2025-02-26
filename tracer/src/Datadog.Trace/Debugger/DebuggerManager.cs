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
using Datadog.Trace.Debugger.ExceptionAutoInstrumentation;
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

        private static DebuggerManager? _instance;

        private object _locker;

        private DebuggerManager(DebuggerSettings debuggerSettings, ExceptionReplaySettings exceptionReplaySettings)
        {
            _locker = new object();
            DebuggerSettings = debuggerSettings;
            ExceptionReplaySettings = exceptionReplaySettings;

            var tracerManager = TracerManager.Instance;
            try
            {
                DynamicInstrumentationHelper.ServiceName = TraceUtil.NormalizeTag(tracerManager.Settings.ServiceName ?? tracerManager.DefaultServiceName);
            }
            catch (Exception e)
            {
                DynamicInstrumentationHelper.ServiceName = tracerManager.DefaultServiceName;
                Log.Error(e, "Could not set `DynamicInstrumentationHelper.ServiceName`.");
            }
        }

        internal static DebuggerManager Instance
        {
            get
            {
                var instance = Interlocked.CompareExchange(ref _instance, null, null);
                if (instance == null)
                {
                    Interlocked.Exchange(ref _instance, new DebuggerManager(DebuggerSettings.FromDefaultSource(), ExceptionReplaySettings.FromDefaultSource()));
                    instance = _instance;
                }

                return instance!;
            }
        }

        internal DebuggerSettings DebuggerSettings { get; private set; }

        internal ExceptionReplaySettings ExceptionReplaySettings { get; }

        internal DynamicInstrumentation? DynamicInstrumentation { get; private set; }

        internal SpanCodeOrigin.SpanCodeOrigin? CodeOrigin { get; private set; }

        internal IDebuggerUploader? SymbolsUploader { get; private set; }

        internal ExceptionDebugging? ExceptionReplay { get; private set; }

        internal string ServiceName => DynamicInstrumentationHelper.ServiceName;

        // /!\ This method is called by reflection in the Samples.SampleHelpers
        // If you remove it then you need to provide an alternative way to wait for the discovery service
        private static async Task<bool> WaitForDiscoveryService(IDiscoveryService discoveryService)
        {
            var tc = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            // Stop waiting if we're shutting down
            LifetimeManager.Instance.AddShutdownTask(_ => tc.TrySetResult(false));

            discoveryService.SubscribeToChanges(Callback);
            return await tc.Task.ConfigureAwait(false);

            void Callback(AgentConfiguration x)
            {
                tc.TrySetResult(true);
                discoveryService.RemoveSubscription(Callback);
            }
        }

        /// <summary>
        /// For testing only
        /// </summary>
        internal static DebuggerManager ReplaceManager(DebuggerSettings settings, ExceptionReplaySettings exceptionSettings)
        {
            Interlocked.Exchange(ref _instance, new DebuggerManager(settings, exceptionSettings));
            return _instance!;
        }

        internal void InitializeProducts()
        {
            UpdateProductsState(DebuggerSettings);
        }

        private void SetGeneralConfig(DebuggerSettings settings)
        {
            DebuggerSnapshotSerializer.SetConfig(settings);
            Redaction.Instance.SetConfig(settings.RedactedIdentifiers, settings.RedactedExcludedIdentifiers, settings.RedactedTypes);
        }

        private async Task InitializeSymbolUploader()
        {
            try
            {
                var tracerManager = TracerManager.Instance;
                SymbolsUploader = DebuggerFactory.CreateSymbolsUploader(tracerManager.DiscoveryService, RcmSubscriptionManager.Instance, tracerManager.Settings, Instance.ServiceName, Instance.DebuggerSettings, tracerManager.GitMetadataTagsProvider);
                // it will do nothing if it is an instance of NoOpSymbolUploader
                await SymbolsUploader.StartFlushingAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error initializing Symbol Database.");
            }
        }

        private void SetCodeOriginState()
        {
            try
            {
                var enabled = DebuggerSettings.CodeOriginForSpansEnabled.HasValue && DebuggerSettings.CodeOriginForSpansEnabled.Value;
                var disabled = DebuggerSettings.CodeOriginForSpansEnabled.HasValue && !DebuggerSettings.CodeOriginForSpansEnabled.Value;
                var dynamicallyEnabled = DebuggerSettings.DynamicSettings.CodeOriginEnabled.HasValue && DebuggerSettings.DynamicSettings.CodeOriginEnabled.Value;
                var dynamicallyDisabled = DebuggerSettings.DynamicSettings.CodeOriginEnabled.HasValue && !DebuggerSettings.DynamicSettings.CodeOriginEnabled.Value;

                if (disabled)
                {
                    Log.Information("Code Origin for Spans is disabled by environment variable. To enable it, please set {CodeOriginForSpans} environment variable to '1'/'true'.", Datadog.Trace.Configuration.ConfigurationKeys.Debugger.CodeOriginForSpansEnabled);
                    return;
                }

                if (dynamicallyDisabled)
                {
                    Log.Information("Code Origin for Spans is disabled by remote enablement. To enable it, re-enable it via Datadog UI");
                    CodeOrigin = null;
                    return;
                }

                if ((enabled || dynamicallyEnabled) && CodeOrigin == null)
                {
                    CodeOrigin = new SpanCodeOrigin.SpanCodeOrigin(DebuggerSettings);
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
                var enabled = ExceptionReplaySettings.Enabled.HasValue && ExceptionReplaySettings.Enabled.Value;
                var disabled = ExceptionReplaySettings.Enabled.HasValue && !ExceptionReplaySettings.Enabled.Value;
                var dynamicallyEnabled = DebuggerSettings.DynamicSettings.ExceptionReplayEnabled.HasValue && DebuggerSettings.DynamicSettings.ExceptionReplayEnabled.Value;
                var dynamicallyDisabled = DebuggerSettings.DynamicSettings.ExceptionReplayEnabled.HasValue && !DebuggerSettings.DynamicSettings.ExceptionReplayEnabled.Value;

                if (disabled)
                {
                    Log.Information("Exception Replay is disabled by environment variable. To enable it, please set {ExceptionReplayEnabled} environment variable to '1'/'true'.", Datadog.Trace.Configuration.ConfigurationKeys.Debugger.ExceptionReplayEnabled);
                    return;
                }

                if (dynamicallyDisabled)
                {
                    Log.Information("Exception Replay is disabled by remote enablement. To enable it, re-enable it via Datadog UI");
                    ExceptionReplay?.Dispose();
                    ExceptionReplay = null;

                    return;
                }

                if ((enabled || dynamicallyEnabled) && ExceptionReplay == null)
                {
                    var exceptionReplay = ExceptionDebugging.Create(ExceptionReplaySettings);

                    if (exceptionReplay != null)
                    {
                        exceptionReplay.Initialize();
                        ExceptionReplay = exceptionReplay;
                    }
                    else
                    {
                        Log.Information("Exception Replay is disabled.");
                    }
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
                var enabled = DebuggerSettings.DynamicInstrumentationEnabled.HasValue && DebuggerSettings.DynamicInstrumentationEnabled.Value;
                var disabled = DebuggerSettings.DynamicInstrumentationEnabled.HasValue && !DebuggerSettings.DynamicInstrumentationEnabled.Value;
                var dynamicallyEnabled = DebuggerSettings.DynamicSettings.DynamicInstrumentationEnabled.HasValue && DebuggerSettings.DynamicSettings.DynamicInstrumentationEnabled.Value;
                var dynamicallyDisabled = DebuggerSettings.DynamicSettings.DynamicInstrumentationEnabled.HasValue && !DebuggerSettings.DynamicSettings.DynamicInstrumentationEnabled.Value;

                if (disabled)
                {
                    Log.Information("Dynamic Instrumentation is disabled by environment variable. To enable it, please set {DynamicInstrumentationEnabled} environment variable to '1'/'true'.", Datadog.Trace.Configuration.ConfigurationKeys.Debugger.DynamicInstrumentationEnabled);
                    return;
                }

                if (dynamicallyDisabled)
                {
                    Log.Information("Dynamic Instrumentation is disabled by remote enablement. To enable it, re-enable it via Datadog UI");
                    DynamicInstrumentation?.Dispose();
                    DynamicInstrumentation = null;
                    return;
                }

                if ((enabled || dynamicallyEnabled) && DynamicInstrumentation == null)
                {
                    var tracerManager = TracerManager.Instance;
                    var settings = tracerManager.Settings;

                    if (!settings.IsRemoteConfigurationAvailable)
                    {
                        if (DebuggerSettings.DynamicInstrumentationEnabled.HasValue && DebuggerSettings.DynamicInstrumentationEnabled.Value)
                        {
                            Log.Warning("Dynamic Instrumentation is enabled by environment variable but remote configuration is not available in this environment, so Dynamic Instrumentation cannot be enabled.");
                        }

                        tracerManager.Telemetry.ProductChanged(TelemetryProductType.DynamicInstrumentation, enabled: false, error: null);
                        return;
                    }

                    var discoveryService = tracerManager.DiscoveryService;
                    DynamicInstrumentation = DebuggerFactory.CreateDynamicInstrumentation(discoveryService, RcmSubscriptionManager.Instance, settings, Instance.ServiceName, tracerManager.Telemetry, Instance.DebuggerSettings, tracerManager.GitMetadataTagsProvider);
                    Log.Debug("Dynamic Instrumentation has created.");

                    var sw = Stopwatch.StartNew();
                    var isDiscoverySuccessful = await WaitForDiscoveryService(discoveryService).ConfigureAwait(false);
                    TelemetryFactory.Metrics.RecordDistributionSharedInitTime(MetricTags.InitializationComponent.DiscoveryService, sw.ElapsedMilliseconds);

                    if (isDiscoverySuccessful)
                    {
                        sw.Restart();
                        await DynamicInstrumentation.InitializeAsync().ConfigureAwait(false);
                        TelemetryFactory.Metrics.RecordDistributionSharedInitTime(MetricTags.InitializationComponent.DynamicInstrumentation, sw.ElapsedMilliseconds);
                    }
                    else
                    {
                        tracerManager.Telemetry.ProductChanged(TelemetryProductType.DynamicInstrumentation, enabled: false, error: null);
                        Log.Debug("Could not initialize Dynamic Instrumentation because waiting for discovery service has failed");
                    }
                }
            }
            catch (Exception ex)
            {
                TracerManager.Instance.Telemetry.ProductChanged(TelemetryProductType.DynamicInstrumentation, enabled: false, error: null);
                Log.Error(ex, "Error initializing Dynamic Instrumentation.");
            }
        }

        internal void UpdateDynamicConfiguration(DebuggerSettings newDebuggerSettings)
        {
            UpdateProductsState(newDebuggerSettings);
        }

        private void UpdateProductsState(DebuggerSettings newDebuggerSettings)
        {
            lock (_locker)
            {
                DebuggerSettings = newDebuggerSettings;
                OneTimeSetup();
                SetExceptionReplayState();
                SetCodeOriginState();
                _ = Task.Run(async () => { await SetDynamicInstrumentationState().ConfigureAwait(false); });
            }
        }

        private void OneTimeSetup()
        {
            LifetimeManager.Instance.AddShutdownTask(ShutdownTasks);
            SetGeneralConfig(DebuggerSettings);
            _ = Task.Run(async () => { await InitializeSymbolUploader().ConfigureAwait(false); });
        }

        private void ShutdownTasks(Exception? arg)
        {
            DynamicInstrumentation?.Dispose();
            ExceptionReplay?.Dispose();
        }
    }
}
