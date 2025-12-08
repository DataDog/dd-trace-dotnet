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
    /// Shuts down the exporter and ensures all pending exports complete.
    /// </summary>
    /// <param name="timeoutMilliseconds">Maximum time to wait for shutdown</param>
    /// <returns>True if shutdown completed successfully</returns>
    bool Shutdown(int timeoutMilliseconds);
}
#endif
