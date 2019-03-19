using System;
using System.IO;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Wraps a <see cref="IConfigurationSource"/> with strongly-typed
    /// properties for standard Datadog configuration values.
    /// </summary>
    public class TracerSettings
    {
        /// <summary>
        /// The default value for <see cref="AgentHost"/>.
        /// </summary>
        public const string DefaultAgentHost = "localhost";

        /// <summary>
        /// The default value for <see cref="AgentPort"/>.
        /// </summary>
        public const int DefaultAgentPort = 8126;

        /// <summary>
        /// Initializes a new instance of the <see cref="TracerSettings"/> class with default values.
        /// </summary>
        public TracerSettings()
            : this(null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TracerSettings"/> class
        /// using the specified <see cref="IConfigurationSource"/> to initialize values.
        /// </summary>
        /// <param name="source">The <see cref="IConfigurationSource"/> to use when retrieving configuration values.</param>
        public TracerSettings(IConfigurationSource source)
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
                        DefaultAgentHost;

            AgentPort = source?.GetInt32(ConfigurationKeys.AgentPort) ??
                        // backwards compatibility for names used in the past
                        source?.GetInt32("DATADOG_TRACE_AGENT_PORT") ??
                        DefaultAgentPort;
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
        /// Default is <c>true</c>.
        /// </summary>
        public bool TraceEnabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether debug mode is enabled.
        /// Default is <c>false</c>.
        /// </summary>
        public bool DebugEnabled { get; set; }

        /// <summary>
        /// Gets or sets the names of disabled integrations.
        /// </summary>
        public string[] DisabledIntegrationNames { get; set; }

        /// <summary>
        /// Gets or sets the host where the Tracer can connect to the Agent.
        /// Default is <c>"localhost"</c>.
        /// </summary>
        public string AgentHost { get; set; }

        /// <summary>
        /// Gets or sets the TCP port where the Tracer can connect to the Agent.
        /// Default is <c>8126</c>.
        /// </summary>
        public int AgentPort { get; set; }

        /// <summary>
        /// Gets or sets the Uri where the Tracer can connect to the Agent.
        /// Default is <c>"http://localhost:8126"</c>.
        /// </summary>
        public Uri AgentUri { get; set; }

        /// <summary>
        /// Create a <see cref="TracerSettings"/> populated from the default sources
        /// returned by <see cref="CreateDefaultConfigurationSource"/>.
        /// </summary>
        /// <returns>A <see cref="TracerSettings"/> populated from the default sources.</returns>
        public static TracerSettings FromDefaultSources()
        {
            var source = CreateDefaultConfigurationSource();
            return new TracerSettings(source);
        }

        /// <summary>
        /// Creates a <see cref="IConfigurationSource"/> by combining environment variables,
        /// AppSettings where available, and a local datadog.json file, if present.
        /// </summary>
        /// <returns>A new <see cref="IConfigurationSource"/> instance.</returns>
        public static CompositeConfigurationSource CreateDefaultConfigurationSource()
        {
            // env > AppSettings > datadog.json
            var configurationSource = new CompositeConfigurationSource
            {
                new EnvironmentConfigurationSource(),

#if !NETSTANDARD2_0
                // on .NET Framework only, also read from app.config/web.config
                new NameValueConfigurationSource(System.Configuration.ConfigurationManager.AppSettings)
#endif
            };

            // if environment variable is not set, look for default file name in the current directory
            var configurationFileName = configurationSource.GetString(ConfigurationKeys.ConfigurationFileName) ??
                                        Path.Combine(System.Environment.CurrentDirectory, "datadog.json");

            if (Path.GetExtension(configurationFileName).ToUpperInvariant() == ".JSON" &&
                File.Exists(configurationFileName))
            {
                configurationSource.Add(JsonConfigurationSource.LoadFile(configurationFileName));
            }

            return configurationSource;
        }
    }
}
