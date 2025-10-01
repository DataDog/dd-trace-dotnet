// <copyright file="InMemoryExporter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

#nullable enable

using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.Logging;

namespace Datadog.Trace.OTelMetrics
{
    /// <summary>
    /// In-memory exporter for testing purposes.
    /// Stores exported metrics in a thread-safe collection.
    /// </summary>
    internal sealed class InMemoryExporter : MetricExporter
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(InMemoryExporter));
        private readonly List<MetricPoint> _exportedMetrics = new();
        private readonly object _lock = new();

        public IReadOnlyList<MetricPoint> ExportedMetrics
        {
            get
            {
                lock (_lock)
                {
                    return _exportedMetrics.ToArray();
                }
            }
        }

        public override ExportResult Export(IReadOnlyList<MetricPoint> metrics)
        {
            lock (_lock)
            {
                _exportedMetrics.AddRange(metrics);
                Log.Debug<int, int>("InMemoryExporter: Exported {Count} metrics (total: {Total})", metrics.Count, _exportedMetrics.Count);
            }

            return ExportResult.Success;
        }

        public override Task<ExportResult> ExportAsync(IReadOnlyList<MetricPoint> metrics)
        {
            var result = Export(metrics);
            return Task.FromResult(result);
        }

        public override bool Shutdown(int timeoutMilliseconds)
        {
            // No-op for in-memory exporter
            return true;
        }

        public List<MetricPoint> Drain()
        {
            lock (_lock)
            {
                var copy = new List<MetricPoint>(_exportedMetrics);
                _exportedMetrics.Clear();
                return copy;
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _exportedMetrics.Clear();
            }
        }
    }
}
#endif

