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
using Datadog.Trace.Telemetry.Metrics;
using MetricsTransportType = Datadog.Trace.Vendors.StatsdClient.Transport.TransportType;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Contains exporter settings.
    /// </summary>
    public partial class ExporterSettings
    {
        /// <summary>
        /// Allows overriding of file system access for tests.
        /// </summary>
        private readonly Func<string, bool> _fileExists;
        private readonly IConfigurationTelemetry _telemetry;

        private int _partialFlushMinSpans;
        private Uri _agentUri;

        /// <summary>
        /// The default host value for <see cref="AgentUriInternal"/>.
        /// </summary>
        public const string DefaultAgentHost = "localhost";

        /// <summary>
        /// The default port value for <see cref="AgentUriInternal"/>.
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
            : this(null, new ConfigurationTelemetry())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExporterSettings"/> class
        /// using the specified <see cref="IConfigurationSource"/> to initialize values.
        /// </summary>
        /// <param name="source">The <see cref="IConfigurationSource"/> to use when retrieving configuration values.</param>
        /// <remarks>
        /// We deliberately don't use the static <see cref="TelemetryFactory.Config"/> collector here
        /// as we don't want to automatically record these values, only once they're "activated",
        /// in <see cref="Tracer.Configure"/>
        /// </remarks>
        [PublicApi]
        public ExporterSettings(IConfigurationSource? source)
            : this(source, File.Exists, new ConfigurationTelemetry())
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

            TracesPipeTimeoutMsInternal = config
                                 .WithKeys(ConfigurationKeys.TracesPipeTimeoutMs)
                                 .AsInt32(500, value => value > 0)
                                 .Value;

            PartialFlushEnabledInternal = config.WithKeys(ConfigurationKeys.PartialFlushEnabled).AsBool(false);
            PartialFlushMinSpansInternal = config
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
        [PublicApi]
        public Uri AgentUri
        {
            get
            {
                TelemetryFactory.Metrics.Record(PublicApiUsage.ExporterSettings_AgentUri_Get);
                return AgentUriInternal;
            }

            set
            {
                TelemetryFactory.Metrics.Record(PublicApiUsage.ExporterSettings_AgentUri_Set);
                AgentUriInternal = value;
            }
        }

        internal Uri AgentUriInternal
        {
            get => _agentUri;
            set
            {
                SetAgentUriAndTransport(value, ConfigurationOrigins.Code);
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
        [GeneratePublicApi(
            PublicApiUsage.ExporterSettings_TracesPipeName_Get,
            PublicApiUsage.ExporterSettings_TracesPipeName_Set,
            ConfigurationKeys.TracesPipeName)]
        internal string? TracesPipeNameInternal { get; set; }

        /// <summary>
        /// Gets or sets the timeout in milliseconds for the windows named pipe requests.
        /// Default is <c>100</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.TracesPipeTimeoutMs"/>
        [GeneratePublicApi(
            PublicApiUsage.ExporterSettings_TracesPipeTimeoutMs_Get,
            PublicApiUsage.ExporterSettings_TracesPipeTimeoutMs_Set,
            ConfigurationKeys.TracesPipeTimeoutMs)]
        internal int TracesPipeTimeoutMsInternal { get; set; }

        /// <summary>
        /// Gets or sets the windows pipe name where the Tracer can send stats.
        /// Default is <c>null</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.MetricsPipeName"/>
        [GeneratePublicApi(
            PublicApiUsage.ExporterSettings_MetricsPipeName_Get,
            PublicApiUsage.ExporterSettings_MetricsPipeName_Set,
            ConfigurationKeys.MetricsPipeName)]
        internal string? MetricsPipeNameInternal { get; set; }

        /// <summary>
        /// Gets or sets the unix domain socket path where the Tracer can connect to the Agent.
        /// This parameter is deprecated and shall be removed. Consider using AgentUri instead
        /// </summary>
        [GeneratePublicApi(
            PublicApiUsage.ExporterSettings_TracesUnixDomainSocketPath_Get,
            PublicApiUsage.ExporterSettings_TracesUnixDomainSocketPath_Set,
            ConfigurationKeys.TracesUnixDomainSocketPath)]
        internal string? TracesUnixDomainSocketPathInternal { get; set; }

        /// <summary>
        /// Gets or sets the unix domain socket path where the Tracer can send stats.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.MetricsUnixDomainSocketPath"/>
        [GeneratePublicApi(
            PublicApiUsage.ExporterSettings_MetricsUnixDomainSocketPath_Get,
            PublicApiUsage.ExporterSettings_MetricsUnixDomainSocketPath_Set,
            ConfigurationKeys.MetricsUnixDomainSocketPath)]
        internal string? MetricsUnixDomainSocketPathInternal { get; set; }

        /// <summary>
        /// Gets or sets the port where the DogStatsd server is listening for connections.
        /// Default is <c>8125</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.DogStatsdPort"/>
        [GeneratePublicApi(
            PublicApiUsage.ExporterSettings_DogStatsdPort_Get,
            PublicApiUsage.ExporterSettings_DogStatsdPort_Set,
            ConfigurationKeys.DogStatsdPort)]
        internal int DogStatsdPortInternal { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether partial flush is enabled
        /// </summary>
        [GeneratePublicApi(
            PublicApiUsage.ExporterSettings_PartialFlushEnabled_Get,
            PublicApiUsage.ExporterSettings_PartialFlushEnabled_Set,
            ConfigurationKeys.PartialFlushEnabled)]
        internal bool PartialFlushEnabledInternal { get; set; }

        /// <summary>
        /// Gets or sets the minimum number of closed spans in a trace before it's partially flushed
        /// </summary>
        [PublicApi]
        public int PartialFlushMinSpans
        {
            get
            {
                TelemetryFactory.Metrics.Record(PublicApiUsage.ExporterSettings_PartialFlushMinSpans_Get);
                return PartialFlushMinSpansInternal;
            }

            set
            {
                TelemetryFactory.Metrics.Record(PublicApiUsage.ExporterSettings_PartialFlushMinSpans_Set);
                _telemetry.Record(ConfigurationKeys.PartialFlushMinSpans, value, ConfigurationOrigins.Code, value <= 0 ? TelemetryErrorCode.FailedValidation : null);
                PartialFlushMinSpansInternal = value;
            }
        }

        internal int PartialFlushMinSpansInternal
        {
            get => _partialFlushMinSpans;

            set
            {
                if (value <= 0)
                {
                    throw new ArgumentException("The value must be strictly greater than 0", nameof(PartialFlushMinSpansInternal));
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

        internal void CollectTelemetry() => TelemetryFactory.Config.Merge(_telemetry);

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
                MetricsPipeNameInternal = metricsPipeName;
            }
            else if (metricsUnixDomainSocketPath != null)
            {
                MetricsTransport = MetricsTransportType.UDS;
                MetricsUnixDomainSocketPathInternal = metricsUnixDomainSocketPath;

                // check if the file exists to warn the user.
                if (!_fileExists(metricsUnixDomainSocketPath))
                {
                    ValidationWarnings.Add($"The socket {metricsUnixDomainSocketPath} provided in '{ConfigurationKeys.MetricsUnixDomainSocketPath} cannot be found. The tracer will still rely on this socket to send metrics.");
                }
            }
            else if (_fileExists(DefaultMetricsUnixDomainSocket))
            {
                MetricsTransport = MetricsTransportType.UDS;
                MetricsUnixDomainSocketPathInternal = DefaultMetricsUnixDomainSocket;
            }
            else
            {
                MetricsTransport = MetricsTransportType.UDP;
                DogStatsdPortInternal = DefaultDogstatsdPort;
            }

            DogStatsdPortInternal = dogStatsdPort > 0 ? dogStatsdPort : DefaultDogstatsdPort;
        }

        [MemberNotNull(nameof(_agentUri))]
        private void ConfigureTraceTransport(string? agentUri, string? tracesPipeName, string? agentHost, int? agentPort, string? tracesUnixDomainSocketPath)
        {
            var origin = ConfigurationOrigins.Default; // default because only called from constructor

            // Check the parameters in order of precedence
            // For some cases, we allow falling back on another configuration (eg invalid url as the application will need to be restarted to fix it anyway).
            // For other cases (eg a configured unix domain socket path not found), we don't fallback as the problem could be fixed outside the application.
            if (!string.IsNullOrWhiteSpace(agentUri))
            {
                if (TrySetAgentUriAndTransport(agentUri!, origin))
                {
                    return;
                }
            }

            if (!string.IsNullOrWhiteSpace(tracesPipeName))
            {
                TracesTransport = TracesTransportType.WindowsNamedPipe;
                TracesPipeNameInternal = tracesPipeName;

                // The Uri isn't needed anymore in that case, just populating it for retro compatibility.
                if (!Uri.TryCreate($"http://{agentHost ?? DefaultAgentHost}:{agentPort ?? DefaultAgentPort}", UriKind.Absolute, out var uri))
                {
                    // fallback so _agentUri is always non-null
                    uri = CreateDefaultUri();
                }

                SetAgentUriReplacingLocalhost(uri, origin);
                return;
            }

            // This property shouldn't have been introduced. We need to remove it as part of 3.0
            // But while it's here, we need to handle it properly
            if (!string.IsNullOrWhiteSpace(tracesUnixDomainSocketPath))
            {
                if (TrySetAgentUriAndTransport(UnixDomainSocketPrefix + tracesUnixDomainSocketPath, origin))
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
                SetAgentUriAndTransport(new Uri(UnixDomainSocketPrefix + DefaultTracesUnixDomainSocket), origin);
                return;
            }

            ValidationWarnings.Add("No transport configuration found, using default values");

            // we know this URL is valid so don't use TrySet, otherwise can't guarantee _agentUri is non null
            SetAgentUriAndTransport(CreateDefaultUri(), origin);
        }

        [MemberNotNullWhen(true, nameof(_agentUri))]
        private bool TrySetAgentUriAndTransport(string host, int port)
        {
            return TrySetAgentUriAndTransport($"http://{host}:{port}", ConfigurationOrigins.Default); // default because only called from constructor
        }

        [MemberNotNullWhen(true, nameof(_agentUri))]
        private bool TrySetAgentUriAndTransport(string url, ConfigurationOrigins origin)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                ValidationWarnings.Add($"The Uri: '{url}' is not valid. It won't be taken into account to send traces. Note that only absolute urls are accepted.");
                return false;
            }

            SetAgentUriAndTransport(uri, ConfigurationOrigins.Default); // default because only called from constructor
            return true;
        }

        [MemberNotNull(nameof(_agentUri))]
        private void SetAgentUriAndTransport(Uri uri, ConfigurationOrigins origin)
        {
            if (uri.OriginalString.StartsWith(UnixDomainSocketPrefix, StringComparison.OrdinalIgnoreCase))
            {
                TracesTransport = TracesTransportType.UnixDomainSocket;
                TracesUnixDomainSocketPathInternal = uri.PathAndQuery;

                var absoluteUri = uri.AbsoluteUri.Replace(UnixDomainSocketPrefix, string.Empty);
                bool potentiallyInvalid = false;
                if (!Path.IsPathRooted(absoluteUri))
                {
                    potentiallyInvalid = true;
                    ValidationWarnings.Add($"The provided Uri {uri} contains a relative path which may not work. This is the path to the socket that will be used: {uri.PathAndQuery}");
                }

                // check if the file exists to warn the user.
                if (!_fileExists(uri.PathAndQuery))
                {
                    // We don't fallback in that case as the file could be mounted separately.
                    potentiallyInvalid = true;
                    ValidationWarnings.Add($"The socket provided {uri.PathAndQuery} cannot be found. The tracer will still rely on this socket to send traces.");
                }

                RecordTraceTransport(nameof(TracesTransportType.UnixDomainSocket), origin);
                _telemetry.Record(
                    ConfigurationKeys.TracesUnixDomainSocketPath,
                    TracesUnixDomainSocketPathInternal,
                    recordValue: true,
                    origin,
                    potentiallyInvalid ? TelemetryErrorCode.PotentiallyInvalidUdsPath : null);
            }
            else
            {
                TracesTransport = TracesTransportType.Default;
                RecordTraceTransport(nameof(TracesTransportType.Default), origin);
            }

            SetAgentUriReplacingLocalhost(uri, origin);
        }

        [MemberNotNull(nameof(_agentUri))]
        private void SetAgentUriReplacingLocalhost(Uri uri, ConfigurationOrigins origin)
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

            _telemetry.Record(ConfigurationKeys.AgentUri, _agentUri.ToString(), recordValue: true, origin);
        }

        private Uri CreateDefaultUri() => new Uri($"http://{DefaultAgentHost}:{DefaultAgentPort}");

        private void RecordTraceTransport(string transport, ConfigurationOrigins origin = ConfigurationOrigins.Default)
            => _telemetry.Record(ConfigTelemetryData.AgentTraceTransport, transport, recordValue: true, ConfigurationOrigins.Default);
    }
}
