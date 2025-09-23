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
using System.Threading;
using Datadog.Trace.Logging;

namespace Datadog.Trace.OTelMetrics
{
    internal static class MetricReaderHandler
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(MetricReaderHandler));

        // Temporary storage for aggregation (like RuntimeMetrics._exceptionCounts)
        // Will be cleared after each export to prevent memory leaks
        private static readonly ConcurrentDictionary<MetricStreamIdentity, MetricState> CapturedMetrics = new();
        private static readonly HashSet<string> MetricStreamNames = new(StringComparer.OrdinalIgnoreCase);

        private static int _metricCount;

        public static void OnInstrumentPublished(Instrument instrument, MeterListener listener)
        {
            var meterName = instrument.Meter.Name;

            // RFC requirement: Validate instrument name syntax
            if (!IsValidInstrumentName(instrument.Name))
            {
                Log.Warning("Invalid instrument name '{InstrumentName}' from meter '{MeterName}'. Instrument names must be ASCII, start with letter, contain only alphanumeric characters, '_', '.', '/', '-' and be max 255 characters.", instrument.Name, meterName);
                return; // Skip this instrument
            }

            bool shouldEnable;
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
                // Create state object for this instrument to avoid ConcurrentDictionary contention
                var state = CreateMetricState(instrument);
                if (state != null)
                {
                    listener.EnableMeasurementEvents(instrument, state);
                    Log.Debug("Enabled measurement events for instrument: {InstrumentName} from meter: {MeterName}", instrument.Name, meterName);
                }
                else
                {
                    Log.Warning("Failed to create MetricState for instrument '{InstrumentName}' from meter '{MeterName}'. Instrument will be ignored.", instrument.Name, meterName);
                }
            }
        }

        public static void OnMeasurementRecordedLong(Instrument instrument, long value, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
        {
            if (state is MetricState metricState)
            {
                metricState.RecordMeasurementLong(value, tags);
            }
            else
            {
                // Log error - this should not happen if we follow OTel SDK pattern
                Log.Warning("Measurement recorded for instrument '{InstrumentName}' but no MetricState provided. Measurement will be dropped.", instrument.Name);
            }
        }

        public static void OnMeasurementRecordedDouble(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
        {
            if (state is MetricState metricState)
            {
                metricState.RecordMeasurementDouble(value, tags);
            }
            else
            {
                // Log error - this should not happen if we follow OTel SDK pattern
                Log.Warning("Measurement recorded for instrument '{InstrumentName}' but no MetricState provided. Measurement will be dropped.", instrument.Name);
            }
        }

        // Overloads for different numeric types
        public static void OnMeasurementRecordedByte(Instrument instrument, byte value, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
        {
            OnMeasurementRecordedLong(instrument, value, tags, state);
        }

        public static void OnMeasurementRecordedShort(Instrument instrument, short value, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
        {
            OnMeasurementRecordedLong(instrument, value, tags, state);
        }

        public static void OnMeasurementRecordedInt(Instrument instrument, int value, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
        {
            OnMeasurementRecordedLong(instrument, value, tags, state);
        }

        public static void OnMeasurementRecordedFloat(Instrument instrument, float value, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
        {
            OnMeasurementRecordedDouble(instrument, value, tags, state);
        }

        internal static IReadOnlyList<MetricPoint> GetMetricsForExport(bool clearAfterGet = false)
        {
            var allMetricPoints = new List<MetricPoint>();

            foreach (var kvp in CapturedMetrics)
            {
                var metricState = kvp.Value;
                var metricPoints = metricState.GetMetricPoints();
                allMetricPoints.AddRange(metricPoints);
            }

            if (clearAfterGet)
            {
                CapturedMetrics.Clear();
            }

            return allMetricPoints;
        }

        // For testing only
        internal static IReadOnlyList<MetricPoint> GetCapturedMetricsForTesting()
        {
            var allMetricPoints = new List<MetricPoint>();

            foreach (var kvp in CapturedMetrics)
            {
                var metricState = kvp.Value;
                var metricPoints = metricState.GetMetricPoints();
                allMetricPoints.AddRange(metricPoints);
            }

            return allMetricPoints;
        }

        // For testing only - reset all captured metrics
        internal static void ResetForTesting()
        {
            CapturedMetrics.Clear();
            MetricStreamNames.Clear();
            Interlocked.Exchange(ref _metricCount, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsValidInstrumentName(string name)
        {
            // RFC requirement: Instrument name validation
            // 1. Not null and not empty
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            // 2. Maximum length 255 characters
            if (name.Length > 255)
            {
                return false;
            }

            // 3. ASCII string
            if (!IsAsciiString(name))
            {
                return false;
            }

            // 4. First character must be alphabetic
            if (!char.IsLetter(name[0]))
            {
                return false;
            }

            // 5. Subsequent characters must be alphanumeric, '_', '.', '/', '-'
            for (int i = 1; i < name.Length; i++)
            {
                char c = name[i];
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '.' && c != '/' && c != '-')
                {
                    return false;
                }
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsAsciiString(string str)
        {
            // ASCII range is 0-127
            foreach (char c in str)
            {
                if (c > 127)
                {
                    return false;
                }
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static AggregationTemporality? GetTemporality(InstrumentType instrumentType)
        {
            // Gauges have no temporality according to OTLP spec
            if (instrumentType is InstrumentType.Gauge or InstrumentType.ObservableGauge)
            {
                return null;
            }

            return Tracer.Instance.Settings.OtlpMetricsTemporalityPreference switch
            {
                // CumulativePreference: All instruments get Cumulative
                Configuration.OtlpTemporality.Cumulative => AggregationTemporality.Cumulative,

                // DeltaPreference: Counter/ObservableCounter/Histogram → Delta, UpDownCounter/ObservableUpDownCounter → Cumulative
                Configuration.OtlpTemporality.Delta => instrumentType switch
                {
                    InstrumentType.Counter or InstrumentType.ObservableCounter or InstrumentType.Histogram
                        => AggregationTemporality.Delta,
                    InstrumentType.UpDownCounter or InstrumentType.ObservableUpDownCounter
                        => AggregationTemporality.Cumulative,
                    _ => AggregationTemporality.Delta
                },

                // LowMemoryPreference: Counter/Histogram → Delta, ObservableCounter/UpDownCounter/ObservableUpDownCounter → Cumulative
                Configuration.OtlpTemporality.LowMemory => instrumentType switch
                {
                    InstrumentType.Counter or InstrumentType.Histogram
                        => AggregationTemporality.Delta,
                    InstrumentType.ObservableCounter or InstrumentType.UpDownCounter or InstrumentType.ObservableUpDownCounter
                        => AggregationTemporality.Cumulative,
                    _ => AggregationTemporality.Delta
                },

                _ => AggregationTemporality.Delta
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static InstrumentType? GetInstrumentType(string instrumentType)
        {
            if (instrumentType.StartsWith("System.Diagnostics.Metrics.Counter`1"))
            {
                return InstrumentType.Counter;
            }
            else if (instrumentType.StartsWith("System.Diagnostics.Metrics.ObservableCounter`1"))
            {
                return InstrumentType.ObservableCounter;
            }
            else if (instrumentType.StartsWith("System.Diagnostics.Metrics.UpDownCounter`1"))
            {
                return InstrumentType.UpDownCounter;
            }
            else if (instrumentType.StartsWith("System.Diagnostics.Metrics.ObservableUpDownCounter`1"))
            {
                return InstrumentType.ObservableUpDownCounter;
            }
            else if (instrumentType.StartsWith("System.Diagnostics.Metrics.Gauge`1"))
            {
                return InstrumentType.Gauge;
            }
            else if (instrumentType.StartsWith("System.Diagnostics.Metrics.ObservableGauge`1"))
            {
                return InstrumentType.ObservableGauge;
            }
            else if (instrumentType.StartsWith("System.Diagnostics.Metrics.Histogram`1"))
            {
                return InstrumentType.Histogram;
            }

            return null;
        }

        private static MetricState? CreateMetricState(Instrument instrument)
        {
            try
            {
                var instrumentType = instrument.GetType().FullName;
                if (instrumentType is null)
                {
                    Log.Warning("Unable to get the full name of the instrument type for: {InstrumentName}", instrument.Name);
                    return null;
                }

                var aggregationType = GetInstrumentType(instrumentType);
                if (aggregationType == null)
                {
                    Log.Debug("Skipping unsupported instrument: {InstrumentName} of type: {InstrumentType}", instrument.Name, instrumentType);
                    return null;
                }

                var metricStreamIdentity = new MetricStreamIdentity(instrument, aggregationType.Value);

                // RFC requirement: Check for duplicate instrument registration
                if (CapturedMetrics.ContainsKey(metricStreamIdentity))
                {
                    Log.Warning(
                        "Duplicate instrument registration detected: {InstrumentType} '{InstrumentName}' (Unit='{Unit}', Description='{Description}') from meter '{MeterName}'. Previous instrument will be reused.",
                        [aggregationType.Value, instrument.Name, instrument.Unit ?? "null", instrument.Description ?? "null", instrument.Meter.Name]);

                    return null;
                }

                // Check for duplicate metric stream names
                if (!MetricStreamNames.Add(metricStreamIdentity.MetricStreamName))
                {
                    Log.Warning("Duplicate metric stream detected: {MetricStreamName}. Measurements from this instrument will still be exported but may result in conflicts.", metricStreamIdentity.MetricStreamName);
                }

                var state = new MetricState(metricStreamIdentity);

                // Store in global dictionary for export
                CapturedMetrics.TryAdd(metricStreamIdentity, state);
                Interlocked.Increment(ref _metricCount);

                return state;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating metric state for instrument: {InstrumentName}", instrument.Name);
                return null;
            }
        }
    }
}
#endif
