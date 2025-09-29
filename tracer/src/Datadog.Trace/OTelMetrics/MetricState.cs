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

namespace Datadog.Trace.OTelMetrics;

/// <summary>
/// Represents the state for a metric instrument, used to avoid ConcurrentDictionary contention
/// </summary>
internal class MetricState
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(MetricState));
    private readonly MetricStreamIdentity _identity;

    private readonly ConcurrentDictionary<TagSet, MetricPoint> _points = new();

    public MetricState(MetricStreamIdentity identity)
    {
        _identity = identity;
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
            case InstrumentType.ObservableCounter:
            case InstrumentType.UpDownCounter:
            case InstrumentType.ObservableUpDownCounter:
                point.UpdateCounter(value);
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
            case InstrumentType.ObservableCounter:
            case InstrumentType.UpDownCounter:
            case InstrumentType.ObservableUpDownCounter:
                point.UpdateCounter(value);
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

    public void RecordMeasurementGaugeLong(long value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        => GetOrCreatePoint(tags).UpdateGauge(value);

    public void RecordMeasurementGaugeDouble(double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        => GetOrCreatePoint(tags).UpdateGauge(value);

    public void RecordMeasurementHistogramLong(long value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        => GetOrCreatePoint(tags).UpdateHistogram(value);

    public void RecordMeasurementHistogramDouble(double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        => GetOrCreatePoint(tags).UpdateHistogram(value);

    public IEnumerable<MetricPoint> GetMetricPoints() => _points.Values;

    public MetricStreamIdentity GetIdentity() => _identity;

    private MetricPoint GetOrCreatePoint(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var tagSet = TagSet.FromSpan(tags);

        var tagDict = new Dictionary<string, object?>();
        for (int i = 0; i < tags.Length; i++)
        {
            var kv = tags[i];
            tagDict[kv.Key] = kv.Value;
        }

        return _points.GetOrAdd(tagSet, _ => new MetricPoint(
            _identity.InstrumentName,
            _identity.MeterName,
            _identity.InstrumentType,
            MetricReaderHandler.GetTemporality(_identity.InstrumentType),
            tagDict));
    }
}
#endif
