// <copyright file="TelemetryClientConfiguration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Runtime.InteropServices;

namespace Datadog.Trace.LibDatadog.DataPipeline;

/// <summary>
/// Represents a configuration for the telemetry client.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct TelemetryClientConfiguration
{
    /// <summary>
    /// The interval at which telemetry should be sent, in milliseconds.
    /// </summary>
    public ulong Interval;

    /// <summary>
    /// A V4 UUID that represents a tracer session. This ID should:
    /// - Be generated when the tracer starts
    /// - Be identical within the context of a host (i.e. multiple threads/processes
    ///  that belong to a single instrumented app should share the same runtime_id)
    ///  - Be associated with traces to allow correlation between traces and telemetry data
    /// </summary>
    public CharSlice RuntimeId;

    /// <summary>
    /// Whether to enable debug mode for telemetry. When enabled, sets the dd-telemetry-debug-enabled header to true.
    /// Defaults to false.
    /// </summary>
    public bool DebugEnabled;

    /// <summary>
    /// Stable session identifier for the instrumented process. Attached to
    /// telemetry requests as the DD-Session-ID HTTP header. An empty
    /// CharSlice is treated as unset.
    /// </summary>
    public CharSlice SessionId;

    /// <summary>
    /// Identifier of the root process in a hierarchy of instrumented
    /// processes. Attached to telemetry requests as the DD-Root-Session-ID
    /// HTTP header when distinct from <see cref="SessionId"/>. An empty
    /// CharSlice is treated as unset.
    /// </summary>
    public CharSlice RootSessionId;

    /// <summary>
    /// Identifier of the immediate parent process. Attached to telemetry
    /// requests as the DD-Parent-Session-ID HTTP header when distinct from
    /// <see cref="SessionId"/>. An empty CharSlice is treated as unset.
    /// </summary>
    public CharSlice ParentSessionId;
}
