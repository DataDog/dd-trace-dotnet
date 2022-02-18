// <copyright file="ImmutableExporterSettings.cs" company="Datadog">
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
    /// Contains exporter related settings.
    /// </summary>
    public class ImmutableExporterSettings
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ImmutableExporterSettings"/> class
        /// using the specified <see cref="IConfigurationSource"/> to initialize values.
        /// </summary>
        /// <param name="source">The <see cref="IConfigurationSource"/> to use when retrieving configuration values.</param>
        public ImmutableExporterSettings(IConfigurationSource source)
            : this(new ExporterSettings(source))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImmutableExporterSettings"/> class from
        /// a TracerSettings instance.
        /// </summary>
        /// <param name="settings">The tracer settings to use to populate the immutable tracer settings</param>
        public ImmutableExporterSettings(ExporterSettings settings)
            : this(settings, File.Exists)
        {
        }

        internal ImmutableExporterSettings(ExporterSettings settings, Func<string, bool> fileExists)
        {
            var builder = new SettingsBuilder(fileExists);
            builder.ConfigureTraceTransport(settings);
            builder.ConfigureMetricsTransport(settings);
            builder.ConfigurePartialFlush(settings);

            AgentUri = builder.AgentUri;
            TracesPipeName = builder.TracesPipeName;
            MetricsPipeName = builder.MetricsPipeName;
            MetricsUnixDomainSocketPath = builder.MetricsUnixDomainSocketPath;
            TracesUnixDomainSocketPath = builder.TracesUnixDomainSocketPath;
            MetricsTransport = builder.MetricsTransport;
            TracesTransport = builder.TracesTransport;
            DogStatsdPort = builder.DogStatsdPort;
            PartialFlushEnabled = builder.PartialFlushEnabled;
            PartialFlushMinSpans = builder.PartialFlushMinSpans;
            TracesPipeTimeoutMs = builder.TracesPipeTimeoutMs;
        }

        /// <summary>
        /// Gets the Uri where the Tracer can connect to the Agent.
        /// Default is <c>"http://localhost:8126"</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.AgentUri"/>
        /// <seealso cref="ConfigurationKeys.AgentHost"/>
        /// <seealso cref="ConfigurationKeys.AgentPort"/>
        public Uri AgentUri { get; }

        /// <summary>
        /// Gets the windows pipe name where the Tracer can connect to the Agent.
        /// Default is <c>null</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.TracesPipeName"/>
        public string TracesPipeName { get; }

        /// <summary>
        /// Gets the timeout in milliseconds for the windows named pipe requests.
        /// Default is <c>100</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.TracesPipeTimeoutMs"/>
        public int TracesPipeTimeoutMs { get; }

        /// <summary>
        /// Gets the windows pipe name where the Tracer can send stats.
        /// Default is <c>null</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.MetricsPipeName"/>
        public string MetricsPipeName { get; }

        /// <summary>
        /// Gets the port where the DogStatsd server is listening for connections.
        /// Default is <c>8125</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.DogStatsdPort"/>
        public int DogStatsdPort { get; }

        /// <summary>
        /// Gets a value indicating whether partial flush is enabled
        /// </summary>
        public bool PartialFlushEnabled { get; }

        /// <summary>
        /// Gets the minimum number of closed spans in a trace before it's partially flushed
        /// </summary>
        public int PartialFlushMinSpans { get; }

        /// <summary>
        /// Gets the unix domain socket path where the Tracer can connect to the Agent.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.TracesUnixDomainSocketPath"/>
        public string TracesUnixDomainSocketPath { get; }

        /// <summary>
        /// Gets the unix domain socket path where the Tracer can send stats.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.MetricsUnixDomainSocketPath"/>
        public string MetricsUnixDomainSocketPath { get; }

        /// <summary>
        /// Gets the transport used to send traces to the Agent.
        /// </summary>
        internal TracesTransportType TracesTransport { get; }

        /// <summary>
        /// Gets the transport used to connect to the DogStatsD.
        /// </summary>
        internal Vendors.StatsdClient.Transport.TransportType MetricsTransport { get; }

        private class SettingsBuilder
        {
            private readonly Func<string, bool> _fileExists;

            public SettingsBuilder(Func<string, bool> fileExists)
            {
                _fileExists = fileExists;
            }

            public Uri AgentUri { get; private set; }

            public string TracesPipeName { get; private set; }

            public int TracesPipeTimeoutMs { get; private set;  }

            public string MetricsPipeName { get; private set; }

            public int DogStatsdPort { get; private set; }

            public bool PartialFlushEnabled { get; private set; }

            public int PartialFlushMinSpans { get; private set; }

            public string TracesUnixDomainSocketPath { get; private set; }

            public string MetricsUnixDomainSocketPath { get; private set; }

            public TracesTransportType TracesTransport { get; private set; }

            public MetricsTransportType MetricsTransport { get; private set; }

            public void ConfigurePartialFlush(ExporterSettings settings)
            {
                PartialFlushEnabled = settings.PartialFlushEnabled;
                PartialFlushMinSpans = settings.PartialFlushMinSpans != default ? settings.PartialFlushMinSpans : 500;
            }

            public void ConfigureMetricsTransport(ExporterSettings settings)
            {
                var agentHost = settings.AgentHost;
                var dogStatsdPort = settings.DogStatsdPort;
                var metricsPipeName = settings.MetricsPipeName;
                var metricsUnixDomainSocketPath = settings.MetricsUnixDomainSocketPath;

                // Agent port is set to zero in places like AAS where it's needed to prevent port conflict
                // The agent will fail to start if it can not bind a port, so we need to override 8126 to prevent port conflict
                // Port 0 means it will pick some random available port
                if (dogStatsdPort < 0)
                {
                    Log.Warning("The provided dogStatsD port isn't valid, it should be positive.");
                }

                if (dogStatsdPort > 0 || agentHost != null)
                {
                    // No need to set AgentHost, it is taken from the AgentUri and set in ConfigureTrace
                    MetricsTransport = MetricsTransportType.UDP;
                    DogStatsdPort = dogStatsdPort > 0 ? dogStatsdPort : ExporterSettings.DefaultDogstatsdPort;
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
                else if (_fileExists(ExporterSettings.DefaultMetricsUnixDomainSocket))
                {
                    MetricsTransport = MetricsTransportType.UDS;
                    MetricsUnixDomainSocketPath = ExporterSettings.DefaultMetricsUnixDomainSocket;
                }
                else
                {
                    MetricsTransport = MetricsTransportType.UDP;
                    DogStatsdPort = ExporterSettings.DefaultDogstatsdPort;
                }
            }

            public void ConfigureTraceTransport(ExporterSettings settings)
            {
                // Check the parameters in order of precedence
                // For some cases, we allow falling back on another configuration (eg invalid url as the application will need to be restarted to fix it anyway).
                // For other cases (eg a configured unix domain socket path not found), we don't fallback as the problem could be fixed outside the application.
                if (settings.AgentUri is not null)
                {
                    if (TrySetAgentUriAndTransport(settings.AgentUri))
                    {
                        return;
                    }
                }

                if (!string.IsNullOrWhiteSpace(settings.TracesUnixDomainSocketPath))
                {
                    SetUdsAsTraceTransportAndCheckFile(settings.TracesUnixDomainSocketPath, ConfigurationKeys.TracesUnixDomainSocketPath);
                    SetAgentUri(settings.AgentHost ?? ExporterSettings.DefaultAgentHost, settings.AgentPort ?? ExporterSettings.DefaultAgentPort); // this one can throw
                    return;
                }

                if (!string.IsNullOrWhiteSpace(settings.TracesPipeName))
                {
                    TracesTransport = TracesTransportType.WindowsNamedPipe;
                    TracesPipeName = settings.TracesPipeName;
                    TracesPipeTimeoutMs = settings.TracesPipeTimeoutMs > 0 ? settings.TracesPipeTimeoutMs : 500;
                    SetAgentUri(settings.AgentHost ?? ExporterSettings.DefaultAgentHost, settings.AgentPort ?? ExporterSettings.DefaultAgentPort); // this one can throw
                    return;
                }

                if ((settings.AgentPort != null && settings.AgentPort != 0) || settings.AgentHost != null)
                {
                    // Agent port is set to zero in places like AAS where it's needed to prevent port conflict
                    // The agent will fail to start if it can not bind a port, so we need to override 8126 to prevent port conflict
                    // Port 0 means it will pick some random available port

                    if (TrySetAgentUriAndTransport(settings.AgentHost ?? ExporterSettings.DefaultAgentHost, settings.AgentPort ?? ExporterSettings.DefaultAgentPort))
                    {
                        return;
                    }
                }

                if (_fileExists(ExporterSettings.DefaultTracesUnixDomainSocket))
                {
                    SetUdsAsTraceTransport(ExporterSettings.DefaultTracesUnixDomainSocket);
                    SetAgentUri(ExporterSettings.DefaultAgentHost, ExporterSettings.DefaultAgentPort);
                    return;
                }

                TrySetAgentUriAndTransport(ExporterSettings.DefaultAgentHost, ExporterSettings.DefaultAgentPort);
            }

            private bool TrySetAgentUriAndTransport(string host, int port)
            {
                return TrySetAgentUriAndTransport($"http://{host}:{port}");
            }

            private bool TrySetAgentUriAndTransport(string url)
            {
                if (!Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var uri))
                {
                    Log.Warning($"The provided Uri: ${url} is not valid. Falling back on alternative transport settings.");
                    return false;
                }

                return TrySetAgentUriAndTransport(uri);
            }

            private bool TrySetAgentUriAndTransport(Uri uri)
            {
                if (uri.OriginalString.StartsWith(ExporterSettings.UnixDomainSocketPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    SetUdsAsTraceTransportAndCheckFile(uri.PathAndQuery, ConfigurationKeys.AgentUri);
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
                var uri = new Uri($"http://{host ?? ExporterSettings.DefaultAgentHost}:{port ?? ExporterSettings.DefaultAgentPort}");
                SetAgentUriReplacingLocalhost(uri);
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
                    Log.Warning($"The socket {udsPath} provided in '{configurationKey} cannot be found. The tracer will still rely on this socket to send traces.");
                }
            }

            private void SetUdsAsMetricsTransportAndCheckFile(string udsPath, string configurationKey)
            {
                MetricsTransport = MetricsTransportType.UDS;
                MetricsUnixDomainSocketPath = udsPath;

                // check if the file exists to warn the user.
                if (!_fileExists(udsPath))
                {
                    Log.Warning($"The socket {udsPath} provided in '{configurationKey} cannot be found. The tracer will still rely on this socket to send metrics.");
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
                    AgentUri = builder.Uri;
                }
                else
                {
                    AgentUri = uri;
                }
            }
        }
    }
}
