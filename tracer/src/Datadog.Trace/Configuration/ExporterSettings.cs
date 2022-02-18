// <copyright file="ExporterSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Contains exporter settings.
    /// </summary>
    public class ExporterSettings
    {
        private int _partialFlushMinSpans;

        /// <summary>
        /// The default host value for <see cref="AgentUri"/>.
        /// </summary>
        public const string DefaultAgentHost = "localhost";

        /// <summary>
        /// The default port value for <see cref="AgentUri"/>.
        /// </summary>
        public const int DefaultAgentPort = 8126;

        /// <summary>
        /// The default port value for dogstatsd.
        /// </summary>
        internal const int DefaultDogstatsdPort = 8125;

        /// <summary>
        /// Prefix for unix domain sockets.
        /// </summary>
        internal const string UnixDomainSocketPrefix = "unix://";

        /// <summary>
        /// Default traces UDS path.
        /// </summary>
        internal const string DefaultTracesUnixDomainSocket = "/var/run/datadog/apm.socket";

        /// <summary>
        /// Default metrics UDS path.
        /// </summary>
        internal const string DefaultMetricsUnixDomainSocket = "/var/run/datadog/dsd.socket";

        /// <summary>
        /// Initializes a new instance of the <see cref="ExporterSettings"/> class with default values.
        /// </summary>
        public ExporterSettings()
            : this(null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExporterSettings"/> class
        /// using the specified <see cref="IConfigurationSource"/> to initialize values.
        /// </summary>
        /// <param name="source">The <see cref="IConfigurationSource"/> to use when retrieving configuration values.</param>
        public ExporterSettings(IConfigurationSource source)
        {
            // The settings here should not do further checks that the setter of the API does (thus only the type should be checked basically)
            // All the logic to assign the transport will be in the ImmutableExporterSettings .ctor

            var traceAgentUrl = source?.GetString(ConfigurationKeys.AgentUri);
            if (!string.IsNullOrWhiteSpace(traceAgentUrl))
            {
                if (!Uri.TryCreate(traceAgentUrl, UriKind.RelativeOrAbsolute, out var uri))
                {
                    ValidationWarnings.Add($"The provided Uri: ${traceAgentUrl} provided in '{ConfigurationKeys.AgentUri}' is not valid. It won't be taken into account to send traces.");
                }
                else
                {
                    AgentUri = uri;
                }
            }

            TracesUnixDomainSocketPath = source?.GetString(ConfigurationKeys.TracesUnixDomainSocketPath);
            TracesPipeName = source?.GetString(ConfigurationKeys.TracesPipeName);
            TracesPipeTimeoutMs = source?.GetInt32(ConfigurationKeys.TracesPipeTimeoutMs) ?? 0;
            AgentHost = source?.GetString(ConfigurationKeys.AgentHost) ??
                        // backwards compatibility for names used in the past
                        source?.GetString("DD_TRACE_AGENT_HOSTNAME") ??
                        source?.GetString("DATADOG_TRACE_AGENT_HOSTNAME");

            AgentPort = source?.GetInt32(ConfigurationKeys.AgentPort) ??
                        // backwards compatibility for names used in the past
                        source?.GetInt32("DATADOG_TRACE_AGENT_PORT");

            DogStatsdPort = source?.GetInt32(ConfigurationKeys.DogStatsdPort) ?? 0;
            MetricsPipeName = source?.GetString(ConfigurationKeys.MetricsPipeName);
            MetricsUnixDomainSocketPath = source?.GetString(ConfigurationKeys.MetricsUnixDomainSocketPath);

            PartialFlushEnabled = source?.GetBool(ConfigurationKeys.PartialFlushEnabled) ?? false;
            var partialFlushMinSpans = source?.GetInt32(ConfigurationKeys.PartialFlushMinSpans);
            if (partialFlushMinSpans > 0)
            {
                PartialFlushMinSpans = partialFlushMinSpans.Value;
            }
        }

        /// <summary>
        /// Gets or sets the Uri where the Tracer can connect to the Agent to send Traces
        /// Default is <c>"http://localhost:8126"</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.AgentUri"/>
        /// <seealso cref="ConfigurationKeys.AgentHost"/>
        /// <seealso cref="ConfigurationKeys.AgentPort"/>
        public Uri AgentUri { get; set; }

        /// <summary>
        /// Gets or sets the windows pipe name where the Tracer can connect to the Agent.
        /// Default is <c>null</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.TracesPipeName"/>
        public string TracesPipeName { get; set; }

        /// <summary>
        /// Gets or sets the timeout in milliseconds for the windows named pipe requests.
        /// Default is <c>100</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.TracesPipeTimeoutMs"/>
        public int TracesPipeTimeoutMs { get; set; }

        /// <summary>
        /// Gets or sets the windows pipe name where the Tracer can send stats.
        /// Default is <c>null</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.MetricsPipeName"/>
        public string MetricsPipeName { get; set; }

        /// <summary>
        /// Gets or sets the unix domain socket path where the Tracer can connect to the Agent.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.TracesUnixDomainSocketPath"/>
        public string TracesUnixDomainSocketPath { get; set; }

        /// <summary>
        /// Gets or sets the unix domain socket path where the Tracer can send stats.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.MetricsUnixDomainSocketPath"/>
        public string MetricsUnixDomainSocketPath { get; set; }

        /// <summary>
        /// Gets or sets the port where the DogStatsd server is listening for connections.
        /// Default is <c>8125</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.DogStatsdPort"/>
        public int DogStatsdPort { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether partial flush is enabled
        /// </summary>
        public bool PartialFlushEnabled { get; set; }

        /// <summary>
        /// Gets or sets the minimum number of closed spans in a trace before it's partially flushed
        /// </summary>
        public int PartialFlushMinSpans
        {
            get => _partialFlushMinSpans;
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentException("The value must be strictly greater than 0", nameof(PartialFlushMinSpans));
                }

                _partialFlushMinSpans = value;
            }
        }

        /// <summary>
        /// Gets or sets the agent host that can be used to reach dogStatsD
        /// Default is <c>"localhost"</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.AgentHost"/>
        internal string AgentHost { get; set; }

        /// <summary>
        /// Gets the agent port that can be used to send traces.
        /// It is not the preferred way to set the transport, thus why it's internal
        /// </summary>
        /// <seealso cref="ConfigurationKeys.AgentPort"/>
        internal int? AgentPort { get; }

        internal List<string> ValidationWarnings { get; }
    }
}
