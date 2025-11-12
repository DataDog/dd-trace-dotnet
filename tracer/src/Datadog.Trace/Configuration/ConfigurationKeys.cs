// <copyright file="ConfigurationKeys.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using Datadog.Trace.Telemetry;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// String constants for standard Datadog configuration keys.
    /// </summary>
    internal static partial class ConfigurationKeys
    {
        /// <summary>
        /// Configuration key to enable experimental features.
        /// </summary>
        public const string ExperimentalFeaturesEnabled = "DD_TRACE_EXPERIMENTAL_FEATURES_ENABLED";

        /// <summary>
        /// Configuration key for the path to the configuration file.
        /// Can only be set with an environment variable
        /// or in the <c>app.config</c>/<c>web.config</c> file.
        /// </summary>
        public const string ConfigurationFileName = "DD_TRACE_CONFIG_FILE";

        /// <summary>
        /// Configuration key for the application's environment. Sets the "env" tag on every <see cref="Span"/>.
        /// </summary>
        /// <seealso cref="TracerSettings.Environment"/>
        public const string Environment = "DD_ENV";

        /// <summary>
        /// Configuration key for the application's default service name.
        /// Used as the service name for top-level spans,
        /// and used to determine service name of some child spans.
        /// </summary>
        /// <seealso cref="TracerSettings.ServiceName"/>
        public const string ServiceName = "DD_SERVICE";

        /// <summary>
        /// Configuration key for the application's version. Sets the "version" tag on every <see cref="Span"/>.
        /// </summary>
        /// <seealso cref="TracerSettings.ServiceVersion"/>
        public const string ServiceVersion = "DD_VERSION";

        /// <summary>
        /// Configuration key for the application's git repo URL. Sets the "_dd.git.repository_url" tag on every <see cref="Span"/>.
        /// </summary>
        /// <seealso cref="TracerSettings.GitRepositoryUrl"/>
        public const string GitRepositoryUrl = "DD_GIT_REPOSITORY_URL";

        /// <summary>
        /// Configuration key for the application's git commit hash. Sets the "_dd.git.commit.sha" tag on every <see cref="Span"/>.
        /// </summary>
        /// <seealso cref="TracerSettings.GitCommitSha"/>
        public const string GitCommitSha = "DD_GIT_COMMIT_SHA";

        /// <summary>
        /// Configuration key for enabling the tagging of every telemetry event with git metadata.
        /// Default is value is true (enabled).
        /// </summary>
        /// <seealso cref="TracerSettings.GitMetadataEnabled"/>
        public const string GitMetadataEnabled = "DD_TRACE_GIT_METADATA_ENABLED";

        /// <summary>
        /// Configuration key for enabling or disabling the Tracer.
        /// Default is value is true (enabled).
        /// </summary>
        /// <seealso cref="TracerSettings.TraceEnabled"/>
        public const string TraceEnabled = "DD_TRACE_ENABLED";

        /// <summary>
        /// Configuration key for enabling or disabling the Tracer's debug mode.
        /// Default is value is false (disabled).
        /// </summary>
        public const string DebugEnabled = "DD_TRACE_DEBUG";

        /// <summary>
        /// Configuration key for enabling or disabling the generation of APM traces.
        /// Default is value is true (enabled).
        /// </summary>
        public const string ApmTracingEnabled = "DD_APM_TRACING_ENABLED";

        /// <summary>
        /// Configuration key for enabling or disabling the Tracer's debugger mode.
        /// Default is value is false (disabled).
        /// </summary>
        public const string WaitForDebuggerAttach = "DD_INTERNAL_WAIT_FOR_DEBUGGER_ATTACH";

        /// <summary>
        /// Configuration key for enabling or disabling the Tracer's native debugger mode.
        /// Default is value is false (disabled).
        /// </summary>
        public const string WaitForNativeDebuggerAttach = "DD_INTERNAL_WAIT_FOR_NATIVE_DEBUGGER_ATTACH";

        /// <summary>
        /// Configuration key for a list of integrations to disable. All other integrations remain enabled.
        /// Default is empty (all integrations are enabled).
        /// Supports multiple values separated with semi-colons.
        /// </summary>
        /// <seealso cref="TracerSettings.DisabledIntegrationNames"/>
        public const string DisabledIntegrations = "DD_DISABLED_INTEGRATIONS";

        /// <summary>
        /// Configuration key for a list of ActivitySource names (supports globbing) that will be disabled.
        /// Default is empty (all ActivitySources will be subscribed to by default).
        /// <para><b>Disabling ActivitySources may break distributed tracing if those Activities are used to propagate trace context.</b></para>
        /// <para>
        /// Supports multiple values separated with commas.
        /// For example: "SomeGlob.*.PatternSource,Some.Specific.Source"
        /// </para>
        /// </summary>
        /// <remarks>
        /// <para>
        /// When the tracer doesn't subscribe to an ActivitySource, we will <em>NOT</em> propagate the trace context from those Activities (we don't see them anymore).
        /// <br/><b>This means that distributed tracing flows that rely on these Activities for context propagation
        /// will break and cause disconnected traces.</b>
        /// </para>
        /// <para>
        /// Potential impact on distributed tracing:
        /// <list type="bullet">
        /// <item>
        ///   <description>
        ///     Service A -> Ignored Activity -> Service B
        ///     <para>Creates a single trace with Service A as root and Service B as child</para>
        ///   </description>
        /// </item>
        /// <item>
        ///   <description>
        ///     Service A -> Disabled Activity -> Service B
        ///     <para>Creates TWO separate traces with Service A and Service B each as root spans</para>
        ///   </description>
        /// </item>
        /// </list>
        /// </para>
        /// </remarks>
        public const string DisabledActivitySources = "DD_TRACE_DISABLED_ACTIVITY_SOURCES";

        /// <summary>
        /// Configuration key for enabling or disabling default Analytics.
        /// </summary>
        /// <seealso cref="TracerSettings.AnalyticsEnabled"/>
        [Obsolete(DeprecationMessages.AppAnalytics)]
        public const string GlobalAnalyticsEnabled = "DD_TRACE_ANALYTICS_ENABLED";

        /// <summary>
        /// Configuration key for a list of tags to be applied globally to spans.
        /// Supports multiple key key-value pairs which are comma-separated, and for which the key and
        /// value are colon-separated. For example Key1:Value1, Key2:Value2
        /// </summary>
        /// <seealso cref="TracerSettings.GlobalTags"/>
        public const string GlobalTags = "DD_TAGS";

        /// <summary>
        /// Configuration key for a map of header keys to tag names.
        /// Automatically apply header values as tags on traces.
        /// </summary>
        /// <seealso cref="TracerSettings.HeaderTags"/>
        public const string HeaderTags = "DD_TRACE_HEADER_TAGS";

        /// <summary>
        /// Configuration key for a map of metadata keys to tag names.
        /// Automatically apply GRPC metadata values as tags on traces.
        /// </summary>
        /// <seealso cref="TracerSettings.HeaderTags"/>
        public const string GrpcTags = "DD_TRACE_GRPC_TAGS";

        /// <summary>
        /// Propagate the process tags in every supported payload
        /// </summary>
        public const string PropagateProcessTags = "DD_EXPERIMENTAL_PROPAGATE_PROCESS_TAGS_ENABLED";

        /// <summary>
        /// Configuration key for a map of services to rename.
        /// </summary>
        /// <seealso cref="TracerSettings.ServiceNameMappings"/>
        public const string ServiceNameMappings = "DD_TRACE_SERVICE_MAPPING";

        /// <summary>
        /// Configuration key for setting the size in bytes of the trace buffer
        /// </summary>
        public const string BufferSize = "DD_TRACE_BUFFER_SIZE";

        /// <summary>
        /// Configuration key for setting the batch interval in milliseconds for the serialization queue.
        /// Set to 0 to disable.
        /// </summary>
        public const string SerializationBatchInterval = "DD_TRACE_BATCH_INTERVAL";

        /// <summary>
        /// Configuration key for enabling or disabling the automatic injection
        /// of correlation identifiers into the logging context.
        /// </summary>
        /// <seealso cref="TracerSettings.LogsInjectionEnabled"/>
        public const string LogsInjectionEnabled = "DD_LOGS_INJECTION";

        /// <summary>
        /// Configuration key for setting the number of traces allowed
        /// to be submitted per second.
        /// </summary>
        /// <seealso cref="TracerSettings.MaxTracesSubmittedPerSecond"/>
        [Obsolete("This parameter is obsolete and should be replaced by `DD_TRACE_RATE_LIMIT`")]
        public const string MaxTracesSubmittedPerSecond = "DD_MAX_TRACES_PER_SECOND";

        /// <summary>
        /// Configuration key for setting the number of traces allowed
        /// to be submitted per second.
        /// </summary>
        /// <seealso cref="TracerSettings.MaxTracesSubmittedPerSecond"/>
        public const string TraceRateLimit = "DD_TRACE_RATE_LIMIT";

        /// <summary>
        /// Configuration key for setting custom sampling rules based on regular expressions.
        /// Semi-colon separated list of sampling rules.
        /// The rule is matched in order of specification. The first match in a list is used.
        ///
        /// Per entry:
        ///   The item "sample_rate" is required in decimal format.
        ///   The item "service" is optional in regular expression format, to match on service name.
        ///   The item "name" is optional in regular expression format, to match on operation name.
        ///
        /// To give a rate of 50% to any traces in a service starting with the text "cart":
        ///   '[{"sample_rate":0.5, "service":"cart.*"}]'
        ///
        /// To give a rate of 20% to any traces which have an operation name of "http.request":
        ///   '[{"sample_rate":0.2, "name":"http.request"}]'
        ///
        /// To give a rate of 100% to any traces within a service named "background" and with an operation name of "sql.query":
        ///   '[{"sample_rate":1.0, "service":"background", "name":"sql.query"}]
        ///
        /// To give a rate of 10% to all traces
        ///   '[{"sample_rate":0.1}]'
        ///
        /// To configure multiple rules, separate by semi-colon and order from most specific to least specific:
        ///   '[{"sample_rate":0.5, "service":"cart.*"}, {"sample_rate":0.2, "name":"http.request"}, {"sample_rate":1.0, "service":"background", "name":"sql.query"}, {"sample_rate":0.1}]'
        ///
        /// If no rules are specified, or none match, default internal sampling logic will be used.
        /// </summary>
        /// <seealso cref="TracerSettings.CustomSamplingRules"/>
        public const string CustomSamplingRules = "DD_TRACE_SAMPLING_RULES";

        /// <summary>
        /// Configuration key for setting the format of <see cref="CustomSamplingRules"/>.
        /// Valid values are <c>regex</c> or <c>glob</c>.
        /// If the value is not recognized, trace sampling rules are disabled.
        /// </summary>
        public const string CustomSamplingRulesFormat = "DD_TRACE_SAMPLING_RULES_FORMAT";

        /// <summary>
        /// Configuration key for setting custom <em>span</em> sampling rules based on glob patterns.
        /// Comma separated list of span sampling rules.
        /// The rule is matched in order of specification. The first match in a list is used.
        /// The supported glob pattern characters are '*' and '?'.
        /// A '*' matches any contiguous substring.
        /// A '?' matches exactly one character.
        ///
        /// Per entry:
        ///     The item "service" is a glob pattern string, to match on the service name.
        ///         Optional and defaults to '*'.
        ///     The item "name" is a glob pattern string, to match on the operation name.
        ///         Optional and defaults to '*'.
        ///     The item "sample_rate" is a float and is the probability of keeping a matched span.
        ///         Optional and defaults to 1.0 (keep all).
        ///     The item "max_per_second" is a float and is the maximum number of spans that can be kept per second for the rule.
        ///         Optional and defaults to unlimited.
        ///
        /// Examples:
        /// Match all spans that have a service name of "cart" and an operation name of "checkout" with a kept limit of 1000 per second.
        ///     "[{"service": "cart", "name": "checkout", "max_per_second": 1000}]"
        ///
        /// Match 50% of spans that have a service name of "cart" and an operation name of "checkout" with a kept limit of 1000 per second.
        ///     "[{"service": "cart", "name": "checkout", "sample_rate": 0.5, "max_per_second": 1000}]"
        ///
        /// Match all spans that start with "cart" without any limits and any operation name.
        ///     "[{"service": "cart*"}]"
        /// </summary>
        /// <seealso cref="TracerSettings.SpanSamplingRules"/>
        public const string SpanSamplingRules = "DD_SPAN_SAMPLING_RULES";

        /// <summary>
        /// Configuration key for setting the global rate for the sampler.
        /// </summary>
        public const string GlobalSamplingRate = "DD_TRACE_SAMPLE_RATE";

        public const string RareSamplerEnabled = "DD_APM_ENABLE_RARE_SAMPLER";

        /// <summary>
        /// Configuration key for enabling or disabling internal metrics sent to DogStatsD.
        /// Default value is <c>false</c> (disabled).
        /// </summary>
        public const string TracerMetricsEnabled = "DD_TRACE_METRICS_ENABLED";

        /// <summary>
        /// Configuration key for enabling or disabling runtime metrics sent to DogStatsD.
        /// Default value is <c>false</c> (disabled).
        /// </summary>
        public const string RuntimeMetricsEnabled = "DD_RUNTIME_METRICS_ENABLED";

        /// <summary>
        /// Use libdatadog data pipeline to send traces.
        /// Default value is <c>false</c> (disabled).
        /// </summary>
        public const string TraceDataPipelineEnabled = "DD_TRACE_DATA_PIPELINE_ENABLED";

        /// <summary>
        /// Configuration key for when a standalone instance of the Trace Agent needs to be started.
        /// </summary>
        public const string TraceAgentPath = "DD_TRACE_AGENT_PATH";

        /// <summary>
        /// Configuration key for arguments to pass to the Trace Agent process.
        /// </summary>
        public const string TraceAgentArgs = "DD_TRACE_AGENT_ARGS";

        /// <summary>
        /// Configuration key for when a standalone instance of DogStatsD needs to be started.
        /// </summary>
        public const string DogStatsDPath = "DD_DOGSTATSD_PATH";

        /// <summary>
        /// Configuration key for arguments to pass to the DogStatsD process.
        /// </summary>
        public const string DogStatsDArgs = "DD_DOGSTATSD_ARGS";

        /// <summary>
        /// Configuration key for enabling or disabling the use of System.Diagnostics.DiagnosticSource.
        /// Default value is <c>true</c> (enabled).
        /// </summary>
        public const string DiagnosticSourceEnabled = "DD_DIAGNOSTIC_SOURCE_ENABLED";

        /// <summary>
        /// Configuration key for setting the API key, used by the Agent.
        /// </summary>
        public const string ApiKey = "DD_API_KEY";

        /// <summary>
        /// Configuration key for setting the default Datadog destination site.
        /// Defaults to "datadoghq.com".
        /// </summary>
        public const string Site = "DD_SITE";

        /// <summary>
        /// Configuration key for overriding which URLs are skipped by the tracer.
        /// </summary>
        /// <seealso cref="TracerSettings.HttpClientExcludedUrlSubstrings"/>
        public const string HttpClientExcludedUrlSubstrings = "DD_TRACE_HTTP_CLIENT_EXCLUDED_URL_SUBSTRINGS";

        /// <summary>
        /// Configuration key for the application's server http statuses to set spans as errors by.
        /// </summary>
        /// <seealso cref="TracerSettings.HttpServerErrorStatusCodes"/>
        [Obsolete("This parameter is obsolete and should be replaced by `DD_TRACE_HTTP_SERVER_ERROR_STATUSES`")]
        public const string DeprecatedHttpServerErrorStatusCodes = "DD_HTTP_SERVER_ERROR_STATUSES";

        /// <summary>
        /// Configuration key for the application's server http statuses to set spans as errors by.
        /// </summary>
        /// <seealso cref="TracerSettings.HttpServerErrorStatusCodes"/>
        public const string HttpServerErrorStatusCodes = "DD_TRACE_HTTP_SERVER_ERROR_STATUSES";

        /// <summary>
        /// Configuration key for the application's client http statuses to set spans as errors by.
        /// </summary>
        /// <seealso cref="TracerSettings.HttpClientErrorStatusCodes"/>
        [Obsolete("This parameter is obsolete and should be replaced by `DD_TRACE_HTTP_CLIENT_ERROR_STATUSES`")]
        public const string DeprecatedHttpClientErrorStatusCodes = "DD_HTTP_CLIENT_ERROR_STATUSES";

        /// <summary>
        /// Configuration key for the application's client http statuses to set spans as errors by.
        /// </summary>
        /// <seealso cref="TracerSettings.HttpClientErrorStatusCodes"/>
        public const string HttpClientErrorStatusCodes = "DD_TRACE_HTTP_CLIENT_ERROR_STATUSES";

        /// <summary>
        /// Configuration key indicating the optional name of the custom header to take into account to report the ip address from.
        /// If this variable is set all other IP related headers should be ignored
        /// Default is value is null (do not override).
        /// </summary>
        /// <seealso cref="TracerSettings.IpHeader"/>
        public const string IpHeader = "DD_TRACE_CLIENT_IP_HEADER";

        /// <summary>
        /// Configuration key indicating if the header should be collected. The default for DD_TRACE_CLIENT_IP_ENABLED is false.
        /// </summary>
        /// <seealso cref="TracerSettings.IpHeaderEnabled"/>
        public const string IpHeaderEnabled = "DD_TRACE_CLIENT_IP_ENABLED";

        /// <summary>
        /// Configuration key to enable or disable the creation of a span context on exiting a successful Kafka
        /// Consumer.Consume() call, and closing the scope on entering Consumer.Consume().
        /// Default value is <c>true</c> (enabled).
        /// </summary>
        /// <seealso cref="TracerSettings.KafkaCreateConsumerScopeEnabled"/>
        public const string KafkaCreateConsumerScopeEnabled = "DD_TRACE_KAFKA_CREATE_CONSUMER_SCOPE_ENABLED";

        /// <summary>
        /// Configuration key to enable or disable the creation of individual message spans and span links
        /// when using Azure Service Bus batch operations.
        /// Default value is <c>true</c> (enabled).
        /// </summary>
        /// <seealso cref="TracerSettings.AzureServiceBusBatchLinksEnabled"/>
        public const string AzureServiceBusBatchLinksEnabled = "DD_TRACE_AZURE_SERVICEBUS_BATCH_LINKS_ENABLED";

        /// <summary>
        /// Configuration key for controlling whether route parameters in ASP.NET and ASP.NET Core resource names
        /// should be expanded with their values. Only applies when
        /// <see cref="ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled"/> is enabled.
        /// </summary>
        /// <seealso cref="TracerSettings.ExpandRouteTemplatesEnabled"/>
        public const string ExpandRouteTemplatesEnabled = "DD_TRACE_EXPAND_ROUTE_TEMPLATES_ENABLED";

        /// <summary>
        /// Configuration key for enabling or disabling span links creation for Azure EventHubs batch operations.
        /// When enabled, TryAdd spans are created and linked to the send span.
        /// When disabled, TryAdd spans are not created, and therefore they are never linked to the send span.
        /// Default value is <c>true</c> (enabled).
        /// </summary>
        /// <seealso cref="TracerSettings.AzureEventHubsBatchLinksEnabled"/>
        public const string AzureEventHubsBatchLinksEnabled = "DD_TRACE_AZURE_EVENTHUBS_BATCH_LINKS_ENABLED";

        /// <summary>
        /// Configuration key for enabling computation of stats (aka trace metrics) on the tracer side
        /// </summary>
        public const string StatsComputationEnabled = "DD_TRACE_STATS_COMPUTATION_ENABLED";

        /// <summary>
        /// Configuration key for configuring the interval (in seconds) for sending stats (aka trace metrics)
        /// </summary>
        public const string StatsComputationInterval = "_DD_TRACE_STATS_COMPUTATION_INTERVAL";

        /// <summary>
        /// Configuration key for setting the header injection propagation style.
        /// If <see cref="PropagationStyle"/> is also defined, this value overrides the header injection styles.
        /// </summary>
        /// <seealso cref="Datadog.Trace.Propagators.ContextPropagationHeaderStyle"/>
        /// <seealso cref="TracerSettings.PropagationStyleInject"/>
        public const string PropagationStyleInject = "DD_TRACE_PROPAGATION_STYLE_INJECT";

        /// <summary>
        /// Configuration key for setting the header extraction propagation style.
        /// If <see cref="PropagationStyle"/> is also defined, this value overrides the header extraction styles.
        /// </summary>
        /// <seealso cref="Datadog.Trace.Propagators.ContextPropagationHeaderStyle"/>
        /// <seealso cref="TracerSettings.PropagationStyleExtract"/>
        public const string PropagationStyleExtract = "DD_TRACE_PROPAGATION_STYLE_EXTRACT";

        /// <summary>
        /// Configuration key for setting the propagation style for both header injection and extraction.
        /// If <see cref="PropagationStyleInject"/> or <see cref="PropagationStyleExtract"/> are also defined,
        /// they will override any header injections or extraction styled defined here, respectively.
        /// </summary>
        /// <seealso cref="Datadog.Trace.Propagators.ContextPropagationHeaderStyle"/>
        public const string PropagationStyle = "DD_TRACE_PROPAGATION_STYLE";

        /// <summary>
        /// Configuration key for setting the header extraction propagation behavior. Accepted values are:
        /// <ul>
        ///   <li>continue: Extracted span context becomes the parent and baggage is propagated</li>
        ///   <li>restart: Extracted span context becomes a span link (a new trace is started) and baggage is propagated</li>
        ///   <li>ignore: We disregard the incoming trace context headers and we also disregard baggage</li>
        /// </ul>
        /// Default value is continue.
        /// </summary>
        public const string PropagationBehaviorExtract = "DD_TRACE_PROPAGATION_BEHAVIOR_EXTRACT";

        /// <summary>
        /// Configuration key to configure if propagation should only extract the first header once a configure
        /// propagator extracts a valid trace context.
        /// </summary>
        /// <seealso cref="TracerSettings.PropagationExtractFirstOnly"/>
        public const string PropagationExtractFirstOnly = "DD_TRACE_PROPAGATION_EXTRACT_FIRST";

        /// <summary>
        /// Configuration key to set the maximum number of items that can be
        /// injected into the baggage header when propagating to a downstream service.
        /// Default value is 64 items.
        /// </summary>
        /// <seealso cref="TracerSettings.BaggageMaximumItems"/>
        public const string BaggageMaximumItems = "DD_TRACE_BAGGAGE_MAX_ITEMS";

        /// <summary>
        /// Configuration key to set the maximum number of bytes that can be
        /// injected into the baggage header when propagating to a downstream service.
        /// Default value is 8192 bytes.
        /// </summary>
        /// <seealso cref="TracerSettings.BaggageMaximumBytes"/>
        public const string BaggageMaximumBytes = "DD_TRACE_BAGGAGE_MAX_BYTES";

        /// <summary>
        /// Configuration key for controlling which baggage keys are converted into span tags.
        /// Default value is "user.id,session.id,account.id".
        ///
        /// Behavior options:
        /// - Empty string: No baggage keys are converted into span tags (feature disabled)
        /// - Comma-separated list: Only baggage keys matching exact, case-sensitive names in the list are added as span tags
        /// - Wildcard (*): All baggage keys are converted into span tags
        /// </summary>
        /// <seealso cref="TracerSettings.BaggageTagKeys"/>
        public const string BaggageTagKeys = "DD_TRACE_BAGGAGE_TAG_KEYS";

        /// <summary>
        /// Configuration key for enabling automatic instrumentation on specified methods.
        /// Default value is "" (disabled).
        /// </summary>
        public const string TraceMethods = "DD_TRACE_METHODS";

        /// <summary>
        /// Configuration key for specifying a custom regex to obfuscate query strings.
        /// Default value is in TracerSettingsConstants
        ///  WARNING: This regex cause crashes under netcoreapp2.1 / linux / arm64, dont use on manual instrumentation in this environment
        /// </summary>
        /// <seealso cref="TracerSettings.ObfuscationQueryStringRegex"/>
        public const string ObfuscationQueryStringRegex = "DD_TRACE_OBFUSCATION_QUERY_STRING_REGEXP";

        /// <summary>
        /// Configuration key for specifying a timeout in milliseconds to the execution of the query string obfuscation regex
        /// Default value is 200ms
        /// </summary>
        /// <seealso cref="TracerSettings.ObfuscationQueryStringRegexTimeout"/>
        public const string ObfuscationQueryStringRegexTimeout = "DD_TRACE_OBFUSCATION_QUERY_STRING_REGEXP_TIMEOUT";

        /// <summary>
        /// Configuration key for setting the max size of the querystring to report, before obfuscation
        /// Default value is 5000, 0 means that we don't limit the size.
        /// </summary>
        /// <seealso cref="TracerSettings.QueryStringReportingSize"/>
        public const string QueryStringReportingSize = "DD_HTTP_SERVER_TAG_QUERY_STRING_SIZE";

        /// <summary>
        /// Configuration key for enabling/disabling reporting query string
        /// Default value is true
        /// </summary>
        /// <seealso cref="TracerSettings.QueryStringReportingEnabled"/>
        public const string QueryStringReportingEnabled = "DD_HTTP_SERVER_TAG_QUERY_STRING";

        /// <summary>
        /// Configuration key for setting DBM propagation mode
        /// Default value is disabled, expected values are either: disabled, service or full
        /// </summary>
        /// <seealso cref="TracerSettings.DbmPropagationMode"/>
        public const string DbmPropagationMode = "DD_DBM_PROPAGATION_MODE";

        /// <summary>
        /// Configuration key for setting the schema version for service naming and span attributes
        /// Accepted values are: "v1", "v0"
        /// Default value is "v0"
        /// </summary>
        public const string MetadataSchemaVersion = "DD_TRACE_SPAN_ATTRIBUTE_SCHEMA";

        /// <summary>
        /// Configuration key for automatically populating the peer.service tag
        /// from predefined precursor attributes when the span attribute schema is v0.
        /// This is ignored when the span attribute schema is v1 or later.
        /// Default value is false
        /// </summary>
        public const string PeerServiceDefaultsEnabled = "DD_TRACE_PEER_SERVICE_DEFAULTS_ENABLED";

        /// <summary>
        /// Configuration key for a map of services to rename.
        /// </summary>
        /// <seealso cref="TracerSettings.PeerServiceNameMappings"/>
        public const string PeerServiceNameMappings = "DD_TRACE_PEER_SERVICE_MAPPING";

        /// <summary>
        /// Configuration key for unifying client service names when the span
        /// attribute schema is v0. This is ignored when the span attribute
        /// schema is v1 or later.
        /// Default value is false
        /// </summary>
        public const string RemoveClientServiceNamesEnabled = "DD_TRACE_REMOVE_INTEGRATION_SERVICE_NAMES_ENABLED";

        /// <summary>
        /// Configuration key for the comma-separated list of user disabled
        /// ADO.NET CommandType names that should not have Span created for them.
        /// <para>"InterceptableDbCommand" and "ProfiledDbCommand" are always disabled by default.</para>
        /// </summary>
        /// <seealso cref="TracerSettings.DisabledAdoNetCommandTypes"/>
        public const string DisabledAdoNetCommandTypes = "DD_TRACE_DISABLED_ADONET_COMMAND_TYPES";

        /// <summary>
        /// Configuration key for toggling span pointers on AWS requests.
        /// Default value is true
        /// </summary>
        public const string SpanPointersEnabled = "DD_TRACE_AWS_ADD_SPAN_POINTERS";

        /// <summary>
        /// Configuration key for specifying which GraphQL error extensions to capture.
        /// A comma-separated list of extension keys to capture. Empty or not present means no extensions are captured.
        /// </summary>
        /// <seealso cref="TracerSettings.GraphQLErrorExtensions"/>
        public const string GraphQLErrorExtensions = "DD_TRACE_GRAPHQL_ERROR_EXTENSIONS";

        /// <summary>
        /// Configuration key for deactivating reading the application monitoring config file through libdatadog (hands off config).
        /// True by default
        /// </summary>
        public const string ApplicationMonitoringConfigFileEnabled = "DD_APPLICATION_MONITORING_CONFIG_FILE_ENABLED";

        /// <summary>
        /// Configuration key to disable polling the /info endpoint in the trace agent for feature discovery.
        /// Default value is true (polling enabled).
        /// </summary>
        /// <seealso cref="TracerSettings.AgentFeaturePollingEnabled"/>
        public const string AgentFeaturePollingEnabled = "DD_AGENT_FEATURE_POLLING_ENABLED";

        /// <summary>
        /// String constants for CI Visibility configuration keys.
        /// </summary>
        public static class CIVisibility
        {
            /// <summary>
            /// An internal key used to "tell" tracer settings that we're running in CI Visibility mode
            /// </summary>
            public const string IsRunningInCiVisMode = "_DD_INTERNAL_IS_RUNNING_IN_CIVISIBILITY";

            /// <summary>
            /// Configuration key for enabling or disabling CI Visibility.
            /// Default value is false (disabled).
            /// </summary>
            public const string Enabled = "DD_CIVISIBILITY_ENABLED";

            /// <summary>
            /// Configuration key for enabling or disabling Agentless in CI Visibility.
            /// Default value is false (disabled).
            /// </summary>
            public const string AgentlessEnabled = "DD_CIVISIBILITY_AGENTLESS_ENABLED";

            /// <summary>
            /// Configuration key for setting the agentless url endpoint
            /// </summary>
            public const string AgentlessUrl = "DD_CIVISIBILITY_AGENTLESS_URL";

            /// <summary>
            /// Configuration key for enabling or disabling Logs direct submission.
            /// Default value is false (disabled).
            /// </summary>
            public const string Logs = "DD_CIVISIBILITY_LOGS_ENABLED";

            /// <summary>
            /// Configuration key for enabling or disabling Code Coverage in CI Visibility.
            /// </summary>
            public const string CodeCoverage = "DD_CIVISIBILITY_CODE_COVERAGE_ENABLED";

            /// <summary>
            /// Configuration key for re-signing assemblies after the Code Coverage modification.
            /// </summary>
            public const string CodeCoverageSnkFile = "DD_CIVISIBILITY_CODE_COVERAGE_SNK_FILEPATH";

            /// <summary>
            /// Configuration key for enabling or disabling jit optimizations in the Code Coverage
            /// </summary>
            public const string CodeCoverageEnableJitOptimizations = "DD_CIVISIBILITY_CODE_COVERAGE_ENABLE_JIT_OPTIMIZATIONS";

            /// <summary>
            /// Configuration key for selecting the code coverage mode LineExecution or LineCallCount
            /// </summary>
            public const string CodeCoverageMode = "DD_CIVISIBILITY_CODE_COVERAGE_MODE";

            /// <summary>
            /// Configuration key for setting the code coverage jsons destination path.
            /// </summary>
            public const string CodeCoveragePath = "DD_CIVISIBILITY_CODE_COVERAGE_PATH";

            /// <summary>
            /// Configuration key for enabling or disabling Uploading Git Metadata in CI Visibility
            /// Default Value is false (disabled)
            /// </summary>
            public const string GitUploadEnabled = "DD_CIVISIBILITY_GIT_UPLOAD_ENABLED";

            /// <summary>
            /// Configuration key for enabling or disabling Intelligent Test Runner test skipping feature in CI Visibility
            /// </summary>
            public const string TestsSkippingEnabled = "DD_CIVISIBILITY_TESTSSKIPPING_ENABLED";

            /// <summary>
            /// Configuration key for enabling or disabling Intelligent Test Runner in CI Visibility
            /// Default Value is false (disabled)
            /// </summary>
            public const string IntelligentTestRunnerEnabled = "DD_CIVISIBILITY_ITR_ENABLED";

            /// <summary>
            /// Configuration key for forcing Agent's EVP Proxy
            /// </summary>
            public const string ForceAgentsEvpProxy = "DD_CIVISIBILITY_FORCE_AGENT_EVP_PROXY";

            /// <summary>
            /// Configuration key for setting the external code coverage file path
            /// </summary>
            public const string ExternalCodeCoveragePath = "DD_CIVISIBILITY_EXTERNAL_CODE_COVERAGE_PATH";

            /// <summary>
            /// Configuration key for enabling or disabling Datadog.Trace GAC installation
            /// </summary>
            public const string InstallDatadogTraceInGac = "DD_CIVISIBILITY_GAC_INSTALL_ENABLED";

            /// <summary>
            /// Configuration key for enabling or disabling the early flake detection feature in CI Visibility
            /// </summary>
            public const string EarlyFlakeDetectionEnabled = "DD_CIVISIBILITY_EARLY_FLAKE_DETECTION_ENABLED";

            /// <summary>
            /// Configuration key for enabling or disabling the known tests feature in CI Visibility
            /// </summary>
            public const string KnownTestsEnabled = "DD_CIVISIBILITY_KNOWN_TESTS_ENABLED";

            /// <summary>
            /// Configuration key for setting the code coverage collector path
            /// </summary>
            public const string CodeCoverageCollectorPath = "DD_CIVISIBILITY_CODE_COVERAGE_COLLECTORPATH";

            /// <summary>
            /// Configuration key for set the rum flushing wait in milliseconds
            /// </summary>
            public const string RumFlushWaitMillis = "DD_CIVISIBILITY_RUM_FLUSH_WAIT_MILLIS";

            /// <summary>
            /// Configuration key for set the test session name
            /// </summary>
            public const string TestSessionName = "DD_TEST_SESSION_NAME";

            /// <summary>
            /// Configuration key for a kill-switch that allows to explicitly disable retries even if the remote setting is enabled.
            /// </summary>
            public const string FlakyRetryEnabled = "DD_CIVISIBILITY_FLAKY_RETRY_ENABLED";

            /// <summary>
            /// Configuration key for the maximum number of retry attempts for a single test case.
            /// </summary>
            public const string FlakyRetryCount = "DD_CIVISIBILITY_FLAKY_RETRY_COUNT";

            /// <summary>
            /// Configuration key for the maximum number of retry attempts for the entire session.
            /// </summary>
            public const string TotalFlakyRetryCount = "DD_CIVISIBILITY_TOTAL_FLAKY_RETRY_COUNT";

            /// <summary>
            /// Configuration key for a kill-switch that allows to explicitly disable dynamic instrumentation even if the remote setting is enabled.
            /// </summary>
            public const string DynamicInstrumentationEnabled = "DD_CIVISIBILITY_DI_ENABLED";

            /// <summary>
            /// Configuration key for enabling Impacted Tests Detection.
            /// </summary>
            public const string ImpactedTestsDetectionEnabled = "DD_CIVISIBILITY_IMPACTED_TESTS_DETECTION_ENABLED";

            /// <summary>
            /// Configuration key for enabling or disabling the Test Management feature.
            /// </summary>
            public const string TestManagementEnabled = "DD_TEST_MANAGEMENT_ENABLED";

            /// <summary>
            /// Configuration key for the number of retries to fix a flaky test.
            /// </summary>
            public const string TestManagementAttemptToFixRetries = "DD_TEST_MANAGEMENT_ATTEMPT_TO_FIX_RETRIES";
        }

        /// <summary>
        /// String constants for proxy configuration keys.
        /// </summary>
        public static class Proxy
        {
            /// <summary>
            /// Configuration key to set a proxy server for https requests.
            /// </summary>
            public const string ProxyHttps = "DD_PROXY_HTTPS";

            /// <summary>
            /// Configuration key to set a list of hosts that should bypass the proxy.
            /// The list is space-separated.
            /// </summary>
            public const string ProxyNoProxy = "DD_PROXY_NO_PROXY";
        }

        /// <summary>
        /// String constants for debug configuration keys.
        /// </summary>
        internal static class Debug
        {
            /// <summary>
            /// Configuration key for forcing the automatic instrumentation to only use the mdToken method lookup mechanism.
            /// </summary>
            public const string ForceMdTokenLookup = "DD_TRACE_DEBUG_LOOKUP_MDTOKEN";

            /// <summary>
            /// Configuration key for forcing the automatic instrumentation to only use the fallback method lookup mechanism.
            /// </summary>
            public const string ForceFallbackLookup = "DD_TRACE_DEBUG_LOOKUP_FALLBACK";
        }

        internal static class FeatureFlags
        {
            /// <summary>
            /// Feature Flag: enables updated resource names on `aspnet.request`, `aspnet-mvc.request`,
            /// `aspnet-webapi.request`, and `aspnet_core.request` spans. Enables `aspnet_core_mvc.request` spans and
            /// additional features on `aspnet_core.request` spans.
            /// </summary>
            /// <seealso cref="TracerSettings.RouteTemplateResourceNamesEnabled"/>
            public const string RouteTemplateResourceNamesEnabled = "DD_TRACE_ROUTE_TEMPLATE_RESOURCE_NAMES_ENABLED";

            /// <summary>
            /// Configuration key to enable or disable the updated WCF instrumentation that delays execution
            /// until later in the WCF pipeline when the WCF server exception handling is established.
            /// </summary>
            /// <seealso cref="TracerSettings.DelayWcfInstrumentationEnabled"/>
            public const string DelayWcfInstrumentationEnabled = "DD_TRACE_DELAY_WCF_INSTRUMENTATION_ENABLED";

            /// <summary>
            /// Configuration key to enable or disable improved template-based resource names
            /// when using WCF Web HTTP. Requires <see cref="DelayWcfInstrumentationEnabled"/> be set
            /// to true. Enabled by default
            /// </summary>
            /// <seealso cref="TracerSettings.WcfWebHttpResourceNamesEnabled"/>
            public const string WcfWebHttpResourceNamesEnabled = "DD_TRACE_WCF_WEB_HTTP_RESOURCE_NAMES_ENABLED";

            /// <summary>
            /// Feature flag to enable obfuscating the <c>LocalPath</c> of a WCF request that goes
            /// into the <c>resourceName</c> of a span.
            /// <para>Note: that this only applies when the WCF action is an empty string.</para>
            /// </summary>
            /// <seealso cref="TracerSettings.WcfObfuscationEnabled"/>
            public const string WcfObfuscationEnabled = "DD_TRACE_WCF_RESOURCE_OBFUSCATION_ENABLED";

            /// <summary>
            /// Enables a fix for header tags normalization.
            /// We used to normalize tag names even if they were specified in user configuration, but we should not.
            /// Default value is <c>true</c>.
            /// </summary>
            public const string HeaderTagsNormalizationFixEnabled = "DD_TRACE_HEADER_TAG_NORMALIZATION_FIX_ENABLED";

            /// <summary>
            /// Enables beta support for instrumentation via the System.Diagnostics API and the OpenTelemetry SDK.
            /// </summary>
            public const string OpenTelemetryEnabled = "DD_TRACE_OTEL_ENABLED";

            /// <summary>
            /// Enables experimental support for exporting OTLP metrics generated by the OpenTelemetry Metrics API.
            /// This feature is only available starting with .NET 6.0, as it relies on the BCL class MeterListener
            /// which is shipped in-box starting with .NET 6.
            /// </summary>
            public const string OpenTelemetryMetricsEnabled = "DD_METRICS_OTEL_ENABLED";

            /// <summary>
            /// Enables support for collecting and exporting logs generated by the the OpenTelemetry Logs API.
            /// This feature is available starting with .NET 3.1 when using Microsoft.Extensions.Logging.
            /// </summary>
            public const string OpenTelemetryLogsEnabled = "DD_LOGS_OTEL_ENABLED";

            /// <summary>
            /// List of meters to add to the metrics exporter for the experimental OpenTelemetry Metrics API support.
            /// </summary>
            public const string OpenTelemetryMeterNames = "DD_METRICS_OTEL_METER_NAMES";

            /// <summary>
            /// Enables generating 128-bit trace ids instead of 64-bit trace ids.
            /// Note that a 128-bit trace id may be received from an upstream service or from
            /// an Activity even if we are not generating them ourselves.
            /// Default value is <c>true</c> (enabled).
            /// </summary>
            public const string TraceId128BitGenerationEnabled = "DD_TRACE_128_BIT_TRACEID_GENERATION_ENABLED";

            /// <summary>
            /// Enables injecting 128-bit trace ids into logs as a hexadecimal string.
            /// If disabled, 128-bit trace ids will be truncated to the lower 64 bits,
            /// and injected as decimal strings. 64-bit trace ids are
            /// always injected as decimal strings, regardless of this setting.
            /// If unset, this configuration will take the value of the <see cref="ConfigurationKeys.FeatureFlags.TraceId128BitGenerationEnabled"/> configuration,
            /// which is <c>true</c> by default.
            /// Note: This configuration can be set independently of the <see cref="ConfigurationKeys.FeatureFlags.TraceId128BitGenerationEnabled"/> configuration,
            /// so it's possible to inject 128-bit trace ids into logs even if the application is only generating 64-bit trace ids, since distributed traces from upstream
            /// services may contain 128-bit trace ids.
            /// </summary>
            public const string TraceId128BitLoggingEnabled = "DD_TRACE_128_BIT_TRACEID_LOGGING_ENABLED";

            /// <summary>
            /// Configuration key to enabling or disabling the collection of shell commands executions.
            /// Default value is <c>false</c> (disabled). Will change in the future to <c>true</c>
            /// when an obfuscation mechanism will be implemented in the agent.
            /// </summary>
            internal const string CommandsCollectionEnabled = "DD_TRACE_COMMANDS_COLLECTION_ENABLED";

            public const string BypassHttpRequestUrlCachingEnabled = "DD_TRACE_BYPASS_HTTP_REQUEST_URL_CACHING_ENABLED";

            /// <summary>
            /// Configuration key to enable or disable the generation of an inferred span for proxy services.
            /// Enabling this will create a fake span based on provided proxy headers to allow for better
            /// observability when a client request routes through some proxy to an application.
            /// Default value is <c>false</c> (disabled).
            /// </summary>
            internal const string InferredProxySpansEnabled = "DD_TRACE_INFERRED_PROXY_SERVICES_ENABLED";

            /// <summary>
            /// Configuration key to enable or disable the injection of the Datadog trace context into stored procedures.
            /// Default value is <c>false</c> (enabled).
            /// When enabled, Datadog trace context will be injected into individual stored procedure calls when the following requirements are met:
            /// <ul>
            ///   <li>The database is Microsoft SQL Server and <see cref="DbmPropagationMode"/> is set to <c>service</c> or <c>full</c>.</li>
            ///   <li>The stored procedure call does not have <c>Output</c>, <c>InputOutput</c>, or <c>Return</c> ADO.NET command parameters.</li>
            /// </ul>
            /// </summary>
            public const string InjectContextIntoStoredProceduresEnabled = "DD_TRACE_INJECT_CONTEXT_INTO_STORED_PROCEDURES_ENABLED";
        }

        internal static class Telemetry
        {
            /// <summary>
            /// Configuration key for enabling or disabling internal telemetry.
            /// Default value is <c>true</c> (enabled).
            /// </summary>
            public const string Enabled = "DD_INSTRUMENTATION_TELEMETRY_ENABLED";

            /// <summary>
            /// Configuration key for sending telemetry direct to telemetry intake. If enabled, and
            /// <see cref="ConfigurationKeys.ApiKey"/> is set, sends telemetry direct to intake if agent is not
            /// available. Enabled by default if <see cref="ConfigurationKeys.ApiKey"/> is available.
            /// </summary>
            public const string AgentlessEnabled = "DD_INSTRUMENTATION_TELEMETRY_AGENTLESS_ENABLED";

            /// <summary>
            /// Configuration key for sending telemetry via agent proxy. If enabled, sends telemetry
            /// via agent proxy. Enabled by default. If disabled, or agent is not available, telemetry
            /// is sent to agentless endpoint, based on <see cref="AgentlessEnabled"/> setting.
            /// </summary>
            public const string AgentProxyEnabled = "DD_INSTRUMENTATION_TELEMETRY_AGENT_PROXY_ENABLED";

            /// <summary>
            /// Configuration key for the telemetry URL where the Tracer sends telemetry. Only applies when agentless
            /// telemetry is in use (otherwise telemetry is sent to the agent using
            /// <see cref="ExporterSettings.AgentUri"/> instead)
            /// </summary>
            public const string Uri = "DD_INSTRUMENTATION_TELEMETRY_URL";

            /// <summary>
            /// Configuration key for how often telemetry should be sent, in seconds. Must be between 1 and 3600.
            /// For testing purposes. Defaults to 60
            /// <see cref="TelemetrySettings.HeartbeatInterval"/>
            /// </summary>
            public const string HeartbeatIntervalSeconds = "DD_TELEMETRY_HEARTBEAT_INTERVAL";

            /// <summary>
            /// Configuration key for whether dependency data is sent via telemetry.
            /// Required for some ASM features. Default value is <c>true</c> (enabled).
            /// <see cref="TelemetrySettings.DependencyCollectionEnabled"/>
            /// </summary>
            public const string DependencyCollectionEnabled = "DD_TELEMETRY_DEPENDENCY_COLLECTION_ENABLED";

            /// <summary>
            /// Configuration key for whether telemetry metrics should be sent.
            /// <see cref="TelemetrySettings.MetricsEnabled"/>
            /// </summary>
            public const string MetricsEnabled = "DD_TELEMETRY_METRICS_ENABLED";

            /// <summary>
            /// Configuration key for whether to enable debug mode of telemetry.
            /// <see cref="TelemetrySettings.DebugEnabled"/>
            /// </summary>
            public const string DebugEnabled = "DD_INTERNAL_TELEMETRY_DEBUG_ENABLED";

            /// <summary>
            /// Configuration key to allow telemetry compression.
            /// <see cref="TelemetrySettings.CompressionMethod"/>
            /// </summary>
            public const string TelemetryCompressionMethod = "DD_INSTRUMENTATION_TELEMETRY_COMPRESSION_METHOD";

            /// <summary>
            /// Configuration key for whether to enable redacted error log collection.
            /// </summary>
            public const string TelemetryLogsEnabled = "DD_TELEMETRY_LOG_COLLECTION_ENABLED";
        }

        internal static class TagPropagation
        {
            /// <summary>
            /// Configuration key for the maximum length of an outgoing propagation header's value ("x-datadog-tags")
            /// when injecting it into downstream service calls.
            /// </summary>
            /// <remarks>
            /// This value is not used when extracting an incoming propagation header from an upstream service.
            /// </remarks>
            public const string HeaderMaxLength = "DD_TRACE_X_DATADOG_TAGS_MAX_LENGTH";
        }

        internal static class DataStreamsMonitoring
        {
            /// <summary>
            /// Enables data streams monitoring support
            /// </summary>
            /// <see cref="TracerSettings.IsDataStreamsMonitoringEnabled"/>
            public const string Enabled = "DD_DATA_STREAMS_ENABLED";

            /// <summary>
            /// Configuration key for enabling legacy binary headers in Data Streams Monitoring.
            /// false by default if DSM is in default state, true otherwise
            /// </summary>
             /// <see cref="TracerSettings.IsDataStreamsLegacyHeadersEnabled"/>
            public const string LegacyHeadersEnabled = "DD_DATA_STREAMS_LEGACY_HEADERS";
        }
    }
}
