// <copyright file="TracerManagerFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Reflection;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.AppSec;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources;
using Datadog.Trace.ContinuousProfiler;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DogStatsd;
using Datadog.Trace.Iast;
using Datadog.Trace.LibDatadog;
using Datadog.Trace.LibDatadog.DataPipeline;
using Datadog.Trace.LibDatadog.HandsOffConfiguration;
using Datadog.Trace.Logging;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.Logging.TracerFlare;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Processors;
using Datadog.Trace.Propagators;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.RemoteConfigurationManagement.Transport;
using Datadog.Trace.RuntimeMetrics;
using Datadog.Trace.Sampling;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.StatsdClient;
using ConfigurationKeys = Datadog.Trace.Configuration.ConfigurationKeys;
using MetricsTransportType = Datadog.Trace.Vendors.StatsdClient.Transport.TransportType;
using NativeInterop = Datadog.Trace.ContinuousProfiler.NativeInterop;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Datadog.Trace
{
    internal class TracerManagerFactory
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<TracerManagerFactory>();

        public static readonly TracerManagerFactory Instance = new();

        /// <summary>
        /// The primary factory method, called by <see cref="TracerManager"/>,
        /// providing the previous global <see cref="TracerManager"/> instance (may be null)
        /// </summary>
        internal TracerManager CreateTracerManager(TracerSettings settings, TracerManager previous)
        {
            // TODO: If relevant settings have not changed, continue using existing statsd/agent writer/runtime metrics etc
            // If reusing the runtime metrics/statsd, need to propagate the new value of DD_TAGS from dynamic config
            var tracer = CreateTracerManager(
                settings,
                agentWriter: null,
                sampler: null,
                scopeManager: previous?.ScopeManager, // no configuration, so can always use the same one
                statsd: null, // For now, let's continue to always create a new StatsD instance
                runtimeMetrics: previous?.RuntimeMetrics,
                logSubmissionManager: previous?.DirectLogSubmission,
                telemetry: null,
                discoveryService: null,
                dataStreamsManager: null,
                remoteConfigurationManager: null,
                dynamicConfigurationManager: null,
                tracerFlareManager: null,
                spanEventsManager: null);

            try
            {
                if (Profiler.Instance.Status.IsProfilerReady)
                {
                    var mutableSettings = tracer.PerTraceSettings.Settings;
                    NativeInterop.SetApplicationInfoForAppDomain(RuntimeId.Get(), mutableSettings.DefaultServiceName, mutableSettings.Environment, mutableSettings.ServiceVersion);
                }
            }
            catch (Exception ex)
            {
                // We failed to retrieve the runtime from native this can be because:
                // - P/Invoke issue (unknown dll, unknown entrypoint...)
                // - We are running in a partial trust environment
                Log.Warning(ex, "Failed to set the service name for native.");
            }

            return tracer;
        }

        /// <summary>
        /// Internal for use in tests that create "standalone" <see cref="TracerManager"/> by
        /// <see cref="Tracer(TracerSettings, IAgentWriter, ITraceSampler, IScopeManager, IDogStatsd, ITelemetryController, IDiscoveryService)"/>
        /// </summary>
        internal TracerManager CreateTracerManager(
            TracerSettings settings,
            IAgentWriter agentWriter,
            ITraceSampler sampler,
            IScopeManager scopeManager,
            IDogStatsd statsd,
            RuntimeMetricsWriter runtimeMetrics,
            DirectLogSubmissionManager logSubmissionManager,
            ITelemetryController telemetry,
            IDiscoveryService discoveryService,
            DataStreamsManager dataStreamsManager,
            IRemoteConfigurationManager remoteConfigurationManager,
            IDynamicConfigurationManager dynamicConfigurationManager,
            ITracerFlareManager tracerFlareManager,
            ISpanEventsManager spanEventsManager)
        {
            settings ??= TracerSettings.FromDefaultSourcesInternal();
            var result = GlobalConfigurationSource.CreationResult;
            if (result.Result is not Result.Success)
            {
                Log.Warning(result.Exception, "Failed to create the global configuration source with status: {Status} and error message: {ErrorMessage}", result.Result.ToString(), result.ErrorMessage);
            }

            var libdatadogAvailaibility = LibDatadogAvailabilityHelper.IsLibDatadogAvailable;
            if (libdatadogAvailaibility.Exception is not null)
            {
                Log.Warning(libdatadogAvailaibility.Exception, "An exception occurred while checking if libdatadog is available");
            }

            // TODO: Update anything that accesses tracerSettings.MutableSettings or tracerSettings.Manager.InitialTracerSettings
            // to subscribe to changes, once we stop creating a new TracerManager whenever there's a config change

            var defaultServiceName = settings.MutableSettings.DefaultServiceName;

            discoveryService ??= GetDiscoveryService(settings);

            // Technically we don't _always_ need a dogstatsd instance, because we only need it if runtime metrics
            // are enabled _or_ tracer metrics are enabled. However, tracer metrics can be enabled and disabled dynamically
            // at runtime, which makes managing the lifetime of the statsd instance more complex than we'd like, so
            // for simplicity, we _always_ create a new statsd instance
            statsd ??= new StatsdManager(settings, includeDefaultTags: true);
            runtimeMetrics ??= settings.RuntimeMetricsEnabled && !DistributedTracer.Instance.IsChildTracer
                                   ? new RuntimeMetricsWriter(statsd, TimeSpan.FromSeconds(10), settings.IsRunningInAzureAppService)
                                   : null;

            sampler ??= GetSampler(settings);
            agentWriter ??= GetAgentWriter(settings, statsd, rates => sampler.SetDefaultSampleRates(rates), discoveryService, telemetrySettings);
            scopeManager ??= new AsyncLocalScopeManager();

            var gitMetadataTagsProvider = GetGitMetadataTagsProvider(settings, settings.Manager.InitialMutableSettings, scopeManager, telemetry);
            logSubmissionManager = DirectLogSubmissionManager.Create(
                settings,
                settings.LogSubmissionSettings,
                settings.AzureAppServiceMetadata,
                gitMetadataTagsProvider);

            TelemetryFactory.Metrics.SetWafAndRulesVersion(Security.Instance.DdlibWafVersion, Security.Instance.WafRuleFileVersion);
            ErrorData? initError = !string.IsNullOrEmpty(Security.Instance.InitializationError)
                                       ? new ErrorData(TelemetryErrorCode.AppsecConfigurationError, Security.Instance.InitializationError)
                                       : null;
            telemetry.ProductChanged(TelemetryProductType.AppSec, enabled: Security.Instance.AppsecEnabled, initError);

            var profiler = Profiler.Instance;
            telemetry.RecordProfilerSettings(profiler);
            telemetry.ProductChanged(TelemetryProductType.Profiler, enabled: profiler.Status.IsProfilerReady, error: null);

            dataStreamsManager ??= DataStreamsManager.Create(settings, profiler.Settings, discoveryService);

            if (ShouldEnableRemoteConfiguration(settings))
            {
                if (remoteConfigurationManager == null)
                {
                    var sw = Stopwatch.StartNew();

                    remoteConfigurationManager =
                        RemoteConfigurationManager.Create(
                            discoveryService,
                            RemoteConfigurationSettings.FromDefaultSource(),
                            settings,
                            gitMetadataTagsProvider,
                            RcmSubscriptionManager.Instance);

                    TelemetryFactory.Metrics.RecordDistributionSharedInitTime(MetricTags.InitializationComponent.Rcm, sw.ElapsedMilliseconds);
                }

                dynamicConfigurationManager ??= new DynamicConfigurationManager(RcmSubscriptionManager.Instance);
                tracerFlareManager ??= new TracerFlareManager(discoveryService, RcmSubscriptionManager.Instance, telemetry, TracerFlareApi.Create(settings.Exporter));
                spanEventsManager ??= new SpanEventsManager(discoveryService);
            }
            else
            {
                remoteConfigurationManager ??= new NullRemoteConfigurationManager();
                dynamicConfigurationManager ??= new NullDynamicConfigurationManager();
                tracerFlareManager ??= new NullTracerFlareManager();
                spanEventsManager ??= new NullSpanEventsManager();

                if (RcmSubscriptionManager.Instance.HasAnySubscription)
                {
                    Log.Debug($"{nameof(RcmSubscriptionManager)} has subscriptions but remote configuration is not available in this scenario.");
                }
            }

            return CreateTracerManagerFrom(
                settings,
                agentWriter,
                scopeManager,
                statsd,
                runtimeMetrics,
                logSubmissionManager,
                telemetry,
                discoveryService,
                dataStreamsManager,
                gitMetadataTagsProvider,
                sampler,
                GetSpanSampler(settings),
                remoteConfigurationManager,
                dynamicConfigurationManager,
                tracerFlareManager,
                spanEventsManager);
        }

        protected virtual TelemetrySettings CreateTelemetrySettings(TracerSettings settings) =>
            TelemetrySettings.FromSource(
                GlobalConfigurationSource.Instance,
                TelemetryFactory.Config,
                settings,
                isAgentAvailable: null);

        protected virtual ITelemetryController CreateTelemetryController(TracerSettings settings, IDiscoveryService discoveryService, TelemetrySettings telemetrySettings)
            => TelemetryFactory.Instance.CreateTelemetryController(settings, telemetrySettings, discoveryService);

        protected virtual IGitMetadataTagsProvider GetGitMetadataTagsProvider(TracerSettings settings, MutableSettings initialMutableSettings, IScopeManager scopeManager, ITelemetryController telemetry)
        {
            return new GitMetadataTagsProvider(settings, initialMutableSettings, scopeManager, telemetry);
        }

        protected virtual bool ShouldEnableRemoteConfiguration(TracerSettings settings)
            => settings.IsRemoteConfigurationAvailable;

        /// <summary>
        ///  Can be overriden to create a different <see cref="TracerManager"/>, e.g. <see cref="Ci.TestOptimizationTracerManager"/>
        /// </summary>
        protected virtual TracerManager CreateTracerManagerFrom(
            TracerSettings settings,
            IAgentWriter agentWriter,
            IScopeManager scopeManager,
            IDogStatsd statsd,
            RuntimeMetricsWriter runtimeMetrics,
            DirectLogSubmissionManager logSubmissionManager,
            ITelemetryController telemetry,
            IDiscoveryService discoveryService,
            DataStreamsManager dataStreamsManager,
            IGitMetadataTagsProvider gitMetadataTagsProvider,
            ITraceSampler traceSampler,
            ISpanSampler spanSampler,
            IRemoteConfigurationManager remoteConfigurationManager,
            IDynamicConfigurationManager dynamicConfigurationManager,
            ITracerFlareManager tracerFlareManager,
            ISpanEventsManager spanEventsManager)
            => new TracerManager(settings, agentWriter, scopeManager, statsd, runtimeMetrics, logSubmissionManager, telemetry, discoveryService, dataStreamsManager, gitMetadataTagsProvider, traceSampler, spanSampler, remoteConfigurationManager, dynamicConfigurationManager, tracerFlareManager, spanEventsManager);

        protected virtual ITraceSampler GetSampler(TracerSettings settings)
        {
            // TODO: This may need to be updated to be dynamic, and to handle changes to enablement
            // e.g. AppSec can be enabled/disabled dynamically, which could change this flag at runtime,
            // leaving us with the wrong sampler in place
            if (settings.ApmTracingEnabled == false &&
                (Security.Instance.Settings.AppsecEnabled || Iast.Iast.Instance.Settings.Enabled))
            {
                // Custom sampler for ASM and IAST standalone billing mode
                var samplerStandalone = new TraceSampler.Builder(new TracerRateLimiter(maxTracesPerInterval: 1, intervalMilliseconds: 60_000));
                samplerStandalone.RegisterRule(new GlobalSamplingRateRule(1.0f));
                return samplerStandalone.Build();
            }

            return new ManagedTraceSampler(settings);
        }

        protected virtual ISpanSampler GetSpanSampler(TracerSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.SpanSamplingRules))
            {
                return new SpanSampler([]);
            }

            return new SpanSampler(SpanSamplingRule.BuildFromConfigurationString(settings.SpanSamplingRules, RegexBuilder.DefaultTimeout));
        }

        protected virtual IAgentWriter GetAgentWriter(TracerSettings settings, IDogStatsd statsd, Action<Dictionary<string, float>> updateSampleRates, IDiscoveryService discoveryService, TelemetrySettings telemetrySettings)
        {
            // Currently we assume this _can't_ toggle at runtime, may need to revisit this if that changes
            IApi api = settings.DataPipelineEnabled && ManagedTraceExporter.TryCreateTraceExporter(settings, updateSampleRates, telemetrySettings, out var traceExporter)
                           ? traceExporter
                           : new ManagedApi(settings.Manager, statsd, updateSampleRates, settings.PartialFlushEnabled);

            var statsAggregator = StatsAggregator.Create(api, settings, discoveryService);

            return new AgentWriter(api, statsAggregator, statsd, settings);
        }

        internal virtual IDiscoveryService GetDiscoveryService(TracerSettings settings)
            => settings.AgentFeaturePollingEnabled ?
                   DiscoveryService.Create(settings.Exporter) :
                   NullDiscoveryService.Instance;
    }
}
