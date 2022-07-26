// <copyright file="ConfigurationKeys.Exporter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// String constants for standard Datadog configuration keys.
    /// </summary>
    internal static partial class ConfigurationKeys
    {
        /// <summary>
        /// Configuration key for the Agent host where the Tracer can send traces.
        /// Overridden by <see cref="AgentUri"/> if present.
        /// Default value is "localhost".
        /// </summary>
        /// <seealso cref="ExporterSettings.AgentUri"/>
        public const string AgentHost = "DD_AGENT_HOST";

        /// <summary>
        /// Configuration key for the Agent port where the Tracer can send traces.
        /// Default value is 8126.
        /// </summary>
        /// <seealso cref="ExporterSettings.AgentUri"/>
        public const string AgentPort = "DD_TRACE_AGENT_PORT";

        /// <summary>
        /// Configuration key for setting the timeout in seconds for sending traces to the Agent.
        /// Default value is <c>10</c>.
        /// </summary>
        /// <seealso cref="ExporterSettings.TracesTimeout"/>
        public const string TracesTimeout = "DD_TRACE_AGENT_TIMEOUT";

        /// <summary>
        /// Configuration key for the named pipe where the Tracer can send traces.
        /// Default value is <c>null</c>.
        /// </summary>
        /// <seealso cref="ExporterSettings.TracesPipeName"/>
        public const string TracesPipeName = "DD_TRACE_PIPE_NAME";

        /// <summary>
        /// Configuration key for setting the timeout in milliseconds for named pipes communication.
        /// Default value is <c>0</c>.
        /// </summary>
        /// <seealso cref="ExporterSettings.TracesPipeTimeoutMs"/>
        public const string TracesPipeTimeoutMs = "DD_TRACE_PIPE_TIMEOUT_MS";

        /// <summary>
        /// Configuration key for the named pipe that DogStatsD binds to.
        /// Default value is <c>null</c>.
        /// </summary>
        /// <seealso cref="ExporterSettings.MetricsPipeName"/>
        public const string MetricsPipeName = "DD_DOGSTATSD_PIPE_NAME";

        /// <summary>
        /// Sibling setting for <see cref="AgentPort"/>.
        /// Used to force a specific port binding for the Trace Agent.
        /// Default value is 8126.
        /// </summary>
        /// <seealso cref="ExporterSettings.AgentUri"/>
        public const string TraceAgentPortKey = "DD_APM_RECEIVER_PORT";

        /// <summary>
        /// Configuration key for the Agent URL where the Tracer can send traces.
        /// Overrides values in <see cref="AgentHost"/> and <see cref="AgentPort"/> if present.
        /// Default value is "http://localhost:8126".
        /// </summary>
        /// <seealso cref="ExporterSettings.AgentUri"/>
        public const string AgentUri = "DD_TRACE_AGENT_URL";

        /// <summary>
        /// Configuration key for the DogStatsd port where the Tracer can send metrics.
        /// Default value is 8125.
        /// </summary>
        /// <seealso cref="ExporterSettings.DogStatsdPort"/>
        public const string DogStatsdPort = "DD_DOGSTATSD_PORT";

        /// <summary>
        /// Configuration key to enable sending partial traces to the agent
        /// </summary>
        /// <seealso cref="ExporterSettings.PartialFlushEnabled"/>
        public const string PartialFlushEnabled = "DD_TRACE_PARTIAL_FLUSH_ENABLED";

        /// <summary>
        /// Configuration key to set the minimum number of closed spans in a trace before it's partially flushed
        /// </summary>
        /// <seealso cref="ExporterSettings.PartialFlushMinSpans"/>
        public const string PartialFlushMinSpans = "DD_TRACE_PARTIAL_FLUSH_MIN_SPANS";

        /// <summary>
        /// Configuration key for the unix domain socket where the Tracer can send traces.
        /// Default value is <c>null</c>.
        /// </summary>
        /// <seealso cref="ExporterSettings.TracesUnixDomainSocketPath"/>
        public const string TracesUnixDomainSocketPath = "DD_APM_RECEIVER_SOCKET";

        /// <summary>
        /// Configuration key for the unix domain socket that DogStatsD binds to.
        /// Default value is <c>null</c>.
        /// </summary>
        /// <seealso cref="ExporterSettings.MetricsUnixDomainSocketPath"/>
        public const string MetricsUnixDomainSocketPath = "DD_DOGSTATSD_SOCKET";
    }
}
