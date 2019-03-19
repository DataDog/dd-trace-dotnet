using System;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Wraps a <see cref="IConfigurationSource"/> with strongly-typed
    /// properties for standard Datadog configuration values.
    /// </summary>
    public class TracerConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TracerConfiguration"/> class with default values.
        /// </summary>
        public TracerConfiguration()
            : this(null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TracerConfiguration"/> class
        /// using the specified <see cref="IConfigurationSource"/> to initialize values.
        /// </summary>
        /// <param name="source">The <see cref="IConfigurationSource"/> to use when retrieving configuration values.</param>
        public TracerConfiguration(IConfigurationSource source)
        {
            Environment = source?.GetString(ConfigurationKeys.Environment);

            ServiceName = source?.GetString(ConfigurationKeys.ServiceName);

            TraceEnabled = source?.GetBool(ConfigurationKeys.DebugEnabled) ??
                           // default value
                           false;

            DebugEnabled = source?.GetBool(ConfigurationKeys.DebugEnabled) ??
                           // default value
                           false;

            DisabledIntegrationNames = source?.GetString(ConfigurationKeys.DisabledIntegrations)
                                             ?.Split(';')
                                    ?? new string[0];

            AgentHost = source?.GetString(ConfigurationKeys.AgentHost) ??
                        // backwards compatibility for names used in the past
                        source?.GetString("DD_TRACE_AGENT_HOSTNAME") ??
                        source?.GetString("DATADOG_TRACE_AGENT_HOSTNAME") ??
                        // default value
                        "localhost";

            AgentPort = source?.GetInt32(ConfigurationKeys.AgentPort) ??
                        // backwards compatibility for names used in the past
                        source?.GetInt32("DATADOG_TRACE_AGENT_PORT") ??
                        // default value
                        8126;
        }

        /// <summary>
        /// Gets or sets the default environment name applied to all spans.
        /// </summary>
        public string Environment { get; set; }

        /// <summary>
        /// Gets or sets the service name applied to top-level spans and used to build derived service names.
        /// </summary>
        public string ServiceName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether tracing is enabled.
        /// </summary>
        public bool TraceEnabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether debug mode is enabled.
        /// </summary>
        public bool DebugEnabled { get; set; }

        /// <summary>
        /// Gets or sets the names of disabled integrations.
        /// </summary>
        public string[] DisabledIntegrationNames { get; set; }

        /// <summary>
        /// Gets or sets the host where the Tracer can connect to the Agent.
        /// </summary>
        public string AgentHost { get; set; }

        /// <summary>
        /// Gets or sets the TCP port where the Tracer can connect to the Agent.
        /// </summary>
        public int AgentPort { get; set; }
    }
}
