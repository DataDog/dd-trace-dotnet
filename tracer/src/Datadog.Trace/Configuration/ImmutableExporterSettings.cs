// <copyright file="ImmutableExporterSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Contains exporter related settings.
    /// </summary>
    public partial class ImmutableExporterSettings
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ImmutableExporterSettings"/> class
        /// using the specified <see cref="IConfigurationSource"/> to initialize values.
        /// </summary>
        /// <param name="source">The <see cref="IConfigurationSource"/> to use when retrieving configuration values.</param>
        [PublicApi]
        public ImmutableExporterSettings(IConfigurationSource source)
            : this(new ExporterSettings(source, new ConfigurationTelemetry()), true)
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.ImmutableExporterSettings_Ctor_Source);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImmutableExporterSettings"/> class from
        /// a TracerSettings instance.
        /// </summary>
        /// <param name="settings">The tracer settings to use to populate the immutable tracer settings</param>
        [PublicApi]
        public ImmutableExporterSettings(ExporterSettings settings)
            : this(settings, true)
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.ImmutableExporterSettings_Ctor_Settings);
        }

        internal ImmutableExporterSettings(ExporterSettings settings, bool unused)
        {
            // unused parameter is purely so we can avoid calling public APIs
            AgentUriInternal = settings.AgentUriInternal;

            TracesTransport = settings.TracesTransport;
            TracesPipeNameInternal = settings.TracesPipeNameInternal;
            TracesPipeTimeoutMsInternal = settings.TracesPipeTimeoutMsInternal;

            MetricsTransport = settings.MetricsTransport;
            MetricsHostname = settings.MetricsHostname;
            MetricsPipeNameInternal = settings.MetricsPipeNameInternal;
            DogStatsdPortInternal = settings.DogStatsdPortInternal;

            TracesUnixDomainSocketPathInternal = settings.TracesUnixDomainSocketPathInternal;
            MetricsUnixDomainSocketPathInternal = settings.MetricsUnixDomainSocketPathInternal;

            PartialFlushEnabledInternal = settings.PartialFlushEnabledInternal;
            PartialFlushMinSpansInternal = settings.PartialFlushMinSpansInternal;
            ValidationWarnings = settings.ValidationWarnings.ToList();
        }

        /// <summary>
        /// Gets the Uri where the Tracer can connect to the Agent.
        /// Default is <c>"http://localhost:8126"</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.AgentUri"/>
        /// <seealso cref="ConfigurationKeys.AgentHost"/>
        /// <seealso cref="ConfigurationKeys.AgentPort"/>
        [GeneratePublicApi(PublicApiUsage.ImmutableExporterSettings_AgentUri_Get)]
        internal Uri AgentUriInternal { get; }

        /// <summary>
        /// Gets the windows pipe name where the Tracer can connect to the Agent.
        /// Default is <c>null</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.TracesPipeName"/>
        [GeneratePublicApi(PublicApiUsage.ImmutableExporterSettings_TracesPipeName_Get)]
        internal string? TracesPipeNameInternal { get; }

        /// <summary>
        /// Gets the timeout in milliseconds for the windows named pipe requests.
        /// Default is <c>100</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.TracesPipeTimeoutMs"/>
        [GeneratePublicApi(PublicApiUsage.ImmutableExporterSettings_TracesPipeTimeoutMs_Get)]
        internal int TracesPipeTimeoutMsInternal { get; }

        /// <summary>
        /// Gets the windows pipe name where the Tracer can send stats.
        /// Default is <c>null</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.MetricsPipeName"/>
        [GeneratePublicApi(PublicApiUsage.ImmutableExporterSettings_MetricsPipeName_Get)]
        internal string? MetricsPipeNameInternal { get; }

        /// <summary>
        /// Gets the port where the DogStatsd server is listening for connections.
        /// Default is <c>8125</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.DogStatsdPort"/>
        [GeneratePublicApi(PublicApiUsage.ImmutableExporterSettings_DogStatsdPort_Get)]
        internal int DogStatsdPortInternal { get; }

        /// <summary>
        /// Gets a value indicating whether partial flush is enabled
        /// </summary>
        [GeneratePublicApi(PublicApiUsage.ImmutableExporterSettings_PartialFlushEnabled_Get)]
        internal bool PartialFlushEnabledInternal { get; }

        /// <summary>
        /// Gets the minimum number of closed spans in a trace before it's partially flushed
        /// </summary>
        [GeneratePublicApi(PublicApiUsage.ImmutableExporterSettings_PartialFlushMinSpans_Get)]
        internal int PartialFlushMinSpansInternal { get; }

        /// <summary>
        /// Gets the unix domain socket path where the Tracer can connect to the Agent.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.TracesUnixDomainSocketPath"/>
        [GeneratePublicApi(PublicApiUsage.ImmutableExporterSettings_TracesUnixDomainSocketPath_Get)]
        internal string? TracesUnixDomainSocketPathInternal { get; }

        /// <summary>
        /// Gets the unix domain socket path where the Tracer can send stats.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.MetricsUnixDomainSocketPath"/>
        [GeneratePublicApi(PublicApiUsage.ImmutableExporterSettings_MetricsUnixDomainSocketPath_Get)]
        internal string? MetricsUnixDomainSocketPathInternal { get; }

        /// <summary>
        /// Gets the transport used to send traces to the Agent.
        /// </summary>
        internal TracesTransportType TracesTransport { get; }

        /// <summary>
        /// Gets the transport used to connect to the DogStatsD.
        /// </summary>
        internal Vendors.StatsdClient.Transport.TransportType MetricsTransport { get; }

        /// <summary>
        /// Gets the agent host to use when <see cref="MetricsTransport"/> is <see cref="Vendors.StatsdClient.Transport.TransportType.UDP"/>
        /// </summary>
        internal string MetricsHostname { get; }

        internal List<string> ValidationWarnings { get; }
    }
}
