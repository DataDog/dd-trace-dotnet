// <copyright file="DebuggerManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.Debugger.Configurations;
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

        private VendoredMicrosoftCode.System.Collections.Immutable.ImmutableList<IDynamicDebuggerConfiguration> _products = [];

        internal static readonly DebuggerManager Instance = new(DebuggerSettings.FromDefaultSource(), ExceptionReplaySettings.FromDefaultSource());

        internal DebuggerSettings DebuggerSettings { get; } = debuggerSettings;

        internal ExceptionReplaySettings ExceptionReplaySettings { get; } = exceptionReplaySettings;

        internal DynamicInstrumentation? DynamicInstrumentation { get; private set; }

        internal SpanCodeOrigin.SpanCodeOrigin? CodeOrigin { get; private set; }

        internal IDebuggerUploader? SymbolsUploader { get; private set; }

        internal ExceptionDebugging? ExceptionReplay => ExceptionDebugging.Instance;

        internal string ServiceName => DynamicInstrumentationHelper.ServiceName;

        internal async Task InitializeInstrumentationBasedProducts()
        {
            await InitializeSymbolUploader().ConfigureAwait(false);
            InitializeSpanOrigin();
            DebuggerSnapshotSerializer.UpdateConfiguration(Instance.DebuggerSettings);
            Redaction.UpdateConfiguration(Instance.DebuggerSettings);
            InitializeExceptionReplay();
            await InitializeDynamicInstrumentation().ConfigureAwait(false);
        }

        private void InitializeSpanOrigin()
        {
            if (!(DebuggerSettings.DynamicSettings.SpanOriginEntryEnabled ?? !DebuggerSettings.CodeOriginForSpansEnabled))
            {
                Log.Information("Code Origin for Spans is disabled. To enable it, please set {CodeOriginForSpans} environment variable to '1'/'true'.", Datadog.Trace.Configuration.ConfigurationKeys.Debugger.CodeOriginForSpansEnabled);
                return;
            }

            CodeOrigin = new SpanCodeOrigin.SpanCodeOrigin(Instance.DebuggerSettings);
            _products.Add(CodeOrigin);
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
            if (!(DebuggerSettings.DynamicSettings.ExceptionReplayEnabled ?? ExceptionReplaySettings.Enabled))
            {
                Log.Information("Exception Replay is disabled. To enable it, please set {ExceptionReplayEnabled} environment variable to '1'/'true'.", Datadog.Trace.Configuration.ConfigurationKeys.Debugger.ExceptionReplayEnabled);
                return;
            }

            try
            {
                if (ExceptionDebugging.Initialize())
                {
                    _products.Add(ExceptionDebugging.Instance);
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
            if (!(DebuggerSettings.DynamicSettings.DynamicInstrumentationEnabled ?? !DebuggerSettings.DynamicInstrumentationEnabled))
            {
                Log.Information("Dynamic Instrumentation is disabled. To enable it, please set {DynamicInstrumentationEnabled} environment variable to '1'/'true'.", Datadog.Trace.Configuration.ConfigurationKeys.Debugger.DynamicInstrumentationEnabled);
                return;
            }

            var tracerManager = TracerManager.Instance;
            var settings = tracerManager.Settings;
            var discoveryService = tracerManager.DiscoveryService;

            DynamicInstrumentation = DebuggerFactory.CreateDynamicInstrumentation(discoveryService, RcmSubscriptionManager.Instance, settings, Instance.ServiceName, tracerManager.Telemetry, Instance.DebuggerSettings, tracerManager.GitMetadataTagsProvider);
            Log.Debug("dynamic Instrumentation has created.");
            _products.Add(DynamicInstrumentation);

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
            throw new NotImplementedException();
        }
    }
}
