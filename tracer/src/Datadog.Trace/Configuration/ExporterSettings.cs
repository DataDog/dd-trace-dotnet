// <copyright file="ExporterSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using Datadog.Trace.Agent;
using Datadog.Trace.Vendors.Serilog;
using MetricsTransportType = Datadog.Trace.Vendors.StatsdClient.Transport.TransportType;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Contains exporter settings.
    /// </summary>
    public class ExporterSettings
    {
        /// <summary>
        /// Allows overriding of file system access for tests.
        /// </summary>
        private readonly Func<string, bool> _fileExists;

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
        /// Gets or sets the Uri where the Tracer can connect to the Agent to send Traces
        /// Default is <c>"http://localhost:8126"</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.AgentUri"/>
        /// <seealso cref="ConfigurationKeys.AgentHost"/>
        /// <seealso cref="ConfigurationKeys.AgentPort"/>
        public Uri AgentUri { get; set; }

        /// <summary    >
        /// Gets or sets the agent host that can be used to reach dogStatsD
        /// Default is <c>"localhost"</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.AgentHost"/>
        public string AgentHost { get; set; }

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
            var agentHost = source?.GetString(ConfigurationKeys.AgentHost);
            var dogStatsdPort = source?.GetInt32(ConfigurationKeys.DogStatsdPort);
            var metricsPipeName = source?.GetString(ConfigurationKeys.MetricsPipeName);
            var metricsUnixDomainSocketPath = source?.GetString(ConfigurationKeys.MetricsUnixDomainSocketPath);

            if ((dogStatsdPort != null && dogStatsdPort != 0) || agentHost != null)
            {
                // Agent port is set to zero in places like AAS where it's needed to prevent port conflict
                // The agent will fail to start if it can not bind a port, so we need to override 8126 to prevent port conflict
                // Port 0 means it will pick some random available port
                AgentHost = agentHost ?? DefaultAgentHost;
                DogStatsdPort = dogStatsdPort ?? DefaultDogstatsdPort;
                MetricsTransport = MetricsTransportType.UDP;
            }
            else if (!string.IsNullOrWhiteSpace(metricsPipeName))
            {
                MetricsTransport = MetricsTransportType.NamedPipe;
                MetricsPipeName = metricsPipeName;
            }
            else if (metricsUnixDomainSocketPath != null)
            {
                SetUdsAsMetricsTransportAndCheckFile(metricsUnixDomainSocketPath, ConfigurationKeys.MetricsUnixDomainSocketPath);
            }
            else if (_fileExists(DefaultMetricsUnixDomainSocket))
            {
                MetricsTransport = MetricsTransportType.UDS;
                MetricsUnixDomainSocketPath = DefaultMetricsUnixDomainSocket;
            }
            else
            {
                // Nothing is configured, we should assume UDP for metrics
                DogStatsdPort = DefaultDogstatsdPort;
                MetricsTransport = MetricsTransportType.UDP;
            }
        }

        private void ConfigureTraceTransport(IConfigurationSource source)
        {
            var traceAgentUrl = source?.GetString(ConfigurationKeys.AgentUri);
            var udsPath = source?.GetString(ConfigurationKeys.TracesUnixDomainSocketPath);
            var tracesPipeName = source?.GetString(ConfigurationKeys.TracesPipeName);
            var agentHost = source?.GetString(ConfigurationKeys.AgentHost) ??
                            // backwards compatibility for names used in the past
                            source?.GetString("DD_TRACE_AGENT_HOSTNAME") ??
                            source?.GetString("DATADOG_TRACE_AGENT_HOSTNAME");

            var agentPort = source?.GetInt32(ConfigurationKeys.AgentPort) ??
                            // backwards compatibility for names used in the past
                            source?.GetInt32("DATADOG_TRACE_AGENT_PORT");

            // Check the parameters in order of precedence
            // For some cases, we allow falling back on another configuration (eg invalid url as the application will need to be restarted to fix it anyway).
            // For other cases (eg a configured unix domain socket path not found), we don't fallback as the problem could be fixed outside the application.
            if (!string.IsNullOrWhiteSpace(traceAgentUrl))
            {
                if (TrySetAgentUri(traceAgentUrl))
                {
                    return;
                }
            }

            if (!string.IsNullOrWhiteSpace(udsPath))
            {
                SetUdsAsTraceTransportAndCheckFile(udsPath, ConfigurationKeys.TracesUnixDomainSocketPath);
                SetAgentUriAnyway(agentHost ?? DefaultAgentHost, agentPort ?? DefaultAgentPort); // this one can throw
                return;
            }

            if (!string.IsNullOrWhiteSpace(tracesPipeName))
            {
                TracesTransport = TracesTransportType.WindowsNamedPipe;
                TracesPipeName = tracesPipeName;
                TracesPipeTimeoutMs = source?.GetInt32(ConfigurationKeys.TracesPipeTimeoutMs)
#if DEBUG
                                   ?? 20_000;
#else
                    ?? 500;

#endif
                SetAgentUriAnyway(agentHost ?? DefaultAgentHost, agentPort ?? DefaultAgentPort); // this one can throw
                return;
            }

            if ((agentPort != null && agentPort != 0) || agentHost != null)
            {
                // Agent port is set to zero in places like AAS where it's needed to prevent port conflict
                // The agent will fail to start if it can not bind a port, so we need to override 8126 to prevent port conflict
                // Port 0 means it will pick some random available port

                if (TrySetAgentUri(agentHost ?? DefaultAgentHost, agentPort ?? DefaultAgentPort))
                {
                    return;
                }
            }

            if (_fileExists(DefaultTracesUnixDomainSocket))
            {
                SetUdsAsTraceTransport(DefaultTracesUnixDomainSocket);
                SetAgentUriAnyway(DefaultAgentHost, DefaultAgentPort);
                return;
            }

            TrySetAgentUri(DefaultAgentHost, DefaultAgentPort);
        }

        private bool TrySetAgentUri(string host, int port)
        {
            return TrySetAgentUri($"http://{host}:{port}");
        }

        private bool TrySetAgentUri(string url)
        {
            if (!Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var uri))
            {
                Log.Warning($"The provided Uri: ${url} is not valid. Falling back on alternative transport settings.");
                return false;
            }

            if (url.StartsWith(UnixDomainSocketPrefix))
            {
                var path = url.Replace(UnixDomainSocketPrefix, string.Empty);
                SetUdsAsTraceTransportAndCheckFile(path, ConfigurationKeys.AgentUri);
            }
            else
            {
                TracesTransport = TracesTransportType.Default;
            }

            AgentUri = uri;
            FixLocalhostInUri();

            return true;
        }

        private void SetAgentUriAnyway(string host, int? port)
        {
            // Still build the Uri no matter what the transport as we send it in the http message
            // TBH, I don't know if we should handle the case where we use agentHost or agentPort.
            // Can user have configured both agenthost, agentport, and a UDS path (or an url or a pipe)?

            // I allow this one to throw, as this was the previous behaviour and because I don't know what to do.
            AgentUri ??= new Uri($"http://{host ?? DefaultAgentHost}:{port ?? DefaultAgentPort}");
            FixLocalhostInUri();
        }

        private void SetUdsAsTraceTransport(string udsPath)
        {
            TracesTransport = TracesTransportType.UnixDomainSocket;
            TracesUnixDomainSocketPath = udsPath;
        }

        private void SetUdsAsTraceTransportAndCheckFile(string udsPath, string configurationKey)
        {
            SetUdsAsTraceTransport(udsPath);

            // check if the file exists to warn the user.
            if (!_fileExists(udsPath))
            {
                // We don't fallback in that case as the file could be mounted separately.
                Log.Warning($"The socket {udsPath} provided in '{configurationKey} cannot be found.");
            }
        }

        private void SetUdsAsMetricsTransportAndCheckFile(string udsPath, string configurationKey)
        {
            MetricsTransport = MetricsTransportType.UDS;
            MetricsUnixDomainSocketPath = udsPath;

            // check if the file exists to warn the user.
            if (!_fileExists(udsPath))
            {
                Log.Warning($"The socket {udsPath} provided in '{configurationKey} cannot be found.");
            }
        }

        private void FixLocalhostInUri()
        {
            if (string.Equals(AgentUri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                // Replace localhost with 127.0.0.1 to avoid DNS resolution.
                // When ipv6 is enabled, localhost is first resolved to ::1, which fails
                // because the trace agent is only bound to ipv4.
                // This causes delays when sending traces.
                var builder = new UriBuilder(AgentUri) { Host = "127.0.0.1" };
                AgentUri = builder.Uri;
            }
        }
    }
}
