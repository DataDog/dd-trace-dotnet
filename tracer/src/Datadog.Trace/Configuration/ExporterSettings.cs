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
    [GenerateSnapshot]
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
        /// The default port value for dogstatsd.
        /// </summary>
        internal const int DefaultDogstatsdPort = 8125;

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
            TelemetryFactory.Metrics.Record(PublicApiUsage.ExporterSettings_Ctor);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExporterSettings"/> class
        /// using the specified <see cref="IConfigurationSource"/> to initialize values.
        /// </summary>
        /// <param name="source">The <see cref="IConfigurationSource"/> to use when retrieving configuration values.</param>
        /// <remarks>
        /// We deliberately don't use the static <see cref="TelemetryFactory.Config"/> collector here
        /// as we don't want to automatically record these values, only once they're "activated",
        /// in <see cref="Tracer.Configure(TracerSettings)"/>
        /// </remarks>
        [PublicApi]
        public ExporterSettings(IConfigurationSource? source)
            : this(source, File.Exists, new ConfigurationTelemetry())
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.ExporterSettings_Ctor_Source);
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

        [IgnoreForSnapshot] // We record the config when it's changed
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

#pragma warning disable SA1624 // Documentation summary should begin with "Gets" - the documentation is primarily for public property
        /// <summary>
        /// Gets or sets the windows pipe name where the Tracer can connect to the Agent.
        /// Default is <c>null</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.TracesPipeName"/>
        [GeneratePublicApi(
            PublicApiUsage.ExporterSettings_TracesPipeName_Get,
            PublicApiUsage.ExporterSettings_TracesPipeName_Set)]
        [ConfigKey(ConfigurationKeys.TracesPipeName)]
        internal string? TracesPipeNameInternal { get; private set; }

        /// <summary>
        /// Gets or sets the timeout in milliseconds for the windows named pipe requests.
        /// Default is <c>100</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.TracesPipeTimeoutMs"/>
        [GeneratePublicApi(
            PublicApiUsage.ExporterSettings_TracesPipeTimeoutMs_Get,
            PublicApiUsage.ExporterSettings_TracesPipeTimeoutMs_Set)]
        [ConfigKey(ConfigurationKeys.TracesPipeTimeoutMs)]
        internal int TracesPipeTimeoutMsInternal { get; set; }

        /// <summary>
        /// Gets or sets the windows pipe name where the Tracer can send stats.
        /// Default is <c>null</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.MetricsPipeName"/>
        [GeneratePublicApi(
            PublicApiUsage.ExporterSettings_MetricsPipeName_Get,
            PublicApiUsage.ExporterSettings_MetricsPipeName_Set)]
        [ConfigKey(ConfigurationKeys.MetricsPipeName)]
        internal string? MetricsPipeNameInternal { get; private set; }

        /// <summary>
        /// Gets or sets the unix domain socket path where the Tracer can connect to the Agent.
        /// This parameter is deprecated and shall be removed. Consider using AgentUri instead
        /// </summary>
        [GeneratePublicApi(
            PublicApiUsage.ExporterSettings_TracesUnixDomainSocketPath_Get,
            PublicApiUsage.ExporterSettings_TracesUnixDomainSocketPath_Set)]
        [ConfigKey(ConfigurationKeys.TracesUnixDomainSocketPath)]
        internal string? TracesUnixDomainSocketPathInternal { get; private set; }

        /// <summary>
        /// Gets or sets the unix domain socket path where the Tracer can send stats.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.MetricsUnixDomainSocketPath"/>
        [GeneratePublicApi(
            PublicApiUsage.ExporterSettings_MetricsUnixDomainSocketPath_Get,
            PublicApiUsage.ExporterSettings_MetricsUnixDomainSocketPath_Set)]
        [ConfigKey(ConfigurationKeys.MetricsUnixDomainSocketPath)]
        internal string? MetricsUnixDomainSocketPathInternal { get; private set; }

        /// <summary>
        /// Gets or sets the port where the DogStatsd server is listening for connections.
        /// Default is <c>8125</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.DogStatsdPort"/>
        [GeneratePublicApi(
            PublicApiUsage.ExporterSettings_DogStatsdPort_Get,
            PublicApiUsage.ExporterSettings_DogStatsdPort_Set)]
        [ConfigKey(ConfigurationKeys.DogStatsdPort)]
        internal int DogStatsdPortInternal { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether partial flush is enabled
        /// </summary>
        [GeneratePublicApi(
            PublicApiUsage.ExporterSettings_PartialFlushEnabled_Get,
            PublicApiUsage.ExporterSettings_PartialFlushEnabled_Set)]
        [ConfigKey(ConfigurationKeys.PartialFlushEnabled)]
        internal bool PartialFlushEnabledInternal { get; private set; }
#pragma warning restore SA1624

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
                PartialFlushMinSpansInternal = value;
            }
        }

        [IgnoreForSnapshot] // we record the config when it's changed
        internal int PartialFlushMinSpansInternal
        {
            get => _partialFlushMinSpans;

            private set
            {
                if (value <= 0)
                {
                    throw new ArgumentException("The value must be strictly greater than 0", nameof(PartialFlushMinSpansInternal));
                }

                // Not recording the error condition above, because it can never actually be used
                // If we instead rejected `value` and used a default, then we _would_ record it here
                _telemetry.Record(ConfigurationKeys.PartialFlushMinSpans, value, ConfigurationOrigins.Code);
                _partialFlushMinSpans = value;
            }
        }

#pragma warning disable SA1624 // Documentation summary should begin with "Gets" - the documentation is primarily for public property
        /// <summary>
        /// Gets or sets the transport used to send traces to the Agent.
        /// </summary>
        [IgnoreForSnapshot] // We record this in telemetry when we set it
        internal TracesTransportType TracesTransport { get; private set; }

        /// <summary>
        /// Gets or sets the transport used to connect to the DogStatsD.
        /// Default is <c>TransportStrategy.Tcp</c>.
        /// </summary>
        [IgnoreForSnapshot] // We don't record this in telemetry currently, but if we do, we'll record it when we set its
        internal MetricsTransportType MetricsTransport { get; private set; }
#pragma warning restore SA1624

        internal List<string> ValidationWarnings { get; }

        internal IConfigurationTelemetry Telemetry => _telemetry;

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

        private void RecordTraceTransport(string transport, ConfigurationOrigins origin = ConfigurationOrigins.Default)
            => _telemetry.Record(ConfigTelemetryData.AgentTraceTransport, transport, recordValue: true, ConfigurationOrigins.Default);
    }
}
