using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Sampling;

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

            ServiceName = source?.GetString(ConfigurationKeys.ServiceName) ??
                          // backwards compatibility for names used in the past
                          source?.GetString("DD_SERVICE_NAME");

            ServiceVersion = source?.GetString(ConfigurationKeys.ServiceVersion);

            TraceEnabled = source?.GetBool(ConfigurationKeys.TraceEnabled) ??
                           // default value
                           true;

            var disabledIntegrationNames = source?.GetString(ConfigurationKeys.DisabledIntegrations)
                                                 ?.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries) ??
                                           Enumerable.Empty<string>();

            DisabledIntegrationNames = new HashSet<string>(disabledIntegrationNames, StringComparer.OrdinalIgnoreCase);

            var agentHost = source?.GetString(ConfigurationKeys.AgentHost) ??
                            // backwards compatibility for names used in the past
                            source?.GetString("DD_TRACE_AGENT_HOSTNAME") ??
                            source?.GetString("DATADOG_TRACE_AGENT_HOSTNAME") ??
                            // default value
                            DefaultAgentHost;

            var agentPort = source?.GetInt32(ConfigurationKeys.AgentPort) ??
                            // backwards compatibility for names used in the past
                            source?.GetInt32("DATADOG_TRACE_AGENT_PORT") ??
                            // default value
                            DefaultAgentPort;

            var agentUri = source?.GetString(ConfigurationKeys.AgentUri) ??
                           // default value
                           $"http://{agentHost}:{agentPort}";

            AgentUri = new Uri(agentUri);

            AnalyticsEnabled = source?.GetBool(ConfigurationKeys.GlobalAnalyticsEnabled) ??
                               // default value
                               false;

            LogsInjectionEnabled = source?.GetBool(ConfigurationKeys.LogsInjectionEnabled) ??
                                   // default value
                                   false;

            var maxTracesPerSecond = source?.GetInt32(ConfigurationKeys.MaxTracesSubmittedPerSecond);

            if (maxTracesPerSecond != null)
            {
                // Ensure our flag for the rate limiter is enabled
                RuleBasedSampler.OptInTracingWithoutLimits();
            }
            else
            {
                maxTracesPerSecond = 100; // default
            }

            MaxTracesSubmittedPerSecond = maxTracesPerSecond.Value;

            Integrations = new IntegrationSettingsCollection(source);

            GlobalTags = source?.GetDictionary(ConfigurationKeys.GlobalTags) ??
                         // backwards compatibility for names used in the past
                         source?.GetDictionary("DD_TRACE_GLOBAL_TAGS") ??
                         // default value (empty)
                         new ConcurrentDictionary<string, string>();

            // Filter out tags with empty keys or empty values
            GlobalTags = GlobalTags.Where(kvp => !string.IsNullOrEmpty(kvp.Key) && !string.IsNullOrEmpty(kvp.Value))
                                   .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            DogStatsdPort = source?.GetInt32(ConfigurationKeys.DogStatsdPort) ??
                            // default value
                            8125;

            TracerMetricsEnabled = source?.GetBool(ConfigurationKeys.TracerMetricsEnabled) ??
                                   // default value
                                   false;

            CustomSamplingRules = source?.GetString(ConfigurationKeys.CustomSamplingRules);

            GlobalSamplingRate = source?.GetDouble(ConfigurationKeys.GlobalSamplingRate);

            DiagnosticSourceEnabled = source?.GetBool(ConfigurationKeys.DiagnosticSourceEnabled) ??
                                      // default value
                                      true;
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
        /// Gets or sets the version tag applied to all spans.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.ServiceVersion"/>
        public string ServiceVersion { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether tracing is enabled.
        /// Default is <c>true</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.TraceEnabled"/>
        public bool TraceEnabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether debug is enabled for a tracer.
        /// This property is obsolete. Manage the debug setting through GlobalSettings.
        /// </summary>
        /// <seealso cref="GlobalSettings.DebugEnabled"/>
        [Obsolete]
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
        /// Gets or sets a value indicating the maximum number of traces set to AutoKeep (p1) per second.
        /// Default is <c>100</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.MaxTracesSubmittedPerSecond"/>
        public int MaxTracesSubmittedPerSecond { get; set; }

        /// <summary>
        /// Gets or sets a value indicating custom sampling rules.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.CustomSamplingRules"/>
        public string CustomSamplingRules { get; set; }

        /// <summary>
        /// Gets or sets a value indicating a global rate for sampling.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.GlobalSamplingRate"/>
        public double? GlobalSamplingRate { get; set; }

        /// <summary>
        /// Gets a collection of <see cref="Integrations"/> keyed by integration name.
        /// </summary>
        public IntegrationSettingsCollection Integrations { get; }

        /// <summary>
        /// Gets or sets the global tags, which are applied to all <see cref="Span"/>s.
        /// </summary>
        public IDictionary<string, string> GlobalTags { get; set; }

        /// <summary>
        /// Gets or sets the port where the DogStatsd server is listening for connections.
        /// Default is <c>8125</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.DogStatsdPort"/>
        public int DogStatsdPort { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether internal metrics
        /// are enabled and sent to DogStatsd.
        /// </summary>
        public bool TracerMetricsEnabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the use
        /// of <see cref="System.Diagnostics.DiagnosticSource"/> is enabled.
        /// </summary>
        public bool DiagnosticSourceEnabled { get; set; }

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
            return GlobalSettings.CreateDefaultConfigurationSource();
        }

        internal bool IsIntegrationEnabled(string name)
        {
            bool disabled = Integrations[name].Enabled == false || DisabledIntegrationNames.Contains(name);
            return TraceEnabled && !disabled;
        }

        internal bool IsOptInIntegrationEnabled(string name)
        {
            bool disabled = Integrations[name].Enabled != true || DisabledIntegrationNames.Contains(name);
            return TraceEnabled && !disabled;
        }

        internal double? GetIntegrationAnalyticsSampleRate(string name, bool enabledWithGlobalSetting)
        {
            var integrationSettings = Integrations[name];
            var analyticsEnabled = integrationSettings.AnalyticsEnabled ?? (enabledWithGlobalSetting && AnalyticsEnabled);
            return analyticsEnabled ? integrationSettings.AnalyticsSampleRate : (double?)null;
        }
    }
}
