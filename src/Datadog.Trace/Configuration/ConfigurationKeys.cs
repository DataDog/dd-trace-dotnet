namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// String constants for standard Datadog configuration keys.
    /// </summary>
    public static class ConfigurationKeys
    {
        /// <summary>
        /// Configuration key for the path to the configuration file.
        /// Can only be set with an environment variable.
        /// </summary>
        public const string ConfigurationFileName = "DD_DOTNET_TRACER_CONFIG_FILE";

        /// <summary>
        /// Configuration key for the application's environment. Sets the "env" tag on every <see cref="Span"/>.
        /// </summary>
        public const string Environment = "DD_ENV";

        /// <summary>
        /// Configuration key for the application's default service name.
        /// Used as the service name for top-level spans,
        /// and used to determine service name of some child spans.
        /// </summary>
        public const string ServiceName = "DD_SERVICE_NAME";

        /// <summary>
        /// Configuration key for enabling or disabling the Tracer.
        /// Default is enabled.
        /// </summary>
        public const string TraceEnabled = "DD_TRACE_ENABLED";

        /// <summary>
        /// Configuration key for enabling or disabling the Tracer's debug mode.
        /// Default is disabled.
        /// </summary>
        public const string DebugEnabled = "DD_TRACE_DEBUG";

        /// <summary>
        /// Configuration key for a list of integrations to disable. All other integrations remain enabled.
        /// Default is empty (all integrations are enabled).
        /// Supports multiple values separated with semi-colons.
        /// </summary>
        public const string DisabledIntegrations = "DD_DISABLED_INTEGRATIONS";

        /// <summary>
        /// Configuration key for the Agent host where the Tracer can send traces.
        /// Default is "localhost".
        /// </summary>
        public const string AgentHost = "DD_AGENT_HOST";

        /// <summary>
        /// Configuration key for the Agent port where the Tracer can send traces.
        /// Default is 8126.
        /// </summary>
        public const string AgentPort = "DD_TRACE_AGENT_PORT";

        /// <summary>
        /// Configuration key for the Agent URL where the Tracer can send traces.
        /// Default is "http://localhost:8126".
        /// </summary>
        public const string AgentUri = "DD_TRACE_AGENT_URL";
    }
}
