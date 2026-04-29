// <copyright file="IOtlpExporter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_1_OR_GREATER

using System.Collections.Generic;
using System.Threading.Tasks;

#nullable enable

namespace Datadog.Trace.OpenTelemetry.Logs;

/// <summary>
/// Interface for OTLP log exporters.
/// Allows for dependency injection and testing.
/// </summary>
internal interface IOtlpExporter
{
    /// <summary>
    /// Exports a batch of logs using OTLP protocol asynchronously.
    /// </summary>
    /// <param name="logs">Batch of logs to export</param>
    /// <returns>ExportResult indicating success or failure</returns>
    Task<ExportResult> ExportAsync(IReadOnlyList<LogPoint> logs);

    /// <summary>
    /// Releases the exporter's HTTP resources. Does not wait on pending exports:
    /// the final flush runs synchronously in the sink's DisposeAsync before this is
    /// called, bounded by the HTTP client timeout (OTEL_EXPORTER_OTLP_TIMEOUT).
    /// </summary>
    /// <returns>True if shutdown completed successfully</returns>
    bool Shutdown();
}
#endif
