// <copyright file="MetricPoint.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;

namespace Datadog.Trace.OTelMetrics;

internal class MetricPoint(string instrumentName, string meterName, InstrumentType instrumentType, AggregationTemporality? temporality, Dictionary<string, object?> tags, string unit = "", string description = "")
{
    internal static readonly double[] DefaultHistogramBounds = [0, 5, 10, 25, 50, 75, 100, 250, 500, 750, 1000, 2500, 5000, 7500, 10000];
    private readonly long[] _runningBucketCounts = instrumentType == InstrumentType.Histogram ? new long[DefaultHistogramBounds.Length + 1] : [];
    private readonly object _histogramLock = new();
    private long _runningCountValue;
    private double _runningDoubleValue;
    private double _runningMin = double.PositiveInfinity;
    private double _runningMax = double.NegativeInfinity;

    public string InstrumentName { get; } = instrumentName;

    public string MeterName { get; } = meterName;

    public InstrumentType InstrumentType { get; } = instrumentType;

    public AggregationTemporality? AggregationTemporality { get; } = temporality;

    public Dictionary<string, object?> Tags { get; } = tags;

    public string Unit { get; } = unit;

    public string Description { get; } = description;

    internal long RunningCount => _runningCountValue;

    internal double RunningSum => _runningDoubleValue;

    internal double RunningMin => _runningMin;

    internal double RunningMax => _runningMax;

    internal long[] RunningBucketCounts => _runningBucketCounts;

    public DateTimeOffset StartTime { get; private set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset EndTime { get; private set; } = DateTimeOffset.UtcNow;

    public long SnapshotCount { get; private set; }

    public double SnapshotSum { get; private set; }

    public double SnapshotGaugeValue { get; private set; }

    public double SnapshotMin { get; private set; }

    public double SnapshotMax { get; private set; }

    public long[] SnapshotBucketCounts { get; private set; } = [];

    internal void UpdateCounter(double value)
    {
        lock (_histogramLock)
        {
            _runningDoubleValue += value;
        }
    }

    internal void UpdateObservableCounter(double currentValue)
    {
        lock (_histogramLock)
        {
            _runningDoubleValue = currentValue;
        }
    }

    internal void UpdateGauge(double value)
    {
        Interlocked.Exchange(ref _runningDoubleValue, value);
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
        }
    }

    private static int FindBucketIndex(double value)
    {
        for (var i = 0; i < DefaultHistogramBounds.Length; i++)
        {
            if (value <= DefaultHistogramBounds[i])
            {
                return i;
            }
        }

        return DefaultHistogramBounds.Length; // Overflow bucket
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

            // Create a snapshot copy with current values
            var snapshot = new MetricPoint(InstrumentName, MeterName, InstrumentType, AggregationTemporality, Tags, Unit, Description)
            {
                StartTime = this.StartTime,
                EndTime = endTime,
                SnapshotCount = _runningCountValue,
                SnapshotSum = _runningDoubleValue,
                SnapshotGaugeValue = _runningDoubleValue,
                SnapshotMin = _runningMin,
                SnapshotMax = _runningMax
            };

            // Copy bucket counts for histograms
            if (_runningBucketCounts.Length > 0)
            {
                snapshot.SnapshotBucketCounts = new long[_runningBucketCounts.Length];
                Array.Copy(_runningBucketCounts, snapshot.SnapshotBucketCounts, _runningBucketCounts.Length);
            }

            if (AggregationTemporality == OTelMetrics.AggregationTemporality.Delta)
            {
                _runningCountValue = 0;
                _runningDoubleValue = 0.0;
                _runningMin = double.PositiveInfinity;
                _runningMax = double.NegativeInfinity;
                if (_runningBucketCounts.Length > 0)
                {
                    Array.Clear(_runningBucketCounts, 0, _runningBucketCounts.Length);
                }

                // Update start time for next delta window
                StartTime = endTime;
            }

            return snapshot;
        }
    }
}
#endif
