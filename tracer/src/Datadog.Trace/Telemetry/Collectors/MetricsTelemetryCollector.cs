// <copyright file="MetricsTelemetryCollector.cs" company="Datadog">
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

internal partial class MetricsTelemetryCollector : IMetricsTelemetryCollector
{
    private MetricBuffer _buffer;
    private MetricBuffer _reserveBuffer;

    public MetricsTelemetryCollector()
    {
        _buffer = new();
        _reserveBuffer = _buffer.Clone();
    }

    public void Record(PublicApiUsage publicApi)
    {
        // This can technically overflow, but it's _very_ unlikely as we reset every minute
        // Negative values are normalized during polling
        Interlocked.Increment(ref _buffer.PublicApiCounts[(int)publicApi]);
    }

    public MetricResults GetMetrics()
    {
        var buffer = Interlocked.Exchange(ref _buffer, _reserveBuffer);
        var publicApis = buffer.PublicApiCounts;
        var counts = buffer.Counts;
        var gauges = buffer.Gauges;
        var distributions = buffer.Distributions;

        var metricData = GetMetricData(publicApis, counts, gauges);
        var distributionData = GetDistributionData(distributions);

        // prepare the buffer for next time
        buffer.Clear();
        Interlocked.Exchange(ref _reserveBuffer, buffer);

        return new(metricData, distributionData);
    }

    public void Clear()
    {
        _reserveBuffer.Clear();
        var buffer = Interlocked.Exchange(ref _buffer, _reserveBuffer);
        buffer.Clear();
    }

    private static List<MetricData>? GetMetricData(int[] publicApis, MetricKey[] counts, MetricKey[] gauges)
    {
        var apiLength = publicApis.Count(x => x > 0);
        var countsLength = counts.Count(x => x.Value > 0);
        var gaugesLength = gauges.Count(x => x.Value > 0);

        var totalLength = apiLength + countsLength + gaugesLength;
        if (totalLength == 0)
        {
            return null;
        }

        var data = new List<MetricData>(totalLength);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        if (apiLength > 0)
        {
            for (var i = publicApis.Length - 1; i >= 0; i--)
            {
                var value = publicApis[i];
                if (value < 0)
                {
                    // handles overflow
                    value = int.MaxValue;
                }

                if (value > 0 && ((PublicApiUsage)i).ToStringFast() is { } metricName)
                {
                    data.Add(
                        new MetricData(
                            metricName,
                            points: new MetricSeries { new(timestamp, value) },
                            common: false,
                            type: TelemetryMetricType.Count));
                }
            }
        }

        if (countsLength > 0)
        {
            var index = counts.Length - 1;
            for (var i = CountEntryCounts.Length - 1; i >= 0; i--)
            {
                var metric = (Count)i;
                var entries = CountEntryCounts[i];
                for (var j = entries - 1; j >= 0; j--)
                {
                    var metricKey = counts[index];
                    var value = metricKey.Value;
                    if (value < 0)
                    {
                        // handles overflow
                        value = int.MaxValue;
                    }

                    if (value > 0 && metric.GetName() is { } metricName)
                    {
                        data.Add(
                            new MetricData(
                                metricName,
                                points: new MetricSeries { new(timestamp, value) },
                                common: metric.IsCommon(),
                                type: TelemetryMetricType.Count)
                            {
                                Namespace = metric.GetNamespace(),
                                Tags = metricKey.Tags,
                            });
                    }

                    index--;
                }
            }
        }

        if (gaugesLength > 0)
        {
            var index = gauges.Length - 1;
            for (var i = GaugeEntryCounts.Length - 1; i >= 0; i--)
            {
                var metric = (Gauge)i;
                var entries = GaugeEntryCounts[i];
                for (var j = entries - 1; j >= 0; j--)
                {
                    var metricKey = gauges[index];
                    var value = metricKey.Value;
                    if (value > 0 && metric.GetName() is { } metricName)
                    {
                        data.Add(
                            new MetricData(
                                metricName,
                                points: new MetricSeries { new(timestamp, value) },
                                common: metric.IsCommon(),
                                type: TelemetryMetricType.Gauge)
                            {
                                Namespace = metric.GetNamespace(),
                                Tags = metricKey.Tags
                            });
                    }

                    index--;
                }
            }
        }

        return data;
    }

    private static List<DistributionMetricData>? GetDistributionData(DistributionKey[] distributions)
    {
        var distributionsLength = distributions.Count(x => x.Values.Count > 0);

        if (distributionsLength == 0)
        {
            return null;
        }

        var data = new List<DistributionMetricData>(distributionsLength);

        var index = distributions.Length - 1;
        for (var i = DistributionEntryCounts.Length - 1; i >= 0; i--)
        {
            var metric = (Distribution)i;
            var entries = DistributionEntryCounts[i];
            for (var j = entries - 1; j >= 0; j--)
            {
                var metricKey = distributions[index];
                var queue = metricKey.Values;
                if (queue.Count > 0 && metric.GetName() is { } metricName)
                {
                    var points = new List<double>(queue.Count);
                    while (queue.TryDequeue(out var point))
                    {
                        points.Add(point);
                    }

                    data.Add(
                        new DistributionMetricData(
                            metricName,
                            points: points,
                            common: metric.IsCommon())
                        {
                            Namespace = metric.GetNamespace(),
                            Tags = metricKey.Tags,
                        });
                }

                index--;
            }
        }

        return data;
    }

    private record struct MetricKey
    {
        public int Value;
        public readonly string[]? Tags;

        public MetricKey(string[]? tags)
        {
            Value = 0;
            Tags = tags;
        }
    }

    private record struct DistributionKey
    {
        public BoundedConcurrentQueue<double> Values;
        public readonly string[]? Tags;

        public DistributionKey(string[]? tags)
        {
            Values = new(queueLimit: 1000);
            Tags = tags;
        }
    }

    private class MetricBuffer
    {
#pragma warning disable SA1401 // fields should be private
        public readonly int[] PublicApiCounts;
        public readonly MetricKey[] Counts;
        public readonly MetricKey[] Gauges;
        public readonly DistributionKey[] Distributions;

#pragma warning restore SA1401

        public MetricBuffer()
            : this(
                publicApiCounts: new int[PublicApiUsageExtensions.Length],
                counts: GetCountBuffer(),
                gauges: GetGaugeBuffer(),
                distributions: GetDistributionBuffer())
        {
        }

        private MetricBuffer(
            int[] publicApiCounts,
            MetricKey[] counts,
            MetricKey[] gauges,
            DistributionKey[] distributions)
        {
            PublicApiCounts = publicApiCounts;
            Counts = counts;
            Gauges = gauges;
            Distributions = distributions;
        }

        public MetricBuffer Clone()
        {
            var publicApiCounts = new int[PublicApiUsageExtensions.Length];
            var counts = new MetricKey[Counts.Length];
            var gauges = new MetricKey[Gauges.Length];
            var distributions = new DistributionKey[Distributions.Length];

            // Ensures we copy the reference the string[] of tags, to reduce allocation
            Array.Copy(Counts, counts, Counts.Length);
            Array.Copy(Gauges, gauges, Gauges.Length);
            Array.Copy(Distributions, distributions, Distributions.Length);
            return new MetricBuffer(publicApiCounts, counts, gauges, distributions);
        }

        public void Clear()
        {
            for (var i = 0; i < PublicApiCounts.Length; i++)
            {
                PublicApiCounts[i] = 0;
            }

            for (var i = 0; i < Counts.Length; i++)
            {
                Counts[i].Value = 0;
            }

            for (var i = 0; i < Gauges.Length; i++)
            {
                Gauges[i].Value = 0;
            }

            for (var i = 0; i < Distributions.Length; i++)
            {
                while (Distributions[i].Values.TryDequeue(out _)) { }
            }
        }
    }
}
