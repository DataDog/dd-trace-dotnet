// <copyright file="OtelMetricsPipeline.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

#nullable enable

using System;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.OpenTelemetry.Metrics
{
    /// <summary>
    /// Composition root for the OpenTelemetry Metrics pipeline.
    /// Owns the MetricReader, MetricReaderHandler, and MetricExporter.
    /// </summary>
    internal sealed class OtelMetricsPipeline : IAsyncDisposable
    {
        private readonly MetricReader _reader;

        public OtelMetricsPipeline(TracerSettings settings, MetricExporter exporter)
        {
            var handler = new MetricReaderHandler(settings);
            _reader = new MetricReader(settings, handler, exporter);
        }

        public void Start()
        {
            _reader.Start();
        }

        public Task StopAsync()
        {
            return _reader.StopAsync();
        }

        public Task ForceCollectAndExportAsync()
        {
            return _reader.ForceCollectAndExportAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync().ConfigureAwait(false);
        }
    }
}
#endif

