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
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;

namespace Datadog.Trace.OpenTelemetry.Metrics;

/// <summary>
/// Instance-based handler for MeterListener events.
/// Thread-safe aggregation of metrics with safe snapshotting via ToArray().
/// </summary>
internal sealed class MetricReaderHandler
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(MetricReaderHandler));
    private readonly TracerSettings _settings;
    private readonly ConcurrentDictionary<MetricStreamIdentity, MetricState> _streams = new();
    private readonly HashSet<string> _streamNames = new(StringComparer.OrdinalIgnoreCase);

    public MetricReaderHandler(TracerSettings settings)
    {
        _settings = settings;
    }

    public void OnInstrumentPublished(Instrument instrument, MeterListener listener)
    {
        var meterName = instrument.Meter.Name;

        if (!IsValidInstrumentName(instrument.Name))
        {
            Log.Warning("Invalid instrument name '{InstrumentName}' from meter '{MeterName}'. Instrument names must be ASCII, start with letter, contain only alphanumeric characters, '_', '.', '/', '-' and be max 255 characters.", instrument.Name, meterName);
            return;
        }

        bool shouldEnable;
        var enabledMeterNames = _settings.OpenTelemetryMeterNames;

        if (enabledMeterNames.Count > 0)
        {
            shouldEnable = enabledMeterNames.Contains(meterName);
        }
        else
        {
            shouldEnable = !meterName.StartsWith("System.", StringComparison.Ordinal) && !meterName.StartsWith("Microsoft.", StringComparison.Ordinal);
        }

        if (!shouldEnable)
        {
            return;
        }

        var instrumentType = GetInstrumentType(instrument.GetType().FullName);
        if (instrumentType == null)
        {
            Log.Debug("Skipping unsupported instrument: {InstrumentName} of type: {InstrumentType}", instrument.Name, instrument.GetType().FullName);
            return;
        }

        var temporality = GetTemporality(instrumentType.Value, _settings.OtlpMetricsTemporalityPreference);
        var identity = new MetricStreamIdentity(instrument, instrumentType.Value);

        if (_streams.ContainsKey(identity))
        {
            Log.Warning(
                "Duplicate instrument registration detected: {InstrumentType} '{InstrumentName}' (Unit='{Unit}', Description='{Description}') from meter '{MeterName}'. Previous instrument will be reused.",
                [instrumentType.Value, instrument.Name, instrument.Unit ?? "null", instrument.Description ?? "null", instrument.Meter.Name]);
            return;
        }

        // Check for duplicate metric stream name
        if (!_streamNames.Add(identity.MetricStreamName))
        {
            Log.Warning("Duplicate metric stream detected: {MetricStreamName}. Measurements from this instrument will still be exported but may result in conflicts.", identity.MetricStreamName);
        }

        var state = new MetricState(identity, temporality);

        if (_streams.TryAdd(identity, state))
        {
            listener.EnableMeasurementEvents(instrument, state);
            Log.Debug("Enabled measurement events for instrument: {InstrumentName} from meter: {MeterName}", instrument.Name, meterName);
        }
    }

    public void OnMeasurementRecordedLong(Instrument instrument, long value, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
    {
        if (state is MetricState metricState)
        {
            metricState.RecordMeasurementLong(value, tags);
        }
    }

    public void OnMeasurementRecordedDouble(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
    {
        if (state is MetricState metricState)
        {
            metricState.RecordMeasurementDouble(value, tags);
        }
    }

    public void OnMeasurementRecordedByte(Instrument instrument, byte value, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
    {
        OnMeasurementRecordedLong(instrument, value, tags, state);
    }

    public void OnMeasurementRecordedShort(Instrument instrument, short value, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
    {
        OnMeasurementRecordedLong(instrument, value, tags, state);
    }

    public void OnMeasurementRecordedInt(Instrument instrument, int value, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
    {
        OnMeasurementRecordedLong(instrument, value, tags, state);
    }

    public void OnMeasurementRecordedFloat(Instrument instrument, float value, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
    {
        OnMeasurementRecordedDouble(instrument, value, tags, state);
    }

    /// <summary>
    /// Gets a safe snapshot of all metric points.
    /// Uses ToArray() to avoid enumeration race conditions.
    /// </summary>
    public List<MetricPoint> GetMetricPointsSnapshot()
    {
        var pairs = _streams.ToArray();
        var list = new List<MetricPoint>(pairs.Length);

        foreach (var (_, state) in pairs)
        {
            // BuildPoints creates MetricPoint snapshots and performs delta resets if needed
            state.BuildPoints(list);
        }

        return list;
    }

    private static bool IsValidInstrumentName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        if (name.Length > 255)
        {
            return false;
        }

        if (!char.IsLetter(name[0]))
        {
            return false;
        }

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

    private static AggregationTemporality? GetTemporality(InstrumentType kind, OtlpTemporalityPreference preference)
    {
        return kind switch
        {
            InstrumentType.Gauge or InstrumentType.ObservableGauge
                => null,

            InstrumentType.UpDownCounter or InstrumentType.ObservableUpDownCounter
                => AggregationTemporality.Cumulative,

            InstrumentType.Counter or InstrumentType.ObservableCounter or InstrumentType.Histogram
                => preference switch
                {
                    OtlpTemporalityPreference.Cumulative => AggregationTemporality.Cumulative,
                    OtlpTemporalityPreference.Delta => AggregationTemporality.Delta,
                    OtlpTemporalityPreference.LowMemory => kind is InstrumentType.ObservableCounter
                        ? AggregationTemporality.Cumulative
                        : AggregationTemporality.Delta,
                    _ => AggregationTemporality.Delta
                },

            _ => AggregationTemporality.Delta
        };
    }

    private static InstrumentType? GetInstrumentType(string? instrumentType)
    {
        if (string.IsNullOrEmpty(instrumentType))
        {
            return null;
        }

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
}
#endif
