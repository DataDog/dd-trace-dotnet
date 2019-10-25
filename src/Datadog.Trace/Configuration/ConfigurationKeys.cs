namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// String constants for standard Datadog configuration keys.
    /// </summary>
    public static class ConfigurationKeys
    {
        /// <summary>
        /// Configuration key for the path to the configuration file.
        /// Can only be set with an environment variable
        /// or in the <c>app.config</c>/<c>web.config</c> file.
        /// </summary>
        public const string ConfigurationFileName = "DD_DOTNET_TRACER_CONFIG_FILE";

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
        public const string ServiceName = "DD_SERVICE_NAME";

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
        /// <seealso cref="TracerSettings.DebugEnabled"/>
        public const string DebugEnabled = "DD_TRACE_DEBUG";

        /// <summary>
        /// Configuration key for a list of integrations to disable. All other integrations remain enabled.
        /// Default is empty (all integrations are enabled).
        /// Supports multiple values separated with semi-colons.
        /// </summary>
        /// <seealso cref="TracerSettings.DisabledIntegrationNames"/>
        public const string DisabledIntegrations = "DD_DISABLED_INTEGRATIONS";

        /// <summary>
        /// Configuration key for the Agent host where the Tracer can send traces.
        /// Overriden by <see cref="AgentUri"/> if present.
        /// Default value is "localhost".
        /// </summary>
        /// <seealso cref="TracerSettings.AgentUri"/>
        public const string AgentHost = "DD_AGENT_HOST";

        /// <summary>
        /// Configuration key for the Agent port where the Tracer can send traces.
        /// Default value is 8126.
        /// </summary>
        /// <seealso cref="TracerSettings.AgentUri"/>
        public const string AgentPort = "DD_TRACE_AGENT_PORT";

        /// <summary>
        /// Configuration key for the Agent URL where the Tracer can send traces.
        /// Overrides values in <see cref="AgentHost"/> and <see cref="AgentPort"/> if present.
        /// Default value is "http://localhost:8126".
        /// </summary>
        /// <seealso cref="TracerSettings.AgentUri"/>
        public const string AgentUri = "DD_TRACE_AGENT_URL";

        /// <summary>
        /// Configuration key for enabling or disabling default Analytics.
        /// </summary>
        /// <seealso cref="TracerSettings.AnalyticsEnabled"/>
        public const string GlobalAnalyticsEnabled = "DD_TRACE_ANALYTICS_ENABLED";

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
        public const string MaxTracesSubmittedPerSecond = "DD_MAX_TRACES_PER_SECOND";

        /// <summary>
        /// Configuration key for setting custom sampling rules based on regular expressions.
        /// Semi-colon separated list of sampling rules.
        /// The rule is matched in order of specification. The first match in a list is used.
        ///
        /// Per entry:
        ///   The item 'rate' is required in decimal format.
        ///   The item 'service' is optional in regular expression format, to match on service name.
        ///   The item 'operation' is optional in regular expression format, to match on operation name.
        ///
        /// Example:
        ///   'rate:0.5, service:cart.* ; rate:0.2, operation:web ; rate:1.0, service:background, operation:insert ; rate:0.1'
        ///
        /// The example above will match every trace in services that start with the name cart and give them a 50% sample rate.
        /// If a trace does not come from a service starting with the name cart, but it has an operation name of web, it will have a 20% sample rate applied.
        /// If a trace matches neither of those, and has a service name of background and an operation name of insert, 100% will be applied.
        /// Finally, if there are no matches, the last rule of 10% will be applied.
        ///
        /// If no rules are specified, or none match, default internal sampling logic will be used.
        /// </summary>
        /// <seealso cref="TracerSettings.CustomSamplingRules"/>
        public const string CustomSamplingRules = "DD_CUSTOM_SAMPLING_RULES";

        /// <summary>
        /// String format patterns used to match integration-specific configuration keys.
        /// </summary>
        public static class Integrations
        {
            /// <summary>
            /// Configuration key pattern for enabling or disabling an integration.
            /// </summary>
            public const string Enabled = "DD_{0}_ENABLED";

            /// <summary>
            /// Configuration key pattern for enabling or disabling Analytics in an integration.
            /// </summary>
            public const string AnalyticsEnabled = "DD_{0}_ANALYTICS_ENABLED";

            /// <summary>
            /// Configuration key pattern for setting Analytics sampling rate in an integration.
            /// </summary>
            public const string AnalyticsSampleRate = "DD_{0}_ANALYTICS_SAMPLE_RATE";
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
    }
}
