// <copyright file="TracerManagerFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources;
using Datadog.Trace.Logging;
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
            // TODO: If relevant settings have not changed, continue using existing agent writer etc
            var tracer = CreateTracerManager(
                settings,
                sampler: null,
                scopeManager: previous?.ScopeManager, // no configuration, so can always use the same one
                telemetry: null,
                dynamicConfigurationManager: null,
                tracerFlareManager: null);

            return tracer;
        }

        internal TracerManager CreateTracerManager(
            TracerSettings settings,
            ITraceSampler sampler,
            IScopeManager scopeManager,
            ITelemetryController telemetry,
            IDynamicConfigurationManager dynamicConfigurationManager,
            ITracerFlareManager tracerFlareManager,
            ServiceRemappingHash serviceRemappingHash = null)
        {
            settings ??= TracerSettings.FromDefaultSourcesInternal();
            var result = GlobalConfigurationSource.CreationResult;
            if (result.Result is not Result.Success)
            {
                Log.Warning(result.Exception, "Failed to create the global configuration source with status: {Status} and error message: {ErrorMessage}", result.Result.ToString(), result.ErrorMessage);
            }

            serviceRemappingHash ??= new ServiceRemappingHash(settings.Manager.InitialMutableSettings.ProcessTags?.SerializedTags);
            telemetry ??= CreateTelemetryController();

            sampler ??= GetSampler(settings);
            scopeManager ??= new AsyncLocalScopeManager();

            var gitMetadataTagsProvider = GetGitMetadataTagsProvider(settings, settings.Manager.InitialMutableSettings, scopeManager, telemetry);

            dynamicConfigurationManager ??= new NullDynamicConfigurationManager();
            tracerFlareManager ??= new NullTracerFlareManager();

            return CreateTracerManagerFrom(
                settings,
                scopeManager,
                telemetry,
                gitMetadataTagsProvider,
                sampler,
                GetSpanSampler(settings),
                dynamicConfigurationManager,
                tracerFlareManager,
                serviceRemappingHash);
        }

        protected virtual ITelemetryController CreateTelemetryController()
            => TelemetryFactory.Instance.CreateTelemetryController();

        protected virtual IGitMetadataTagsProvider GetGitMetadataTagsProvider(TracerSettings settings, MutableSettings initialMutableSettings, IScopeManager scopeManager, ITelemetryController telemetry)
        {
            return new GitMetadataTagsProvider(settings, initialMutableSettings);
        }

        protected virtual TracerManager CreateTracerManagerFrom(
            TracerSettings settings,
            IScopeManager scopeManager,
            ITelemetryController telemetry,
            IGitMetadataTagsProvider gitMetadataTagsProvider,
            ITraceSampler traceSampler,
            ISpanSampler spanSampler,
            IDynamicConfigurationManager dynamicConfigurationManager,
            ITracerFlareManager tracerFlareManager,
            ServiceRemappingHash serviceRemappingHash)
        {
            return new TracerManager(settings, scopeManager, telemetry, gitMetadataTagsProvider, traceSampler, spanSampler, dynamicConfigurationManager, tracerFlareManager, serviceRemappingHash);
        }

        protected virtual ITraceSampler GetSampler(TracerSettings settings)
        {
            if (settings.ApmTracingEnabled == false && Iast.Iast.Instance.Settings.Enabled)
            {
                // Standalone IAST mode: NullAgentWriter discards all spans, so there is no cost
                // to keeping all traces. Remove the rate limiter so every request gets a TraceContext
                // and IastRequestContext is properly initialised for vulnerability detection.
                var samplerStandalone = new TraceSampler.Builder(null);
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
    }
}
