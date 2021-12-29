// <copyright file="ExporterSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using Datadog.Trace.Agent;
using MetricsTransportType = Datadog.Trace.Vendors.StatsdClient.Transport.TransportType;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Contains exporter settings.
    /// </summary>
    public class ExporterSettings
    {
        private int _partialFlushMinSpans;

        /// <summary>
        /// Allows overriding of file system access for tests.
        /// </summary>
        private Func<string, bool> _fileExists;

        /// <summary>
        /// The flag used to determine if there is an explicit host configured for traces and metrics.
        /// </summary>
        private bool hasExplicitAgentHost;

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
            : this(source, File.Exists)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExporterSettings"/> class.
        /// Direct use in tests only.
        /// </summary>
        internal ExporterSettings(IConfigurationSource source, Func<string, bool> fileExists)
        {
            _fileExists = fileExists;

            // It is important that the trace transport runs first to determine if there is an explicit host
            ConfigureTraceTransport(source);
            ConfigureMetricsTransport(source);

            PartialFlushEnabled = source?.GetBool(ConfigurationKeys.PartialFlushEnabled)
                // default value
                ?? false;

            var partialFlushMinSpans = source?.GetInt32(ConfigurationKeys.PartialFlushMinSpans);

            if ((partialFlushMinSpans ?? 0) <= 0)
            {
                partialFlushMinSpans = 500;
            }

            PartialFlushMinSpans = partialFlushMinSpans.Value;
        }

        /// <summary>
        /// Gets or sets the Uri where the Tracer can connect to the Agent.
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
        /// Gets or sets the transport used to send traces to the Agent.
        /// </summary>
        internal TracesTransportType TracesTransport { get; set; }

        /// <summary>
        /// Gets or sets the transport used to connect to the DogStatsD.
        /// Default is <c>TransportStrategy.Tcp</c>.
        /// </summary>
        internal MetricsTransportType MetricsTransport { get; set; }

        private void ConfigureMetricsTransport(IConfigurationSource source)
        {
            MetricsTransportType? metricsTransport = null;

            var dogStatsdPort = source?.GetInt32(ConfigurationKeys.DogStatsdPort);

            MetricsPipeName = source?.GetString(ConfigurationKeys.MetricsPipeName);

            // Agent port is set to zero in places like AAS where it's needed to prevent port conflict.
            // The agent will fail to start if it can not bind a port.
            // If the dogstatsd port isn't explicitly configured, check for pipes or sockets.
            if (!hasExplicitAgentHost && (dogStatsdPort == 0 || dogStatsdPort == null))
            {
                if (!string.IsNullOrWhiteSpace(MetricsPipeName))
                {
                    metricsTransport = MetricsTransportType.NamedPipe;
                }
                else
                {
                    // Check for UDS
                    var metricsUnixDomainSocketPath = source?.GetString(ConfigurationKeys.MetricsUnixDomainSocketPath);
                    if (metricsUnixDomainSocketPath != null)
                    {
                        metricsTransport = MetricsTransportType.UDS;
                        MetricsUnixDomainSocketPath = metricsUnixDomainSocketPath;
                    }
                    else if (_fileExists(DefaultMetricsUnixDomainSocket))
                    {
                        metricsTransport = MetricsTransportType.UDS;
                        MetricsUnixDomainSocketPath = DefaultMetricsUnixDomainSocket;
                    }
                }
            }

            if (metricsTransport == null)
            {
                // UDP if nothing explicit was configured or a port is set
                DogStatsdPort = dogStatsdPort ?? DefaultDogstatsdPort;
                metricsTransport = MetricsTransportType.UDP;
            }

            MetricsTransport = metricsTransport.Value;
        }

        private void ConfigureTraceTransport(IConfigurationSource source)
        {
            TracesTransportType? traceTransport = null;

            var agentHost = source?.GetString(ConfigurationKeys.AgentHost) ??
                            source?.GetString("DD_TRACE_AGENT_URL") ??
                            // backwards compatibility for names used in the past
                            source?.GetString("DD_TRACE_AGENT_HOSTNAME") ??
                            source?.GetString("DATADOG_TRACE_AGENT_HOSTNAME");

            var agentPort = source?.GetInt32(ConfigurationKeys.AgentPort) ??
                            // backwards compatibility for names used in the past
                            source?.GetInt32("DATADOG_TRACE_AGENT_PORT");

            TracesPipeName = source?.GetString(ConfigurationKeys.TracesPipeName);

            // Agent port is set to zero in places like AAS where it's needed to prevent port conflict
            // The agent will fail to start if it can not bind a port
            var hasExplicitAgentHostVariable = (agentPort != null && agentPort != 0) || agentHost != null;
            var isConventionBasedSocket = false;

            if (hasExplicitAgentHostVariable)
            {
                // If there is any explicit configuration for TCP, prioritize it
                if (agentHost?.StartsWith(UnixDomainSocketPrefix) ?? false)
                {
                    isConventionBasedSocket = true;
                    hasExplicitAgentHostVariable = false;
                }
                else
                {
                    hasExplicitAgentHost = true;
                    traceTransport = TracesTransportType.Default;
                }
            }
            else if (!string.IsNullOrWhiteSpace(TracesPipeName))
            {
                traceTransport = TracesTransportType.WindowsNamedPipe;

                TracesPipeTimeoutMs = source?.GetInt32(ConfigurationKeys.TracesPipeTimeoutMs)
#if DEBUG
                    ?? 20_000;
#else
                    ?? 500;
#endif
            }

            if (traceTransport == null)
            {
                // Check for UDS
                if (isConventionBasedSocket)
                {
                    traceTransport = TracesTransportType.UnixDomainSocket;
                    TracesUnixDomainSocketPath = agentHost;
                }
                else
                {
                    // check for explicit UDS config
                    var traceSocket = source?.GetString(ConfigurationKeys.TracesUnixDomainSocketPath);

                    if (traceSocket != null)
                    {
                        traceTransport = TracesTransportType.UnixDomainSocket;
                        TracesUnixDomainSocketPath = traceSocket;
                    }
                    else
                    {
                        // check for default file
                        if (_fileExists(DefaultTracesUnixDomainSocket))
                        {
                            traceTransport = TracesTransportType.UnixDomainSocket;
                            TracesUnixDomainSocketPath = DefaultTracesUnixDomainSocket;
                        }
                    }
                }
            }

            // Still build the Uri no matter what the transport as we send it in the http message
            agentHost = agentHost ?? DefaultAgentHost;
            agentPort = agentPort ?? DefaultAgentPort;

            var agentUri = source?.GetString(ConfigurationKeys.AgentUri) ??
                           // default value
                           $"http://{agentHost}:{agentPort}";

            AgentUri = new Uri(agentUri);

            if (string.Equals(AgentUri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                // Replace localhost with 127.0.0.1 to avoid DNS resolution.
                // When ipv6 is enabled, localhost is first resolved to ::1, which fails
                // because the trace agent is only bound to ipv4.
                // This causes delays when sending traces.
                var builder = new UriBuilder(agentUri) { Host = "127.0.0.1" };
                AgentUri = builder.Uri;
            }

            TracesTransport = traceTransport ?? TracesTransportType.Default;
        }
    }
}
