// <copyright file="TraceExporterInputFormat.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.LibDatadog.DataPipeline;

/// <summary>
/// Represents the format of the input traces, as expected by the trace exporter.
/// </summary>
internal enum TraceExporterInputFormat
{
    /// <summary>
    /// Used when the traces are sent to the agent without processing. The whole payload is sent as is to the agent.
    /// </summary>
    Proxy = 0,

    /// <summary>
    /// Version 0.4 of the trace exporter format.
    /// </summary>
    V04 = 1,
}
