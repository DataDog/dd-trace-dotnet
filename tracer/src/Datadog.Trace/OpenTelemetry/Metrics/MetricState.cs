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

    /// <summary>
    /// Default per-stream cardinality limit, matching the OpenTelemetry metrics SDK spec default.
    /// Caps the number of distinct attribute sets tracked for a single instrument so that
    /// high-cardinality (or attacker-controlled) attribute values cannot grow memory without bound.
    /// </summary>
    internal const int DefaultCardinalityLimit = 2000;

    /// <summary>
    /// Attribute key for the overflow series, as defined by the OpenTelemetry metrics SDK spec.
    /// Measurements with new attribute sets beyond the cardinality limit are aggregated here.
    /// </summary>
    internal const string OverflowAttributeKey = "otel.metric.overflow";

    private static readonly KeyValuePair<string, object?>[] OverflowTags = [new(OverflowAttributeKey, true)];
    private static readonly TagSet OverflowTagSet = TagSet.FromSpan(OverflowTags);

    private readonly MetricStreamIdentity _identity;
    private readonly AggregationTemporality? _temporality;

    private readonly ConcurrentDictionary<TagSet, MetricPoint> _points = new();
    private readonly object _pointsLock = new();
    private readonly int _cardinalityLimit;

    private int _realSeriesCount;
    private int _overflowLogged;

    public MetricState(MetricStreamIdentity identity, AggregationTemporality? temporality, int cardinalityLimit = DefaultCardinalityLimit)
    {
        _identity = identity;
        _temporality = temporality;
        _cardinalityLimit = cardinalityLimit > 0 ? cardinalityLimit : DefaultCardinalityLimit;
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

        while (true)
        {
            var point = GetOrCreatePoint(tags);
            var recorded = _identity.InstrumentType switch
            {
                InstrumentType.Counter or InstrumentType.UpDownCounter => point.UpdateCounter(value),
                InstrumentType.ObservableCounter or InstrumentType.ObservableUpDownCounter => point.UpdateObservableCounter(value),
                InstrumentType.Gauge or InstrumentType.ObservableGauge => point.UpdateGauge(value),
                InstrumentType.Histogram => point.UpdateHistogram(value),
                _ => true
            };

            if (recorded)
            {
                return;
            }
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

        while (true)
        {
            var point = GetOrCreatePoint(tags);
            var recorded = _identity.InstrumentType switch
            {
                InstrumentType.Counter or InstrumentType.UpDownCounter => point.UpdateCounter(value),
                InstrumentType.ObservableCounter or InstrumentType.ObservableUpDownCounter => point.UpdateObservableCounter(value),
                InstrumentType.Gauge or InstrumentType.ObservableGauge => point.UpdateGauge(value),
                InstrumentType.Histogram => point.UpdateHistogram(value),
                _ => true
            };

            if (recorded)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Builds metric point snapshots and adds them to the provided list.
    /// For Delta temporality, resets the running values after snapshotting.
    /// Only exports metric points that have received new measurements since the last export.
    /// </summary>
    public void BuildPoints(List<MetricPoint> into)
    {
        foreach (var pair in _points)
        {
            var point = pair.Value;
            if (!point.HasDataToExport())
            {
                TryReclaimPoint(pair.Key, point);
                continue;
            }

            var snapshot = point.CreateSnapshotAndReset();
            into.Add(snapshot);
            TryReclaimPoint(pair.Key, point);
        }
    }

    private MetricPoint GetOrCreatePoint(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var tagSet = TagSet.FromSpan(tags);

        while (true)
        {
            if (_points.TryGetValue(tagSet, out var existingPoint))
            {
                if (!existingPoint.IsRetired)
                {
                    return existingPoint;
                }

                TryRemovePoint(tagSet, existingPoint);
                continue;
            }

            lock (_pointsLock)
            {
                if (_points.TryGetValue(tagSet, out existingPoint))
                {
                    if (!existingPoint.IsRetired)
                    {
                        return existingPoint;
                    }

                    TryRemovePoint(tagSet, existingPoint);
                    continue;
                }

                // Cardinality limit (OpenTelemetry metrics SDK spec): when the stream is full,
                // fold any new attribute set into a single overflow series tagged
                // otel.metric.overflow=true. Existing attribute sets keep updating via the fast
                // path above, and new real-series insertion is synchronized so concurrent traffic
                // cannot overshoot the configured number of real series.
                if (Volatile.Read(ref _realSeriesCount) >= _cardinalityLimit)
                {
                    return GetOrCreateOverflowPoint();
                }

                var dict = new Dictionary<string, object?>(tags.Length);
                for (int i = 0; i < tags.Length; i++)
                {
                    var kv = tags[i];
                    dict[kv.Key] = kv.Value;
                }

                var point = CreatePoint(dict);
                if (_points.TryAdd(tagSet, point))
                {
                    Interlocked.Increment(ref _realSeriesCount);
                    return point;
                }
            }
        }
    }

    private MetricPoint GetOrCreateOverflowPoint()
    {
        if (_points.TryGetValue(OverflowTagSet, out var overflowPoint))
        {
            return overflowPoint;
        }

        if (Interlocked.Exchange(ref _overflowLogged, 1) == 0)
        {
            Log.Warning(
                "Cardinality limit ({CardinalityLimit}) reached for instrument '{InstrumentName}' from meter '{MeterName}'. Additional attribute sets are aggregated into an overflow series tagged 'otel.metric.overflow=true'. Reduce the cardinality of this metric's attributes.",
                [_cardinalityLimit, _identity.InstrumentName, _identity.MeterName]);
        }

        var dict = new Dictionary<string, object?>(1) { [OverflowAttributeKey] = true };
        return _points.GetOrAdd(OverflowTagSet, _ => CreatePoint(dict, aggregateObservableValues: true));
    }

    private void TryReclaimPoint(TagSet tagSet, MetricPoint point)
    {
        if (tagSet.Equals(OverflowTagSet)
         || _temporality != AggregationTemporality.Delta
         || _identity.InstrumentType is not (InstrumentType.Counter or InstrumentType.Histogram)
         || !point.TryRetireIfIdle())
        {
            return;
        }

        TryRemovePoint(tagSet, point);
    }

    private void TryRemovePoint(TagSet tagSet, MetricPoint point)
    {
        if (((ICollection<KeyValuePair<TagSet, MetricPoint>>)_points).Remove(new KeyValuePair<TagSet, MetricPoint>(tagSet, point))
         && !tagSet.Equals(OverflowTagSet))
        {
            Interlocked.Decrement(ref _realSeriesCount);
        }
    }

    private MetricPoint CreatePoint(Dictionary<string, object?> tags, bool aggregateObservableValues = false)
        => new MetricPoint(
            _identity.InstrumentName,
            _identity.MeterName,
            _identity.MeterVersion,
            _identity.MeterTags,
            _identity.InstrumentType,
            _temporality,
            tags,
            _identity.Unit,
            _identity.Description,
            _identity.IsLongType,
            aggregateObservableValues: aggregateObservableValues);
}
#endif
