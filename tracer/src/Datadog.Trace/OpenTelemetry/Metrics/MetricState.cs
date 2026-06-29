// <copyright file="MetricState.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Datadog.Trace.Logging;

namespace Datadog.Trace.OpenTelemetry.Metrics;

/// <summary>
/// Represents the state for a metric instrument, used to avoid ConcurrentDictionary contention
/// </summary>
internal sealed class MetricState
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(MetricState));

    // TODO: store the overflow attribute value as a real bool (true) once OtlpMetricsSerializer
    // supports non-string AnyValue types; today it serializes every attribute via ToString().
    private static readonly Dictionary<string, object?> OverflowTags = new(1) { ["otel.metric.overflow"] = "true" };

    private readonly MetricStreamIdentity _identity;
    private readonly AggregationTemporality? _temporality;
    private readonly int _maxCardinality;

    private readonly ConcurrentDictionary<ulong, MetricPoint> _points = new();

    private int _pointCount;
    private MetricPoint? _overflowPoint;

    public MetricState(MetricStreamIdentity identity, AggregationTemporality? temporality, int maxCardinality)
    {
        _identity = identity;
        _temporality = temporality;
        _maxCardinality = maxCardinality;
    }

    public void RecordMeasurementLong(long value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        if ((_identity.InstrumentType == InstrumentType.Counter || _identity.InstrumentType == InstrumentType.Histogram) && value < 0)
        {
            Log.Warning(
                "Ignoring negative value {Value} for {InstrumentType} instrument: {InstrumentName}. API usage is incorrect.",
                value,
                _identity.InstrumentType,
                _identity.InstrumentName);
            return;
        }

        var point = GetOrCreatePoint(tags);
        switch (_identity.InstrumentType)
        {
            case InstrumentType.Counter:
            case InstrumentType.UpDownCounter:
                point.UpdateCounter(value);
                break;
            case InstrumentType.ObservableCounter:
            case InstrumentType.ObservableUpDownCounter:
                point.UpdateObservableCounter(value);
                break;
            case InstrumentType.Gauge:
            case InstrumentType.ObservableGauge:
                point.UpdateGauge(value);
                break;
            case InstrumentType.Histogram:
                point.UpdateHistogram(value);
                break;
        }
    }

    public void RecordMeasurementDouble(double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        if ((_identity.InstrumentType == InstrumentType.Counter || _identity.InstrumentType == InstrumentType.Histogram) && value < 0)
        {
            Log.Warning(
                "Ignoring negative value {Value} for {InstrumentType} instrument: {InstrumentName}. API usage is incorrect.",
                value,
                _identity.InstrumentType,
                _identity.InstrumentName);
            return;
        }

        var point = GetOrCreatePoint(tags);
        switch (_identity.InstrumentType)
        {
            case InstrumentType.Counter:
            case InstrumentType.UpDownCounter:
                point.UpdateCounter(value);
                break;
            case InstrumentType.ObservableCounter:
            case InstrumentType.ObservableUpDownCounter:
                point.UpdateObservableCounter(value);
                break;
            case InstrumentType.Gauge:
            case InstrumentType.ObservableGauge:
                point.UpdateGauge(value);
                break;
            case InstrumentType.Histogram:
                point.UpdateHistogram(value);
                break;
        }
    }

    /// <summary>
    /// Builds metric point snapshots and adds them to the provided list.
    /// For Delta temporality, resets the running values after snapshotting.
    /// Only exports metric points that have received new measurements since the last export.
    /// </summary>
    public void BuildPoints(List<MetricPoint> into)
    {
        foreach (var point in _points.Values)
        {
            if (!point.HasDataToExport())
            {
                continue;
            }

            var snapshot = point.CreateSnapshotAndReset();
            into.Add(snapshot);
        }

        // The overflow point is tracked separately (not in _points), so export it explicitly.
        var overflow = Volatile.Read(ref _overflowPoint);
        if (overflow is not null && overflow.HasDataToExport())
        {
            into.Add(overflow.CreateSnapshotAndReset());
        }
    }

    private MetricPoint GetOrCreatePoint(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var hash = MetricTagsHash.Compute(tags);

        if (_points.TryGetValue(hash, out var existingPoint))
        {
            return existingPoint;
        }

        if (Volatile.Read(ref _pointCount) >= _maxCardinality)
        {
            // Cardinality limit reached: route new tag sets into a single shared overflow point
            // rather than growing the dictionary without bound. A benign read-then-add race here may
            // let _points exceed the limit by a small number of points, which is acceptable.
            var existing = Volatile.Read(ref _overflowPoint);
            return existing ?? CreateOverflowPoint();
        }

        var dict = new Dictionary<string, object?>(tags.Length);
        for (int i = 0; i < tags.Length; i++)
        {
            var kv = tags[i];
            dict[kv.Key] = kv.Value;
        }

        var newPoint = new MetricPoint(
            _identity.InstrumentName,
            _identity.MeterName,
            _identity.MeterVersion,
            _identity.MeterTags,
            _identity.InstrumentType,
            _temporality,
            dict,
            _identity.Unit,
            _identity.Description,
            _identity.IsLongType);

        // TryAdd (rather than GetOrAdd) so we increment _pointCount only when we insert,
        // in the case where two instances are racing to add the point
        if (_points.TryAdd(hash, newPoint))
        {
            Interlocked.Increment(ref _pointCount);
            return newPoint;
        }

        // Lost the race against another thread adding the same tag set; reuse the winner.
        // (the default is never actually used, just here to keep compiler happy)
        return _points.GetValueOrDefault(hash, newPoint);
    }

    private MetricPoint CreateOverflowPoint()
    {
        var created = new MetricPoint(
            _identity.InstrumentName,
            _identity.MeterName,
            _identity.MeterVersion,
            _identity.MeterTags,
            _identity.InstrumentType,
            _temporality,
            OverflowTags,
            _identity.Unit,
            _identity.Description,
            _identity.IsLongType,
            isOverflow: true);

        return Interlocked.CompareExchange(ref _overflowPoint, created, null) ?? created;
    }
}
#endif
