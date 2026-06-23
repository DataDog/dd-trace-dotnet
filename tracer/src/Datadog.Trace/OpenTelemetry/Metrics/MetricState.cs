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
    private readonly int _cardinalityLimit;

    private volatile bool _overflowActive;
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
    }

    private MetricPoint GetOrCreatePoint(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var tagSet = TagSet.FromSpan(tags);

        if (_points.TryGetValue(tagSet, out var existingPoint))
        {
            return existingPoint;
        }

        // Cardinality limit (OpenTelemetry metrics SDK spec): once the stream is full, fold any
        // new attribute set into a single overflow series tagged otel.metric.overflow=true. This
        // bounds memory at _cardinalityLimit points per stream regardless of input, so a
        // high-cardinality or attacker-controlled attribute value cannot exhaust memory.
        // Already-tracked attribute sets keep updating via the fast path above. Once overflow is
        // active we skip the (relatively expensive) Count check on the hot path.
        if (_overflowActive || _points.Count >= _cardinalityLimit - 1)
        {
            return GetOrCreateOverflowPoint();
        }

        var dict = new Dictionary<string, object?>(tags.Length);
        for (int i = 0; i < tags.Length; i++)
        {
            var kv = tags[i];
            dict[kv.Key] = kv.Value;
        }

        return _points.GetOrAdd(tagSet, _ => CreatePoint(dict));
    }

    private MetricPoint GetOrCreateOverflowPoint()
    {
        _overflowActive = true;

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
        return _points.GetOrAdd(OverflowTagSet, _ => CreatePoint(dict));
    }

    private MetricPoint CreatePoint(Dictionary<string, object?> tags)
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
            _identity.IsLongType);
}
#endif
