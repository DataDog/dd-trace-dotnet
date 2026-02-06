// <copyright file="MetricPoint.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;

namespace Datadog.Trace.OpenTelemetry.Metrics;

internal sealed class MetricPoint(string instrumentName, string meterName, string meterVersion, KeyValuePair<string, object?>[] meterTags, InstrumentType instrumentType, AggregationTemporality? temporality, Dictionary<string, object?> tags, string unit = "", string description = "", bool isLongType = false, double[]? explicitBounds = null)
{
    internal static readonly double[] DefaultHistogramBounds = [0, 5, 10, 25, 50, 75, 100, 250, 500, 750, 1000, 2500, 5000, 7500, 10000];
    private readonly long[] _runningBucketCounts = instrumentType == InstrumentType.Histogram ? new long[DefaultHistogramBounds.Length + 1] : [];
    private readonly double[] _runningBucketBounds = instrumentType == InstrumentType.Histogram ? (explicitBounds ?? DefaultHistogramBounds) : [];
    private readonly object _histogramLock = new();
    private long _runningCountValue;
    private double _runningDoubleValue;
    private double _runningMin = double.PositiveInfinity;
    private double _runningMax = double.NegativeInfinity;
    private bool _hasMeasurements;
    private double _lastObservedCumulative = double.NaN;

    public string InstrumentName { get; } = instrumentName;

    public string MeterName { get; } = meterName;

    public string MeterVersion { get; } = meterVersion;

    public KeyValuePair<string, object?>[] MeterTags { get; } = meterTags;

    public InstrumentType InstrumentType { get; } = instrumentType;

    public AggregationTemporality? AggregationTemporality { get; } = temporality;

    public Dictionary<string, object?> Tags { get; } = tags;

    public string Unit { get; } = unit;

    public string Description { get; } = description;

    public bool IsLongType { get; } = isLongType;

    internal long RunningCount => _runningCountValue;

    internal double RunningSum => _runningDoubleValue;

    internal double RunningMin => _runningMin;

    internal double RunningMax => _runningMax;

    internal long[] RunningBucketCounts => _runningBucketCounts;

    internal double[] RunningBucketBounds => _runningBucketBounds;

    public DateTimeOffset StartTime { get; internal set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset EndTime { get; internal set; } = DateTimeOffset.UtcNow;

    public long SnapshotCount { get; internal set; }

    public double SnapshotSum { get; internal set; }

    public double SnapshotGaugeValue { get; internal set; }

    public double SnapshotMin { get; internal set; }

    public double SnapshotMax { get; internal set; }

    public long[] SnapshotBucketCounts { get; internal set; } = [];

    public double[] SnapshotBucketBounds { get; internal set; } = [];

    internal void UpdateCounter(double value)
    {
        lock (_histogramLock)
        {
            _runningDoubleValue += value;
            _hasMeasurements = true;
        }
    }

    internal void UpdateObservableCounter(double currentValue)
    {
        lock (_histogramLock)
        {
            if (double.IsNaN(_lastObservedCumulative))
            {
                _hasMeasurements = true;
            }
            else if (currentValue != _lastObservedCumulative)
            {
                _hasMeasurements = true;
            }

            _runningDoubleValue = currentValue;
        }
    }

    internal void UpdateGauge(double value)
    {
        Interlocked.Exchange(ref _runningDoubleValue, value);
        lock (_histogramLock)
        {
            _hasMeasurements = true;
        }
    }

    internal void UpdateHistogram(double value)
    {
        var bucketIndex = FindBucketIndex(value);

        lock (_histogramLock)
        {
            unchecked
            {
                _runningCountValue++;
                _runningDoubleValue += value;
                _runningBucketCounts[bucketIndex]++;
            }

            _runningMin = Math.Min(_runningMin, value);
            _runningMax = Math.Max(_runningMax, value);
            _hasMeasurements = true;
        }
    }

    public bool HasDataToExport() => _hasMeasurements;

    private int FindBucketIndex(double value)
    {
        for (var i = 0; i < _runningBucketBounds.Length; i++)
        {
            if (value <= _runningBucketBounds[i])
            {
                return i;
            }
        }

        return _runningBucketBounds.Length;
    }

    /// <summary>
    /// Creates a snapshot copy of the current metric state for export.
    /// For delta temporality, this resets the running values after taking the snapshot.
    /// For cumulative temporality, this preserves the running values.
    /// The temporality behavior is determined by the AggregationTemporality set at construction.
    /// </summary>
    /// <returns>A new MetricPoint containing the snapshot values</returns>
    public MetricPoint CreateSnapshotAndReset()
    {
        lock (_histogramLock)
        {
            var endTime = DateTimeOffset.UtcNow;

            double sumForSnapshot = _runningDoubleValue;

            if (InstrumentType is InstrumentType.ObservableCounter or InstrumentType.ObservableUpDownCounter)
            {
                var previousCumulative = double.IsNaN(_lastObservedCumulative) ? 0 : _lastObservedCumulative;
                var delta = _runningDoubleValue - previousCumulative;

                sumForSnapshot = AggregationTemporality == Metrics.AggregationTemporality.Delta
                    ? delta
                    : _runningDoubleValue;

                _lastObservedCumulative = _runningDoubleValue;
            }

            var snapshot = new MetricPoint(InstrumentName, MeterName, MeterVersion, MeterTags, InstrumentType, AggregationTemporality, Tags, Unit, Description, IsLongType)
            {
                StartTime = this.StartTime,
                EndTime = endTime,
                SnapshotCount = _runningCountValue,
                SnapshotSum = sumForSnapshot,
                SnapshotGaugeValue = _runningDoubleValue,
                SnapshotMin = _runningMin,
                SnapshotMax = _runningMax
            };

            if (_runningBucketCounts.Length > 0)
            {
                snapshot.SnapshotBucketCounts = new long[_runningBucketCounts.Length];
                Array.Copy(_runningBucketCounts, snapshot.SnapshotBucketCounts, _runningBucketCounts.Length);
            }

            if (_runningBucketBounds.Length > 0)
            {
                snapshot.SnapshotBucketBounds = new double[_runningBucketBounds.Length];
                Array.Copy(_runningBucketBounds, snapshot.SnapshotBucketBounds, _runningBucketBounds.Length);
            }

            if (AggregationTemporality == Metrics.AggregationTemporality.Delta)
            {
                _runningCountValue = 0;
                _runningDoubleValue = 0.0;
                _runningMin = double.PositiveInfinity;
                _runningMax = double.NegativeInfinity;
                if (_runningBucketCounts.Length > 0)
                {
                    Array.Clear(_runningBucketCounts, 0, _runningBucketCounts.Length);
                }

                StartTime = endTime;
            }

            _hasMeasurements = false;

            return snapshot;
        }
    }
}
#endif
