// <copyright file="MetricExporter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

using System.Collections.Generic;
using System.Threading.Tasks;

#nullable enable

namespace Datadog.Trace.OpenTelemetry.Metrics
{
    /// <summary>
    /// Base class for exporting metrics data via OTLP.
    /// </summary>
    internal abstract class MetricExporter
    {
        /// <summary>
        /// Exports a batch of metrics asynchronously.
        /// </summary>
        public abstract Task<ExportResult> ExportAsync(IReadOnlyList<MetricPoint> metrics);

        /// <summary>
        /// Shuts down the exporter.
        /// </summary>
        public abstract bool Shutdown(int timeoutMilliseconds);
    }
}
#endif
