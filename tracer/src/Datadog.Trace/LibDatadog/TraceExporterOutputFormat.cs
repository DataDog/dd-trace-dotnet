// <copyright file="TraceExporterOutputFormat.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.LibDatadog;

/// <summary>
/// Represents the format of the output traces, as expected by the trace exporter.
/// </summary>
internal enum TraceExporterOutputFormat
{
    /// <summary>
    /// Version 0.4 of the trace exporter format.
    /// </summary>
    V04 = 0,

    /// <summary>
    /// Version 0.7 of the trace exporter format.
    /// </summary>
    V07 = 1,
}
