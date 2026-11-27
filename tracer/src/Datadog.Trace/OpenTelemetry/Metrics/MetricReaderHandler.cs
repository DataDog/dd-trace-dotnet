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
    // Number of registered streams per stream name: different identities can share a name.
    private readonly Dictionary<string, int> _streamNames = new(StringComparer.OrdinalIgnoreCase);
    // Guarded by _completionLock.
    private readonly Queue<MetricState> _completedStreams = new();
    private readonly object _completionLock = new();

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

        if (!shouldEnable && _settings.OtlpRuntimeMetricsEnabled)
        {
            shouldEnable = meterName is "System.Runtime";
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

        var state = new MetricState(identity, temporality, _settings.OpenTelemetryMetricsCardinalityLimit);

        if (_streams.TryAdd(identity, state))
        {
            // Registered before enabling events: enabling can complete the stream right away (disposed meter).
            RegisterStreamName(identity.MetricStreamName);
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
    /// Called when the listener stops receiving an instrument's measurements (its Meter or the listener was
    /// disposed). The stream is unregistered right away, so a re-created instrument with the same identity is
    /// subscribed again, and its remaining data is exported once more at the next collection.
    /// </summary>
    public void OnMeasurementsCompleted(Instrument instrument, object? state)
    {
        if (state is not MetricState metricState)
        {
            return;
        }

        lock (_completionLock)
        {
            if (!_streams.TryRemove(new KeyValuePair<MetricStreamIdentity, MetricState>(metricState.Identity, metricState)))
            {
                return;
            }

            _completedStreams.Enqueue(metricState);
        }

        ReleaseStreamName(metricState.Identity.MetricStreamName);
    }

    /// <summary>
    /// Gets a safe snapshot of all metric points.
    /// Uses ToArray() to avoid enumeration race conditions.
    /// </summary>
    public List<MetricPoint> GetMetricPointsSnapshot()
    {
        var list = new List<MetricPoint>();
        List<MetricState>? completed = null;
        KeyValuePair<MetricStreamIdentity, MetricState>[] pairs;

        // Streams are chosen under the lock, so a stream completing meanwhile is either still in _streams or
        // already queued, never missed. A stream completing after this point is exported again next time.
        // Points are built outside the lock: Meter.Dispose() takes it on the application's thread.
        lock (_completionLock)
        {
            if (_completedStreams.Count > 0)
            {
                completed = new List<MetricState>(_completedStreams);
                _completedStreams.Clear();
            }

            pairs = _streams.ToArray();
        }

        // Last export of the streams whose instrument completed since the previous collection.
        if (completed is not null)
        {
            foreach (var completedState in completed)
            {
                completedState.BuildPoints(list);
            }
        }

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
            InstrumentType.ObservableGauge
                => null,

            // A synchronous gauge read with the cumulative or lowmemory preference repeats its last value on every collection.
            InstrumentType.Gauge
                => preference is OtlpTemporalityPreference.Cumulative or OtlpTemporalityPreference.LowMemory
                    ? AggregationTemporality.Cumulative
                    : null,

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

        if (instrumentType.StartsWithOrdinal("System.Diagnostics.Metrics.Counter`1"))
        {
            return InstrumentType.Counter;
        }
        else if (instrumentType.StartsWithOrdinal("System.Diagnostics.Metrics.ObservableCounter`1"))
        {
            return InstrumentType.ObservableCounter;
        }
        else if (instrumentType.StartsWithOrdinal("System.Diagnostics.Metrics.UpDownCounter`1"))
        {
            return InstrumentType.UpDownCounter;
        }
        else if (instrumentType.StartsWithOrdinal("System.Diagnostics.Metrics.ObservableUpDownCounter`1"))
        {
            return InstrumentType.ObservableUpDownCounter;
        }
        else if (instrumentType.StartsWithOrdinal("System.Diagnostics.Metrics.Gauge`1"))
        {
            return InstrumentType.Gauge;
        }
        else if (instrumentType.StartsWithOrdinal("System.Diagnostics.Metrics.ObservableGauge`1"))
        {
            return InstrumentType.ObservableGauge;
        }
        else if (instrumentType.StartsWithOrdinal("System.Diagnostics.Metrics.Histogram`1"))
        {
            return InstrumentType.Histogram;
        }

        return null;
    }

    private void RegisterStreamName(string metricStreamName)
    {
        int count;
        lock (_streamNames)
        {
            _streamNames.TryGetValue(metricStreamName, out count);
            _streamNames[metricStreamName] = count + 1;
        }

        if (count > 0)
        {
            Log.Warning("Duplicate metric stream detected: {MetricStreamName}. Measurements from this instrument will still be exported but may result in conflicts.", metricStreamName);
        }
    }

    private void ReleaseStreamName(string metricStreamName)
    {
        lock (_streamNames)
        {
            if (_streamNames.TryGetValue(metricStreamName, out var count))
            {
                if (count <= 1)
                {
                    _streamNames.Remove(metricStreamName);
                }
                else
                {
                    _streamNames[metricStreamName] = count - 1;
                }
            }
        }
    }
}
#endif
