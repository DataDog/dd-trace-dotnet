// <copyright file="MetricReader.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

#nullable enable

using System;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;

namespace Datadog.Trace.OTelMetrics
{
    /// <summary>
    /// Instance-based metric reader that collects and exports metrics on a periodic interval.
    /// Owns the MeterListener and coordinates between the handler and exporter.
    /// </summary>
    internal sealed class MetricReader
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(MetricReader));
        private readonly TracerSettings _settings;
        private readonly MetricReaderHandler _handler;
        private readonly MetricExporter _exporter;
        private MeterListener? _listener;
        private Timer? _timer;

        public MetricReader(TracerSettings settings, MetricReaderHandler handler, MetricExporter exporter)
        {
            _settings = settings;
            _handler = handler;
            _exporter = exporter;
        }

        public void Start()
        {
            var listener = new MeterListener
            {
                MeasurementsCompleted = static (_, _) => { },
                InstrumentPublished = _handler.OnInstrumentPublished
            };

            // Register measurement callbacks
            listener.SetMeasurementEventCallback<byte>(_handler.OnMeasurementRecordedByte);
            listener.SetMeasurementEventCallback<short>(_handler.OnMeasurementRecordedShort);
            listener.SetMeasurementEventCallback<int>(_handler.OnMeasurementRecordedInt);
            listener.SetMeasurementEventCallback<long>(_handler.OnMeasurementRecordedLong);
            listener.SetMeasurementEventCallback<float>(_handler.OnMeasurementRecordedFloat);
            listener.SetMeasurementEventCallback<double>(_handler.OnMeasurementRecordedDouble);

            listener.Start();
            _listener = listener;

            var interval = TimeSpan.FromMilliseconds(_settings.OtelMetricExportIntervalMs);
            _timer = new Timer(
                callback: async void (_) =>
                {
                    try
                    {
                        await ForceCollectAndExportAsync().ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        Log.Error(e, "Failed to export metrics");
                    }
                },
                state: null,
                dueTime: interval,
                period: interval);

            Log.Debug<int>("MetricReader started with {IntervalMs}ms export interval", _settings.OtelMetricExportIntervalMs);
        }

        public async Task StopAsync()
        {
            _timer?.DisposeAsync();
            _timer = null;

            try
            {
                await ForceCollectAndExportAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error during final metrics export on shutdown");
            }
            finally
            {
                _exporter.Shutdown(_settings.OtlpMetricsTimeoutMs);
                _listener?.Dispose();
                _listener = null;
                Log.Debug("MetricReader stopped");
            }
        }

        public void CollectObservableInstruments()
        {
            if (_listener != null)
            {
                try
                {
                    _listener.RecordObservableInstruments();
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Error collecting observable instruments");
                }
            }
        }

        public async Task ForceCollectAndExportAsync()
        {
            try
            {
                // Collect async instruments first
                CollectObservableInstruments();

                // Get a snapshot of all metric points (thread-safe via ToArray())
                var points = _handler.GetMetricPointsSnapshot();
                if (points.Count == 0)
                {
                    Log.Debug("No metrics to export");
                    return;
                }

                Log.Debug<int>("Exporting {Count} metric points", points.Count);
                var result = await _exporter.ExportAsync(points).ConfigureAwait(false);
                if (result == ExportResult.Failure)
                {
                    Log.Warning("Metrics export failed");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in metrics export process");
            }
        }
    }
}
#endif

