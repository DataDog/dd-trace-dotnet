using System;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Wraps a <see cref="IConfigurationSource"/> with strongly-typed
    /// properties for standard Datadog configuration values.
    /// </summary>
    public class Configuration
    {
        private readonly IConfigurationSource _source;

        /// <summary>
        /// Initializes a new instance of the <see cref="Configuration"/> class.
        /// </summary>
        /// <param name="source">The <see cref="IConfigurationSource"/> to use when retrieving configuration values.</param>
        public Configuration(IConfigurationSource source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        /// <summary>
        /// Gets the default environment name applied to all spans.
        /// </summary>
        public string Environment => _source.GetString(ConfigurationKeys.Environment);

        /// <summary>
        /// Gets the service name applied to top-level spans and used to build derived service names.
        /// </summary>
        public string ServiceName => _source.GetString(ConfigurationKeys.ServiceName);

        /// <summary>
        /// Gets a value indicating whether debug mode is enabled.
        /// </summary>
        public bool DebugEnabled => _source.GetBool(ConfigurationKeys.DebugEnabled) ??
                                    // default value
                                    false;

        /// <summary>
        /// Gets the host where the Tracer can connect to the Agent.
        /// </summary>
        public string AgentHost => _source.GetString(ConfigurationKeys.AgentHost) ??
                                   // backwards compatibility for names used in the past
                                   _source.GetString("DD_TRACE_AGENT_HOSTNAME") ??
                                   _source.GetString("DATADOG_TRACE_AGENT_HOSTNAME") ??
                                   // default value
                                   "localhost";

        /// <summary>
        /// Gets the TCP port where the Tracer can connect to the Agent.
        /// </summary>
        public int AgentPort => _source.GetInt32(ConfigurationKeys.AgentPort) ??
                                // backwards compatibility for names used in the past
                                _source.GetInt32("DATADOG_TRACE_AGENT_PORT") ??
                                // default value
                                8126;
    }
}
