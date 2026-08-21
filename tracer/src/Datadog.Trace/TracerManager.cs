// <copyright file="TracerManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Configuration.Schema;
using Datadog.Trace.Logging;
using Datadog.Trace.Logging.TracerFlare;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Processors;
using Datadog.Trace.Propagators;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Util;
using Datadog.Trace.Util.Http;
using Datadog.Trace.Util.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace
{
    /// <summary>
    /// This class is responsible for managing the singleton objects associated with a Tracer.
    /// In normal usage, the <see cref="Instance"/> should be the only "live" instance. For testing
    /// purposes, we still need to create instances and keep them separate.
    /// </summary>
    internal class TracerManager
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<TracerManager>();

        private static volatile bool _firstInitialization = true;
        private static Timer _heartbeatTimer;

        private static TracerManager _instance;
        private static bool _globalInstanceInitialized;
        private static object _globalInstanceLock = new();

        private readonly IDisposable _settingSubscription;
        private PerTraceSettings _perTraceSettings;

        public TracerManager(
            TracerSettings settings,
            IScopeManager scopeManager,
            IGitMetadataTagsProvider gitMetadataTagsProvider,
            IDynamicConfigurationManager dynamicConfigurationManager,
            ITracerFlareManager tracerFlareManager,
            ServiceRemappingHash serviceRemappingHash)
        {
            Settings = settings;
            ScopeManager = scopeManager;
            GitMetadataTagsProvider = gitMetadataTagsProvider;
            QueryStringManager = new(settings.QueryStringReportingEnabled, settings.ObfuscationQueryStringRegexTimeout, settings.QueryStringReportingSize, settings.ObfuscationQueryStringRegex);

            DynamicConfigurationManager = dynamicConfigurationManager;
            TracerFlareManager = tracerFlareManager;

            ServiceRemappingHash = serviceRemappingHash;

            SpanContextPropagator = SpanContextPropagatorFactory.GetSpanContextPropagator(settings.PropagationStyleInject, settings.PropagationStyleExtract, settings.PropagationExtractFirstOnly, settings.PropagationBehaviorExtract);
            UpdatePerTraceSettings(settings.Manager.InitialMutableSettings);
            _settingSubscription = settings.Manager.SubscribeToChanges(changes =>
            {
                if (changes.UpdatedMutable is { } mutable)
                {
                    UpdatePerTraceSettings(mutable);
                }
            });

            void UpdatePerTraceSettings(MutableSettings mutableSettings)
            {
                var schema = new NamingSchema(settings.MetadataSchemaVersion, settings.PeerServiceTagsEnabled, settings.RemoveClientServiceNamesEnabled, mutableSettings.DefaultServiceName, mutableSettings.ServiceNameMappings, settings.PeerServiceNameMappings);
                Interlocked.Exchange(ref _perTraceSettings, new(schema, mutableSettings));
            }
        }

        /// <summary>
        /// Gets the global <see cref="TracerManager"/> instance used by all <see cref="Tracer"/> instances
        /// </summary>
        public static TracerManager Instance
        {
            get
            {
                return LazyInitializer.EnsureInitialized(
                    ref _instance,
                    ref _globalInstanceInitialized,
                    ref _globalInstanceLock,
                    () => CreateInitializedTracer(settings: null, TracerManagerFactory.Instance));
            }
        }

        public IGitMetadataTagsProvider GitMetadataTagsProvider { get; }

        /// <summary>
        /// Gets this tracer's settings.
        /// </summary>
        public TracerSettings Settings { get; }

        /// <summary>
        /// Gets the tracer's scope manager, which determines which span is currently active, if any.
        /// </summary>
        public IScopeManager ScopeManager { get; }

        /// Gets the global <see cref="QueryStringManager"/> instance.
        public QueryStringManager QueryStringManager { get; }

        public ITraceProcessor[] TraceProcessors { get; }

        public ITagProcessor[] TagProcessors { get; }

        public IDynamicConfigurationManager DynamicConfigurationManager { get; }

        public ITracerFlareManager TracerFlareManager { get; }

        public PerTraceSettings PerTraceSettings => Volatile.Read(ref _perTraceSettings);

        public SpanContextPropagator SpanContextPropagator { get; }

        public ServiceRemappingHash ServiceRemappingHash { get; }

        /// <summary>
        /// Replaces the global <see cref="TracerManager"/> settings. This affects all <see cref="Tracer"/> instances
        /// which use the global <see cref="TracerManager"/>
        /// </summary>
        /// <param name="settings">The settings to use </param>
        /// <param name="factory">The factory to use to create the <see cref="TracerManager"/></param>
        public static void ReplaceGlobalManager(TracerSettings settings, TracerManagerFactory factory)
        {
            TracerManager oldManager;
            TracerManager newManager;
            lock (_globalInstanceLock)
            {
                oldManager = _instance;
                newManager = CreateInitializedTracer(settings, factory);
                _instance = newManager;
                _globalInstanceInitialized = true;
            }

            if (oldManager is not null)
            {
                // Fire and forget
                _ = CleanUpOldTracerManager(oldManager, newManager);
            }
        }

        /// <summary>
        /// Sets the global tracer instance without any validation or cleanup.
        /// Intended use is for unit testing only
        /// </summary>
        public static void UnsafeReplaceGlobalManager(TracerManager instance)
        {
            lock (_globalInstanceLock)
            {
                _instance = instance;
                _globalInstanceInitialized = true;
            }
        }

        /// <summary>
        /// Start internal processes that require Tracer.Instance is already set
        /// </summary>
        internal void Start()
        {
            // Must be idempotent and thread safe
            DynamicConfigurationManager.Start();
            TracerFlareManager.Start();
        }

        /// <summary>
        /// Internal for testing only.
        /// Run all the shutdown tasks for a standalone <see cref="TracerManager"/> instance,
        /// stopping the background processes.
        /// </summary>
        [TestingOnly]
        internal Task ShutdownAsync() => RunShutdownTasksAsync(this, null);

        private static async Task CleanUpOldTracerManager(TracerManager oldManager, TracerManager newManager)
        {
            try
            {
                oldManager._settingSubscription.Dispose();
                var agentWriterReplaced = false;

                var discoveryReplaced = false;

                var dynamicConfigurationManagerReplaced = false;
                if (oldManager.DynamicConfigurationManager != newManager.DynamicConfigurationManager && oldManager.DynamicConfigurationManager is not null)
                {
                    dynamicConfigurationManagerReplaced = true;
                    oldManager.DynamicConfigurationManager.Dispose();
                }

                var tracerFlareManagerReplaced = false;
                if (oldManager.TracerFlareManager != newManager.TracerFlareManager && oldManager.TracerFlareManager is not null)
                {
                    tracerFlareManagerReplaced = true;
                    oldManager.TracerFlareManager.Dispose();
                }

                Log.Information(
                    exception: null,
                    "Replaced global instances. AgentWriter: {AgentWriterReplaced}, Discovery: {DiscoveryReplaced}, DynamicConfigurationManager: {DynamicConfigurationManagerReplaced}, TracerFlareManager {TracerFlareManagerReplaced}",
                    new object[] { agentWriterReplaced, discoveryReplaced, dynamicConfigurationManagerReplaced, tracerFlareManagerReplaced });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error cleaning up old tracer manager");
            }
        }

        // should only be called inside a global lock, i.e. by TracerManager.Instance or ReplaceGlobalManager
        private static TracerManager CreateInitializedTracer(TracerSettings settings, TracerManagerFactory factory)
        {
            if (_instance is ILockedTracer)
            {
                ThrowHelper.ThrowInvalidOperationException("The current tracer instance cannot be replaced.");
            }

            var newManager = factory.CreateTracerManager(settings, _instance);

            if (_firstInitialization)
            {
                _firstInitialization = false;
                OneTimeSetup(newManager.Settings);
            }

            return newManager;
        }

        private static void OneTimeSetup(TracerSettings tracerSettings)
        {
            // Register callbacks to make sure we flush the traces before exiting
            LifetimeManager.Instance.AddAsyncShutdownTask(RunShutdownTasksAsync);

            // start the heartbeat loop
            _heartbeatTimer = new Timer(HeartbeatCallback, state: null, dueTime: TimeSpan.Zero, period: TimeSpan.FromMinutes(1));
        }

        private static Task RunShutdownTasksAsync(Exception ex) => RunShutdownTasksAsync(_instance, _heartbeatTimer);

        private static async Task RunShutdownTasksAsync(TracerManager instance, Timer heartbeatTimer)
        {
            try
            {
                if (heartbeatTimer is not null)
                {
                    Log.Debug("Disposing Heartbeat timer.");
#if NETCOREAPP3_1_OR_GREATER
                    await heartbeatTimer.DisposeAsync().ConfigureAwait(false);
#else
                    heartbeatTimer.Dispose();
#endif
                }

                if (instance is not null)
                {
                    instance._settingSubscription.Dispose();
                    Log.Debug("Disposing DynamicConfigurationManager");
                    instance.DynamicConfigurationManager.Dispose();
                    Log.Debug("Disposing TracerFlareManager");
                    instance.TracerFlareManager.Dispose();

                    Log.Debug("Finished waiting for disposals.");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error flushing traces on shutdown.");
            }
        }

        private static void HeartbeatCallback(object state)
        {
        }
    }
}
