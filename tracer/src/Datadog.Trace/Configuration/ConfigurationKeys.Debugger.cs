// <copyright file="ConfigurationKeys.Debugger.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Debugger;
using Datadog.Trace.Debugger.ExceptionAutoInstrumentation;

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
            public const string MaxDepthToSerialize = "DD_DYNAMIC_INSTRUMENTATION_MAX_DEPTH_TO_SERIALIZE";

            /// <summary>
            /// Configuration key for the maximum duration (in milliseconds) to run serialization for probe snapshots.
            /// Default value is 150 ms.
            /// </summary>
            /// <seealso cref="DebuggerSettings.MaxSerializationTimeInMilliseconds"/>
            public const string MaxTimeToSerialize = "DD_DYNAMIC_INSTRUMENTATION_MAX_TIME_TO_SERIALIZE";

            /// <summary>
            /// Configuration key for the maximum upload batch size.
            /// Default value is 100.
            /// </summary>
            /// <seealso cref="DebuggerSettings.UploadBatchSize"/>
            public const string UploadBatchSize = "DD_DYNAMIC_INSTRUMENTATION_UPLOAD_BATCH_SIZE";

            /// <summary>
            /// Configuration key for the maximum symbol size to upload (in bytes).
            /// Default value is 1 mb.
            /// </summary>
            /// <seealso cref="DebuggerSettings.SymbolDatabaseBatchSizeInBytes"/>
            public const string SymbolDatabaseBatchSizeInBytes = "DD_SYMBOL_DATABASE_BATCH_SIZE_BYTES";

            /// <summary>
            /// Configuration key for allowing upload of symbol data (such as method names, parameter names, etc) to Datadog.
            /// Default value is false (disabled).
            /// </summary>
            /// <seealso cref="DebuggerSettings.SymbolDatabaseUploadEnabled"/>
            public const string SymbolDatabaseUploadEnabled = "DD_SYMBOL_DATABASE_UPLOAD_ENABLED";

            /// <summary>
            /// Configuration key for a separated comma list of libraries to include in the 3rd party detection
            /// Default value is empty.
            /// </summary>
            public const string ThirdPartyDetectionIncludes = "DD_THIRD_PARTY_DETECTION_INCLUDES";

            /// <summary>
            /// Configuration key for a separated comma list of libraries to include in the 3rd party detection
            /// Default value is empty.
            /// </summary>
            public const string ThirdPartyDetectionExcludes = "DD_THIRD_PARTY_DETECTION_EXCLUDES";

            /// <summary>
            /// Configuration key for a separated comma list of libraries to include in the 3rd party detection
            /// Default value is empty.
            /// </summary>
            public const string SymDbThirdPartyDetectionIncludes = "DD_SYMBOL_DATABASE_THIRD_PARTY_DETECTION_INCLUDES";

            /// <summary>
            /// Configuration key for a separated comma list of libraries to include in the 3rd party detection
            /// Default value is empty.
            /// </summary>
            public const string SymDbThirdPartyDetectionExcludes = "DD_SYMBOL_DATABASE_THIRD_PARTY_DETECTION_EXCLUDES";

            /// <summary>
            /// Configuration key for the interval (in seconds) between sending probe statuses.
            /// Default value is 3600.
            /// </summary>
            /// <seealso cref="DebuggerSettings.DiagnosticsIntervalSeconds"/>
            public const string DiagnosticsInterval = "DD_DYNAMIC_INSTRUMENTATION_DIAGNOSTICS_INTERVAL";

            /// <summary>
            /// Configuration key for the interval (in milliseconds) between flushing statuses.
            /// Default value is 0 (dynamic).
            /// </summary>
            /// <seealso cref="DebuggerSettings.UploadFlushIntervalMilliseconds"/>
            public const string UploadFlushInterval = "DD_DYNAMIC_INSTRUMENTATION_UPLOAD_FLUSH_INTERVAL";

            /// <summary>
            /// Configuration key for set of identifiers that are used in redaction decisions.
            /// </summary>
            /// <seealso cref="DebuggerSettings.RedactedIdentifiers"/>
            public const string RedactedIdentifiers = "DD_DYNAMIC_INSTRUMENTATION_REDACTED_IDENTIFIERS";

            /// <summary>
            /// Configuration key for set of types that are used in redaction decisions.
            /// </summary>
            /// <seealso cref="DebuggerSettings.RedactedTypes"/>
            public const string RedactedTypes = "DD_DYNAMIC_INSTRUMENTATION_REDACTED_TYPES";

            /// <summary>
            /// The old configuration key for enabling or disabling Exception Replay.
            /// Default value is false (disabled).
            /// </summary>
            /// <seealso cref="ExceptionReplaySettings.Enabled"/>
            [Obsolete]
            public const string ExceptionDebuggingEnabled = "DD_EXCEPTION_DEBUGGING_ENABLED";

            /// <summary>
            /// Configuration key for enabling or disabling Exception Replay.
            /// Default value is false (disabled).
            /// </summary>
            /// <seealso cref="ExceptionReplaySettings.Enabled"/>
            public const string ExceptionReplayEnabled = "DD_EXCEPTION_REPLAY_ENABLED";

            /// <summary>
            /// Configuration key for the maximum number of frames in a call stack we would like to capture values for.
            /// </summary>
            /// <seealso cref="ExceptionReplaySettings.MaximumFramesToCapture"/>
            public const string ExceptionReplayMaxFramesToCapture = "DD_EXCEPTION_REPLAY_MAX_FRAMES_TO_CAPTURE";

            /// <summary>
            /// Configuration key to enable capturing the variables of all the frames in exception call stack.
            /// Default value is false.
            /// </summary>
            /// <seealso cref="ExceptionReplaySettings.CaptureFullCallStack"/>
            public const string ExceptionReplayCaptureFullCallStackEnabled = "DD_EXCEPTION_REPLAY_CAPTURE_FULL_CALLSTACK_ENABLED";

            /// <summary>
            /// Configuration key for the interval used to track exceptions
            /// Default value is <c>1</c>h.
            /// </summary>
            /// <seealso cref="ExceptionReplaySettings.RateLimit"/>
            public const string RateLimitSeconds = "DD_EXCEPTION_REPLAY_RATE_LIMIT_SECONDS";

            /// <summary>
            /// Configuration key for setting the maximum number of exceptions to be analyzed by Exception Replay within a 1-second time interval.
            /// Default value is <c>100</c>.
            /// </summary>
            /// <seealso cref="ExceptionReplaySettings.MaxExceptionAnalysisLimit"/>
            public const string MaxExceptionAnalysisLimit = "DD_EXCEPTION_REPLAY_MAX_EXCEPTION_ANALYSIS_LIMIT";
        }
    }
}
