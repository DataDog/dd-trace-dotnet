// <copyright file="TracerSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Datadog.Trace.ClrProfiler.ServerlessInstrumentation;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.Propagators;
using Datadog.Trace.Vendors.Serilog;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Contains Tracer settings.
    /// </summary>
    public class TracerSettings
    {
        /// <summary>
        /// Default obfuscation query string regex if none specified via env DD_OBFUSCATION_QUERY_STRING_REGEXP
        /// </summary>
        internal const string DefaultObfuscationQueryStringRegex = @"((?i)(?:p(?:ass)?w(?:or)?d|pass(?:_?phrase)?|secret|(?:api_?|private_?|public_?|access_?|secret_?)key(?:_?id)?|token|consumer_?(?:id|key|secret)|sign(?:ed|ature)?|auth(?:entication|orization)?)(?:(?:\s|%20)*(?:=|%3D)[^&]+|(?:""|%22)(?:\s|%20)*(?::|%3A)(?:\s|%20)*(?:""|%22)(?:%2[^2]|%[^2]|[^""%])+(?:""|%22))|bearer(?:\s|%20)+[a-z0-9\._\-]|token(?::|%3A)[a-z0-9]{13}|gh[opsu]_[0-9a-zA-Z]{36}|ey[I-L](?:[\w=-]|%3D)+\.ey[I-L](?:[\w=-]|%3D)+(?:\.(?:[\w.+\/=-]|%3D|%2F|%2B)+)?|[\-]{5}BEGIN(?:[a-z\s]|%20)+PRIVATE(?:\s|%20)KEY[\-]{5}[^\-]+[\-]{5}END(?:[a-z\s]|%20)+PRIVATE(?:\s|%20)KEY|ssh-rsa(?:\s|%20)*(?:[a-z0-9\/\.+]|%2F|%5C|%2B){100,})";

        /// <summary>
        /// Initializes a new instance of the <see cref="TracerSettings"/> class with default values.
        /// </summary>
        public TracerSettings()
            : this(null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TracerSettings"/> class with default values,
        /// or initializes the configuration from environment variables and configuration files.
        /// Calling <c>new TracerSettings(true)</c> is equivalent to calling <c>TracerSettings.FromDefaultSources()</c>
        /// </summary>
        /// <param name="useDefaultSources">If <c>true</c>, creates a <see cref="TracerSettings"/> populated from
        /// the default sources such as environment variables etc. If <c>false</c>, uses the default values.</param>
        public TracerSettings(bool useDefaultSources)
            : this(useDefaultSources ? GlobalConfigurationSource.Instance : null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TracerSettings"/> class
        /// using the specified <see cref="IConfigurationSource"/> to initialize values.
        /// </summary>
        /// <param name="source">The <see cref="IConfigurationSource"/> to use when retrieving configuration values.</param>
        public TracerSettings(IConfigurationSource? source)
        {
            var commaSeparator = new[] { ',' };
            source ??= NullConfigurationSource.Instance;

            Environment = source.GetString(ConfigurationKeys.Environment);

            ServiceName = source.GetString(ConfigurationKeys.ServiceName) ??
                          // backwards compatibility for names used in the past
                          source.GetString("DD_SERVICE_NAME");

            ServiceVersion = source.GetString(ConfigurationKeys.ServiceVersion);

            GitCommitSha = source.GetString(ConfigurationKeys.GitCommitSha);

            GitRepositoryUrl = source.GetString(ConfigurationKeys.GitRepositoryUrl);

            GitMetadataEnabled = source.GetBool(ConfigurationKeys.GitMetadataEnabled) ?? true;

            TraceEnabled = source.GetBool(ConfigurationKeys.TraceEnabled) ??
                           // default value
                           true;

            var disabledIntegrationNames = source.GetString(ConfigurationKeys.DisabledIntegrations)
                                                 ?.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries) ??
                                           Enumerable.Empty<string>();

            DisabledIntegrationNames = new HashSet<string>(disabledIntegrationNames, StringComparer.OrdinalIgnoreCase);

            Integrations = new IntegrationSettingsCollection(source);

            Exporter = new ExporterSettings(source);

#pragma warning disable 618 // App analytics is deprecated, but still used
            AnalyticsEnabled = source.GetBool(ConfigurationKeys.GlobalAnalyticsEnabled) ??
                               // default value
                               false;
#pragma warning restore 618

            MaxTracesSubmittedPerSecond = source.GetInt32(ConfigurationKeys.TraceRateLimit) ??
#pragma warning disable 618 // this parameter has been replaced but may still be used
                                          source.GetInt32(ConfigurationKeys.MaxTracesSubmittedPerSecond) ??
#pragma warning restore 618
                                          // default value
                                          100;

            GlobalTags = source.GetDictionary(ConfigurationKeys.GlobalTags) ??
                         // backwards compatibility for names used in the past
                         source.GetDictionary("DD_TRACE_GLOBAL_TAGS") ??
                         // default value (empty)
                         new ConcurrentDictionary<string, string>();

            // Filter out tags with empty keys or empty values, and trim whitespace
            GlobalTags = GlobalTags.Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key) && !string.IsNullOrWhiteSpace(kvp.Value))
                                   .ToDictionary(kvp => kvp.Key.Trim(), kvp => kvp.Value.Trim());

            var inputHeaderTags = source.GetDictionary(ConfigurationKeys.HeaderTags, allowOptionalMappings: true) ??
                                  // default value (empty)
                                  new Dictionary<string, string>();

            var headerTagsNormalizationFixEnabled = source.GetBool(ConfigurationKeys.FeatureFlags.HeaderTagsNormalizationFixEnabled) ?? true;
            // Filter out tags with empty keys or empty values, and trim whitespaces
            HeaderTags = InitializeHeaderTags(inputHeaderTags, headerTagsNormalizationFixEnabled);
            MetadataSchemaVersion = source.GetString(ConfigurationKeys.MetadataSchemaVersion) ?? "v0";

            var serviceNameMappings = source.GetDictionary(ConfigurationKeys.ServiceNameMappings)
                                            ?.Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key) && !string.IsNullOrWhiteSpace(kvp.Value))
                                            ?.ToDictionary(kvp => kvp.Key.Trim(), kvp => kvp.Value.Trim());

            ServiceNameMappings = new ServiceNames(serviceNameMappings);

            TracerMetricsEnabled = source.GetBool(ConfigurationKeys.TracerMetricsEnabled) ??
                                   // default value
                                   false;

            StatsComputationEnabled = source.GetBool(ConfigurationKeys.StatsComputationEnabled) ?? false;

            StatsComputationInterval = source.GetInt32(ConfigurationKeys.StatsComputationInterval) ?? 10;

            RuntimeMetricsEnabled = source.GetBool(ConfigurationKeys.RuntimeMetricsEnabled) ??
                                    false;

            CustomSamplingRules = source.GetString(ConfigurationKeys.CustomSamplingRules);

            SpanSamplingRules = source.GetString(ConfigurationKeys.SpanSamplingRules);

            GlobalSamplingRate = source.GetDouble(ConfigurationKeys.GlobalSamplingRate);

            StartupDiagnosticLogEnabled = source.GetBool(ConfigurationKeys.StartupDiagnosticLogEnabled) ??
                                          // default value
                                          true;

            var httpServerErrorStatusCodes = source.GetString(ConfigurationKeys.HttpServerErrorStatusCodes) ??
                                             // Default value
                                             "500-599";

            HttpServerErrorStatusCodes = ParseHttpCodesToArray(httpServerErrorStatusCodes);

            var httpClientErrorStatusCodes = source.GetString(ConfigurationKeys.HttpClientErrorStatusCodes) ??
                                             // Default value
                                             "400-499";
            HttpClientErrorStatusCodes = ParseHttpCodesToArray(httpClientErrorStatusCodes);

            TraceBufferSize = source.GetInt32(ConfigurationKeys.BufferSize)
                           ?? 1024 * 1024 * 10; // 10MB

            TraceBatchInterval = source.GetInt32(ConfigurationKeys.SerializationBatchInterval)
                              ?? 100;

            RouteTemplateResourceNamesEnabled = source.GetBool(ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled)
                                             ?? true;

            ExpandRouteTemplatesEnabled = source.GetBool(ConfigurationKeys.ExpandRouteTemplatesEnabled)
                                          // disabled by default if route template resource names enabled
                                       ?? !RouteTemplateResourceNamesEnabled;

            KafkaCreateConsumerScopeEnabled = source.GetBool(ConfigurationKeys.KafkaCreateConsumerScopeEnabled)
                                           ?? true; // default

            DelayWcfInstrumentationEnabled = source.GetBool(ConfigurationKeys.FeatureFlags.DelayWcfInstrumentationEnabled)
                                          ?? false;

            WcfObfuscationEnabled = source.GetBool(ConfigurationKeys.FeatureFlags.WcfObfuscationEnabled)
                                 ?? true; // default value

            ObfuscationQueryStringRegex = source.GetString(ConfigurationKeys.ObfuscationQueryStringRegex) ?? DefaultObfuscationQueryStringRegex;

            QueryStringReportingEnabled = source.GetBool(ConfigurationKeys.QueryStringReportingEnabled) ?? true;

            QueryStringReportingSize = source.GetInt32(ConfigurationKeys.QueryStringReportingSize) ?? 5000; // 5000 being the tag value length limit

            ObfuscationQueryStringRegexTimeout = source.GetDouble(ConfigurationKeys.ObfuscationQueryStringRegexTimeout) is { } x and > 0 ? x : 200;

            IsActivityListenerEnabled = source.GetBool(ConfigurationKeys.FeatureFlags.OpenTelemetryEnabled) ??
                                        source.GetBool("DD_TRACE_ACTIVITY_LISTENER_ENABLED") ??
                                        // default value
                                        false;

            var propagationStyleInject = source.GetString(ConfigurationKeys.PropagationStyleInject) ??
                                         source.GetString("DD_PROPAGATION_STYLE_INJECT") ?? // deprecated setting name
                                         source.GetString(ConfigurationKeys.PropagationStyle);

            PropagationStyleInject = TrimSplitString(propagationStyleInject, commaSeparator);

            if (PropagationStyleInject.Length == 0)
            {
                // default value
                PropagationStyleInject = new[]
                                         {
                                             ContextPropagationHeaderStyle.W3CTraceContext,
                                             ContextPropagationHeaderStyle.Datadog
                                         };
            }

            var propagationStyleExtract = source.GetString(ConfigurationKeys.PropagationStyleExtract) ??
                                          source.GetString("DD_PROPAGATION_STYLE_EXTRACT") ?? // deprecated setting name
                                          source.GetString(ConfigurationKeys.PropagationStyle);

            PropagationStyleExtract = TrimSplitString(propagationStyleExtract, commaSeparator);

            if (PropagationStyleExtract.Length == 0)
            {
                // default value
                PropagationStyleExtract = new[]
                                          {
                                              ContextPropagationHeaderStyle.W3CTraceContext,
                                              ContextPropagationHeaderStyle.Datadog
                                          };
            }

            // If Activity support is enabled, we must enable the W3C Trace Context propagators.
            // It's ok to include W3C multiple times, we handle that later.
            if (IsActivityListenerEnabled)
            {
                PropagationStyleInject = PropagationStyleInject.Concat(ContextPropagationHeaderStyle.W3CTraceContext);
                PropagationStyleExtract = PropagationStyleExtract.Concat(ContextPropagationHeaderStyle.W3CTraceContext);
            }
            else
            {
                DisabledIntegrationNames.Add(nameof(Configuration.IntegrationId.OpenTelemetry));
            }

            LogSubmissionSettings = new DirectLogSubmissionSettings(source);

            TraceMethods = source.GetString(ConfigurationKeys.TraceMethods) ??
                           // Default value
                           string.Empty;

            var grpcTags = source.GetDictionary(ConfigurationKeys.GrpcTags, allowOptionalMappings: true) ??
                           // default value (empty)
                           new Dictionary<string, string>();

            // Filter out tags with empty keys or empty values, and trim whitespaces
            GrpcTags = InitializeHeaderTags(grpcTags, headerTagsNormalizationFixEnabled: true);

            var outgoingTagPropagationHeaderMaxLength = source.GetInt32(ConfigurationKeys.TagPropagation.HeaderMaxLength);

            OutgoingTagPropagationHeaderMaxLength = outgoingTagPropagationHeaderMaxLength is >= 0 and <= Tagging.TagPropagation.OutgoingTagPropagationHeaderMaxLength ? (int)outgoingTagPropagationHeaderMaxLength : Tagging.TagPropagation.OutgoingTagPropagationHeaderMaxLength;

            IpHeader = source.GetString(ConfigurationKeys.IpHeader) ?? source.GetString(ConfigurationKeys.AppSec.CustomIpHeader);

            IpHeaderEnabled = source.GetBool(ConfigurationKeys.IpHeaderEnabled) ?? false;

            IsDataStreamsMonitoringEnabled = source.GetBool(ConfigurationKeys.DataStreamsMonitoring.Enabled) ??
                                             // default value
                                             false;

            IsRareSamplerEnabled = source.GetBool(ConfigurationKeys.RareSamplerEnabled) ?? false;

            IsRunningInAzureAppService = source.GetString(ConfigurationKeys.AzureAppService.AzureAppServicesContextKey)?.ToBoolean() ?? false;
            if (IsRunningInAzureAppService)
            {
                AzureAppServiceMetadata = new ImmutableAzureAppServiceSettings(source);
                if (AzureAppServiceMetadata.IsUnsafeToTrace)
                {
                    TraceEnabled = false;
                }
            }

            var urlSubstringSkips = source.GetString(ConfigurationKeys.HttpClientExcludedUrlSubstrings) ??
                                    // default value
                                    (IsRunningInAzureAppService ? ImmutableAzureAppServiceSettings.DefaultHttpClientExclusions :
                                     Serverless.Metadata is { IsRunningInLambda: true } m ? m.DefaultHttpClientExclusions : null);

            HttpClientExcludedUrlSubstrings = urlSubstringSkips != null
                                                  ? TrimSplitString(urlSubstringSkips.ToUpperInvariant(), commaSeparator)
                                                  : Array.Empty<string>();

            var dbmPropagationMode = source.GetString(ConfigurationKeys.DbmPropagationMode);
            DbmPropagationMode = dbmPropagationMode == null ? DbmPropagationLevel.Disabled : ValidateDbmPropagationInput(dbmPropagationMode);
        }

        /// <summary>
        /// Gets or sets the default environment name applied to all spans.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.Environment"/>
        public string? Environment { get; set; }

        /// <summary>
        /// Gets or sets the service name applied to top-level spans and used to build derived service names.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.ServiceName"/>
        public string? ServiceName { get; set; }

        /// <summary>
        /// Gets or sets the version tag applied to all spans.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.ServiceVersion"/>
        public string? ServiceVersion { get; set; }

        /// <summary>
        /// Gets or sets the application's git repository url.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.GitRepositoryUrl"/>
        internal string? GitRepositoryUrl { get; set; }

        /// <summary>
        /// Gets or sets the application's git commit hash.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.GitCommitSha"/>
        internal string? GitCommitSha { get; set; }

        /// <summary>
        /// Gets a value indicating whether we should tag every telemetry event with git metadata.
        /// Default value is <c>true</c> (enabled).
        /// </summary>
        /// <seealso cref="ConfigurationKeys.GitMetadataEnabled"/>
        internal bool GitMetadataEnabled { get; }

        /// <summary>
        /// Gets or sets a value indicating whether tracing is enabled.
        /// Default is <c>true</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.TraceEnabled"/>
        public bool TraceEnabled { get; set; }

        /// <summary>
        /// Gets or sets the names of disabled integrations.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.DisabledIntegrations"/>
        public HashSet<string> DisabledIntegrationNames { get; set; }

        /// <summary>
        /// Gets or sets the transport settings that dictate how the tracer connects to the agent.
        /// </summary>
        public ExporterSettings Exporter { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether default Analytics are enabled.
        /// Settings this value is a shortcut for setting
        /// <see cref="Configuration.IntegrationSettings.AnalyticsEnabled"/> on some predetermined integrations.
        /// See the documentation for more details.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.GlobalAnalyticsEnabled"/>
        [Obsolete(DeprecationMessages.AppAnalytics)]
        public bool AnalyticsEnabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether correlation identifiers are
        /// automatically injected into the logging context.
        /// Default is <c>false</c>, unless <see cref="ConfigurationKeys.DirectLogSubmission.EnabledIntegrations"/>
        /// enables Direct Log Submission.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.LogsInjectionEnabled"/>
        public bool LogsInjectionEnabled
        {
            get => LogSubmissionSettings?.LogsInjectionEnabled ?? false;
            set => LogSubmissionSettings.LogsInjectionEnabled = value;
        }

        /// <summary>
        /// Gets or sets a value indicating the maximum number of traces set to AutoKeep (p1) per second.
        /// Default is <c>100</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.TraceRateLimit"/>
        public int MaxTracesSubmittedPerSecond { get; set; }

        /// <summary>
        /// Gets or sets a value indicating custom sampling rules.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.CustomSamplingRules"/>
        public string? CustomSamplingRules { get; set; }

        /// <summary>
        /// Gets or sets a value indicating span sampling rules.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.SpanSamplingRules"/>
        internal string? SpanSamplingRules { get; set; }

        /// <summary>
        /// Gets or sets a value indicating a global rate for sampling.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.GlobalSamplingRate"/>
        public double? GlobalSamplingRate { get; set; }

        /// <summary>
        /// Gets a collection of <see cref="Integrations"/> keyed by integration name.
        /// </summary>
        public IntegrationSettingsCollection Integrations { get; }

        /// <summary>
        /// Gets or sets the global tags, which are applied to all <see cref="Span"/>s.
        /// </summary>
        public IDictionary<string, string> GlobalTags { get; set; }

        /// <summary>
        /// Gets or sets the map of header keys to tag names, which are applied to the root <see cref="Span"/>
        /// of incoming and outgoing HTTP requests.
        /// </summary>
        public IDictionary<string, string> HeaderTags { get; set; }

        /// <summary>
        /// Gets or sets a custom request header configured to read the ip from. For backward compatibility, it fallbacks on DD_APPSEC_IPHEADER
        /// </summary>
        internal string? IpHeader { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the ip header should not be collected. The default is false.
        /// </summary>
        internal bool IpHeaderEnabled { get; set; }

        /// <summary>
        /// Gets or sets the map of metadata keys to tag names, which are applied to the root <see cref="Span"/>
        /// of incoming and outgoing GRPC requests.
        /// </summary>
        public IDictionary<string, string> GrpcTags { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether internal metrics
        /// are enabled and sent to DogStatsd.
        /// </summary>
        public bool TracerMetricsEnabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether stats are computed on the tracer side
        /// </summary>
        public bool StatsComputationEnabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the use
        /// of System.Diagnostics.DiagnosticSource is enabled.
        /// Default is <c>true</c>.
        /// </summary>
        /// <remark>
        /// This value cannot be set in code. Instead,
        /// set it using the <c>DD_TRACE_DIAGNOSTIC_SOURCE_ENABLED</c>
        /// environment variable or in configuration files.
        /// </remark>
        public bool DiagnosticSourceEnabled
        {
            get => GlobalSettings.Instance.DiagnosticSourceEnabled;
            set { }
        }

        /// <summary>
        /// Gets or sets a value indicating whether a span context should be created on exiting a successful Kafka
        /// Consumer.Consume() call, and closed on entering Consumer.Consume().
        /// </summary>
        /// <seealso cref="ConfigurationKeys.KafkaCreateConsumerScopeEnabled"/>
        public bool KafkaCreateConsumerScopeEnabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to enable the updated WCF instrumentation that delays execution
        /// until later in the WCF pipeline when the WCF server exception handling is established.
        /// </summary>
        internal bool DelayWcfInstrumentationEnabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to obfuscate the <c>LocalPath</c> of a WCF request that goes
        /// into the <c>resourceName</c> of a span.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.FeatureFlags.WcfObfuscationEnabled"/>
        internal bool WcfObfuscationEnabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating the regex to apply to obfuscate http query strings.
        /// </summary>
        internal string ObfuscationQueryStringRegex { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether or not http.url should contain the query string, enabled by default
        /// </summary>
        internal bool QueryStringReportingEnabled { get; set; }

        /// <summary>
        /// Gets or sets a value limiting the size of the querystring to report and obfuscate
        /// Default value is 5000, 0 means that we don't limit the size.
        /// </summary>
        internal int QueryStringReportingSize { get; set; }

        /// <summary>
        /// Gets or sets a value indicating a timeout in milliseconds to the execution of the query string obfuscation regex
        /// Default value is 100ms
        /// </summary>
        internal double ObfuscationQueryStringRegexTimeout { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the diagnostic log at startup is enabled
        /// </summary>
        public bool StartupDiagnosticLogEnabled { get; set; }

        /// <summary>
        /// Gets or sets the time interval (in seconds) for sending stats
        /// </summary>
        internal int StatsComputationInterval { get; set; }

        /// <summary>
        /// Gets or sets the maximum length of an outgoing propagation header's value ("x-datadog-tags")
        /// when injecting it into downstream service calls.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.TagPropagation.HeaderMaxLength"/>
        /// <remarks>
        /// This value is not used when extracting an incoming propagation header from an upstream service.
        /// </remarks>
        internal int OutgoingTagPropagationHeaderMaxLength { get; set; }

        /// <summary>
        /// Gets or sets a value indicating the injection propagation style.
        /// </summary>
        internal string[] PropagationStyleInject { get; set; }

        /// <summary>
        /// Gets or sets a value indicating the extraction propagation style.
        /// </summary>
        internal string[] PropagationStyleExtract { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether runtime metrics
        /// are enabled and sent to DogStatsd.
        /// </summary>
        internal bool RuntimeMetricsEnabled { get; set; }

        /// <summary>
        /// Gets or sets the comma separated list of url patterns to skip tracing.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.HttpClientExcludedUrlSubstrings"/>
        internal string[] HttpClientExcludedUrlSubstrings { get; set; }

        /// <summary>
        /// Gets or sets the HTTP status code that should be marked as errors for server integrations.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.HttpServerErrorStatusCodes"/>
        internal bool[] HttpServerErrorStatusCodes { get; set; }

        /// <summary>
        /// Gets or sets the HTTP status code that should be marked as errors for client integrations.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.HttpClientErrorStatusCodes"/>
        internal bool[] HttpClientErrorStatusCodes { get; set; }

        /// <summary>
        /// Gets configuration values for changing service names based on configuration
        /// </summary>
        internal ServiceNames ServiceNameMappings { get; }

        /// <summary>
        /// Gets or sets a value indicating the size in bytes of the trace buffer
        /// </summary>
        internal int TraceBufferSize { get; set; }

        /// <summary>
        /// Gets or sets a value indicating the batch interval for the serialization queue, in milliseconds
        /// </summary>
        internal int TraceBatchInterval { get; set; }

        /// <summary>
        /// Gets a value indicating whether the feature flag to enable the updated ASP.NET resource names is enabled
        /// </summary>
        /// <seealso cref="ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled"/>
        internal bool RouteTemplateResourceNamesEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether resource names for ASP.NET and ASP.NET Core spans should be expanded. Only applies
        /// when <see cref="RouteTemplateResourceNamesEnabled"/> is <code>true</code>.
        /// </summary>
        internal bool ExpandRouteTemplatesEnabled { get; }

        /// <summary>
        /// Gets or sets the direct log submission settings.
        /// </summary>
        internal DirectLogSubmissionSettings LogSubmissionSettings { get; set; }

        /// <summary>
        /// Gets or sets a value indicating the trace methods configuration.
        /// </summary>
        internal string TraceMethods { get; set; }

        /// <summary>
        /// Gets a value indicating whether the activity listener is enabled or not.
        /// </summary>
        internal bool IsActivityListenerEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether data streams monitoring is enabled or not.
        /// </summary>
        internal bool IsDataStreamsMonitoringEnabled { get; }

        /// <summary>
        /// Gets or sets a value indicating whether the rare sampler is enabled or not.
        /// </summary>
        internal bool IsRareSamplerEnabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the tracer is running in AAS
        /// </summary>
        internal bool IsRunningInAzureAppService { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the tracer should propagate service data in db queries
        /// </summary>
        internal DbmPropagationLevel DbmPropagationMode { get; set; }

        /// <summary>
        /// Gets or sets the AAS settings
        /// </summary>
        internal ImmutableAzureAppServiceSettings? AzureAppServiceMetadata { get; set; }

        /// <summary>
        /// Gets or sets the metadata schema version
        /// </summary>
        internal string MetadataSchemaVersion { get; set; }

        /// <summary>
        /// Create a <see cref="TracerSettings"/> populated from the default sources
        /// returned by <see cref="GlobalConfigurationSource.Instance"/>.
        /// </summary>
        /// <returns>A <see cref="TracerSettings"/> populated from the default sources.</returns>
        public static TracerSettings FromDefaultSources()
        {
            return new TracerSettings(GlobalConfigurationSource.Instance);
        }

        /// <summary>
        /// Creates a <see cref="IConfigurationSource"/> by combining environment variables,
        /// AppSettings where available, and a local datadog.json file, if present.
        /// </summary>
        /// <returns>A new <see cref="IConfigurationSource"/> instance.</returns>
        public static CompositeConfigurationSource CreateDefaultConfigurationSource()
        {
            return GlobalConfigurationSource.CreateDefaultConfigurationSource();
        }

        /// <summary>
        /// Sets the HTTP status code that should be marked as errors for client integrations.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.HttpClientErrorStatusCodes"/>
        /// <param name="statusCodes">Status codes that should be marked as errors</param>
        public void SetHttpClientErrorStatusCodes(IEnumerable<int> statusCodes)
        {
            HttpClientErrorStatusCodes = ParseHttpCodesToArray(string.Join(",", statusCodes));
        }

        /// <summary>
        /// Sets the HTTP status code that should be marked as errors for server integrations.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.HttpServerErrorStatusCodes"/>
        /// <param name="statusCodes">Status codes that should be marked as errors</param>
        public void SetHttpServerErrorStatusCodes(IEnumerable<int> statusCodes)
        {
            HttpServerErrorStatusCodes = ParseHttpCodesToArray(string.Join(",", statusCodes));
        }

        /// <summary>
        /// Sets the mappings to use for service names within a <see cref="Span"/>
        /// </summary>
        /// <param name="mappings">Mappings to use from original service name (e.g. <code>sql-server</code> or <code>graphql</code>)
        /// as the <see cref="KeyValuePair{TKey, TValue}.Key"/>) to replacement service names as <see cref="KeyValuePair{TKey, TValue}.Value"/>).</param>
        public void SetServiceNameMappings(IEnumerable<KeyValuePair<string, string>> mappings)
        {
            ServiceNameMappings.SetServiceNameMappings(mappings);
        }

        /// <summary>
        /// Create an instance of <see cref="ImmutableTracerSettings"/> that can be used to build a <see cref="Tracer"/>
        /// </summary>
        /// <returns>The <see cref="ImmutableTracerSettings"/> that can be passed to a <see cref="Tracer"/> instance</returns>
        public ImmutableTracerSettings Build()
        {
            return new ImmutableTracerSettings(this);
        }

        private static IDictionary<string, string> InitializeHeaderTags(IDictionary<string, string> configurationDictionary, bool headerTagsNormalizationFixEnabled)
        {
            var headerTags = new Dictionary<string, string>();

            foreach (var kvp in configurationDictionary)
            {
                var headerName = kvp.Key;
                var providedTagName = kvp.Value;
                if (string.IsNullOrWhiteSpace(headerName))
                {
                    continue;
                }

                // The user has not provided a tag name. The normalization will happen later, when adding the prefix.
                if (string.IsNullOrEmpty(providedTagName))
                {
                    headerTags.Add(headerName.Trim(), string.Empty);
                }
                else if (headerTagsNormalizationFixEnabled && providedTagName.TryConvertToNormalizedTagName(normalizePeriods: false, out var normalizedTagName))
                {
                    // If the user has provided a tag name, then we don't normalize periods in the provided tag name
                    headerTags.Add(headerName.Trim(), normalizedTagName);
                }
                else if (!headerTagsNormalizationFixEnabled && providedTagName.TryConvertToNormalizedTagName(normalizePeriods: true, out var normalizedTagNameNoPeriods))
                {
                    // Back to the previous behaviour if the flag is set
                    headerTags.Add(headerName.Trim(), normalizedTagNameNoPeriods);
                }
            }

            return headerTags;
        }

        internal static string[] TrimSplitString(string? textValues, char[] separators)
        {
            if (string.IsNullOrWhiteSpace(textValues))
            {
                return Array.Empty<string>();
            }

            var values = textValues!.Split(separators);
            var list = new List<string>(values.Length);

            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    list.Add(value.Trim());
                }
            }

            return list.ToArray();
        }

        internal static bool[] ParseHttpCodesToArray(string httpStatusErrorCodes)
        {
            bool[] httpErrorCodesArray = new bool[600];

            void TrySetValue(int index)
            {
                if (index >= 0 && index < httpErrorCodesArray.Length)
                {
                    httpErrorCodesArray[index] = true;
                }
            }

            string[] configurationsArray = httpStatusErrorCodes.Replace(" ", string.Empty).Split(',');

            foreach (string statusConfiguration in configurationsArray)
            {
                int startStatus;

                // Checks that the value about to be used follows the `401-404` structure or single 3 digit number i.e. `401` else log the warning
                if (!Regex.IsMatch(statusConfiguration, @"^\d{3}-\d{3}$|^\d{3}$"))
                {
                    Log.Warning("Wrong format '{0}' for DD_HTTP_SERVER/CLIENT_ERROR_STATUSES configuration.", statusConfiguration);
                }

                // If statusConfiguration equals a single value i.e. `401` parse the value and save to the array
                else if (int.TryParse(statusConfiguration, out startStatus))
                {
                    TrySetValue(startStatus);
                }
                else
                {
                    string[] statusCodeLimitsRange = statusConfiguration.Split('-');

                    startStatus = int.Parse(statusCodeLimitsRange[0]);
                    int endStatus = int.Parse(statusCodeLimitsRange[1]);

                    if (endStatus < startStatus)
                    {
                        startStatus = endStatus;
                        endStatus = int.Parse(statusCodeLimitsRange[0]);
                    }

                    for (int statusCode = startStatus; statusCode <= endStatus; statusCode++)
                    {
                        TrySetValue(statusCode);
                    }
                }
            }

            return httpErrorCodesArray;
        }

        internal static DbmPropagationLevel ValidateDbmPropagationInput(string inputValue)
        {
            DbmPropagationLevel propagationValue;

            if (inputValue.Equals("disabled", StringComparison.OrdinalIgnoreCase))
            {
                propagationValue = DbmPropagationLevel.Disabled;
            }
            else if (inputValue.Equals("service", StringComparison.OrdinalIgnoreCase))
            {
                propagationValue = DbmPropagationLevel.Service;
            }
            else if (inputValue.Equals("full", StringComparison.OrdinalIgnoreCase))
            {
                propagationValue = DbmPropagationLevel.Full;
            }
            else
            {
                propagationValue = DbmPropagationLevel.Disabled;
                Log.Warning("Wrong setting '{0}' for DD_DBM_PROPAGATION_MODE supported values include: disabled, service or full.", inputValue);
            }

            return propagationValue;
        }
    }
}
