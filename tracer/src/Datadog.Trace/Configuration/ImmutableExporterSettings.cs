// <copyright file="ImmutableExporterSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

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
            TraceTransport = settings.TracesTransport;
            TracesPipeName = settings.TracesPipeName;
            TracesPipeTimeoutMs = settings.TracesPipeTimeoutMs;
            MetricsTransport = settings.MetricsTransport;
            MetricsPipeName = settings.MetricsPipeName;
            DogStatsdPort = settings.DogStatsdPort;
        }

        /// <summary>
        /// Gets the transport used to send traces to the Agent.
        /// </summary>
        public TracesTransportType TraceTransport { get; }

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
        /// Gets the transport used to send traces to the Agent.
        /// </summary>
        internal TracesTransportType TracesTransport { get; }

        /// <summary>
        /// Gets the transport used to connect to the DogStatsD.
        /// Default is <c>TransportStrategy.Tcp</c>.
        /// </summary>
        internal Vendors.StatsdClient.Transport.TransportType MetricsTransport { get; }
    }
}
