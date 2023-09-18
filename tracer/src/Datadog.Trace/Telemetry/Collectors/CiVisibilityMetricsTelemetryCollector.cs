// <copyright file="CiVisibilityMetricsTelemetryCollector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Util;

namespace Datadog.Trace.Telemetry;

internal partial class CiVisibilityMetricsTelemetryCollector : MetricsTelemetryCollectorBase, IMetricsTelemetryCollector
{
    private readonly Lazy<AggregatedMetrics> _aggregated = new();
    private MetricBuffer _buffer = new();
    private MetricBuffer _reserveBuffer = new();

    public CiVisibilityMetricsTelemetryCollector()
        : base()
    {
    }

    internal CiVisibilityMetricsTelemetryCollector(TimeSpan aggregationInterval)
        : base(aggregationInterval)
    {
    }

    public void Record(PublicApiUsage publicApi)
    {
        // This can technically overflow, but it's _very_ unlikely as we reset every 10s
        // Negative values are normalized during polling
        Interlocked.Increment(ref _buffer.PublicApiCounts[(int)publicApi]);
    }

    internal override void Clear()
    {
        _reserveBuffer.Clear();
        var buffer = Interlocked.Exchange(ref _buffer, _reserveBuffer);
        buffer.Clear();
    }

    public MetricResults GetMetrics()
    {
        List<MetricData>? metricData;
        List<DistributionMetricData>? distributionData;

        var aggregated = _aggregated.Value;
        lock (aggregated)
        {
            metricData = GetMetricData(aggregated.PublicApiCounts, aggregated.CountCis, aggregated.CountsShared);
            distributionData = GetDistributionData(aggregated.DistributionCis, aggregated.DistributionsShared);
        }

        return new(metricData, distributionData);
    }

    /// <summary>
    /// Internal for testing
    /// </summary>
    internal override void AggregateMetrics()
    {
        var buffer = Interlocked.Exchange(ref _buffer, _reserveBuffer);

        var aggregated = _aggregated.Value;
        // _aggregated, containing the aggregated metrics, is not thread-safe
        // and is also used when getting the metrics for serialization.
        lock (aggregated)
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            AggregateMetric(buffer.PublicApiCounts, timestamp, aggregated.PublicApiCounts);
            AggregateMetric(buffer.CountCIVisibility, timestamp, aggregated.CountCis);
            AggregateMetric(buffer.CountShared, timestamp, aggregated.CountsShared);
            AggregateDistribution(buffer.DistributionCIVisibility, aggregated.DistributionCis);
            AggregateDistribution(buffer.DistributionShared, aggregated.DistributionsShared);
        }

        // prepare the buffer for next time
        buffer.Clear();
        Interlocked.Exchange(ref _reserveBuffer, buffer);
    }

    private static MetricDetails GetCountCiDetails(int i)
    {
        var metric = (CountCIVisibility)i;
        return new MetricDetails(metric.GetName(), metric.GetNamespace(), metric.IsCommon());
    }

    private static MetricDetails GetCountSharedDetails(int i)
    {
        var metric = (CountShared)i;
        return new MetricDetails(metric.GetName(), metric.GetNamespace(), metric.IsCommon());
    }

    private static MetricDetails GetDistributionCiDetails(int i)
    {
        var metric = (DistributionCIVisibility)i;
        return new MetricDetails(metric.GetName(), metric.GetNamespace(), metric.IsCommon());
    }

    private static MetricDetails GetDistributionSharedDetails(int i)
    {
        var metric = (DistributionShared)i;
        return new MetricDetails(metric.GetName(), metric.GetNamespace(), metric.IsCommon());
    }

    /// <summary>
    /// Loop through the aggregated data, looking for any metrics that have points
    /// </summary>
    private List<MetricData>? GetMetricData(AggregatedMetric[] publicApis, AggregatedMetric[] counts, AggregatedMetric[] countsShared)
    {
        var apiLength = publicApis.Count(x => x.HasValues);
        var countsLength = counts.Count(x => x.HasValues);
        var countsSharedLength = countsShared.Count(x => x.HasValues);

        var totalLength = apiLength + countsLength + countsSharedLength;
        if (totalLength == 0)
        {
            return null;
        }

        var data = new List<MetricData>(totalLength);

        if (apiLength > 0)
        {
            AddPublicApiMetricData(publicApis, data);
        }

        if (countsLength > 0)
        {
            AddMetricData(TelemetryMetricType.Count, counts, data, CountCIVisibilityEntryCounts, GetCountCiDetails);
        }

        if (countsSharedLength > 0)
        {
            AddMetricData(TelemetryMetricType.Count, countsShared, data, CountSharedEntryCounts, GetCountSharedDetails);
        }

        return data;
    }

    private List<DistributionMetricData>? GetDistributionData(AggregatedDistribution[] distributions, AggregatedDistribution[] distributionsShared)
    {
        var distributionsLength = distributions.Count(x => x.HasValues);
        var distributionsSharedLength = distributionsShared.Count(x => x.HasValues);

        if (distributionsLength == 0 && distributionsSharedLength == 0)
        {
            return null;
        }

        var data = new List<DistributionMetricData>(distributionsLength + distributionsSharedLength);

        if (distributionsLength > 0)
        {
            AddDistributionData(distributions, data, DistributionCIVisibilityEntryCounts, GetDistributionCiDetails);
        }

        if (distributionsSharedLength > 0)
        {
            AddDistributionData(distributionsShared, data, DistributionSharedEntryCounts, GetDistributionSharedDetails);
        }

        return data;
    }

    private class AggregatedMetrics
    {
#pragma warning disable SA1401 // fields should be private
        public readonly AggregatedMetric[] PublicApiCounts;
        public readonly AggregatedMetric[] CountCis;
        public readonly AggregatedMetric[] CountsShared;
        public readonly AggregatedDistribution[] DistributionCis;
        public readonly AggregatedDistribution[] DistributionsShared;
#pragma warning restore SA1401

        public AggregatedMetrics()
        {
            PublicApiCounts = GetPublicApiCountBuffer();
            CountCis = GetCountCIVisibilityBuffer();
            CountsShared = GetCountSharedBuffer();
            DistributionCis = GetDistributionCIVisibilityBuffer();
            DistributionsShared = GetDistributionSharedBuffer();
        }
    }

    private class MetricBuffer
    {
#pragma warning disable SA1401 // fields should be private
        public readonly int[] PublicApiCounts;
        public readonly int[] CountCIVisibility;
        public readonly int[] CountShared;
        public readonly BoundedConcurrentQueue<double>[] DistributionCIVisibility;
        public readonly BoundedConcurrentQueue<double>[] DistributionShared;

#pragma warning restore SA1401

        public MetricBuffer()
        {
            PublicApiCounts = new int[PublicApiUsageExtensions.Length];
            CountCIVisibility = new int[CountCIVisibilityLength];
            CountShared = new int[CountSharedLength];
            DistributionCIVisibility = new BoundedConcurrentQueue<double>[DistributionCIVisibilityLength];
            DistributionShared = new BoundedConcurrentQueue<double>[DistributionSharedLength];

            for (var i = DistributionCIVisibilityLength - 1; i >= 0; i--)
            {
                DistributionCIVisibility[i] = new BoundedConcurrentQueue<double>(queueLimit: 1000);
            }

            for (var i = DistributionSharedLength - 1; i >= 0; i--)
            {
                DistributionShared[i] = new BoundedConcurrentQueue<double>(queueLimit: 1000);
            }
        }

        public void Clear()
        {
            for (var i = 0; i < PublicApiCounts.Length; i++)
            {
                PublicApiCounts[i] = 0;
            }

            for (var i = 0; i < CountCIVisibility.Length; i++)
            {
                CountCIVisibility[i] = 0;
            }

            for (var i = 0; i < CountShared.Length; i++)
            {
                CountShared[i] = 0;
            }

            for (var i = 0; i < DistributionCIVisibility.Length; i++)
            {
                while (DistributionCIVisibility[i].TryDequeue(out _)) { }
            }

            for (var i = 0; i < DistributionShared.Length; i++)
            {
                while (DistributionShared[i].TryDequeue(out _)) { }
            }
        }
    }
}
