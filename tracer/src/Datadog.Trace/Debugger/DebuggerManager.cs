using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.Debugger.ExceptionAutoInstrumentation;
using Datadog.Trace.Debugger.Snapshots;
using Datadog.Trace.Debugger.SpanCodeOrigin;
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

        internal LiveDebugger? DynamicInstrumentation { get; }

        internal SpanCodeOrigin.SpanCodeOrigin? CodeOrigin { get; }

        internal ExceptionDebugging? ExceptionReplay { get; set; }

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
                    () => Create(DebuggerSettings.FromDefaultSource()));
            }
        }

        internal static DebuggerManager Create(DebuggerSettings settings)
        {
            var manager = new DebuggerManager(settings);
            manager._products.Add(ExceptionDebugging.Instance);
            manager._products.Add(SpanCodeOrigin.SpanCodeOrigin.Instance);
            manager._products.Add(LiveDebugger.Instance);

            if (manager.Settings.DynamicSettings.DynamicInstrumentationEnabled ?? manager.Settings.DynamicInstrumentationEnabled)
            {
                LiveDebuggerFactory.Create()
            }


            DebuggerSnapshotSerializer.UpdateConfiguration(manager.Settings);
            Redaction.UpdateConfiguration(manager.Settings);

            return manager;
        }

        internal async Task InitializeInstrumentationBasedProducts()
        {
            this.InitializeExceptionReplay();

            await InitializeDynamicInstrumentation();
        }

        private void InitializeExceptionReplay()
        {
            if (this.Settings.DynamicSettings.ExceptionReplayEnabled ?? this.Settings.DynamicInstrumentationEnabled)
            {
                ExceptionDebugging.Initialize();
            }
        }

        private static void CreateDynamicInstrumentation()
        {
            var tracer = Tracer.Instance;
            var settings = tracer.Settings;
            var debuggerSettings = DebuggerSettings.FromDefaultSource();

            if (!settings.IsRemoteConfigurationAvailable)
            {
                // live debugger requires RCM, so there's no point trying to initialize it if RCM is not available
                if (debuggerSettings.DynamicInstrumentationEnabled)
                {
                    Log.Warning("Live Debugger is enabled but remote configuration is not available in this environment, so live debugger cannot be enabled.");
                }

                tracer.TracerManager.Telemetry.ProductChanged(TelemetryProductType.DynamicInstrumentation, enabled: false, error: null);
                return;
            }

            // Service Name must be lowercase, otherwise the agent will not be able to find the service
            var serviceName = DynamicInstrumentationHelper.ServiceName;
            var discoveryService = tracer.TracerManager.DiscoveryService;

            Task.Run(
                async () =>
                {
                    try
                    {
                        var sw = Stopwatch.StartNew();
                        var isDiscoverySuccessful = await WaitForDiscoveryService(discoveryService).ConfigureAwait(false);
                        TelemetryFactory.Metrics.RecordDistributionSharedInitTime(MetricTags.InitializationComponent.DiscoveryService, sw.ElapsedMilliseconds);

                        if (isDiscoverySuccessful)
                        {
                            var liveDebugger = LiveDebuggerFactory.Create(discoveryService, RcmSubscriptionManager.Instance, settings, serviceName, tracer.TracerManager.Telemetry, debuggerSettings, tracer.TracerManager.GitMetadataTagsProvider);
                            Log.Debug("dynamic Instrumentation has created.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error creating Dynamic Instrumentation.");
                    }
                });
        }

        // /!\ This method is called by reflection in the SampleHelpers
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

        internal async Task InitializeDynamicInstrumentation()
        {
            if (Settings.DynamicSettings.DynamicInstrumentationEnabled ?? Settings.DynamicInstrumentationEnabled)
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    await DynamicInstrumentation.InitializeAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to initialize Live Debugger");
                }

                TelemetryFactory.Metrics.RecordDistributionSharedInitTime(MetricTags.InitializationComponent.DynamicInstrumentation, sw.ElapsedMilliseconds);
            }
            else
            {
                Log.Information("Live Debugger is disabled. To enable it, please set DD_DYNAMIC_INSTRUMENTATION_ENABLED environment variable to 'true'.");
            }
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
