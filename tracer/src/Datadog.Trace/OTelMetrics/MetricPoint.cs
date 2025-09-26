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

internal class MetricPoint(string instrumentName, string meterName, InstrumentType instrumentType, AggregationTemporality? temporality, Dictionary<string, object?> tags)
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

    internal long RunningCount => _runningCountValue;

    internal double RunningSum => _runningDoubleValue;

    internal double RunningMin => _runningMin;

    internal double RunningMax => _runningMax;

    internal long[] RunningBucketCounts => _runningBucketCounts;

    internal void UpdateCounter(double value)
    {
        lock (_histogramLock)
        {
            _runningDoubleValue += value;
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

        return DefaultHistogramBounds.Length;
    }
}
#endif
