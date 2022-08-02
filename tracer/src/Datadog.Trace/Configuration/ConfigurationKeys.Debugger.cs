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
            /// Configuration key for debugger poll interval (in seconds).
            /// </summary>
            /// <seealso cref="DebuggerSettings.ProbeConfigurationsPollIntervalSeconds"/>
            public const string PollInterval = "DD_DEBUGGER_POLL_INTERVAL";

            /// <summary>
            /// Configuration key for the URL used to query our backend directly for the list of active probes.
            /// This can only be used if DD_API_KEY is also available.
            /// </summary>
            /// <seealso cref="DebuggerSettings.ProbeConfigurationsPath"/>
            public const string SnapshotUrl = "DD_DEBUGGER_SNAPSHOT_URL";

            /// <summary>
            /// Configuration key for probe configuration file full path.
            /// Loads the probe configuration from a local file on disk. Useful for local development and testing.
            /// </summary>
            /// <seealso cref="DebuggerSettings.ProbeConfigurationsPath"/>
            public const string ProbeFile = "DD_DEBUGGER_PROBE_FILE";

            /// <summary>
            /// Configuration key for enabling or disabling Live Debugger.
            /// Default value is false (disabled).
            /// </summary>
            /// <seealso cref="DebuggerSettings.Enabled"/>
            public const string Enabled = "DD_INTERNAL_DEBUGGER_ENABLED";

            /// <summary>
            /// Configuration key for the max object depth to serialize for probe snapshots.
            /// Default value is 1.
            /// </summary>
            /// <seealso cref="DebuggerSettings.MaximumDepthOfMembersToCopy"/>
            public const string MaxDepthToSerialize = "DD_DEBUGGER_MAX_DEPTH_TO_SERIALIZE";

            /// <summary>
            /// Configuration key for the maximum duration (in milliseconds) to run serialization for probe snapshots.
            /// Default value is 150 ms.
            /// </summary>
            /// <seealso cref="DebuggerSettings.MaxSerializationTimeInMilliseconds"/>
            public const string MaxTimeToSerialize = "DD_DEBUGGER_MAX_TIME_TO_SERIALIZE";

            /// <summary>
            /// Configuration key for the maximum upload batch size.
            /// Default value is 100.
            /// </summary>
            /// <seealso cref="DebuggerSettings.UploadBatchSize"/>
            public const string UploadBatchSize = "DD_DEBUGGER_UPLOAD_BATCH_SIZE";

            /// <summary>
            /// Configuration key for the interval (in seconds) between sending probe statuses.
            /// Default value is 3600.
            /// </summary>
            /// <seealso cref="DebuggerSettings.DiagnosticsIntervalSeconds"/>
            public const string DiagnosticsInterval = "DD_DEBUGGER_DIAGNOSTICS_INTERVAL";

            /// <summary>
            /// Configuration key for the interval (in milliseconds) between flushing statuses.
            /// Default value is 0 (dynamic).
            /// </summary>
            /// <seealso cref="DebuggerSettings.UploadFlushIntervalMilliseconds"/>
            public const string UploadFlushInterval = "DD_DEBUGGER_UPLOAD_FLUSH_INTERVAL";
        }
    }
}
