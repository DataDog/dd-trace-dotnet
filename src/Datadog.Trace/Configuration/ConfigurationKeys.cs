namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// String constants for standard Datadog configuration keys.
    /// </summary>
    public static class ConfigurationKeys
    {
        /// <summary>
        /// The application's environment. Sets the "env" tag on every <see cref="Span"/>.
        /// </summary>
        public const string Environment = "DD_ENV";

        /// <summary>
        /// The application's default service name. Used as the service name for top-level spans,
        /// and used to determine service name of some child spans.
        /// </summary>
        public const string ServiceName = "DD_SERVICE_NAME";

        /// <summary>
        /// Enables the Tracer's debug mode.
        /// </summary>
        public const string DebugEnabled = "DD_TRACE_DEBUG";

        /// <summary>
        /// Sets the Agent host where the Tracer can send traces.
        /// </summary>
        public const string AgentHost = "DD_AGENT_HOST";

        /// <summary>
        /// Sets the Agent port where the Tracer can send traces.
        /// </summary>
        public const string AgentPort = "DD_TRACE_AGENT_PORT";
    }
}
