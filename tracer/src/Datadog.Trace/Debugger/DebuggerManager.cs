// <copyright file="DebuggerManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
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
    internal class DebuggerManager(DebuggerSettings debuggerSettings, ExceptionReplaySettings exceptionReplaySettings)
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DebuggerManager));

        internal static readonly DebuggerManager Instance = new(DebuggerSettings.FromDefaultSource(), ExceptionReplaySettings.FromDefaultSource());

        private int _isDiInitialized;

        internal DebuggerSettings DebuggerSettings { get; private set; } = debuggerSettings;

        internal ExceptionReplaySettings ExceptionReplaySettings { get; } = exceptionReplaySettings;

        internal DynamicInstrumentation? DynamicInstrumentation { get; private set; }

        internal SpanCodeOrigin.SpanCodeOrigin? CodeOrigin { get; private set; }

        internal IDebuggerUploader? SymbolsUploader { get; private set; }

        internal ExceptionDebugging? ExceptionReplay { get; private set; }

        internal string ServiceName => DynamicInstrumentationHelper.ServiceName;

        internal async Task InitializeInstrumentationBasedProducts()
        {
            SetGeneralConfig(DebuggerSettings);
            await InitializeSymbolUploader().ConfigureAwait(false);
            InitializeCodeOrigin();
            InitializeExceptionReplay();
            await InitializeDynamicInstrumentation().ConfigureAwait(false);
        }

        private void SetGeneralConfig(DebuggerSettings settings)
        {
            DebuggerSnapshotSerializer.SetConfig(settings);
            Redaction.Instance.SetConfig(settings.RedactedIdentifiers, settings.RedactedExcludedIdentifiers, settings.RedactedTypes);
        }

        private void InitializeCodeOrigin()
        {
            if (!DebuggerSettings.CodeOriginForSpansEnabled)
            {
                Log.Information("Code Origin for Spans is disabled. To enable it, please set {CodeOriginForSpans} environment variable to '1'/'true'.", Datadog.Trace.Configuration.ConfigurationKeys.Debugger.CodeOriginForSpansEnabled);
                return;
            }

            if (DebuggerSettings.DynamicSettings.CodeOriginEnabled.HasValue && !DebuggerSettings.DynamicSettings.CodeOriginEnabled.Value)
            {
                Log.Information("Code Origin for Spans is disabled by remote enablement. To enable it, re-enable it via Datadog UI");
                return;
            }

            CodeOrigin = new SpanCodeOrigin.SpanCodeOrigin(DebuggerSettings);
        }

        private async Task InitializeSymbolUploader()
        {
            var tracerManager = TracerManager.Instance;
            SymbolsUploader = DebuggerFactory.CreateSymbolsUploader(tracerManager.DiscoveryService, RcmSubscriptionManager.Instance, tracerManager.Settings, Instance.ServiceName, Instance.DebuggerSettings, tracerManager.GitMetadataTagsProvider);
            // it will do nothing if it is an instance of NoOpSymbolUploader
            await SymbolsUploader.StartFlushingAsync().ConfigureAwait(false);
        }

        private void InitializeExceptionReplay()
        {
            if (!ExceptionReplaySettings.Enabled)
            {
                Log.Information("Exception Replay is disabled. To enable it, please set {ExceptionReplayEnabled} environment variable to '1'/'true'.", Datadog.Trace.Configuration.ConfigurationKeys.Debugger.ExceptionReplayEnabled);
                return;
            }

            if (DebuggerSettings.DynamicSettings.ExceptionReplayEnabled.HasValue && !DebuggerSettings.DynamicSettings.ExceptionReplayEnabled.Value)
            {
                Log.Information("Exception Replay is disabled by remote enablement. To enable it, re-enable it via Datadog UI");
                return;
            }

            try
            {
                if (ExceptionDebugging.Initialize())
                {
                    ExceptionReplay = ExceptionDebugging.Instance;
                }
                else
                {
                    Log.Information("Exception Replay is disabled.");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error initializing Exception Debugging");
            }
        }

        private async Task InitializeDynamicInstrumentation()
        {
            if (!DebuggerSettings.DynamicInstrumentationEnabled)
            {
                Log.Information("Dynamic Instrumentation is disabled. To enable it, please set {DynamicInstrumentationEnabled} environment variable to '1'/'true'.", Datadog.Trace.Configuration.ConfigurationKeys.Debugger.DynamicInstrumentationEnabled);
                return;
            }

            if (DebuggerSettings.DynamicSettings.DynamicInstrumentationEnabled.HasValue && !DebuggerSettings.DynamicSettings.DynamicInstrumentationEnabled.Value)
            {
                Log.Information("Dynamic Instrumentation is disabled by remote enablement. To enable it, re-enable it via Datadog UI");
                return;
            }

            if (Interlocked.Exchange(ref _isDiInitialized, 1) != 0)
            {
                return;
            }

            var tracerManager = TracerManager.Instance;
            var settings = tracerManager.Settings;
            var discoveryService = tracerManager.DiscoveryService;

            DynamicInstrumentation = DebuggerFactory.CreateDynamicInstrumentation(discoveryService, RcmSubscriptionManager.Instance, settings, Instance.ServiceName, tracerManager.Telemetry, Instance.DebuggerSettings, tracerManager.GitMetadataTagsProvider);
            Log.Debug("dynamic Instrumentation has created.");

            try
            {
                DynamicInstrumentationHelper.ServiceName = TraceUtil.NormalizeTag(settings.ServiceName ?? tracerManager.DefaultServiceName);
            }
            catch (Exception e)
            {
                DynamicInstrumentationHelper.ServiceName = tracerManager.DefaultServiceName;
                Log.Error(e, "Could not set `DynamicInstrumentationHelper.ServiceName`.");
            }

            if (!settings.IsRemoteConfigurationAvailable)
            {
                // live debugger requires RCM, so there's no point trying to initialize it if RCM is not available
                if (DebuggerSettings.DynamicSettings.DynamicInstrumentationEnabled ?? DebuggerSettings.DynamicInstrumentationEnabled)
                {
                    Log.Warning("Dynamic Instrumentation is enabled but remote configuration is not available in this environment, so Dynamic Instrumentation cannot be enabled.");
                }

                tracerManager.Telemetry.ProductChanged(TelemetryProductType.DynamicInstrumentation, enabled: false, error: null);
                return;
            }

            try
            {
                var sw = Stopwatch.StartNew();
                var isDiscoverySuccessful = await Datadog.Trace.ClrProfiler.Instrumentation.WaitForDiscoveryService(discoveryService).ConfigureAwait(false);
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
            catch (Exception ex)
            {
                tracerManager.Telemetry.ProductChanged(TelemetryProductType.DynamicInstrumentation, enabled: false, error: null);
                Log.Error(ex, "Error creating Dynamic Instrumentation.");
            }
        }

        public void UpdateDynamicConfiguration(DebuggerSettings newDebuggerSettings)
        {
            /*
              If the remote config says ‘true’, but env var says ‘false’, we do ‘false’
              If the remote config says ‘false’, but env var says ‘true’, we do ‘false’.
              If none are defined - the default value is defined by tracer
             */

            DebuggerSettings = newDebuggerSettings;

            if (newDebuggerSettings.DynamicSettings.CodeOriginEnabled.HasValue)
            {
                if (newDebuggerSettings.DynamicSettings.CodeOriginEnabled.Value)
                {
                    InitializeCodeOrigin();
                }
                else
                {
                    CodeOrigin = null;
                }
            }

            if (newDebuggerSettings.DynamicSettings.ExceptionReplayEnabled.HasValue)
            {
                if (newDebuggerSettings.DynamicSettings.ExceptionReplayEnabled.Value)
                {
                    InitializeExceptionReplay();
                }
                else
                {
                    ExceptionReplay?.Dispose();
                    ExceptionReplay = null;
                }
            }

            if (newDebuggerSettings.DynamicSettings.DynamicInstrumentationEnabled.HasValue)
            {
                if (newDebuggerSettings.DynamicSettings.DynamicInstrumentationEnabled.Value)
                {
                    _ = Task.Run(async () => { await InitializeDynamicInstrumentation().ConfigureAwait(false); });
                }
                else
                {
                    DynamicInstrumentation?.Dispose();
                    Interlocked.Exchange(ref _isDiInitialized, 0);
                    DynamicInstrumentation = null;
                }
            }
        }
    }
}
