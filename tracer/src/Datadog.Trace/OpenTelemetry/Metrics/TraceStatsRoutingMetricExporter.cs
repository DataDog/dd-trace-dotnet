// <copyright file="TraceStatsRoutingMetricExporter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Vendors.OpenTelemetry.Exporter.OpenTelemetryProtocol;
using Datadog.Trace.Vendors.OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
#nullable enable

namespace Datadog.Trace.OpenTelemetry.Metrics
{
    /// <summary>
    /// TraceStatsRoutingMetricExporter multiplexes the OTLP export operation to configured exporters based on Insrumentation Scope Name.
    /// </summary>
    internal sealed class TraceStatsRoutingMetricExporter : MetricExporter
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(TraceStatsRoutingMetricExporter));
        private readonly OtlpExporter _traceStatsExporter;
        private readonly MetricExporter[] _additionalExporters;

        public TraceStatsRoutingMetricExporter(Configuration.TracerSettings settings, Uri endpoint, Configuration.OtlpProtocol protocol, IReadOnlyDictionary<string, string> headers, params MetricExporter[] additionalExporters)
        {
            _traceStatsExporter = new OtlpExporter(settings, endpoint, protocol, headers);
            _additionalExporters = additionalExporters;
        }

        /// <summary>
        /// Exports a batch of metrics using OTLP protocol asynchronously.
        /// This is the preferred method for better performance.
        /// </summary>
        /// <param name="metrics">Batch of metrics to export</param>
        /// <returns>ExportResult indicating success or failure</returns>
        public override async Task<ExportResult> ExportAsync(IEnumerable<MetricPoint> metrics)
        {
            if (!metrics.Any())
            {
                return ExportResult.Success;
            }

            try
            {
                var tasks = new Task<ExportResult>[_additionalExporters.Length + 1];
                tasks[0] = _traceStatsExporter.ExportAsync(metrics.Where(metric => metric.MeterName == "datadog.trace.metrics"));

                for (int i = 0; i < _additionalExporters.Length; i++)
                {
                    // Typically we will only have one additional exporter
                    // In practice, if we have multiple exporters this should be safe due to MetricReaderHandler.GetMetricPointsSnapshot() returning a List<MetricPoint>
                    tasks[i + 1] = _additionalExporters[i].ExportAsync(metrics.Where(metric => metric.MeterName != "datadog.trace.metrics"));
                }

                var results = await Task.WhenAll(tasks).ConfigureAwait(false);
                if (results.Contains(ExportResult.Failure))
                {
                    return ExportResult.Failure;
                }

                return ExportResult.Success;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error exporting OTLP metrics.");
                return ExportResult.Failure;
            }
        }

        /// <summary>
        /// Shuts down the exporter and ensures all pending exports complete.
        /// </summary>
        /// <param name="timeoutMilliseconds">Maximum time to wait for shutdown</param>
        /// <returns>True if shutdown completed successfully, false otherwise</returns>
        public override bool Shutdown(int timeoutMilliseconds)
        {
            var success = _traceStatsExporter.Shutdown(timeoutMilliseconds);
            foreach (var additionalExporter in _additionalExporters)
            {
                success &= additionalExporter.Shutdown(timeoutMilliseconds);
            }

            return success;
        }
    }
}
#endif
