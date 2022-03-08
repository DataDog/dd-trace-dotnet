// <copyright file="ConfigurationKeys.Debugger.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Debugger;
using Datadog.Trace.Logging.DirectSubmission;

namespace Datadog.Trace.Configuration
{
    internal static partial class ConfigurationKeys
    {
        internal static class Debugger
        {
            /// <summary>
            /// Configuration key for debugger poll interval.
            /// </summary>
            /// <seealso cref="DebuggerSettings.ProbeConfigurationsPollIntervalSeconds"/>
            public const string PollInterval = "DD_DEBUGGER_POLL_INTERVAL";

            /// <summary>
            /// Configuration key for debugger agent mode.
            /// </summary>
            /// <seealso cref="DebuggerSettings.ProbeMode"/>
            public const string AgentMode = "DD_DEBUGGER_AGENT_MODE";

            /// <summary>
            /// Configuration key for the URL used to query our backend directly for the list of active probes.
            /// This can only be used if DD-API-KEY is also available.
            /// </summary>
            /// <seealso cref="DebuggerSettings.ProbeConfigurationsPath"/>
            public const string ProbeUrl = "DD_DEBUGGER_PROBE_URL";

            /// <summary>
            /// Configuration key for probe configuration file full path.
            /// Loads the probe configuration from a local file on disk. Useful for local development and testing.
            /// </summary>
            /// <seealso cref="DebuggerSettings.ProbeConfigurationsPath"/>
            public const string ProbeFile = "DD_DEBUGGER_PROBE_FILE";

            /// <summary>
            /// Configuration key for enabling or disabling Live Debugger.
            /// Default is value is false (disabled).
            /// </summary>
            /// <seealso cref="DebuggerSettings.Enabled"/>
            public const string DebuggerEnabled = "DD_DEBUGGER_ENABLED";

            /// <summary>
            /// Configuration key for the max object depth to serialize for probe snapshots.
            /// Default value is 3.
            /// </summary>
            /// <seealso cref="DebuggerSettings.MaxDepthToSerialize"/>
            public const string MaxDepthToSerialize = "DD_DEBUGGER_MAX_DEPTH_TO_SERIALIZE";

            /// <summary>
            /// Configuration key for the maximum duration (in milliseconds) to run serialization for probe snapshots.
            /// Default value is 150 ms.
            /// </summary>
            /// <seealso cref="DebuggerSettings.SerializationTimeThreshold"/>
            public const string SerializationTimeThreshold = "DD_DEBUGGER_MAX_TIME_TO_SERIALIZE";

            /// <summary>
            /// Configuration key for the Agent host where the Debugger can send snapshots.
            /// Overridden by <see cref="AgentUri"/> if present.
            /// Default value is "127.0.0.1".
            /// </summary>
            /// <seealso cref="DebuggerSettings.AgentHost"/>
            public const string AgentHost = "DD_DEBUGGER_AGENT_HOST";

            /// <summary>
            /// Configuration key for the Agent port where the Debugger can send snapshots.
            /// Overridden by <see cref="AgentUri"/> if present.
            /// Default value is 8126.
            /// </summary>
            /// <seealso cref="DebuggerSettings.AgentPort"/>
            public const string AgentPort = "DD_DEBUGGER_AGENT_PORT";

            /// <summary>
            /// Configuration key for the Agent URL where the Debugger can send snapshots.
            /// Overrides values in <see cref="AgentHost"/> and <see cref="AgentPort"/> if present.
            /// Default value is "http://{AgentHost}:{AgentPort}" in default case is translate to "http://127.0.0.1:8126".
            /// </summary>
            /// <seealso cref="DebuggerSettings.AgentUri"/>
            public const string AgentUri = "DD_DEBUGGER_AGENT_URL";
        }
    }
}
