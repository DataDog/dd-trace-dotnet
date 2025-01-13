using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.Configuration;
using Datadog.Trace.Debugger.ExceptionAutoInstrumentation;
using Datadog.Trace.Debugger.Sink;
using Datadog.Trace.Debugger.Snapshots;
using Datadog.Trace.Debugger.SpanCodeOrigin;
using Datadog.Trace.Debugger.Symbols;
using Datadog.Trace.Debugger.Upload;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Logging;

#nullable enable

namespace Datadog.Trace.Debugger
{
    internal class DebuggerManager(DebuggerSettings settings)
    {
        internal DebuggerSettings Settings { get; } = settings ?? DebuggerSettings.FromDefaultSource();

        public string ServiceName { get; }

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DebuggerManager));

        internal DynamicInstrumentation? DynamicInstrumentation { get; }

        internal SpanCodeOrigin.SpanCodeOrigin? CodeOrigin { get; }

        internal ExceptionDebugging? ExceptionReplay { get; set; }

        private readonly IDebuggerUploader _symbolsUploader;

        private VendoredMicrosoftCode.System.Collections.Immutable.ImmutableList<IDynamicDebuggerConfiguration> _products;
        private static DebuggerManager _instance;
        private static bool _globalInstanceInitialized;
        private static object _globalInstanceLock = new();

        public static DebuggerManager Instance
        {
            get
            {
                return LazyInitializer.EnsureInitialized(
                    ref _instance,
                    ref _globalInstanceInitialized,
                    ref _globalInstanceLock,
                    Create);
            }
        }

        internal static DebuggerManager Create()
        {
            var manager = new DebuggerManager(DebuggerSettings.FromDefaultSource());
            manager._products.Add(ExceptionDebugging.Instance);
            manager._products.Add(SpanCodeOrigin.SpanCodeOrigin.Instance);
            manager._products.Add(DynamicInstrumentation.Instance);
            var symbolsUploader = CreateSymbolsUploader(discoveryService, remoteConfigurationManager, tracerSettings, serviceName, debuggerSettings, gitMetadataTagsProvider);

            DebuggerSnapshotSerializer.UpdateConfiguration(manager.Settings);
            Redaction.UpdateConfiguration(manager.Settings);

            return manager;
        }

        internal async Task InitializeInstrumentationBasedProducts()
        {
            InitializeExceptionReplay();

            await InitializeDynamicInstrumentation().ConfigureAwait(false);
        }

        private void InitializeExceptionReplay()
        {
            if (Settings.DynamicSettings.ExceptionReplayEnabled ?? this.Settings.DynamicInstrumentationEnabled)
            {
                ExceptionDebugging.Initialize();
            }
        }

        private async Task<DynamicInstrumentation?> InitializeDynamicInstrumentation()
        {
            if (!Settings.DynamicSettings.DynamicInstrumentationEnabled ?? !Settings.DynamicInstrumentationEnabled)
            {
                return null;
            }

            var tracer = Tracer.Instance;
            var settings = tracer.Settings;

            if (!settings.IsRemoteConfigurationAvailable)
            {
                // live debugger requires RCM, so there's no point trying to initialize it if RCM is not available
                if (_instance.Settings.DynamicSettings.DynamicInstrumentationEnabled ?? _instance.Settings.DynamicInstrumentationEnabled)
                {
                    Log.Warning("Dynamic Instrumentation is enabled but remote configuration is not available in this environment, so Dynamic Instrumentation cannot be enabled.");
                }

                tracer.TracerManager.Telemetry.ProductChanged(TelemetryProductType.DynamicInstrumentation, enabled: false, error: null);
                return null;
            }

            // Service Name must be lowercase, otherwise the agent will not be able to find the service
            var serviceName = DynamicInstrumentationHelper.ServiceName;
            var discoveryService = tracer.TracerManager.DiscoveryService;
            try
            {
                var sw = Stopwatch.StartNew();
                var isDiscoverySuccessful = await Datadog.Trace.ClrProfiler.Instrumentation.WaitForDiscoveryService(discoveryService).ConfigureAwait(false);
                TelemetryFactory.Metrics.RecordDistributionSharedInitTime(MetricTags.InitializationComponent.DiscoveryService, sw.ElapsedMilliseconds);

                if (isDiscoverySuccessful)
                {
                    sw.Restart();
                    var dynamicInstrumentation = DebuggerFactory.Create(discoveryService, RcmSubscriptionManager.Instance, settings, serviceName, tracer.TracerManager.Telemetry, _instance.Settings, tracer.TracerManager.GitMetadataTagsProvider);
                    Log.Debug("dynamic Instrumentation has created.");
                    await dynamicInstrumentation.InitializeAsync().ConfigureAwait(false);
                    TelemetryFactory.Metrics.RecordDistributionSharedInitTime(MetricTags.InitializationComponent.DynamicInstrumentation, sw.ElapsedMilliseconds);
                    return dynamicInstrumentation;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating Dynamic Instrumentation.");
            }

            return null;
        }

        Task StartAsync()
        {
            LifetimeManager.Instance.AddShutdownTask(ShutdownTask);

            _symbolsUploader.StartFlushingAsync();
            return _snapshotUploader.StartFlushingAsync();
        }

        void ShutdownTask(Exception ex)
        {
            _snapshotUploader.Dispose();
            _symbolsUploader.Dispose();
        }
    }
}
