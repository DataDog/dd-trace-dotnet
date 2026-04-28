// <copyright file="TracerManagerFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.Configuration;
using Datadog.Trace.DogStatsd;
using Datadog.Trace.LibDatadog;
using Datadog.Trace.LibDatadog.DataPipeline;
using Datadog.Trace.LibDatadog.HandsOffConfiguration;
using Datadog.Trace.Logging;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.Logging.TracerFlare;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Sampling;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.StatsdClient;

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
            // TODO: If relevant settings have not changed, continue using existing statsd/agent writer etc
            // If reusing statsd, need to propagate the new value of DD_TAGS from dynamic config
            var tracer = CreateTracerManager(
                settings,
                agentWriter: null,
                sampler: null,
                scopeManager: previous?.ScopeManager, // no configuration, so can always use the same one
                statsd: null, // For now, let's continue to always create a new StatsD instance
                logSubmissionManager: previous?.DirectLogSubmission,
                telemetry: null,
                discoveryService: null,
                dynamicConfigurationManager: null,
                tracerFlareManager: null,
                spanEventsManager: null);

            return tracer;
        }

        /// <summary>
        /// Internal for use in tests that create "standalone" <see cref="TracerManager"/> by
        /// <see cref="Tracer(TracerSettings, IAgentWriter, ITraceSampler, IScopeManager, IStatsdManager, ITelemetryController, IDiscoveryService, ServiceRemappingHash)"/>
        /// </summary>
        internal TracerManager CreateTracerManager(
            TracerSettings settings,
            IAgentWriter agentWriter,
            ITraceSampler sampler,
            IScopeManager scopeManager,
            IStatsdManager statsd,
            DirectLogSubmissionManager logSubmissionManager,
            ITelemetryController telemetry,
            IDiscoveryService discoveryService,
            IDynamicConfigurationManager dynamicConfigurationManager,
            ITracerFlareManager tracerFlareManager,
            ISpanEventsManager spanEventsManager,
            ServiceRemappingHash serviceRemappingHash = null)
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

            serviceRemappingHash ??= new ServiceRemappingHash(settings.Manager.InitialMutableSettings.ProcessTags?.SerializedTags);
            discoveryService ??= GetDiscoveryService(settings, serviceRemappingHash);
            var telemetrySettings = CreateTelemetrySettings(settings);
            telemetry ??= CreateTelemetryController(settings, discoveryService, telemetrySettings);

            statsd ??= new StatsdManager(settings);

            sampler ??= GetSampler(settings);
            agentWriter ??= GetAgentWriter(
                settings,
                statsd,
                rates => sampler.SetDefaultSampleRates(rates),
                discoveryService is NullDiscoveryService ? null : discoveryService.SetCurrentConfigStateHash,
                discoveryService,
                telemetrySettings);
            scopeManager ??= new AsyncLocalScopeManager();

            var gitMetadataTagsProvider = GetGitMetadataTagsProvider(settings, settings.Manager.InitialMutableSettings, scopeManager, telemetry);
            logSubmissionManager = DirectLogSubmissionManager.Create(
                settings,
                settings.LogSubmissionSettings,
                settings.AzureAppServiceMetadata,
                gitMetadataTagsProvider);

            dynamicConfigurationManager ??= new NullDynamicConfigurationManager();
            tracerFlareManager ??= new NullTracerFlareManager();
            spanEventsManager ??= new NullSpanEventsManager();

            return CreateTracerManagerFrom(
                settings,
                agentWriter,
                scopeManager,
                statsd,
                logSubmissionManager,
                telemetry,
                discoveryService,
                gitMetadataTagsProvider,
                sampler,
                GetSpanSampler(settings),
                dynamicConfigurationManager,
                tracerFlareManager,
                spanEventsManager,
                serviceRemappingHash);
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
            return new GitMetadataTagsProvider(settings, initialMutableSettings, telemetry);
        }

        protected virtual TracerManager CreateTracerManagerFrom(
            TracerSettings settings,
            IAgentWriter agentWriter,
            IScopeManager scopeManager,
            IStatsdManager statsd,
            DirectLogSubmissionManager logSubmissionManager,
            ITelemetryController telemetry,
            IDiscoveryService discoveryService,
            IGitMetadataTagsProvider gitMetadataTagsProvider,
            ITraceSampler traceSampler,
            ISpanSampler spanSampler,
            IDynamicConfigurationManager dynamicConfigurationManager,
            ITracerFlareManager tracerFlareManager,
            ISpanEventsManager spanEventsManager,
            ServiceRemappingHash serviceRemappingHash)
        {
            return new TracerManager(settings, agentWriter, scopeManager, statsd, logSubmissionManager, telemetry, discoveryService, gitMetadataTagsProvider, traceSampler, spanSampler, dynamicConfigurationManager, tracerFlareManager, spanEventsManager, serviceRemappingHash);
        }

        protected virtual ITraceSampler GetSampler(TracerSettings settings)
        {
            if (settings.ApmTracingEnabled == false && Iast.Iast.Instance.Settings.Enabled)
            {
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

        protected virtual IAgentWriter GetAgentWriter(TracerSettings settings, IStatsdManager statsd, Action<Dictionary<string, float>> updateSampleRates, Action<string> updateConfigHash, IDiscoveryService discoveryService, TelemetrySettings telemetrySettings)
        {
            // Currently we assume this _can't_ toggle at runtime, may need to revisit this if that changes
            IApi api = settings.DataPipelineEnabled && ManagedTraceExporter.TryCreateTraceExporter(settings, updateSampleRates, telemetrySettings, out var traceExporter)
                           ? traceExporter
                           : new ManagedApi(settings.Manager, statsd, updateSampleRates, updateConfigHash, settings.PartialFlushEnabled);

            var statsAggregator = StatsAggregator.Create(api, settings, discoveryService, isOtlp: false);

            return new AgentWriter(api, statsAggregator, statsd, settings);
        }

        internal virtual IDiscoveryService GetDiscoveryService(TracerSettings settings, ServiceRemappingHash serviceRemappingHash)
        {
            return settings.AgentFeaturePollingEnabled
                       ? DiscoveryService.CreateManaged(settings, ContainerMetadata.Instance, serviceRemappingHash)
                       : NullDiscoveryService.Instance;
        }
    }
}
