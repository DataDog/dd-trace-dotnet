// <copyright file="ExporterSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Telemetry;
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
        private readonly IConfigurationTelemetry _telemetry;

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
        [PublicApi]
        public ExporterSettings()
            : this(null, TelemetryFactoryV2.GetConfigTelemetry())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExporterSettings"/> class
        /// using the specified <see cref="IConfigurationSource"/> to initialize values.
        /// </summary>
        /// <param name="source">The <see cref="IConfigurationSource"/> to use when retrieving configuration values.</param>
        [PublicApi]
        public ExporterSettings(IConfigurationSource? source)
            : this(source, File.Exists, TelemetryFactoryV2.GetConfigTelemetry())
        {
        }

        internal ExporterSettings(IConfigurationSource? source, IConfigurationTelemetry telemetry)
            : this(source, File.Exists, telemetry)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExporterSettings"/> class.
        /// Direct use in tests only.
        /// </summary>
        internal ExporterSettings(IConfigurationSource? source, Func<string, bool> fileExists, IConfigurationTelemetry telemetry)
        {
            _fileExists = fileExists;
            _telemetry = telemetry;

            ValidationWarnings = new List<string>();

            source ??= NullConfigurationSource.Instance;

            // Get values from the config
            var config = new ConfigurationBuilder(source, telemetry);
            var traceAgentUrl = config.WithKeys(ConfigurationKeys.AgentUri).AsString();
            var tracesPipeName = config.WithKeys(ConfigurationKeys.TracesPipeName).AsString();
            var tracesUnixDomainSocketPath = config.WithKeys(ConfigurationKeys.TracesUnixDomainSocketPath).AsString();

            var agentHost = config
                           .WithKeys(ConfigurationKeys.AgentHost, "DD_TRACE_AGENT_HOSTNAME", "DATADOG_TRACE_AGENT_HOSTNAME")
                           .AsString();

            var agentPort = config
                           .WithKeys(ConfigurationKeys.AgentPort, "DATADOG_TRACE_AGENT_PORT")
                           .AsInt32();

            var dogStatsdPort = config.WithKeys(ConfigurationKeys.DogStatsdPort).AsInt32(0);
            var metricsPipeName = config.WithKeys(ConfigurationKeys.MetricsPipeName).AsString();
            var metricsUnixDomainSocketPath = config.WithKeys(ConfigurationKeys.MetricsUnixDomainSocketPath).AsString();

            ConfigureTraceTransport(traceAgentUrl, tracesPipeName, agentHost, agentPort, tracesUnixDomainSocketPath);
            ConfigureMetricsTransport(traceAgentUrl, agentHost, dogStatsdPort, metricsPipeName, metricsUnixDomainSocketPath);

            TracesPipeTimeoutMs = config
                                 .WithKeys(ConfigurationKeys.TracesPipeTimeoutMs)
                                 .AsInt32(500, value => value > 0)
                                 .Value;

            PartialFlushEnabled = config.WithKeys(ConfigurationKeys.PartialFlushEnabled).AsBool(false);
            PartialFlushMinSpans = config
                                  .WithKeys(ConfigurationKeys.PartialFlushMinSpans)
                                  .AsInt32(500, value => value > 0).Value;
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
                SetAgentUriAndTransport(value);
                // In the case the url was a UDS one, we do not change anything.
                if (TracesTransport == TracesTransportType.Default)
                {
                    MetricsTransport = MetricsTransportType.UDP;
                }
            }
        }

        /// <summary>
        /// Gets or sets the windows pipe name where the Tracer can connect to the Agent.
        /// Default is <c>null</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.TracesPipeName"/>
        public string? TracesPipeName { get; set; }

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
        public string? MetricsPipeName { get; set; }

        /// <summary>
        /// Gets or sets the unix domain socket path where the Tracer can connect to the Agent.
        /// This parameter is deprecated and shall be removed. Consider using AgentUri instead
        /// </summary>
        public string? TracesUnixDomainSocketPath { get; set; }

        /// <summary>
        /// Gets or sets the unix domain socket path where the Tracer can send stats.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.MetricsUnixDomainSocketPath"/>
        public string? MetricsUnixDomainSocketPath { get; set; }

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

        private void ConfigureMetricsTransport(string? traceAgentUrl, string? agentHost, int dogStatsdPort, string? metricsPipeName, string? metricsUnixDomainSocketPath)
        {
            // Agent port is set to zero in places like AAS where it's needed to prevent port conflict
            // The agent will fail to start if it can not bind a port, so we need to override 8126 to prevent port conflict
            // Port 0 means it will pick some random available port
            if (dogStatsdPort < 0)
            {
                ValidationWarnings.Add("The provided dogStatsD port isn't valid, it should be positive.");
            }

            if (!string.IsNullOrWhiteSpace(traceAgentUrl) && !traceAgentUrl!.StartsWith(UnixDomainSocketPrefix) && Uri.TryCreate(traceAgentUrl, UriKind.Absolute, out var uri))
            {
                // No need to set AgentHost, it is taken from the AgentUri and set in ConfigureTrace
                MetricsTransport = MetricsTransportType.UDP;
            }
            else if (dogStatsdPort > 0 || agentHost != null)
            {
                // No need to set AgentHost, it is taken from the AgentUri and set in ConfigureTrace
                MetricsTransport = MetricsTransportType.UDP;
            }
            else if (!string.IsNullOrWhiteSpace(metricsPipeName))
            {
                MetricsTransport = MetricsTransportType.NamedPipe;
                MetricsPipeName = metricsPipeName;
            }
            else if (metricsUnixDomainSocketPath != null)
            {
                MetricsTransport = MetricsTransportType.UDS;
                MetricsUnixDomainSocketPath = metricsUnixDomainSocketPath;

                // check if the file exists to warn the user.
                if (!_fileExists(metricsUnixDomainSocketPath))
                {
                    ValidationWarnings.Add($"The socket {metricsUnixDomainSocketPath} provided in '{ConfigurationKeys.MetricsUnixDomainSocketPath} cannot be found. The tracer will still rely on this socket to send metrics.");
                }
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

            DogStatsdPort = dogStatsdPort > 0 ? dogStatsdPort : DefaultDogstatsdPort;
        }

        [MemberNotNull(nameof(_agentUri))]
        private void ConfigureTraceTransport(string? agentUri, string? tracesPipeName, string? agentHost, int? agentPort, string? tracesUnixDomainSocketPath)
        {
            // Check the parameters in order of precedence
            // For some cases, we allow falling back on another configuration (eg invalid url as the application will need to be restarted to fix it anyway).
            // For other cases (eg a configured unix domain socket path not found), we don't fallback as the problem could be fixed outside the application.
            if (!string.IsNullOrWhiteSpace(agentUri))
            {
                if (TrySetAgentUriAndTransport(agentUri!))
                {
                    return;
                }
            }

            if (!string.IsNullOrWhiteSpace(tracesPipeName))
            {
                TracesTransport = TracesTransportType.WindowsNamedPipe;
                TracesPipeName = tracesPipeName;

                // The Uri isn't needed anymore in that case, just populating it for retro compatibility.
                if (!Uri.TryCreate($"http://{agentHost ?? DefaultAgentHost}:{agentPort ?? DefaultAgentPort}", UriKind.Absolute, out var uri))
                {
                    // fallback so _agentUri is always non-null
                    uri = CreateDefaultUri();
                }

                SetAgentUriReplacingLocalhost(uri);
                return;
            }

            // This property shouldn't have been introduced. We need to remove it as part of 3.0
            // But while it's here, we need to handle it properly
            if (!string.IsNullOrWhiteSpace(tracesUnixDomainSocketPath))
            {
                if (TrySetAgentUriAndTransport(UnixDomainSocketPrefix + tracesUnixDomainSocketPath))
                {
                    return;
                }
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
                // setting the urls as well for retro compatibility in the almost impossible case where someone
                // used this config and accessed the AgentUri property as well (to avoid a potential null ref)
                // Using Set not TrySet because we know this is a valid Uri and ensures _agentUri is always non-null
                SetAgentUriAndTransport(new Uri(UnixDomainSocketPrefix + DefaultTracesUnixDomainSocket));
                return;
            }

            ValidationWarnings.Add("No transport configuration found, using default values");

            // we know this URL is valid so don't use TrySet, otherwise can't guarantee _agentUri is non null
            SetAgentUriAndTransport(CreateDefaultUri());
        }

        [MemberNotNullWhen(true, nameof(_agentUri))]
        private bool TrySetAgentUriAndTransport(string host, int port)
        {
            return TrySetAgentUriAndTransport($"http://{host}:{port}");
        }

        [MemberNotNullWhen(true, nameof(_agentUri))]
        private bool TrySetAgentUriAndTransport(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                ValidationWarnings.Add($"The Uri: '{url}' is not valid. It won't be taken into account to send traces. Note that only absolute urls are accepted.");
                return false;
            }

            SetAgentUriAndTransport(uri);
            return true;
        }

        [MemberNotNull(nameof(_agentUri))]
        private void SetAgentUriAndTransport(Uri uri)
        {
            if (uri.OriginalString.StartsWith(UnixDomainSocketPrefix, StringComparison.OrdinalIgnoreCase))
            {
                TracesTransport = TracesTransportType.UnixDomainSocket;
                TracesUnixDomainSocketPath = uri.PathAndQuery;

                var absoluteUri = uri.AbsoluteUri.Replace(UnixDomainSocketPrefix, string.Empty);
                if (!Path.IsPathRooted(absoluteUri))
                {
                    ValidationWarnings.Add($"The provided Uri {uri} contains a relative path which may not work. This is the path to the socket that will be used: {uri.PathAndQuery}");
                }

                // check if the file exists to warn the user.
                if (!_fileExists(uri.PathAndQuery))
                {
                    // We don't fallback in that case as the file could be mounted separately.
                    ValidationWarnings.Add($"The socket provided {uri.PathAndQuery} cannot be found. The tracer will still rely on this socket to send traces.");
                }
            }
            else
            {
                TracesTransport = TracesTransportType.Default;
            }

            SetAgentUriReplacingLocalhost(uri);
        }

        [MemberNotNull(nameof(_agentUri))]
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

        private Uri CreateDefaultUri() => new Uri($"http://{DefaultAgentHost}:{DefaultAgentPort}");
    }
}
