// <copyright file="ImmutableExporterSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Agent;

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
        {
            AgentUri = settings.AgentUri;

            TracesTimeout = settings.TracesTimeout;
            TracesTransport = settings.TracesTransport;
            TracesPipeName = settings.TracesPipeName;
            TracesPipeTimeoutMs = settings.TracesPipeTimeoutMs;

            MetricsTransport = settings.MetricsTransport;
            MetricsPipeName = settings.MetricsPipeName;
            DogStatsdPort = settings.DogStatsdPort;

            TracesUnixDomainSocketPath = settings.TracesUnixDomainSocketPath;
            MetricsUnixDomainSocketPath = settings.MetricsUnixDomainSocketPath;

            PartialFlushEnabled = settings.PartialFlushEnabled;
            PartialFlushMinSpans = settings.PartialFlushMinSpans;
            ValidationWarnings = settings.ValidationWarnings.ToList();
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
        /// Default is <c>500</c>.
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
        /// Gets the timeout in seconds for sending traces to the Agent.
        /// Default is <c>10</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.TracesTimeout"/>
        internal int TracesTimeout { get; }

        /// <summary>
        /// Gets the transport used to send traces to the Agent.
        /// </summary>
        internal TracesTransportType TracesTransport { get; }

        /// <summary>
        /// Gets the transport used to connect to the DogStatsD.
        /// </summary>
        internal Vendors.StatsdClient.Transport.TransportType MetricsTransport { get; }

        internal List<string> ValidationWarnings { get; }
    }
}
