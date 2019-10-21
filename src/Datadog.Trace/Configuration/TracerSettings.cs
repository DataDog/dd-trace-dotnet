using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Contains Tracer settings.
    /// </summary>
    public class TracerSettings
    {
        /// <summary>
        /// The default host value for <see cref="AgentUri"/>.
        /// </summary>
        public const string DefaultAgentHost = "localhost";

        /// <summary>
        /// The default port value for <see cref="AgentUri"/>.
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

            TraceEnabled = source?.GetBool(ConfigurationKeys.TraceEnabled) ??
                           // default value
                           true;

            DebugEnabled = source?.GetBool(ConfigurationKeys.DebugEnabled) ??
                           // default value
                           false;

            var disabledIntegrationNames = source?.GetString(ConfigurationKeys.DisabledIntegrations)
                                                 ?.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries) ??
                                           Enumerable.Empty<string>();

            DisabledIntegrationNames = new HashSet<string>(disabledIntegrationNames, StringComparer.OrdinalIgnoreCase);

            var agentHost = source?.GetString(ConfigurationKeys.AgentHost) ??
                            // backwards compatibility for names used in the past
                            source?.GetString("DD_TRACE_AGENT_HOSTNAME") ??
                            source?.GetString("DATADOG_TRACE_AGENT_HOSTNAME") ??
                            DefaultAgentHost;

            var agentPort = source?.GetInt32(ConfigurationKeys.AgentPort) ??
                            // backwards compatibility for names used in the past
                            source?.GetInt32("DATADOG_TRACE_AGENT_PORT") ??
                            DefaultAgentPort;

            var agentUri = source?.GetString(ConfigurationKeys.AgentUri) ??
                           $"http://{agentHost}:{agentPort}";

            AgentUri = new Uri(agentUri);

            AnalyticsEnabled = source?.GetBool(ConfigurationKeys.GlobalAnalyticsEnabled) ??
                               false;

            LogsInjectionEnabled = source?.GetBool(ConfigurationKeys.LogsInjectionEnabled) ??
                               false;

            Integrations = new IntegrationSettingsCollection(source);

            var gTags = source?.GetString(ConfigurationKeys.GlobalTags);

            if (gTags == null)
            {
                GlobalTags = new Dictionary<string, string>();
            }
            else
            {
                GlobalTags = JsonConvert.DeserializeObject<Dictionary<string, string>>(gTags);
            }
        }

        /// <summary>
        /// Gets or sets the default environment name applied to all spans.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.Environment"/>
        public string Environment { get; set; }

        /// <summary>
        /// Gets or sets the service name applied to top-level spans and used to build derived service names.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.ServiceName"/>
        public string ServiceName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether tracing is enabled.
        /// Default is <c>true</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.TraceEnabled"/>
        public bool TraceEnabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether debug mode is enabled.
        /// Default is <c>false</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.DebugEnabled"/>
        public bool DebugEnabled { get; set; }

        /// <summary>
        /// Gets or sets the names of disabled integrations.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.DisabledIntegrations"/>
        public HashSet<string> DisabledIntegrationNames { get; set; }

        /// <summary>
        /// Gets or sets the Uri where the Tracer can connect to the Agent.
        /// Default is <c>"http://localhost:8126"</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.AgentUri"/>
        /// <seealso cref="ConfigurationKeys.AgentHost"/>
        /// <seealso cref="ConfigurationKeys.AgentPort"/>
        public Uri AgentUri { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether default Analytics are enabled.
        /// Settings this value is a shortcut for setting
        /// <see cref="Configuration.IntegrationSettings.AnalyticsEnabled"/> on some predetermined integrations.
        /// See the documentation for more details.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.GlobalAnalyticsEnabled"/>
        public bool AnalyticsEnabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether correlation identifiers are
        /// automatically injected into the logging context.
        /// Default is <c>false</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.LogsInjectionEnabled"/>
        public bool LogsInjectionEnabled { get; set; }

        /// <summary>
        /// Gets a collection of <see cref="Integrations"/> keyed by integration name.
        /// </summary>
        public IntegrationSettingsCollection Integrations { get; }

        /// <summary>
        /// Gets or sets the global tags, which are applied to all <see cref="Span"/>s.
        /// </summary>
        public Dictionary<string, string> GlobalTags { get; set; }

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

            string currentDirectory = System.Environment.CurrentDirectory;

#if !NETSTANDARD2_0
            // on .NET Framework only, use application's root folder
            // as default path when looking for datadog.json
            if (System.Web.Hosting.HostingEnvironment.IsHosted)
            {
                currentDirectory = System.Web.Hosting.HostingEnvironment.MapPath("~");
            }
#endif

            // if environment variable is not set, look for default file name in the current directory
            var configurationFileName = configurationSource.GetString(ConfigurationKeys.ConfigurationFileName) ??
                                        Path.Combine(currentDirectory, "datadog.json");

            if (Path.GetExtension(configurationFileName).ToUpperInvariant() == ".JSON" &&
                File.Exists(configurationFileName))
            {
                configurationSource.Add(JsonConfigurationSource.FromFile(configurationFileName));
            }

            return configurationSource;
        }

        internal bool IsIntegrationEnabled(string name)
        {
            return TraceEnabled &&
                   Integrations[name].Enabled != false &&
                   !DisabledIntegrationNames.Contains(name);
        }

        internal double? GetIntegrationAnalyticsSampleRate(string name, bool enabledWithGlobalSetting)
        {
            var integrationSettings = Integrations[name];
            var analyticsEnabled = integrationSettings.AnalyticsEnabled ?? (enabledWithGlobalSetting && AnalyticsEnabled);
            return analyticsEnabled ? integrationSettings.AnalyticsSampleRate : (double?)null;
        }
    }
}
