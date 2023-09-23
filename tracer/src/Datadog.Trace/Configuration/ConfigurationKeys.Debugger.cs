// <copyright file="ConfigurationKeys.Debugger.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Debugger;

namespace Datadog.Trace.Configuration
{
    internal static partial class ConfigurationKeys
    {
        internal static class Debugger
        {
            /// <summary>
            /// Configuration key for enabling or disabling Live Debugger.
            /// Default value is false (disabled).
            /// </summary>
            /// <seealso cref="DebuggerSettings.Enabled"/>
            public const string Enabled = "DD_DYNAMIC_INSTRUMENTATION_ENABLED";

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
            /// Configuration key for the maximum symbol size to upload.
            /// Default value is 1 mb.
            /// </summary>
            /// <seealso cref="DebuggerSettings.MaxSymbolSizeToUpload"/>
            public const string MaxSymbolSizeToUpload = "DD_DEBUGGER_MAX_SYMBOL_TO_UPLOAD";

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
