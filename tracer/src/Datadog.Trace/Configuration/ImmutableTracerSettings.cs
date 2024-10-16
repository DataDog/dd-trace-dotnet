// <copyright file="ImmutableTracerSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.ClrProfiler.ServerlessInstrumentation;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Util;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Contains Tracer settings.
    /// </summary>
    public partial record ImmutableTracerSettings
    {
        private readonly bool _traceEnabled;
        private readonly bool _appsecStandaloneEnabled;
        private readonly DomainMetadata _domainMetadata;
        private readonly bool _isDataStreamsMonitoringEnabled;
        private readonly bool _logsInjectionEnabled;
        private readonly ReadOnlyDictionary<string, string> _headerTags;
        private readonly IReadOnlyDictionary<string, string> _serviceNameMappings;
        private readonly IReadOnlyDictionary<string, string> _peerServiceNameMappings;
        private readonly IReadOnlyDictionary<string, string> _globalTags;
        private readonly double? _globalSamplingRate;
        private readonly bool _runtimeMetricsEnabled;
        private readonly string? _customSamplingRules;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImmutableTracerSettings"/> class
        /// using the specified <see cref="IConfigurationSource"/> to initialize values.
        /// </summary>
        /// <param name="source">The <see cref="IConfigurationSource"/> to use when retrieving configuration values.</param>
        [PublicApi]
        public ImmutableTracerSettings(IConfigurationSource source)
            : this(new TracerSettings(source, new ConfigurationTelemetry(), new OverrideErrorLog()), true)
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.ImmutableTracerSettings_Ctor_Source);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImmutableTracerSettings"/> class from
        /// a TracerSettings instance.
        /// </summary>
        /// <param name="settings">The tracer settings to use to populate the immutable tracer settings</param>
        [PublicApi]
        public ImmutableTracerSettings(TracerSettings settings)
            : this(settings, true)
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.ImmutableTracerSettings_Ctor_Settings);
        }

        internal ImmutableTracerSettings(TracerSettings settings, bool unusedParamNotToUsePublicApi)
        {
            // unused parameter is purely so we can avoid calling public APIs

            // DD_SERVICE has precedence over DD_TAGS
            ServiceNameInternal = GetExplicitSettingOrTag(settings.ServiceNameInternal, settings.GlobalTagsInternal, Tags.Service);

            // DD_ENV has precedence over DD_TAGS
            EnvironmentInternal = GetExplicitSettingOrTag(settings.EnvironmentInternal, settings.GlobalTagsInternal, Tags.Env);

            // DD_VERSION has precedence over DD_TAGS
            ServiceVersionInternal = GetExplicitSettingOrTag(settings.ServiceVersionInternal, settings.GlobalTagsInternal, Tags.Version);

            // DD_GIT_COMMIT_SHA has precedence over DD_TAGS
            GitCommitSha = GetExplicitSettingOrTag(settings.GitCommitSha, settings.GlobalTagsInternal, CommonTags.GitCommit);

            // DD_GIT_REPOSITORY_URL has precedence over DD_TAGS
            GitRepositoryUrl = GetExplicitSettingOrTag(settings.GitRepositoryUrl, settings.GlobalTagsInternal, CommonTags.GitRepository);

            // create dictionary copy without "env", "version", "git.commit.sha" or "git.repository.url" tags
            // these value are used for "Environment" and "ServiceVersion", "GitCommitSha" and "GitRepositoryUrl" properties
            // or overriden with DD_ENV, DD_VERSION, DD_GIT_COMMIT_SHA and DD_GIT_REPOSITORY_URL respectively
            var globalTags = settings.GlobalTagsInternal
                                     .Where(kvp => kvp.Key is not (Tags.Service or Tags.Env or Tags.Version or CommonTags.GitCommit or CommonTags.GitRepository))
                                     .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            _globalTags = new ReadOnlyDictionary<string, string>(globalTags);

            GitMetadataEnabled = settings.GitMetadataEnabled;
            _traceEnabled = settings.TraceEnabledInternal;
            _appsecStandaloneEnabled = settings.AppsecStandaloneEnabledInternal;
            ExporterInternal = new ImmutableExporterSettings(settings.ExporterInternal, true);
#pragma warning disable 618 // App analytics is deprecated, but still used
            AnalyticsEnabledInternal = settings.AnalyticsEnabledInternal;
#pragma warning restore 618
            MaxTracesSubmittedPerSecondInternal = settings.MaxTracesSubmittedPerSecondInternal;
            _customSamplingRules = settings.CustomSamplingRulesInternal;
            CustomSamplingRulesFormat = settings.CustomSamplingRulesFormat;
            SpanSamplingRules = settings.SpanSamplingRules;
            _globalSamplingRate = settings.GlobalSamplingRateInternal;
            IntegrationsInternal = new ImmutableIntegrationSettingsCollection(settings.IntegrationsInternal, settings.DisabledIntegrationNamesInternal);
            _headerTags = new ReadOnlyDictionary<string, string>(settings.HeaderTagsInternal);
            GrpcTagsInternal = new ReadOnlyDictionary<string, string>(settings.GrpcTagsInternal);
            IpHeader = settings.IpHeader;
            IpHeaderEnabled = settings.IpHeaderEnabled;
            TracerMetricsEnabledInternal = settings.TracerMetricsEnabledInternal;
            StatsComputationEnabledInternal = settings.StatsComputationEnabledInternal;
            StatsComputationInterval = settings.StatsComputationInterval;
            _runtimeMetricsEnabled = settings.RuntimeMetricsEnabled;
            KafkaCreateConsumerScopeEnabledInternal = settings.KafkaCreateConsumerScopeEnabledInternal;
            StartupDiagnosticLogEnabledInternal = settings.StartupDiagnosticLogEnabledInternal;
            HttpClientExcludedUrlSubstrings = settings.HttpClientExcludedUrlSubstrings;
            HttpServerErrorStatusCodes = settings.HttpServerErrorStatusCodes;
            HttpClientErrorStatusCodes = settings.HttpClientErrorStatusCodes;
            PeerServiceTagsEnabled = settings.PeerServiceTagsEnabled;
            RemoveClientServiceNamesEnabled = settings.RemoveClientServiceNamesEnabled;
            MetadataSchemaVersion = settings.MetadataSchemaVersion;
            _serviceNameMappings = settings.ServiceNameMappings == null ? new Dictionary<string, string>() : new ReadOnlyDictionary<string, string>(settings.ServiceNameMappings);
            _peerServiceNameMappings = settings.PeerServiceNameMappings == null ? new Dictionary<string, string>() : new ReadOnlyDictionary<string, string>(settings.PeerServiceNameMappings);
            TraceBufferSize = settings.TraceBufferSize;
            TraceBatchInterval = settings.TraceBatchInterval;
            RouteTemplateResourceNamesEnabled = settings.RouteTemplateResourceNamesEnabled;
            DelayWcfInstrumentationEnabled = settings.DelayWcfInstrumentationEnabled;
            WcfWebHttpResourceNamesEnabled = settings.WcfWebHttpResourceNamesEnabled;
            WcfObfuscationEnabled = settings.WcfObfuscationEnabled;
            PropagationStyleInject = settings.PropagationStyleInject;
            PropagationStyleExtract = settings.PropagationStyleExtract;
            PropagationExtractFirstOnly = settings.PropagationExtractFirstOnly;
            TraceMethods = settings.TraceMethods;
            IsActivityListenerEnabled = settings.IsActivityListenerEnabled;

            _isDataStreamsMonitoringEnabled = settings.IsDataStreamsMonitoringEnabled;
            IsRareSamplerEnabled = settings.IsRareSamplerEnabled;

            LogSubmissionSettings = ImmutableDirectLogSubmissionSettings.Create(settings.LogSubmissionSettings);
            _logsInjectionEnabled = settings.LogSubmissionSettings.LogsInjectionEnabled;

            // we cached the static instance here, because is being used in the hotpath
            // by IsIntegrationEnabled method (called from all integrations)
            _domainMetadata = DomainMetadata.Instance;

            ExpandRouteTemplatesEnabled = settings.ExpandRouteTemplatesEnabled || !RouteTemplateResourceNamesEnabled;

            // tag propagation
            OutgoingTagPropagationHeaderMaxLength = settings.OutgoingTagPropagationHeaderMaxLength;

            // query string related env variables
            ObfuscationQueryStringRegex = settings.ObfuscationQueryStringRegex;
            QueryStringReportingEnabled = settings.QueryStringReportingEnabled;
            ObfuscationQueryStringRegexTimeout = settings.ObfuscationQueryStringRegexTimeout;
            QueryStringReportingSize = settings.QueryStringReportingSize;

            IsRunningInAzureAppService = settings.IsRunningInAzureAppService;
            AzureAppServiceMetadata = settings.AzureAppServiceMetadata;

            IsRunningMiniAgentInAzureFunctions = settings.IsRunningMiniAgentInAzureFunctions;

            IsRunningInGCPFunctions = settings.IsRunningInGCPFunctions;
            LambdaMetadata = settings.LambdaMetadata;

            TraceId128BitGenerationEnabled = settings.TraceId128BitGenerationEnabled;
            TraceId128BitLoggingEnabled = settings.TraceId128BitLoggingEnabled;

            CommandsCollectionEnabled = settings.CommandsCollectionEnabled;

            static string? GetExplicitSettingOrTag(string? explicitSetting, IDictionary<string, string> globalTags, string tag)
            {
                if (!string.IsNullOrWhiteSpace(explicitSetting))
                {
                    return explicitSetting!.Trim();
                }
                else
                {
                    var version = globalTags.GetValueOrDefault(tag);
                    return string.IsNullOrWhiteSpace(version) ? null : version.Trim();
                }
            }

            DbmPropagationMode = settings.DbmPropagationMode;
            DisabledAdoNetCommandTypes = settings.DisabledAdoNetCommandTypes;

            // We need to take a snapshot of the config telemetry for the tracer settings,
            // but we can't send it to the static collector, as this settings object may never be "activated"
            Telemetry = new ConfigurationTelemetry();
            settings.CollectTelemetry(Telemetry);

            ErrorLog = settings.ErrorLog.Clone();

            // Record the final disabled settings values in the telemetry, we can't quite get this information
            // through the IntegrationTelemetryCollector currently so record it here instead
            StringBuilder? sb = null;

            foreach (var setting in IntegrationsInternal.Settings)
            {
                if (setting.EnabledInternal == false)
                {
                    sb ??= StringBuilderCache.Acquire(StringBuilderCache.MaxBuilderSize);
                    sb.Append(setting.IntegrationNameInternal);
                    sb.Append(';');
                }
            }

            var value = sb is null ? null : StringBuilderCache.GetStringAndRelease(sb);
            Telemetry.Record(ConfigurationKeys.DisabledIntegrations, value, recordValue: true, ConfigurationOrigins.Calculated);
        }

        /// <summary>
        /// Gets the default environment name applied to all spans.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.Environment"/>
        [GeneratePublicApi(PublicApiUsage.ImmutableTracerSettings_Environment_Get)]
        internal string? EnvironmentInternal { get; }

        /// <summary>
        /// Gets the service name applied to top-level spans and used to build derived service names.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.ServiceName"/>
        [GeneratePublicApi(PublicApiUsage.ImmutableTracerSettings_ServiceName_Get)]
        internal string? ServiceNameInternal { get; }

        /// <summary>
        /// Gets the version tag applied to all spans.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.ServiceVersion"/>
        [GeneratePublicApi(PublicApiUsage.ImmutableTracerSettings_ServiceVersion_Get)]
        internal string? ServiceVersionInternal { get; }

        internal ImmutableDynamicSettings DynamicSettings { get; init; } = new();

        /// <summary>
        /// Gets the application's git repository url.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.GitRepositoryUrl"/>
        internal string? GitRepositoryUrl { get; }

        /// <summary>
        /// Gets the application's git commit hash.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.GitCommitSha"/>
        internal string? GitCommitSha { get; }

        /// <summary>
        /// Gets a value indicating whether we should tag every telemetry event with git metadata.
        /// Default value is true (enabled).
        /// </summary>
        /// <seealso cref="ConfigurationKeys.GitMetadataEnabled"/>
        internal bool GitMetadataEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether tracing is enabled.
        /// Default is <c>true</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.TraceEnabled"/>
        [GeneratePublicApi(PublicApiUsage.ImmutableTracerSettings_TraceEnabled_Get)]
        internal bool TraceEnabledInternal => DynamicSettings.TraceEnabled ?? _traceEnabled;

        /// <summary>
        /// Gets a value indicating whether Appsec standalone billing is enabled.
        /// Default is <c>false</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.AppsecStandaloneEnabled"/>
        internal bool AppsecStandaloneEnabledInternal => DynamicSettings.AppsecStandaloneEnabled ?? _appsecStandaloneEnabled;

        /// <summary>
        /// Gets the exporter settings that dictate how the tracer exports data.
        /// </summary>
        [GeneratePublicApi(PublicApiUsage.ImmutableTracerSettings_Exporter_Get)]
        internal ImmutableExporterSettings ExporterInternal { get; }

#pragma warning disable CS1574 // AnalyticsEnabled is obsolete
        /// <summary>
        /// Gets a value indicating whether default Analytics are enabled.
        /// Settings this value is a shortcut for setting
        /// <see cref="Configuration.IntegrationSettings.AnalyticsEnabled"/> on some predetermined integrations.
        /// See the documentation for more details.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.GlobalAnalyticsEnabled"/>
#pragma warning restore CS1574
        [Obsolete(DeprecationMessages.AppAnalytics)]
        [GeneratePublicApi(PublicApiUsage.ImmutableTracerSettings_AnalyticsEnabled_Get)]
        internal bool AnalyticsEnabledInternal { get; }

        /// <summary>
        /// Gets a value indicating whether correlation identifiers are
        /// automatically injected into the logging context.
        /// Default is <c>false</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.LogsInjectionEnabled"/>
        [GeneratePublicApi(PublicApiUsage.ImmutableTracerSettings_LogsInjectionEnabled_Get)]
        internal bool LogsInjectionEnabledInternal => DynamicSettings.LogsInjectionEnabled ?? _logsInjectionEnabled;

        /// <summary>
        /// Gets a value indicating the maximum number of traces set to AutoKeep (p1) per second.
        /// Default is <c>100</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.TraceRateLimit"/>
        [GeneratePublicApi(PublicApiUsage.ImmutableTracerSettings_MaxTracesSubmittedPerSecond_Get)]
        internal int MaxTracesSubmittedPerSecondInternal { get; }

        /// <summary>
        /// Gets a value indicating custom sampling rules.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.CustomSamplingRules"/>
        [GeneratePublicApi(PublicApiUsage.ImmutableTracerSettings_CustomSamplingRules_Get)]
        internal string? CustomSamplingRulesInternal => DynamicSettings.SamplingRules ?? _customSamplingRules;

        internal bool CustomSamplingRulesIsRemote => DynamicSettings.SamplingRules != null;

        /// <summary>
        /// Gets a value indicating the format for custom sampling rules ("regex" or "glob").
        /// </summary>
        /// <seealso cref="ConfigurationKeys.CustomSamplingRulesFormat"/>
        internal string CustomSamplingRulesFormat { get; }

        /// <summary>
        /// Gets a value indicating the span sampling rules.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.SpanSamplingRules"/>
        internal string? SpanSamplingRules { get; }

        /// <summary>
        /// Gets a value indicating a global rate for sampling.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.GlobalSamplingRate"/>
        [GeneratePublicApi(PublicApiUsage.ImmutableTracerSettings_GlobalSamplingRate_Get)]
        internal double? GlobalSamplingRateInternal => DynamicSettings.GlobalSamplingRate ?? _globalSamplingRate;

        /// <summary>
        /// Gets a collection of <see cref="IntegrationsInternal"/> keyed by integration name.
        /// </summary>
        [GeneratePublicApi(PublicApiUsage.ImmutableTracerSettings_Integrations_Get)]
        internal ImmutableIntegrationSettingsCollection IntegrationsInternal { get; }

        /// <summary>
        /// Gets the global tags, which are applied to all <see cref="Span"/>s.
        /// </summary>
        [GeneratePublicApi(PublicApiUsage.ImmutableTracerSettings_GlobalTags_Get)]
        internal IReadOnlyDictionary<string, string> GlobalTagsInternal => DynamicSettings.GlobalTags ?? _globalTags;

        /// <summary>
        /// Gets the map of header keys to tag names, which are applied to the root <see cref="Span"/>
        /// of incoming and outgoing requests.
        /// </summary>
        [GeneratePublicApi(PublicApiUsage.ImmutableTracerSettings_HeaderTags_Get)]
        internal IReadOnlyDictionary<string, string> HeaderTagsInternal => DynamicSettings.HeaderTags ?? _headerTags;

        /// <summary>
        /// Gets the map of metadata keys to tag names, which are applied to the root <see cref="Span"/>
        /// of incoming and outgoing GRPC requests.
        /// </summary>
        [GeneratePublicApi(PublicApiUsage.ImmutableTracerSettings_GrpcTags_Get)]
        internal IReadOnlyDictionary<string, string> GrpcTagsInternal { get; }

        /// <summary>
        /// Gets a custom request header configured to read the ip from. For backward compatibility, it fallbacks on DD_APPSEC_IPHEADER
        /// </summary>
        internal string? IpHeader { get; }

        /// <summary>
        /// Gets a value indicating whether the ip header should be collected. The default is false.
        /// </summary>
        internal bool IpHeaderEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether internal metrics
        /// are enabled and sent to DogStatsd.
        /// </summary>
        [GeneratePublicApi(PublicApiUsage.ImmutableTracerSettings_TracerMetricsEnabled_Get)]
        internal bool TracerMetricsEnabledInternal { get; }

        /// <summary>
        /// Gets a value indicating whether stats are computed on the tracer side
        /// </summary>
        [GeneratePublicApi(PublicApiUsage.ImmutableTracerSettings_StatsComputationEnabled_Get)]
        internal bool StatsComputationEnabledInternal { get; }

        /// <summary>
        /// Gets a value indicating whether a span context should be created on exiting a successful Kafka
        /// Consumer.Consume() call, and closed on entering Consumer.Consume().
        /// </summary>
        /// <seealso cref="ConfigurationKeys.KafkaCreateConsumerScopeEnabled"/>
        [GeneratePublicApi(PublicApiUsage.ImmutableTracerSettings_KafkaCreateConsumerScopeEnabled_Get)]
        internal bool KafkaCreateConsumerScopeEnabledInternal { get; }

        /// <summary>
        /// Gets a value indicating whether the diagnostic log at startup is enabled
        /// </summary>
        [GeneratePublicApi(PublicApiUsage.ImmutableTracerSettings_StartupDiagnosticLogEnabled_Get)]
        internal bool StartupDiagnosticLogEnabledInternal { get; }

        /// <summary>
        /// Gets a value indicating whether runtime metrics
        /// are enabled and sent to DogStatsd.
        /// </summary>
        internal bool RuntimeMetricsEnabled => DynamicSettings.RuntimeMetricsEnabled ?? _runtimeMetricsEnabled;

        /// <summary>
        /// Gets a value indicating the time interval (in seconds) for sending stats
        /// </summary>
        internal int StatsComputationInterval { get; }

        /// <summary>
        /// Gets the comma separated list of url patterns to skip tracing.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.HttpClientExcludedUrlSubstrings"/>
        internal string[] HttpClientExcludedUrlSubstrings { get; }

        /// <summary>
        /// Gets the HTTP status code that should be marked as errors for server integrations.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.HttpServerErrorStatusCodes"/>
        internal bool[] HttpServerErrorStatusCodes { get; }

        /// <summary>
        /// Gets the HTTP status code that should be marked as errors for client integrations.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.HttpClientErrorStatusCodes"/>
        internal bool[] HttpClientErrorStatusCodes { get; }

        /// <summary>
        /// Gets configuration values for changing service names based on configuration
        /// </summary>
        internal IReadOnlyDictionary<string, string> ServiceNameMappings => DynamicSettings.ServiceNameMappings ?? _serviceNameMappings;

        /// <summary>
        /// Gets a value indicating the size in bytes of the trace buffer
        /// </summary>
        internal int TraceBufferSize { get; }

        /// <summary>
        /// Gets a value indicating the batch interval for the serialization queue, in milliseconds
        /// </summary>
        internal int TraceBatchInterval { get; }

        /// <summary>
        /// Gets a value indicating whether the feature flag to enable the updated ASP.NET resource names is enabled
        /// </summary>
        /// <seealso cref="ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled"/>
        internal bool RouteTemplateResourceNamesEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether route parameters in ASP.NET and ASP.NET Core resource names
        /// should be expanded with their values. Only applies when  <see cref="RouteTemplateResourceNamesEnabled"/>
        /// is enabled.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.ExpandRouteTemplatesEnabled"/>
        internal bool ExpandRouteTemplatesEnabled { get; }

        /// <summary>
        /// Gets a value indicating the regex to apply to obfuscate http query strings.
        ///  WARNING: This regex cause crashes under netcoreapp2.1 / linux / arm64, dont use on manual instrumentation in this environment
        /// </summary>
        /// <seealso cref="ConfigurationKeys.ObfuscationQueryStringRegex"/>
        internal string ObfuscationQueryStringRegex { get; }

        /// <summary>
        /// Gets a value indicating whether or not http.url should contain the query string, enabled by default with DD_HTTP_SERVER_TAG_QUERY_STRING
        /// </summary>
        internal bool QueryStringReportingEnabled { get; }

        /// <summary>
        /// Gets a value indicating a timeout in milliseconds to the execution of the query string obfuscation regex
        /// Default value is 200ms
        /// </summary>
        internal double ObfuscationQueryStringRegexTimeout { get; }

        /// <summary>
        /// Gets a value limiting the size of the querystring to report and obfuscate
        /// Default value is 5000, 0 means that we don't limit the size.
        /// </summary>
        internal int QueryStringReportingSize { get; }

        internal ImmutableDirectLogSubmissionSettings LogSubmissionSettings { get; }

        /// <summary>
        /// Gets a value indicating whether to enable the updated WCF instrumentation that delays execution
        /// until later in the WCF pipeline when the WCF server exception handling is established.
        /// </summary>
        internal bool DelayWcfInstrumentationEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether to enable improved template-based resource names
        /// when using WCF Web HTTP.
        /// </summary>
        internal bool WcfWebHttpResourceNamesEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether to obfuscate the <c>LocalPath</c> of a WCF request that goes
        /// into the <c>resourceName</c> of a span.
        /// </summary>
        internal bool WcfObfuscationEnabled { get; }

        /// <summary>
        /// Gets a value indicating the injection propagation style.
        /// </summary>
        internal string[] PropagationStyleInject { get; }

        /// <summary>
        /// Gets a value indicating the extraction propagation style.
        /// </summary>
        internal string[] PropagationStyleExtract { get; }

        /// <summary>
        /// Gets a value indicating whether the propagation should only try
        /// extract the first header.
        /// </summary>
        internal bool PropagationExtractFirstOnly { get; }

        /// <summary>
        /// Gets a value indicating the trace methods configuration.
        /// </summary>
        internal string TraceMethods { get; }

        /// <summary>
        /// Gets a value indicating whether the activity listener is enabled or not.
        /// </summary>
        internal bool IsActivityListenerEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether data streams monitoring is enabled or not.
        /// </summary>
        internal bool IsDataStreamsMonitoringEnabled => DynamicSettings.DataStreamsMonitoringEnabled ?? _isDataStreamsMonitoringEnabled;

        /// <summary>
        /// Gets the maximum length of an outgoing propagation header's value ("x-datadog-tags")
        /// when injecting it into downstream service calls.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.TagPropagation.HeaderMaxLength"/>
        /// <remarks>
        /// This value is not used when extracting an incoming propagation header from an upstream service.
        /// </remarks>
        internal int OutgoingTagPropagationHeaderMaxLength { get; }

        /// <summary>
        /// Gets a value indicating whether the rare sampler is enabled
        /// </summary>
        internal bool IsRareSamplerEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether the tracer is running in AAS
        /// </summary>
        internal bool IsRunningInAzureAppService { get; }

        /// <summary>
        /// Gets a value indicating whether the tracer is running in Azure Functions
        /// on a consumption plan
        /// </summary>
        internal bool IsRunningMiniAgentInAzureFunctions { get; }

        /// <summary>
        /// Gets a value indicating whether the tracer is running in Google Cloud Functions
        /// </summary>
        internal bool IsRunningInGCPFunctions { get; }

        /// <summary>
        /// Gets the AWS Lambda settings, including whether we're currently running in Lambda
        /// </summary>
        internal LambdaMetadata LambdaMetadata { get; }

        /// <summary>
        /// Gets a value indicating whether the tracer should propagate service data in db queries
        /// </summary>
        internal DbmPropagationLevel DbmPropagationMode { get; }

        /// <summary>
        /// Gets a value indicating whether the tracer will generate 128-bit trace ids
        /// instead of 64-bits trace ids.
        /// </summary>
        internal bool TraceId128BitGenerationEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether the tracer will inject 128-bit trace ids into logs, if available,
        /// instead of 64-bit trace ids. Note that a 128-bit trace id may be received from an upstream service
        /// even if we are not generating them.
        /// </summary>
        internal bool TraceId128BitLoggingEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether the tracer will send the shell commands of
        /// the "command_execution" integration to the agent.
        /// </summary>
        internal bool CommandsCollectionEnabled { get; }

        /// <summary>
        /// Gets the AAS settings. Guaranteed not <c>null</c> when <see cref="IsRunningInAzureAppService"/> is not <c>null</c>
        /// </summary>
        internal ImmutableAzureAppServiceSettings? AzureAppServiceMetadata { get; }

        /// <summary>
        /// Gets the GCP Function settings
        /// </summary>
        internal ImmutableGCPFunctionSettings? GCPFunctionSettings { get; }

        /// <summary>
        /// Gets a value indicating whether to calculate the peer.service tag from predefined precursor attributes when using the v0 schema.
        /// </summary>
        internal bool PeerServiceTagsEnabled { get; }

        /// <summary>
        /// Gets configuration values for changing service names based on configuration
        /// </summary>
        internal IReadOnlyDictionary<string, string> PeerServiceNameMappings => _peerServiceNameMappings;

        /// <summary>
        /// Gets a value indicating whether to remove the service names when using the v0 schema.
        /// </summary>
        internal bool RemoveClientServiceNamesEnabled { get; }

        /// <summary>
        /// Gets the metadata schema version
        /// </summary>
        internal SchemaVersion MetadataSchemaVersion { get; }

        /// <summary>
        /// Gets the telemetry that was collected from <see cref="TracerSettings"/> when this instance was built
        /// </summary>
        internal IConfigurationTelemetry Telemetry { get; }

        /// <summary>
        /// Gets the error logs that were collected from <see cref="TracerSettings"/> when this instance was built
        /// </summary>
        internal OverrideErrorLog ErrorLog { get; }

        /// <summary>
        /// Gets a value indicating whether remote configuration is potentially available.
        /// RCM requires the "full" agent (not just the trace agent), so is not available in some scenarios
        /// </summary>
        internal bool IsRemoteConfigurationAvailable =>
            !(IsRunningInAzureAppService
           || IsRunningMiniAgentInAzureFunctions
           || IsRunningInGCPFunctions
           || LambdaMetadata.IsRunningInLambda);

        /// <summary>
        /// Gets the disabled ADO.NET Command Types that won't have spans generated for them.
        /// </summary>
        internal HashSet<string> DisabledAdoNetCommandTypes { get; }

        /// <summary>
        /// Create a <see cref="ImmutableTracerSettings"/> populated from the default sources
        /// returned by <see cref="GlobalConfigurationSource.Instance"/>.
        /// </summary>
        /// <returns>A <see cref="ImmutableTracerSettings"/> populated from the default sources.</returns>
        [PublicApi]
        public static ImmutableTracerSettings FromDefaultSources()
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.ImmutableTracerSettings_FromDefaultSources);
            return new ImmutableTracerSettings(TracerSettings.FromDefaultSourcesInternal(), true);
        }

        // Overriding the default "record" behaviour here
        // This type _shouldn't_ be treated as a record generally, we only made it a record
        // so we could use with {} expressions, but these access public properties by default
        // (rather than only the internal ones)

        /// <inheritdoc />
        // ReSharper disable once BaseObjectGetHashCodeCallInGetHashCode
        public override int GetHashCode() => base.GetHashCode();

        /// <inheritdoc />
        // ReSharper disable once BaseObjectEqualsIsObjectEquals
        public virtual bool Equals(ImmutableTracerSettings? other) => base.Equals(other);

        /// <inheritdoc />
        public override string? ToString()
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.ImmutableTracerSettings_ToString);
            return base.ToString();
        }

        internal bool IsErrorStatusCode(int statusCode, bool serverStatusCode)
        {
            var source = serverStatusCode ? HttpServerErrorStatusCodes : HttpClientErrorStatusCodes;

            if (source == null)
            {
                return false;
            }

            if (statusCode >= source.Length)
            {
                return false;
            }

            return source[statusCode];
        }

        internal bool IsIntegrationEnabled(IntegrationId integration, bool defaultValue = true)
        {
            if (TraceEnabledInternal && !_domainMetadata.ShouldAvoidAppDomain())
            {
                return IntegrationsInternal[integration].EnabledInternal ?? defaultValue;
            }

            return false;
        }

        [Obsolete(DeprecationMessages.AppAnalytics)]
        internal double? GetIntegrationAnalyticsSampleRate(IntegrationId integration, bool enabledWithGlobalSetting)
        {
            var integrationSettings = IntegrationsInternal[integration];
            var analyticsEnabled = integrationSettings.AnalyticsEnabledInternal ?? (enabledWithGlobalSetting && AnalyticsEnabledInternal);
            return analyticsEnabled ? integrationSettings.AnalyticsSampleRateInternal : (double?)null;
        }
    }
}
