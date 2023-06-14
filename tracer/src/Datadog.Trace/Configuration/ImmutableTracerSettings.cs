// <copyright file="ImmutableTracerSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.Util;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Contains Tracer settings.
    /// </summary>
    public class ImmutableTracerSettings
    {
        private readonly DomainMetadata _domainMetadata;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImmutableTracerSettings"/> class
        /// using the specified <see cref="IConfigurationSource"/> to initialize values.
        /// </summary>
        /// <param name="source">The <see cref="IConfigurationSource"/> to use when retrieving configuration values.</param>
        public ImmutableTracerSettings(IConfigurationSource source)
            : this(new TracerSettings(source))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImmutableTracerSettings"/> class from
        /// a TracerSettings instance.
        /// </summary>
        /// <param name="settings">The tracer settings to use to populate the immutable tracer settings</param>
        public ImmutableTracerSettings(TracerSettings settings)
        {
            // DD_ENV has precedence over DD_TAGS
            Environment = GetExplicitSettingOrTag(settings.EnvironmentInternal, settings.GlobalTagsInternal, Tags.Env);

            // DD_VERSION has precedence over DD_TAGS
            ServiceVersion = GetExplicitSettingOrTag(settings.ServiceVersionInternal, settings.GlobalTagsInternal, Tags.Version);

            // DD_GIT_COMMIT_SHA has precedence over DD_TAGS
            GitCommitSha = GetExplicitSettingOrTag(settings.GitCommitSha, settings.GlobalTagsInternal, CommonTags.GitCommit);

            // DD_GIT_REPOSITORY_URL has precedence over DD_TAGS
            GitRepositoryUrl = GetExplicitSettingOrTag(settings.GitRepositoryUrl, settings.GlobalTagsInternal, CommonTags.GitRepository);

            // create dictionary copy without "env", "version", "git.commit.sha" or "git.repository.url" tags
            // these value are used for "Environment" and "ServiceVersion", "GitCommitSha" and "GitRepositoryUrl" properties
            // or overriden with DD_ENV, DD_VERSION, DD_GIT_COMMIT_SHA and DD_GIT_REPOSITORY_URL respectively
            var globalTags = settings.GlobalTagsInternal
                                     .Where(kvp => kvp.Key is not (Tags.Env or Tags.Version or CommonTags.GitCommit or CommonTags.GitRepository))
                                     .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            GlobalTags = new ReadOnlyDictionary<string, string>(globalTags);

            GitMetadataEnabled = settings.GitMetadataEnabled;
            ServiceName = settings.ServiceNameInternal;
            TraceEnabled = settings.TraceEnabledInternal;
            Exporter = new ImmutableExporterSettings(settings.ExporterInternal);
#pragma warning disable 618 // App analytics is deprecated, but still used
            AnalyticsEnabled = settings.AnalyticsEnabledInternal;
#pragma warning restore 618
            MaxTracesSubmittedPerSecond = settings.MaxTracesSubmittedPerSecondInternal;
            CustomSamplingRules = settings.CustomSamplingRulesInternal;
            SpanSamplingRules = settings.SpanSamplingRules;
            GlobalSamplingRate = settings.GlobalSamplingRateInternal;
            Integrations = new ImmutableIntegrationSettingsCollection(settings.IntegrationsInternal, settings.DisabledIntegrationNamesInternal);
            HeaderTags = new ReadOnlyDictionary<string, string>(settings.HeaderTagsInternal);
            GrpcTags = new ReadOnlyDictionary<string, string>(settings.GrpcTagsInternal);
            IpHeader = settings.IpHeader;
            IpHeaderEnabled = settings.IpHeaderEnabled;
            TracerMetricsEnabled = settings.TracerMetricsEnabledInternal;
            StatsComputationEnabled = settings.StatsComputationEnabledInternal;
            StatsComputationInterval = settings.StatsComputationInterval;
            RuntimeMetricsEnabled = settings.RuntimeMetricsEnabled;
            KafkaCreateConsumerScopeEnabled = settings.KafkaCreateConsumerScopeEnabledInternal;
            StartupDiagnosticLogEnabled = settings.StartupDiagnosticLogEnabledInternal;
            HttpClientExcludedUrlSubstrings = settings.HttpClientExcludedUrlSubstrings;
            HttpServerErrorStatusCodes = settings.HttpServerErrorStatusCodes;
            HttpClientErrorStatusCodes = settings.HttpClientErrorStatusCodes;
            PeerServiceTagsEnabled = settings.PeerServiceTagsEnabled;
            RemoveClientServiceNamesEnabled = settings.RemoveClientServiceNamesEnabled;
            MetadataSchemaVersion = settings.MetadataSchemaVersion;
            ServiceNameMappings = settings.ServiceNameMappings ?? new Dictionary<string, string>();
            TraceBufferSize = settings.TraceBufferSize;
            TraceBatchInterval = settings.TraceBatchInterval;
            RouteTemplateResourceNamesEnabled = settings.RouteTemplateResourceNamesEnabled;
            DelayWcfInstrumentationEnabled = settings.DelayWcfInstrumentationEnabled;
            WcfObfuscationEnabled = settings.WcfObfuscationEnabled;
            PropagationStyleInject = settings.PropagationStyleInject;
            PropagationStyleExtract = settings.PropagationStyleExtract;
            TraceMethods = settings.TraceMethods;
            IsActivityListenerEnabled = settings.IsActivityListenerEnabled;
            IsDataStreamsMonitoringEnabled = settings.IsDataStreamsMonitoringEnabled;
            IsRareSamplerEnabled = settings.IsRareSamplerEnabled;

            LogSubmissionSettings = ImmutableDirectLogSubmissionSettings.Create(settings.LogSubmissionSettings);
            // Logs injection is enabled by default if direct log submission is enabled, otherwise disabled by default
            LogsInjectionEnabled = settings.LogSubmissionSettings.LogsInjectionEnabled ?? LogSubmissionSettings.IsEnabled;

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

            IsRunningInAzureFunctions = settings.IsRunningInAzureFunctions;

            IsRunningInGCPFunctions = settings.IsRunningInGCPFunctions;

            TraceId128BitGenerationEnabled = settings.TraceId128BitGenerationEnabled;
            TraceId128BitLoggingEnabled = settings.TraceId128BitLoggingEnabled;

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

            // We need to take a snapshot of the config telemetry for the tracer settings,
            // but we can't send it to the static collector, as this settings object may never be "activated"
            Telemetry = new ConfigurationTelemetry();
            settings.CollectTelemetry(Telemetry);
        }

        /// <summary>
        /// Gets the default environment name applied to all spans.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.Environment"/>
        public string? Environment { get; }

        /// <summary>
        /// Gets the service name applied to top-level spans and used to build derived service names.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.ServiceName"/>
        public string? ServiceName { get; }

        /// <summary>
        /// Gets the version tag applied to all spans.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.ServiceVersion"/>
        public string? ServiceVersion { get; }

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
        /// Defaul value is true (enabled).
        /// </summary>
        /// <seealso cref="ConfigurationKeys.GitMetadataEnabled"/>
        internal bool GitMetadataEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether tracing is enabled.
        /// Default is <c>true</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.TraceEnabled"/>
        public bool TraceEnabled { get; }

        /// <summary>
        /// Gets the exporter settings that dictate how the tracer exports data.
        /// </summary>
        public ImmutableExporterSettings Exporter { get; }

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
        public bool AnalyticsEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether correlation identifiers are
        /// automatically injected into the logging context.
        /// Default is <c>false</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.LogsInjectionEnabled"/>
        public bool LogsInjectionEnabled { get; }

        /// <summary>
        /// Gets a value indicating the maximum number of traces set to AutoKeep (p1) per second.
        /// Default is <c>100</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.TraceRateLimit"/>
        public int MaxTracesSubmittedPerSecond { get; }

        /// <summary>
        /// Gets a value indicating custom sampling rules.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.CustomSamplingRules"/>
        public string? CustomSamplingRules { get; }

        /// <summary>
        /// Gets a value indicating the span sampling rules.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.SpanSamplingRules"/>
        internal string? SpanSamplingRules { get; }

        /// <summary>
        /// Gets a value indicating a global rate for sampling.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.GlobalSamplingRate"/>
        public double? GlobalSamplingRate { get; }

        /// <summary>
        /// Gets a collection of <see cref="Integrations"/> keyed by integration name.
        /// </summary>
        public ImmutableIntegrationSettingsCollection Integrations { get; }

        /// <summary>
        /// Gets the global tags, which are applied to all <see cref="Span"/>s.
        /// </summary>
        public IReadOnlyDictionary<string, string> GlobalTags { get; }

        /// <summary>
        /// Gets the map of header keys to tag names, which are applied to the root <see cref="Span"/>
        /// of incoming and outgoing requests.
        /// </summary>
        public IReadOnlyDictionary<string, string> HeaderTags { get; }

        /// <summary>
        /// Gets the map of metadata keys to tag names, which are applied to the root <see cref="Span"/>
        /// of incoming and outgoing GRPC requests.
        /// </summary>
        public IReadOnlyDictionary<string, string> GrpcTags { get; }

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
        public bool TracerMetricsEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether stats are computed on the tracer side
        /// </summary>
        public bool StatsComputationEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether a span context should be created on exiting a successful Kafka
        /// Consumer.Consume() call, and closed on entering Consumer.Consume().
        /// </summary>
        /// <seealso cref="ConfigurationKeys.KafkaCreateConsumerScopeEnabled"/>
        public bool KafkaCreateConsumerScopeEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether the diagnostic log at startup is enabled
        /// </summary>
        public bool StartupDiagnosticLogEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether runtime metrics
        /// are enabled and sent to DogStatsd.
        /// </summary>
        internal bool RuntimeMetricsEnabled { get; }

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
        internal IDictionary<string, string> ServiceNameMappings { get; }

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
        internal bool IsDataStreamsMonitoringEnabled { get; }

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
        /// Gets a value indicating whether the tracer is running in AAS
        /// </summary>
        internal bool IsRunningInAzureFunctions { get; }

        /// <summary>
        /// Gets a value indicating whether the tracer is running in Google Cloud Functions
        /// </summary>
        internal bool IsRunningInGCPFunctions { get; }

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
        /// Gets the AAS settings. Guaranteed not <c>null</c> when <see cref="IsRunningInAzureAppService"/> is not <c>null</c>
        /// </summary>
        internal ImmutableAzureAppServiceSettings? AzureAppServiceMetadata { get; }

        /// <summary>
        /// Gets a value indicating whether to calculate the peer.service tag from predefined precursor attributes when using the v0 schema.
        /// </summary>
        internal bool PeerServiceTagsEnabled { get; }

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
        /// Create a <see cref="ImmutableTracerSettings"/> populated from the default sources
        /// returned by <see cref="GlobalConfigurationSource.Instance"/>.
        /// </summary>
        /// <returns>A <see cref="ImmutableTracerSettings"/> populated from the default sources.</returns>
        public static ImmutableTracerSettings FromDefaultSources()
        {
            return new ImmutableTracerSettings(GlobalConfigurationSource.Instance);
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
            if (TraceEnabled && !_domainMetadata.ShouldAvoidAppDomain())
            {
                return Integrations[integration].EnabledInternal ?? defaultValue;
            }

            return false;
        }

        [Obsolete(DeprecationMessages.AppAnalytics)]
        internal double? GetIntegrationAnalyticsSampleRate(IntegrationId integration, bool enabledWithGlobalSetting)
        {
            var integrationSettings = Integrations[integration];
            var analyticsEnabled = integrationSettings.AnalyticsEnabledInternal ?? (enabledWithGlobalSetting && AnalyticsEnabled);
            return analyticsEnabled ? integrationSettings.AnalyticsSampleRateInternal : (double?)null;
        }
    }
}
