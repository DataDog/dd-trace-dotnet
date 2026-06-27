// <copyright file="ConfigurationKeys.LiveDebuggerPoc.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Configuration;

internal static partial class ConfigurationKeys
{
    /// <summary>
    /// Internal POC flag for enabling the live debugger flow recorder callbacks.
    /// </summary>
    public const string InternalDebuggerFlowRecorderEnabled = "DD_INTERNAL_DEBUGGER_FLOW_RECORDER_ENABLED";

    /// <summary>
    /// Internal POC file path used when flushing live debugger flow recorder events.
    /// </summary>
    public const string InternalDebuggerFlowRecorderOutputPath = "DD_INTERNAL_DEBUGGER_FLOW_RECORDER_OUTPUT_PATH";

    /// <summary>
    /// Internal POC maximum number of live debugger flow recorder events buffered in memory.
    /// </summary>
    public const string InternalDebuggerFlowRecorderBufferSize = "DD_INTERNAL_DEBUGGER_FLOW_RECORDER_BUFFER_SIZE";
}
