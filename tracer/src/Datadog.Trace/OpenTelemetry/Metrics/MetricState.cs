// <copyright file="MetricState.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Datadog.Trace.Logging;

namespace Datadog.Trace.OpenTelemetry.Metrics;

/// <summary>
/// Represents the state for a metric instrument, used to avoid ConcurrentDictionary contention
/// </summary>
internal sealed class MetricState
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(MetricState));
    private readonly MetricStreamIdentity _identity;
    private readonly AggregationTemporality? _temporality;

    private readonly ConcurrentDictionary<TagSet, MetricPoint> _points = new();

    public MetricState(MetricStreamIdentity identity, AggregationTemporality? temporality)
    {
        _identity = identity;
        _temporality = temporality;
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

        var dict = new Dictionary<string, object?>(tags.Length);
        for (int i = 0; i < tags.Length; i++)
        {
            var kv = tags[i];
            dict[kv.Key] = kv.Value;
        }

        return _points.GetOrAdd(
            tagSet,
            _ => new MetricPoint(
                _identity.InstrumentName,
                _identity.MeterName,
                _identity.MeterVersion,
                _identity.MeterTags,
                _identity.InstrumentType,
                _temporality,
                dict,
                _identity.Unit,
                _identity.Description,
                _identity.IsLongType));
    }
}
#endif
