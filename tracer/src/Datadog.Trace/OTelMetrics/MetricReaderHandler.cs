// <copyright file="MetricReaderHandler.cs" company="Datadog">
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
    internal static class MetricReaderHandler
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(MetricReaderHandler));

        // Temporary storage for aggregation (like RuntimeMetrics._exceptionCounts)
        // Will be cleared after each export to prevent memory leaks
        private static readonly ConcurrentDictionary<string, MetricPoint> CapturedMetrics = new();

        private static readonly Dictionary<string, object?> EmptyTags = new();
        private static string? _cachedTemporality;

        public static void OnInstrumentPublished(Instrument instrument, MeterListener listener)
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
                listener.EnableMeasurementEvents(instrument, state: null);
                Log.Debug("Enabled measurement events for instrument: {InstrumentName} from meter: {MeterName}", instrument.Name, meterName);
            }
        }

        public static void OnMeasurementRecordedLong(Instrument instrument, long value, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
        {
            ProcessMeasurement(instrument, value, tags, isInteger: true);
        }

        public static void OnMeasurementRecordedDouble(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
        {
            ProcessMeasurement(instrument, value, tags, isInteger: false);
        }

        private static void ProcessMeasurement<T>(Instrument instrument, T value, ReadOnlySpan<KeyValuePair<string, object?>> tags, bool isInteger)
            where T : struct
        {
            try
            {
                var instrumentType = instrument.GetType().FullName;
                if (instrumentType is null)
                {
                    Log.Warning("Unable to get the full name of the instrument type for: {InstrumentName}", instrument.Name);
                    return;
                }

                ProcessMeasurementCore(instrument, instrumentType, value, tags, isInteger);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing measurement for instrument: {InstrumentName}", instrument.Name);
            }
        }

        private static void ProcessMeasurementCore<T>(Instrument instrument, string instrumentType, T value, ReadOnlySpan<KeyValuePair<string, object?>> tags, bool isInteger)
            where T : struct
        {
            string aggregationType;
            if (instrumentType.StartsWith("System.Diagnostics.Metrics.Counter`1") ||
                instrumentType.StartsWith("System.Diagnostics.Metrics.ObservableCounter`1"))
            {
                aggregationType = "Counter";
            }
            else if (instrumentType.StartsWith("System.Diagnostics.Metrics.UpDownCounter`1") ||
                     instrumentType.StartsWith("System.Diagnostics.Metrics.ObservableUpDownCounter`1"))
            {
                aggregationType = "UpDownCounter";
            }
            else if (instrumentType.StartsWith("System.Diagnostics.Metrics.Gauge`1") ||
                     instrumentType.StartsWith("System.Diagnostics.Metrics.ObservableGauge`1"))
            {
                aggregationType = "Gauge";
            }
            else if (instrumentType.StartsWith("System.Diagnostics.Metrics.Histogram`1"))
            {
                aggregationType = "Histogram";
            }
            else
            {
                Log.Debug("Skipping unsupported instrument: {InstrumentName} of type: {InstrumentType}", instrument.Name, instrumentType);
                return;
            }

            var doubleValue = Convert.ToDouble(value);

            // RFC requirement: Validate negative values for Counter and Histogram
            if ((aggregationType == "Counter" || aggregationType == "Histogram") && doubleValue < 0)
            {
                Log.Warning("Ignoring negative value {Value} for {InstrumentType} instrument: {InstrumentName}. API usage is incorrect.", doubleValue, aggregationType, instrument.Name);
                return;
            }

            var meterName = instrument.Meter.Name;
            var key = string.Concat(meterName, ".", instrument.Name);
            var temporality = GetTemporalityString();

            var tagsDict = tags.Length == 0 ? EmptyTags : CreateTagsDictionary(tags);

            CapturedMetrics.AddOrUpdate(
                key,
                _ =>
                {
                    var newMetric = new MetricPoint(instrument.Name, meterName, aggregationType, temporality, tagsDict, isInteger);
                    UpdateMetricPoint(newMetric, aggregationType, doubleValue);
                    return newMetric;
                },
                (_, existing) =>
                {
                    existing.IsIntegerValue = isInteger;
                    UpdateMetricPoint(existing, aggregationType, doubleValue);
                    return existing;
                });
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UpdateMetricPoint(MetricPoint metricPoint, string aggregationType, double value)
        {
            switch (aggregationType)
            {
                case "Counter":
                case "UpDownCounter":
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

        internal static void TriggerAsyncCollection()
        {
            Log.Debug("Manually triggering async instrument collection...");
            MetricReader.CollectObservableInstruments();
            Log.Debug("Async collection triggered.");
        }

        internal static IReadOnlyDictionary<string, MetricPoint> GetMetricsForExport(bool clearAfterGet = false)
        {
            var metrics = CapturedMetrics;

            if (clearAfterGet)
            {
                CapturedMetrics.Clear();
            }

            return metrics;
        }

        // For testing only
        internal static IReadOnlyDictionary<string, MetricPoint> GetCapturedMetricsForTesting() => CapturedMetrics;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string GetTemporalityString()
        {
            return _cachedTemporality ??= Tracer.Instance.Settings.OtlpMetricsTemporalityPreference.ToString();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Dictionary<string, object?> CreateTagsDictionary(ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            var tagsDict = new Dictionary<string, object?>(tags.Length);
            for (var i = 0; i < tags.Length; i++)
            {
                var tag = tags[i];
                tagsDict[tag.Key] = tag.Value;
            }

            return tagsDict;
        }
    }
}
#endif
