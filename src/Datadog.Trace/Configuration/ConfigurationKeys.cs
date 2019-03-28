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
    }
}
