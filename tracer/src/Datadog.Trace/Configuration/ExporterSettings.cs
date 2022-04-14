// <copyright file="ExporterSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
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
        /// <summary>
        /// Allows overriding of file system access for tests.
        /// </summary>
        private readonly Func<string, bool> _fileExists;

        private int _partialFlushMinSpans;
        private Uri _agentUri;

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

            ValidationWarnings = new List<string>();

            // Get values from the config
            var traceAgentUrl = source?.GetString(ConfigurationKeys.AgentUri);
            var tracesPipeName = source?.GetString(ConfigurationKeys.TracesPipeName);
            var tracesPipeTimeoutMs = source?.GetInt32(ConfigurationKeys.TracesPipeTimeoutMs) ?? 0;
            var agentHost = source?.GetString(ConfigurationKeys.AgentHost) ??
                        // backwards compatibility for names used in the past
                        source?.GetString("DD_TRACE_AGENT_HOSTNAME") ??
                        source?.GetString("DATADOG_TRACE_AGENT_HOSTNAME");

            var agentPort = source?.GetInt32(ConfigurationKeys.AgentPort) ??
                        // backwards compatibility for names used in the past
                        source?.GetInt32("DATADOG_TRACE_AGENT_PORT");

            var dogStatsdPort = source?.GetInt32(ConfigurationKeys.DogStatsdPort) ?? 0;
            var metricsPipeName = source?.GetString(ConfigurationKeys.MetricsPipeName);
            var metricsUnixDomainSocketPath = source?.GetString(ConfigurationKeys.MetricsUnixDomainSocketPath);
            var partialFlushMinSpans = source?.GetInt32(ConfigurationKeys.PartialFlushMinSpans);

            // UDS socket path variable has been deprecated. Ignore it explicitly here.
            ConfigureTraceTransport(traceAgentUrl, tracesPipeName, tracesPipeTimeoutMs, agentHost, agentPort, string.Empty);
            ConfigureMetricsTransport(agentHost, dogStatsdPort, metricsPipeName, metricsUnixDomainSocketPath);

            TracesPipeTimeoutMs = tracesPipeTimeoutMs > 0 ? tracesPipeTimeoutMs : 500;
            PartialFlushEnabled = source?.GetBool(ConfigurationKeys.PartialFlushEnabled) ?? false;
            PartialFlushMinSpans = partialFlushMinSpans > 0 ? partialFlushMinSpans.Value : 500;
        }

        /// <summary>
        /// Gets or sets the Uri where the Tracer can connect to the Agent.
        /// Default is <c>"http://localhost:8126"</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.AgentUri"/>
        /// <seealso cref="ConfigurationKeys.AgentHost"/>
        /// <seealso cref="ConfigurationKeys.AgentPort"/>
        public Uri AgentUri
        {
            get => _agentUri;
            set
            {
                TrySetAgentUriAndTransport(value);
            }
        }

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
        /// This parameter is deprecated and shall be removed. Consider using AgentUri instead
        /// </summary>
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

        internal List<string> ValidationWarnings { get; }

        private void ConfigureMetricsTransport(string agentHost, int dogStatsdPort, string metricsPipeName, string metricsUnixDomainSocketPath)
        {
            // Agent port is set to zero in places like AAS where it's needed to prevent port conflict
            // The agent will fail to start if it can not bind a port, so we need to override 8126 to prevent port conflict
            // Port 0 means it will pick some random available port
            if (dogStatsdPort < 0)
            {
                ValidationWarnings.Add("The provided dogStatsD port isn't valid, it should be positive.");
            }

            if (dogStatsdPort > 0 || agentHost != null)
            {
                // No need to set AgentHost, it is taken from the AgentUri and set in ConfigureTrace
                MetricsTransport = MetricsTransportType.UDP;
                DogStatsdPort = dogStatsdPort > 0 ? dogStatsdPort : DefaultDogstatsdPort;
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
                MetricsTransport = MetricsTransportType.UDP;
                DogStatsdPort = DefaultDogstatsdPort;
            }
        }

        private void ConfigureTraceTransport(string agentUri, string tracesPipeName, int tracesPipeTimeoutMs, string agentHost, int? agentPort, string tracesUnixDomainSocketPath)
        {
            // Check the parameters in order of precedence
            // For some cases, we allow falling back on another configuration (eg invalid url as the application will need to be restarted to fix it anyway).
            // For other cases (eg a configured unix domain socket path not found), we don't fallback as the problem could be fixed outside the application.
            if (agentUri is not null)
            {
                if (TrySetAgentUriAndTransport(agentUri))
                {
                    return;
                }
            }

            if (!string.IsNullOrWhiteSpace(tracesPipeName))
            {
                TracesTransport = TracesTransportType.WindowsNamedPipe;
                TracesPipeName = tracesPipeName;
                SetAgentUri(agentHost ?? DefaultAgentHost, agentPort ?? DefaultAgentPort); // this one can throw
                return;
            }

            // This property shouldn't have been introduced. We need to remove it as part of 3.0
            // But while it's here, we need to handle it properly
            if (!string.IsNullOrWhiteSpace(tracesUnixDomainSocketPath))
            {
                SetUdsAsTraceTransportAndCheckFile(tracesUnixDomainSocketPath);
                return;
            }

            if ((agentPort != null && agentPort != 0) || agentHost != null)
            {
                // Agent port is set to zero in places like AAS where it's needed to prevent port conflict
                // The agent will fail to start if it can not bind a port, so we need to override 8126 to prevent port conflict
                // Port 0 means it will pick some random available port

                if (TrySetAgentUriAndTransport(agentHost ?? DefaultAgentHost, agentPort ?? DefaultAgentPort))
                {
                    return;
                }
            }

            if (_fileExists(DefaultTracesUnixDomainSocket))
            {
                SetUdsAsTraceTransport(DefaultTracesUnixDomainSocket);
                SetAgentUri(DefaultAgentHost, DefaultAgentPort);
                return;
            }

            TrySetAgentUriAndTransport(DefaultAgentHost, DefaultAgentPort);
        }

        private bool TrySetAgentUriAndTransport(string host, int port)
        {
            return TrySetAgentUriAndTransport($"http://{host}:{port}");
        }

        private bool TrySetAgentUriAndTransport(string url)
        {
            if (!Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var uri))
            {
                ValidationWarnings.Add($"The Uri: '${url}' provided in '{ConfigurationKeys.AgentUri}' is not valid. It won't be taken into account to send traces.");
                return false;
            }

            return TrySetAgentUriAndTransport(uri);
        }

        private bool TrySetAgentUriAndTransport(Uri uri)
        {
            if (uri.OriginalString.StartsWith(UnixDomainSocketPrefix, StringComparison.OrdinalIgnoreCase))
            {
                SetUdsAsTraceTransportAndCheckFile(uri.PathAndQuery);
            }
            else
            {
                TracesTransport = TracesTransportType.Default;
            }

            SetAgentUriReplacingLocalhost(uri);
            return true;
        }

        private void SetAgentUri(string host, int? port)
        {
            // Still build the Uri no matter what the transport as we send it in the http message
            // TBH, I don't know if we should handle the case where we use agentHost or agentPort.
            // Can user have configured both agenthost, agentport, and a UDS path (or an url or a pipe)?

            // I allow this one to throw, as this was the previous behaviour and because I don't know what to do.
            var uri = new Uri($"http://{host ?? DefaultAgentHost}:{port ?? DefaultAgentPort}");
            SetAgentUriReplacingLocalhost(uri);
        }

        private void SetUdsAsTraceTransportAndCheckFile(string udsPath)
        {
            SetUdsAsTraceTransport(udsPath);

            // check if the file exists to warn the user.
            if (!_fileExists(udsPath))
            {
                // We don't fallback in that case as the file could be mounted separately.
                ValidationWarnings.Add($"The socket provided {udsPath} cannot be found. The tracer will still rely on this socket to send traces.");
            }
        }

        private void SetUdsAsTraceTransport(string udsPath)
        {
            TracesTransport = TracesTransportType.UnixDomainSocket;
            TracesUnixDomainSocketPath = udsPath;
        }

        private void SetUdsAsMetricsTransportAndCheckFile(string udsPath, string configurationKey)
        {
            MetricsTransport = MetricsTransportType.UDS;
            MetricsUnixDomainSocketPath = udsPath;

            // check if the file exists to warn the user.
            if (!_fileExists(udsPath))
            {
                ValidationWarnings.Add($"The socket {udsPath} provided in '{configurationKey} cannot be found. The tracer will still rely on this socket to send metrics.");
            }
        }

        private void SetAgentUriReplacingLocalhost(Uri uri)
        {
            if (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                // Replace localhost with 127.0.0.1 to avoid DNS resolution.
                // When ipv6 is enabled, localhost is first resolved to ::1, which fails
                // because the trace agent is only bound to ipv4.
                // This causes delays when sending traces.
                var builder = new UriBuilder(uri) { Host = "127.0.0.1" };
                _agentUri = builder.Uri;
            }
            else
            {
                _agentUri = uri;
            }
        }
    }
}
