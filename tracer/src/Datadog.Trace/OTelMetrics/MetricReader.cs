// <copyright file="MetricReader.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Logging;

namespace Datadog.Trace.OTelMetrics
{
    /// <summary>
    /// MetricReader is responsible for collecting metrics from MeterListener and exporting them via MetricExporter.
    /// This follows the OpenTelemetry Metrics SDK pattern where MetricReader calls MetricExporter.Export().
    /// </summary>
    internal static class MetricReader
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(MetricReader));

        private static System.Diagnostics.Metrics.MeterListener? _meterListenerInstance;
        private static MetricExporter? _metricExporter;
        private static Timer? _exportTimer;
        private static int _initialized;
        private static int _stopped;

        public static bool IsRunning =>
            Interlocked.CompareExchange(ref _initialized, 1, 1) == 1 &&
            Interlocked.CompareExchange(ref _stopped, 0, 0) == 0;

        public static void Initialize()
        {
            if (Interlocked.CompareExchange(ref _initialized, 1, 0) == 1)
            {
                return;
            }

            // Initialize MeterListener for collecting metrics
            var meterListener = new System.Diagnostics.Metrics.MeterListener();
            meterListener.InstrumentPublished = MetricReaderHandler.OnInstrumentPublished;

            meterListener.SetMeasurementEventCallback<long>(MetricReaderHandler.OnMeasurementRecordedLong);
            meterListener.SetMeasurementEventCallback<double>(MetricReaderHandler.OnMeasurementRecordedDouble);

            meterListener.Start();
            _meterListenerInstance = meterListener;

            // Initialize MetricExporter (OTLP implementation)
            _metricExporter = new OtlpExporter(Tracer.Instance.Settings);

            // Start periodic export timer (RFC: 10s default)
            var settings = Tracer.Instance.Settings;
            var exportInterval = TimeSpan.FromMilliseconds(settings.OtelMetricExportIntervalMs);
            _exportTimer = new Timer(
                callback: _ => ExportMetrics(),
                state: null,
                dueTime: exportInterval,
                period: exportInterval);

            // Register for graceful shutdown
            LifetimeManager.Instance.AddAsyncShutdownTask((_) => StopAsync());

            Log.Debug("MetricReader initialized successfully.");
        }

        public static void Stop()
        {
            if (_meterListenerInstance is IDisposable disposableListener)
            {
                _meterListenerInstance = null;
                disposableListener.Dispose();
                Interlocked.Exchange(ref _stopped, 1);
                Log.Debug("MeterListener stopped.");
            }

            _exportTimer?.Dispose();
            _exportTimer = null;
        }

        public static async Task StopAsync()
        {
            _exportTimer?.DisposeAsync();
            _exportTimer = null;

            // Ensure any pending exports complete before shutdown
            try
            {
                // Do a final export before shutdown
                await ExportMetricsAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error during final metrics export on shutdown");
            }
            finally
            {
                _metricExporter?.Shutdown(5000); // 5 second timeout
                _metricExporter = null;
            }
        }

        internal static void CollectObservableInstruments()
        {
            if (_meterListenerInstance != null)
            {
                try
                {
                    _meterListenerInstance.RecordObservableInstruments();
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Error collecting observable instruments.");
                }
            }
        }

        private static async void ExportMetrics()
        {
            try
            {
                await ExportMetricsAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in periodic metrics export");
            }
        }

        private static async Task ExportMetricsAsync()
        {
            if (_metricExporter == null)
            {
                Log.Warning("MetricExporter is not initialized.");
                return;
            }

            try
            {
                // Collect observable instruments first (like OpenTelemetry SDK)
                CollectObservableInstruments();

                // Get captured metrics for export
                var capturedMetrics = MetricReaderHandler.GetMetricsForExport();

                if (capturedMetrics.Count == 0)
                {
                    Log.Debug("No metrics to export.");
                    return;
                }

                // Convert to list for export
                var metricsList = new List<MetricPoint>();
                foreach (var kvp in capturedMetrics)
                {
                    metricsList.Add(kvp.Value);
                }

                // Export via MetricExporter interface (this is the key RFC pattern!)
                // Use async method for better performance
                var result = await _metricExporter.ExportAsync(metricsList).ConfigureAwait(false);
                if (result == ExportResult.Failure)
                {
                    Log.Warning("MetricExporter.ExportAsync() returned Failure - export failed.");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in metrics export process.");
            }
        }
    }
}
#endif

