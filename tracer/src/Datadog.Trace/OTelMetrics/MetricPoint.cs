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
    internal class MetricPoint(string instrumentName, string meterName, InstrumentType instrumentType, AggregationTemporality? temporality, Dictionary<string, object?> tags)
    {
        // Static fields first
        internal static readonly double[] DefaultHistogramBounds = [0, 5, 10, 25, 50, 75, 100, 250, 500, 750, 1000, 2500, 5000, 7500, 10000];

        // Instance fields
        private readonly long[] _runningBucketCounts = instrumentType == InstrumentType.Histogram ? new long[DefaultHistogramBounds.Length + 1] : [];
        private readonly object _histogramLock = new();
        private long _runningCountValue;     // For counters and histogram count
        private double _runningDoubleValue;  // For gauges and histogram sum
        private double _runningMin = double.PositiveInfinity;
        private double _runningMax = double.NegativeInfinity;

        // Constructor

        // Public properties
        public string InstrumentName { get; } = instrumentName;

        public string MeterName { get; } = meterName;

        public InstrumentType InstrumentType { get; } = instrumentType;

        public AggregationTemporality? AggregationTemporality { get; } = temporality;

        public Dictionary<string, object?> Tags { get; } = tags;

        // Internal properties for collection access
        internal long RunningCount => _runningCountValue;

        internal double RunningSum => _runningDoubleValue;

        internal double RunningMin => _runningMin;

        internal double RunningMax => _runningMax;

        internal long[] RunningBucketCounts => _runningBucketCounts;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void UpdateCounter(double value)
        {
            // Use lock to avoid floating-point precision issues with CompareExchange
            lock (_histogramLock)
            {
                _runningDoubleValue += value;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void UpdateGauge(double value)
        {
            Interlocked.Exchange(ref _runningDoubleValue, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void UpdateHistogram(double value)
        {
            // Find bucket index first (outside lock for performance)
            var bucketIndex = FindBucketIndex(value);

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
