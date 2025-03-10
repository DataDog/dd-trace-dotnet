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
using Datadog.Trace.ContinuousProfiler;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DogStatsd;
using Datadog.Trace.Iast;
using Datadog.Trace.Logging;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.Logging.TracerFlare;
using Datadog.Trace.Processors;
using Datadog.Trace.Propagators;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.RemoteConfigurationManagement.Transport;
using Datadog.Trace.RuntimeMetrics;
using Datadog.Trace.Sampling;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.StatsdClient;
using ConfigurationKeys = Datadog.Trace.Configuration.ConfigurationKeys;
using MetricsTransportType = Datadog.Trace.Vendors.StatsdClient.Transport.TransportType;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Datadog.Trace
{
    internal class TracerManagerFactory
    {
        private const string UnknownServiceName = "UnknownService";
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
                statsd: null,
                runtimeMetrics: null,
                logSubmissionManager: previous?.DirectLogSubmission,
                telemetry: null,
                discoveryService: null,
                dataStreamsManager: null,
                remoteConfigurationManager: null,
                dynamicConfigurationManager: null,
                tracerFlareManager: null);

            try
            {
                if (Profiler.Instance.Status.IsProfilerReady)
                {
                    NativeInterop.SetApplicationInfoForAppDomain(RuntimeId.Get(), tracer.DefaultServiceName, tracer.Settings.Environment, tracer.Settings.ServiceVersion);
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
            ITracerFlareManager tracerFlareManager)
        {
            settings ??= TracerSettings.FromDefaultSourcesInternal();

            var defaultServiceName = settings.ServiceName ??
                GetApplicationName(settings) ??
                UnknownServiceName;

            discoveryService ??= GetDiscoveryService(settings);

            bool runtimeMetricsEnabled = settings.RuntimeMetricsEnabled && !DistributedTracer.Instance.IsChildTracer;

            statsd = (settings.TracerMetricsEnabled || runtimeMetricsEnabled)
                         ? (statsd ?? CreateDogStatsdClient(settings, defaultServiceName))
                         : null;
            sampler ??= GetSampler(settings);
            agentWriter ??= GetAgentWriter(settings, settings.TracerMetricsEnabled ? statsd : null, rates => sampler.SetDefaultSampleRates(rates), discoveryService);
            scopeManager ??= new AsyncLocalScopeManager();

            if (runtimeMetricsEnabled)
            {
                runtimeMetrics ??= new RuntimeMetricsWriter(statsd, TimeSpan.FromSeconds(10), settings.IsRunningInAzureAppService);
            }
            else
            {
                runtimeMetrics = null;
            }

            telemetry ??= CreateTelemetryController(settings, discoveryService);

            var gitMetadataTagsProvider = GetGitMetadataTagsProvider(settings, scopeManager, telemetry);
            logSubmissionManager = DirectLogSubmissionManager.Create(
                logSubmissionManager,
                settings,
                settings.LogSubmissionSettings,
                settings.AzureAppServiceMetadata,
                defaultServiceName,
                settings.Environment,
                settings.ServiceVersion,
                gitMetadataTagsProvider);

            telemetry.RecordTracerSettings(settings, defaultServiceName);
            TelemetryFactory.Metrics.SetWafAndRulesVersion(Security.Instance.DdlibWafVersion, Security.Instance.WafRuleFileVersion);
            ErrorData? initError = !string.IsNullOrEmpty(Security.Instance.InitializationError)
                                       ? new ErrorData(TelemetryErrorCode.AppsecConfigurationError, Security.Instance.InitializationError)
                                       : null;
            telemetry.ProductChanged(TelemetryProductType.AppSec, enabled: Security.Instance.AppsecEnabled, initError);

            var profiler = Profiler.Instance;
            telemetry.RecordProfilerSettings(profiler);
            telemetry.ProductChanged(TelemetryProductType.Profiler, enabled: profiler.Status.IsProfilerReady, error: null);

            dataStreamsManager ??= DataStreamsManager.Create(settings, discoveryService, defaultServiceName);

            if (ShouldEnableRemoteConfiguration(settings))
            {
                if (remoteConfigurationManager == null)
                {
                    var sw = Stopwatch.StartNew();

                    var rcmSettings = RemoteConfigurationSettings.FromDefaultSource();
                    var rcmApi = RemoteConfigurationApiFactory.Create(settings.Exporter, rcmSettings, discoveryService);

                    // Service Name must be lowercase, otherwise the agent will not be able to find the service
                    var serviceName = TraceUtil.NormalizeTag(settings.ServiceName ?? defaultServiceName);

                    remoteConfigurationManager =
                        RemoteConfigurationManager.Create(
                            discoveryService,
                            rcmApi,
                            rcmSettings,
                            serviceName,
                            settings,
                            gitMetadataTagsProvider,
                            RcmSubscriptionManager.Instance);

                    TelemetryFactory.Metrics.RecordDistributionSharedInitTime(MetricTags.InitializationComponent.Rcm, sw.ElapsedMilliseconds);
                }

                dynamicConfigurationManager ??= new DynamicConfigurationManager(RcmSubscriptionManager.Instance);
                tracerFlareManager ??= new TracerFlareManager(discoveryService, RcmSubscriptionManager.Instance, telemetry, TracerFlareApi.Create(settings.Exporter));
            }
            else
            {
                remoteConfigurationManager ??= new NullRemoteConfigurationManager();
                dynamicConfigurationManager ??= new NullDynamicConfigurationManager();
                tracerFlareManager ??= new NullTracerFlareManager();

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
                defaultServiceName,
                gitMetadataTagsProvider,
                sampler,
                GetSpanSampler(settings),
                remoteConfigurationManager,
                dynamicConfigurationManager,
                tracerFlareManager);
        }

        protected virtual ITelemetryController CreateTelemetryController(TracerSettings settings, IDiscoveryService discoveryService)
        {
            return TelemetryFactory.Instance.CreateTelemetryController(settings, discoveryService);
        }

        protected virtual IGitMetadataTagsProvider GetGitMetadataTagsProvider(TracerSettings settings, IScopeManager scopeManager, ITelemetryController telemetry)
        {
            return new GitMetadataTagsProvider(settings, scopeManager, telemetry);
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
            string defaultServiceName,
            IGitMetadataTagsProvider gitMetadataTagsProvider,
            ITraceSampler traceSampler,
            ISpanSampler spanSampler,
            IRemoteConfigurationManager remoteConfigurationManager,
            IDynamicConfigurationManager dynamicConfigurationManager,
            ITracerFlareManager tracerFlareManager)
            => new TracerManager(settings, agentWriter, scopeManager, statsd, runtimeMetrics, logSubmissionManager, telemetry, discoveryService, dataStreamsManager, defaultServiceName, gitMetadataTagsProvider, traceSampler, spanSampler, remoteConfigurationManager, dynamicConfigurationManager, tracerFlareManager);

        protected virtual ITraceSampler GetSampler(TracerSettings settings)
        {
            // ISamplingRule is used to implement, in order of precedence:
            // - custom sampling rules
            //   - remote custom rules (provenance: "customer")
            //   - remote dynamic rules (provenance: "dynamic")
            //   - local custom rules (provenance: "local"/none) = DD_TRACE_SAMPLING_RULES
            // - global sampling rate
            //   - remote
            //   - local = DD_TRACE_SAMPLE_RATE
            // - agent sampling rates (as a single rule)

            // Note: the order that rules are registered is important, as they are evaluated in order.
            // The first rule that matches will be used to determine the sampling rate.

            if (settings.ApmTracingEnabledInternal == false &&
                (Security.Instance.Settings.AppsecEnabled || Iast.Iast.Instance.Settings.Enabled))
            {
                // Custom sampler for ASM and IAST standalone billing mode
                var samplerStandalone = new TraceSampler(new TracerRateLimiter(maxTracesPerInterval: 1, intervalMilliseconds: 60_000));
                samplerStandalone.RegisterRule(new GlobalSamplingRateRule(1.0f));
                return samplerStandalone;
            }

            var sampler = new TraceSampler(new TracerRateLimiter(maxTracesPerInterval: settings.MaxTracesSubmittedPerSecond, intervalMilliseconds: null));

            // sampling rules (remote value overrides local value)
            var samplingRulesJson = settings.CustomSamplingRules;

            // check if the rules are remote or local because they have different JSON schemas
            if (settings.CustomSamplingRulesIsRemote)
            {
                // remote sampling rules
                if (!string.IsNullOrWhiteSpace(samplingRulesJson))
                {
                    var remoteSamplingRules =
                        RemoteCustomSamplingRule.BuildFromConfigurationString(
                            samplingRulesJson,
                            RegexBuilder.DefaultTimeout);

                    sampler.RegisterRules(remoteSamplingRules);
                }
            }
            else
            {
                // local sampling rules
                var patternFormatIsValid = SamplingRulesFormat.IsValid(settings.CustomSamplingRulesFormat, out var samplingRulesFormat);

                if (patternFormatIsValid && !string.IsNullOrWhiteSpace(samplingRulesJson))
                {
                    var localSamplingRules =
                        LocalCustomSamplingRule.BuildFromConfigurationString(
                            samplingRulesJson,
                            samplingRulesFormat,
                            RegexBuilder.DefaultTimeout);

                    sampler.RegisterRules(localSamplingRules);
                }
            }

            // global sampling rate (remote value overrides local value)
            if (settings.GlobalSamplingRate is { } globalSamplingRate)
            {
                if (globalSamplingRate is < 0f or > 1f)
                {
                    Log.Warning(
                        "{ConfigurationKey} configuration of {ConfigurationValue} is out of range",
                        ConfigurationKeys.GlobalSamplingRate,
                        settings.GlobalSamplingRate);
                }
                else
                {
                    sampler.RegisterRule(new GlobalSamplingRateRule((float)globalSamplingRate));
                }
            }

            // AgentSamplingRule handles all sampling rates received from the agent as a single "rule".
            // This rule is always present, even if the agent has not yet provided any sampling rates.
            sampler.RegisterAgentSamplingRule(new AgentSamplingRule());

            return sampler;
        }

        protected virtual ISpanSampler GetSpanSampler(TracerSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.SpanSamplingRules))
            {
                return new SpanSampler([]);
            }

            return new SpanSampler(SpanSamplingRule.BuildFromConfigurationString(settings.SpanSamplingRules, RegexBuilder.DefaultTimeout));
        }

        protected virtual IAgentWriter GetAgentWriter(TracerSettings settings, IDogStatsd statsd, Action<Dictionary<string, float>> updateSampleRates, IDiscoveryService discoveryService)
        {
            var apiRequestFactory = TracesTransportStrategy.Get(settings.Exporter);
            var api = new Api(apiRequestFactory, statsd, updateSampleRates, settings.Exporter.PartialFlushEnabled);

            var statsAggregator = StatsAggregator.Create(api, settings, discoveryService);

            return new AgentWriter(api, statsAggregator, statsd, maxBufferSize: settings.TraceBufferSize, batchInterval: settings.TraceBatchInterval, apmTracingEnabled: settings.ApmTracingEnabledInternal);
        }

        protected virtual IDiscoveryService GetDiscoveryService(TracerSettings settings)
            => DiscoveryService.Create(settings.Exporter);

        internal static IDogStatsd CreateDogStatsdClient(TracerSettings settings, string serviceName, List<string> constantTags, string prefix = null)
        {
            try
            {
                var statsd = new DogStatsdService();
                var config = new StatsdConfig
                {
                    ConstantTags = constantTags?.ToArray(),
                    Prefix = prefix,
                    // note that if these are null, statsd tries to grab them directly from the environment, which could be unsafe
                    ServiceName = NormalizerTraceProcessor.NormalizeService(serviceName),
                    Environment = settings.Environment,
                    ServiceVersion = settings.ServiceVersion,
                    Advanced = { TelemetryFlushInterval = null }
                };

                switch (settings.Exporter.MetricsTransport)
                {
                    case MetricsTransportType.NamedPipe:
                        config.PipeName = settings.Exporter.MetricsPipeName;
                        Log.Information("Using windows named pipes for metrics transport: {PipeName}.", config.PipeName);
                        break;
#if NETCOREAPP3_1_OR_GREATER
                    case MetricsTransportType.UDS:
                        config.StatsdServerName = $"{ExporterSettings.UnixDomainSocketPrefix}{settings.Exporter.MetricsUnixDomainSocketPath}";
                        Log.Information("Using unix domain sockets for metrics transport: {Socket}.", config.StatsdServerName);
                        break;
#endif
                    case MetricsTransportType.UDP:
                    default:
                        config.StatsdServerName = settings.Exporter.MetricsHostname;
                        config.StatsdPort = settings.Exporter.DogStatsdPort;
                        Log.Information<string, int>("Using UDP for metrics transport: {Hostname}:{Port}.", config.StatsdServerName, config.StatsdPort);
                        break;
                }

                statsd.Configure(config);
                return statsd;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unable to instantiate StatsD client.");
                return new NoOpStatsd();
            }
        }

        private static IDogStatsd CreateDogStatsdClient(TracerSettings settings, string serviceName)
        {
            var customTagCount = settings.GlobalTags.Count;
            var constantTags = new List<string>(5 + customTagCount)
            {
                "lang:.NET",
                $"lang_interpreter:{FrameworkDescription.Instance.Name}",
                $"lang_version:{FrameworkDescription.Instance.ProductVersion}",
                $"tracer_version:{TracerConstants.AssemblyVersion}",
                $"{Tags.RuntimeId}:{Tracer.RuntimeId}"
            };

            if (customTagCount > 0)
            {
                var tagProcessor = new TruncatorTagsProcessor();
                foreach (var kvp in settings.GlobalTags)
                {
                    var key = kvp.Key;
                    var value = kvp.Value;
                    tagProcessor.ProcessMeta(ref key, ref value);
                    constantTags.Add($"{key}:{value}");
                }
            }

            return CreateDogStatsdClient(settings, serviceName, constantTags);
        }

        /// <summary>
        /// Gets an "application name" for the executing application by looking at
        /// the hosted app name (.NET Framework on IIS only), assembly name, and process name.
        /// </summary>
        /// <returns>The default service name.</returns>
        private static string GetApplicationName(TracerSettings settings)
        {
            try
            {
                if (settings.IsRunningInAzureAppService)
                {
                    return settings.AzureAppServiceMetadata.SiteName;
                }

                if (settings.LambdaMetadata is { IsRunningInLambda: true, ServiceName: var serviceName })
                {
                    return serviceName;
                }

                try
                {
                    if (TryLoadAspNetSiteName(out var siteName))
                    {
                        return siteName;
                    }
                }
                catch (Exception ex)
                {
                    // Unable to call into System.Web.dll
                    Log.Error(ex, "Unable to get application name through ASP.NET settings");
                }

                return Assembly.GetEntryAssembly()?.GetName().Name ??
                       ProcessHelpers.GetCurrentProcessName();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating default service name.");
                return null;
            }
        }

        private static bool TryLoadAspNetSiteName(out string siteName)
        {
#if NETFRAMEWORK
            // System.Web.dll is only available on .NET Framework
            if (System.Web.Hosting.HostingEnvironment.IsHosted)
            {
                // if this app is an ASP.NET application, return "SiteName/ApplicationVirtualPath".
                // note that ApplicationVirtualPath includes a leading slash.
                siteName = (System.Web.Hosting.HostingEnvironment.SiteName + System.Web.Hosting.HostingEnvironment.ApplicationVirtualPath).TrimEnd('/');
                return true;
            }

#endif
            siteName = default;
            return false;
        }
    }
}
