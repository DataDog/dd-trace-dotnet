// <copyright file="ConfigurationKeys.Telemetry.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Telemetry;

namespace Datadog.Trace.Configuration
{
    internal static partial class ConfigurationKeys
    {
        internal static class Telemetry
        {
            /// <summary>
            /// Configuration key for enabling or disabling internal telemetry.
            /// Default value is <c>true</c> (enabled).
            /// </summary>
            public const string Enabled = "DD_INSTRUMENTATION_TELEMETRY_ENABLED";

            /// <summary>
            /// Configuration key for sending telemetry direct to telemetry intake. If enabled, and
            /// <see cref="ConfigurationKeys.ApiKey"/> is set, sends telemetry direct to intake if agent is not
            /// available. Enabled by default if <see cref="ConfigurationKeys.ApiKey"/> is available.
            /// </summary>
            public const string AgentlessEnabled = "DD_INSTRUMENTATION_TELEMETRY_AGENTLESS_ENABLED";

            /// <summary>
            /// Configuration key for sending telemetry via agent proxy. If enabled, sends telemetry
            /// via agent proxy. Enabled by default. If disabled, or agent is not available, telemetry
            /// is sent to agentless endpoint, based on <see cref="AgentlessEnabled"/> setting.
            /// </summary>
            public const string AgentProxyEnabled = "DD_INSTRUMENTATION_TELEMETRY_AGENT_PROXY_ENABLED";

            /// <summary>
            /// Configuration key for the telemetry URL where the Tracer sends telemetry. Only applies when agentless
            /// telemetry is in use (otherwise telemetry is sent to the agent using
            /// <see cref="ExporterSettings.AgentUri"/> instead)
            /// </summary>
            public const string Uri = "DD_INSTRUMENTATION_TELEMETRY_URL";

            /// <summary>
            /// Configuration key for how often telemetry should be sent, in seconds. Must be between 1 and 3600.
            /// For testing purposes. Defaults to 60
            /// <see cref="TelemetrySettings.HeartbeatInterval"/>
            /// </summary>
            public const string HeartbeatIntervalSeconds = "DD_TELEMETRY_HEARTBEAT_INTERVAL";

            /// <summary>
            /// Configuration key for whether to send redacted internal diagnostic logs to Datadog.
            /// Defaults to true
            /// </summary>
            public const string LogCollectionEnabled = "DD_TELEMETRY_LOG_COLLECTION_ENABLED";
        }
    }
}
