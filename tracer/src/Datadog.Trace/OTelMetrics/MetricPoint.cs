// <copyright file="MetricPoint.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Datadog.Trace.OTelMetrics
{
    internal class MetricPoint
    {
        private static readonly double[] DefaultHistogramBounds = [0, 5, 10, 25, 50, 75, 100, 250, 500, 750, 1000, 2500, 5000, 7500, 10000];

        // Histogram-specific
        private readonly long[] _runningBucketCounts;
        private readonly object _histogramLock = new();

        // Thread-safe running values (hot path)
        private long _runningCountValue;     // For counters and histogram count
        private double _runningDoubleValue;  // For gauges and histogram sum
        private double _runningMin = double.PositiveInfinity;
        private double _runningMax = double.NegativeInfinity;

        public MetricPoint(string instrumentName, string meterName, string instrumentType, string temporality, Dictionary<string, object?> tags)
        {
            // Initialize histogram buckets if needed
            if (instrumentType == "Histogram")
            {
                _runningBucketCounts = new long[DefaultHistogramBounds.Length + 1]; // +1 for overflow
                SnapshotBucketCounts = new long[DefaultHistogramBounds.Length + 1];
            }
            else
            {
                _runningBucketCounts = [];
                SnapshotBucketCounts = [];
            }

            InstrumentName = instrumentName;
            MeterName = meterName;
            InstrumentType = instrumentType;
            AggregationTemporality = temporality;
            Tags = tags;
            StartTime = DateTimeOffset.UtcNow;
            EndTime = DateTimeOffset.UtcNow;
        }

        public string InstrumentName { get; }

        public string MeterName { get; }

        public string InstrumentType { get; }

        public string AggregationTemporality { get; }

        public Dictionary<string, object?> Tags { get; }

        public DateTimeOffset StartTime { get; private set; }

        public DateTimeOffset EndTime { get; private set; }

        // Snapshot values (export time)
        public long SnapshotCount { get; private set; }

        public double SnapshotSum { get; private set; }

        public double SnapshotGaugeValue { get; private set; }

        public double SnapshotMin { get; private set; }

        public double SnapshotMax { get; private set; }

        public long[] SnapshotBucketCounts { get; private set; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateCounter(double value)
        {
            // Use lock to avoid floating-point precision issues with CompareExchange
            lock (_histogramLock)
            {
                _runningDoubleValue += value;
            }

            EndTime = DateTimeOffset.UtcNow;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateGauge(double value)
        {
            // Lock-free gauge update (like OTel)
            Interlocked.Exchange(ref _runningDoubleValue, value);
            EndTime = DateTimeOffset.UtcNow;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateHistogram(double value)
        {
            // Find bucket index first (outside lock for performance)
            var bucketIndex = FindBucketIndex(value);

            // Minimal lock scope (like OTel)
            lock (_histogramLock)
            {
                unchecked
                {
                    _runningCountValue++;
                    _runningDoubleValue += value; // Sum
                    _runningBucketCounts[bucketIndex]++;
                }

                _runningMin = Math.Min(_runningMin, value);
                _runningMax = Math.Max(_runningMax, value);
            }

            EndTime = DateTimeOffset.UtcNow;
        }

        public void TakeSnapshot(bool outputDelta)
        {
            EndTime = DateTimeOffset.UtcNow;

            switch (InstrumentType)
            {
                case "Counter":
                    if (outputDelta)
                    {
                        var currentValue = Interlocked.CompareExchange(ref _runningDoubleValue, 0, 0); // Datadog read pattern
                        SnapshotSum = currentValue - SnapshotSum; // Delta calculation
                    }
                    else
                    {
                        SnapshotSum = Interlocked.CompareExchange(ref _runningDoubleValue, 0, 0); // Cumulative
                    }

                    break;

                case "Gauge":
                    SnapshotGaugeValue = Interlocked.CompareExchange(ref _runningDoubleValue, 0, 0);
                    break;

                case "Histogram":
                    lock (_histogramLock)
                    {
                        SnapshotCount = _runningCountValue;
                        SnapshotSum = _runningDoubleValue;
                        SnapshotMin = _runningMin;
                        SnapshotMax = _runningMax;

                        // Copy bucket counts
                        Array.Copy(_runningBucketCounts, SnapshotBucketCounts, _runningBucketCounts.Length);

                        if (outputDelta)
                        {
                            // Reset for delta
                            _runningCountValue = 0;
                            _runningDoubleValue = 0;
                            _runningMin = double.PositiveInfinity;
                            _runningMax = double.NegativeInfinity;
                            Array.Clear(_runningBucketCounts);
                        }
                    }

                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int FindBucketIndex(double value)
        {
            // Linear search for default 15 boundaries (fast enough)
            for (var i = 0; i < DefaultHistogramBounds.Length; i++)
            {
                if (value <= DefaultHistogramBounds[i])
                {
                    return i;
                }
            }

            return DefaultHistogramBounds.Length; // Overflow bucket
        }
    }
}
#endif
