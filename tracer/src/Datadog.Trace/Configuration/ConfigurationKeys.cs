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
        /// Configuration key for a list of integrations to disable. All other integrations remain enabled.
        /// Default is empty (all integrations are enabled).
        /// Supports multiple values separated with semi-colons.
        /// </summary>
        /// <seealso cref="TracerSettings.DisabledIntegrationNames"/>
        public const string DisabledIntegrations = "DD_DISABLED_INTEGRATIONS";

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
        /// Configuration key for a map of services to rename.
        /// </summary>
        /// <seealso cref="TracerSettings.ServiceNameMappings"/>
        public const string ServiceNameMappings = "DD_TRACE_SERVICE_MAPPING";

        /// <summary>
        /// Configuration key for setting the size in bytes of the trace buffer
        /// </summary>
        public const string BufferSize = "DD_TRACE_BUFFER_SIZE";

        /// <summary>
        /// Configuration key for setting the batch interval in milliseconds for the serialization queue
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
        /// Configuration key for enabling or disabling the diagnostic log at startup
        /// </summary>
        /// <seealso cref="TracerSettings.StartupDiagnosticLogEnabled"/>
        public const string StartupDiagnosticLogEnabled = "DD_TRACE_STARTUP_LOGS";

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
        /// Configuration key for setting the global rate for the sampler.
        /// </summary>
        public const string GlobalSamplingRate = "DD_TRACE_SAMPLE_RATE";

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
        /// Configuration key for setting the approximate maximum size,
        /// in bytes, for Tracer log files.
        /// Default value is 10 MB.
        /// </summary>
        public const string MaxLogFileSize = "DD_MAX_LOGFILE_SIZE";

        /// <summary>
        /// Configuration key for setting the number of seconds between,
        /// identical log messages, for Tracer log files.
        /// Default value is 60s. Setting to 0 disables rate limiting.
        /// </summary>
        public const string LogRateLimit = "DD_TRACE_LOGGING_RATE";

        /// <summary>
        /// Configuration key for setting the path to the .NET Tracer native log file.
        /// This also determines the output folder of the .NET Tracer managed log files.
        /// Overridden by <see cref="LogDirectory"/> if present.
        /// </summary>
        [Obsolete(DeprecationMessages.LogPath)]
        public const string ProfilerLogPath = "DD_TRACE_LOG_PATH";

        /// <summary>
        /// Configuration key for setting the directory of the .NET Tracer logs.
        /// Overrides the value in <see cref="ProfilerLogPath"/> if present.
        /// Default value is "%ProgramData%"\Datadog .NET Tracer\logs\" on Windows
        /// or "/var/log/datadog/dotnet/" on Linux.
        /// </summary>
        public const string LogDirectory = "DD_TRACE_LOG_DIRECTORY";

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
        /// Configuration key for setting the Application key, used by the ITR.
        /// </summary>
        public const string ApplicationKey = "DD_APPLICATION_KEY";

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
        public const string HttpServerErrorStatusCodes = "DD_HTTP_SERVER_ERROR_STATUSES";

        /// <summary>
        /// Configuration key for the application's client http statuses to set spans as errors by.
        /// </summary>
        /// <seealso cref="TracerSettings.HttpClientErrorStatusCodes"/>
        public const string HttpClientErrorStatusCodes = "DD_HTTP_CLIENT_ERROR_STATUSES";

        /// <summary>
        /// Configuration key indicating the optional name of the custom header to take into account to report the ip address from.
        /// If this variable is set all other IP related headers should be ignored
        /// Default is value is null (do not override).
        /// </summary>
        /// <seealso cref="TracerSettings.IpHeader"/>
        public const string IpHeader = "DD_TRACE_CLIENT_IP_HEADER";

        /// <summary>
        /// Configuration key indicating if the header should not be collected. The default for DD_TRACE_CLIENT_IP_HEADER_DISABLED is false.
        /// </summary>
        /// <seealso cref="TracerSettings.IpHeaderDisabled"/>
        public const string IpHeaderDisabled = "DD_TRACE_CLIENT_IP_HEADER_DISABLED";

        /// <summary>
        /// Configuration key to enable or disable the creation of a span context on exiting a successful Kafka
        /// Consumer.Consume() call, and closing the scope on entering Consumer.Consume().
        /// Default value is <c>true</c> (enabled).
        /// </summary>
        /// <seealso cref="TracerSettings.KafkaCreateConsumerScopeEnabled"/>
        public const string KafkaCreateConsumerScopeEnabled = "DD_TRACE_KAFKA_CREATE_CONSUMER_SCOPE_ENABLED";

        /// <summary>
        /// Configuration key for controlling whether route parameters in ASP.NET and ASP.NET Core resource names
        /// should be expanded with their values. Only applies when
        /// <see cref="ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled"/> is enabled.
        /// </summary>
        /// <seealso cref="TracerSettings.ExpandRouteTemplatesEnabled"/>
        public const string ExpandRouteTemplatesEnabled = "DD_TRACE_EXPAND_ROUTE_TEMPLATES_ENABLED";

        /// <summary>
        /// Configuration key for enabling computation of stats (aka trace metrics) on the tracer side
        /// </summary>
        public const string StatsComputationEnabled = "DD_TRACE_STATS_COMPUTATION_ENABLED";

        /// <summary>
        /// Configuration key for configuring the interval (in seconds) for sending stats (aka trace metrics)
        /// </summary>
        public const string StatsComputationInterval = "_DD_TRACE_STATS_COMPUTATION_INTERVAL";

        /// <summary>
        /// Configuration key for setting the propagation style injection.
        /// </summary>
        public const string PropagationStyleInject = "DD_PROPAGATION_STYLE_INJECT";

        /// <summary>
        /// Configuration key for setting the propagation style extraction.
        /// </summary>
        public const string PropagationStyleExtract = "DD_PROPAGATION_STYLE_EXTRACT";

        /// <summary>
        /// Configuration key for enabling automatic instrumentation on specified methods.
        /// Default value is "" (disabled).
        /// </summary>
        public const string TraceMethods = "DD_TRACE_METHODS";

        /// <summary>
        /// Configuration key for specifying a custom regex to obfuscate query strings.
        /// Default value is in TracerSettings
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
        /// Configuration key for enabling/disabling reporting query string
        /// Default value is true
        /// </summary>
        /// <seealso cref="TracerSettings.QueryStringReportingEnabled"/>
        public const string QueryStringReportingEnabled = "DD_HTTP_SERVER_TAG_QUERY_STRING";

        /// <summary>
        /// String constants for CI Visibility configuration keys.
        /// </summary>
        public static class CIVisibility
        {
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
        /// String format patterns used to match integration-specific configuration keys.
        /// </summary>
        public static class Integrations
        {
            /// <summary>
            /// Configuration key pattern for enabling or disabling an integration.
            /// </summary>
            public const string Enabled = "DD_TRACE_{0}_ENABLED";

            /// <summary>
            /// Configuration key pattern for enabling or disabling Analytics in an integration.
            /// </summary>
            [Obsolete(DeprecationMessages.AppAnalytics)]
            public const string AnalyticsEnabled = "DD_TRACE_{0}_ANALYTICS_ENABLED";

            /// <summary>
            /// Configuration key pattern for setting Analytics sampling rate in an integration.
            /// </summary>
            [Obsolete(DeprecationMessages.AppAnalytics)]
            public const string AnalyticsSampleRate = "DD_TRACE_{0}_ANALYTICS_SAMPLE_RATE";
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
            /// Feature flag to enable obfuscating the <c>LocalPath</c> of a WCF request that goes
            /// into the <c>resourceName</c> of a span.
            /// <para>Note: that this only applies when the WCF action is an empty string.</para>
            /// </summary>
            /// <seealso cref="TracerSettings.WcfObfuscationEnabled"/>
            public const string WcfObfuscationEnabled = "DD_TRACE_WCF_RESOURCE_OBFUSCATION_ENABLED";

            /// <summary>
            /// Enables a fix around header tags normalization.
            /// We used to normalize periods even if a tag was provided for a header, whereas we should not.
            /// This flag defaults to true and is here in case customers need retrocompatibility only
            /// </summary>
            public const string HeaderTagsNormalizationFixEnabled = "DD_TRACE_HEADER_TAG_NORMALIZATION_FIX_ENABLED";

            /// <summary>
            /// Enables experimental support for activity listener
            /// </summary>
            public const string ActivityListenerEnabled = "DD_TRACE_ACTIVITY_LISTENER_ENABLED";
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
            /// <see cref="ConfigurationKeys.ApiKey"/> is set, sends telemetry direct to intake. Otherwise, sends
            /// telemetry to Agent. Enabled by default if <see cref="ConfigurationKeys.ApiKey"/> is available.
            /// </summary>
            public const string AgentlessEnabled = "DD_INSTRUMENTATION_TELEMETRY_AGENTLESS_ENABLED";

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
        }
    }
}
