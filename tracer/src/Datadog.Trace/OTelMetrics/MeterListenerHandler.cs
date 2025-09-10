// <copyright file="MeterListenerHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using Datadog.Trace.Logging;

namespace Datadog.Trace.OTelMetrics
{
    internal static class MeterListenerHandler
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(MeterListenerHandler));

        private static readonly ConcurrentDictionary<string, MetricPoint> CapturedMetrics = new();

        public static void OnInstrumentPublished(Instrument instrument, System.Diagnostics.Metrics.MeterListener listener)
        {
            bool shouldEnable;
            var meterName = instrument.Meter.Name;
            var enabledMeterNames = Tracer.Instance.Settings.OpenTelemetryMeterNames;

            if (enabledMeterNames.Length > 0)
            {
                shouldEnable = enabledMeterNames.Contains(meterName);
            }
            else
            {
                shouldEnable = !meterName.StartsWith("System.", StringComparison.Ordinal);
            }

            if (shouldEnable)
            {
                Log.Debug("Enabling measurement events for instrument: {InstrumentName} from meter: {MeterName}", instrument.Name, meterName);
                listener.EnableMeasurementEvents(instrument, state: null);
            }
            else
            {
                Log.Debug("Skipping instrument: {InstrumentName} from meter: {MeterName} (filtered out)", instrument.Name, meterName);
            }
        }

        public static void OnMeasurementRecordedLong(Instrument instrument, long value, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
        {
            ProcessMeasurement(instrument, value, tags);
        }

        public static void OnMeasurementRecordedDouble(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
        {
            // Reuse the same logic but for double values
            ProcessMeasurement(instrument, value, tags);
        }

        private static void ProcessMeasurement<T>(Instrument instrument, T value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
            where T : struct
        {
            var instrumentType = instrument.GetType().FullName;
            if (instrumentType is null)
            {
                Log.Warning("Unable to get the full name of the instrument type for: {InstrumentName}", instrument.Name);
                return;
            }

            // Handle synchronous instruments as per RFC
            string aggregationType;
            if (instrumentType.StartsWith("System.Diagnostics.Metrics.Counter`1") ||
                instrumentType.StartsWith("System.Diagnostics.Metrics.UpDownCounter`1"))
            {
                aggregationType = "Counter"; // Both use Sum Aggregation -> Sum Metric Point
            }
            else if (instrumentType.StartsWith("System.Diagnostics.Metrics.Gauge`1"))
            {
                aggregationType = "Gauge"; // Last Value Aggregation -> Gauge Metric Point
            }
            else if (instrumentType.StartsWith("System.Diagnostics.Metrics.Histogram`1"))
            {
                aggregationType = "Histogram"; // Histogram Aggregation -> Histogram Metric Point
            }
            else
            {
                Log.Debug("Skipping unsupported instrument: {InstrumentName} of type: {InstrumentType}", instrument.Name, instrumentType);
                return;
            }

            var tagsDict = new Dictionary<string, object?>(tags.Length);
            var tagsArray = new string[tags.Length];
            for (var i = 0; i < tags.Length; i++)
            {
                var tag = tags[i];
                tagsDict[tag.Key] = tag.Value;
                tagsArray[i] = tag.Key + "=" + tag.Value;
            }

            // High-performance aggregation (like OTel)
            var doubleValue = Convert.ToDouble(value);
            var meterName = instrument.Meter.Name;
            var key = $"{meterName}.{instrument.Name}";
            var temporality = Tracer.Instance.Settings.OtlpMetricsTemporalityPreference.ToString();

            var metricPoint = CapturedMetrics.AddOrUpdate(
                key,
                // Create new MetricPoint
                _ => new MetricPoint(instrument.Name, meterName, aggregationType, temporality, tagsDict),
                // Update existing MetricPoint (lock-free where possible)
                (_, existing) =>
                {
                    UpdateMetricPoint(existing, aggregationType, doubleValue);
                    return existing;
                });

            // For new MetricPoints, record the first measurement
            if (metricPoint is { SnapshotCount: 0, SnapshotSum: 0, SnapshotGaugeValue: 0 })
            {
                UpdateMetricPoint(metricPoint, aggregationType, doubleValue);
            }

            Log.Debug("Captured {InstrumentType} measurement: {InstrumentName} = {Value}", aggregationType, instrument.Name, value);

            var tagsString = string.Join(",", tagsArray);

            // Take snapshot for display (like OTel export)
            metricPoint.TakeSnapshot(outputDelta: false);

            // Output the appropriate aggregated value for console testing
            var displayValue = aggregationType switch
            {
                "Counter" => metricPoint.SnapshotSum.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "Gauge" => metricPoint.SnapshotGaugeValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "Histogram" => $"count={metricPoint.SnapshotCount.ToString(System.Globalization.CultureInfo.InvariantCulture)},sum={metricPoint.SnapshotSum.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                _ => value.ToString()
            };

            Console.WriteLine($"[METRICS_CAPTURE] {key}|{metricPoint.InstrumentType}|{displayValue}|{tagsString}");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UpdateMetricPoint(MetricPoint metricPoint, string aggregationType, double value)
        {
            // High-performance updates (like OTel)
            switch (aggregationType)
            {
                case "Counter":
                    metricPoint.UpdateCounter(value);
                    break;
                case "Gauge":
                    metricPoint.UpdateGauge(value);
                    break;
                case "Histogram":
                    metricPoint.UpdateHistogram(value);
                    break;
            }
        }
    }
}
#endif
