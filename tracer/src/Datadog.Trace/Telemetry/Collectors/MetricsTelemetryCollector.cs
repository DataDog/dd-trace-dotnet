// <copyright file="MetricsTelemetryCollector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Util;

namespace Datadog.Trace.Telemetry;

internal class MetricsTelemetryCollector : IMetricsTelemetryCollector
{
    private MetricBuffer _buffer;
    private MetricBuffer _reserveBuffer;

    public MetricsTelemetryCollector()
    {
        _buffer = new();
        _reserveBuffer = new();
    }

    public void Record(PublicApiUsage publicApi)
    {
        // This can technically overflow, but it's _very_ unlikely as we reset every minute
        // Negative values are normalized during polling
        Interlocked.Increment(ref _buffer.PublicApiCounts[(int)publicApi]);
    }

    public void Record(Count count, int increment = 1)
    {
        AssertTags(count, 0);
        // This can technically overflow, but it's _very_ unlikely as we reset every minute
        // Negative values are normalized during polling
        Interlocked.Add(ref _buffer.Counts[(int)count], increment);
    }

    public void Record(Count count, MetricTags tag, int increment = 1)
    {
        AssertTags(count, 1);
        var metricKey = new MetricKey(count, tag, null, null);
        RecordCount(in metricKey, increment);
    }

    public void Record(Count count, MetricTags tag1, MetricTags tag2, int increment = 1)
    {
        AssertTags(count, 2);
        var metricKey = new MetricKey(count, tag1, tag2, null);
        RecordCount(in metricKey, increment);
    }

    public void Record(Count count, MetricTags tag1, MetricTags tag2, MetricTags tag3, int increment = 1)
    {
        AssertTags(count, 3);
        var metricKey = new MetricKey(count, tag1, tag2, tag3);
        RecordCount(in metricKey, increment);
    }

    public void Record(Gauge metric, int value)
    {
        AssertTags(metric, 0);
        Interlocked.Exchange(ref _buffer.Gauges[(int)metric], value);
    }

    public void Record(Gauge metric, MetricTags tag, int value)
    {
        AssertTags(metric, 1);
        var metricKey = new MetricKey(metric, tag, null, null);
        RecordGauge(in metricKey, value);
    }

    public void Record(Distribution metric, double value)
    {
        AssertTags(metric, 0);
        _buffer.Distributions[(int)metric].TryEnqueue(value);
    }

    public void Record(Distribution metric, MetricTags tag, double value)
    {
        AssertTags(metric, 1);
        var metricKey = new MetricKey(metric, tag, null, null);
        RecordDistribution(in metricKey, value);
    }

    public MetricResults GetMetrics()
    {
        var buffer = Interlocked.Exchange(ref _buffer, _reserveBuffer);
        var publicApis = buffer.PublicApiCounts;
        var counts = buffer.Counts;
        var countsWithTags = buffer.CountsWithTags;
        var gauges = buffer.Gauges;
        var gaugesWithTags = buffer.GaugesWithTags;
        var distributions = buffer.Distributions;
        var distributionsWithTags = buffer.DistributionsWithTags;

        var metricData = GetMetricData(publicApis, counts, countsWithTags, gauges, gaugesWithTags);
        var distributionData = GetDistributionData(distributions, distributionsWithTags);

        // prepare the buffer for next time
        buffer.Clear();
        Interlocked.Exchange(ref _reserveBuffer, buffer);

        return new(metricData, distributionData);
    }

    [Conditional("DEBUG")]
    private static void AssertTags(Count metric, int actualTags)
        => Debug.Assert(metric.ExpectedTags() == actualTags, $"Expected {metric} to have {metric.ExpectedTags()} tags, but found {actualTags}");

    [Conditional("DEBUG")]
    private static void AssertTags(Gauge metric, int actualTags)
        => Debug.Assert(metric.ExpectedTags() == actualTags, $"Expected {metric} to have {metric.ExpectedTags()} tags, but found {actualTags}");

    [Conditional("DEBUG")]
    private static void AssertTags(Distribution metric, int actualTags)
        => Debug.Assert(metric.ExpectedTags() == actualTags, $"Expected {metric} to have {metric.ExpectedTags()} tags, but found {actualTags}");

    private static List<MetricData>? GetMetricData(
        int[] publicApis,
        int[] counts,
        ConcurrentDictionary<MetricKey, int> countsWithTags,
        int[] gauges,
        ConcurrentDictionary<MetricKey, int> gaugesWithTags)
    {
        var apiLength = publicApis.Count(x => x > 0);
        var countsLength = counts.Count(x => x > 0);
        var countsWithTagsLength = countsWithTags.Count(x => x.Value > 0);
        var gaugesLength = gauges.Count(x => x > 0);
        var gaugesWithTagsLength = gaugesWithTags.Count(x => x.Value > 0);

        var totalLength = apiLength + countsLength + countsWithTagsLength + gaugesLength + gaugesWithTagsLength;
        if (totalLength == 0)
        {
            return null;
        }

        var data = new List<MetricData>(totalLength);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        if (apiLength > 0)
        {
            for (var i = 0; i < publicApis.Length; i++)
            {
                var value = publicApis[i];
                if (value < 0)
                {
                    // handles overflow
                    value = int.MaxValue;
                }

                if (value > 0 && ((PublicApiUsage)i).ToStringFast() is { } metricName)
                {
                    // TODO: should public api be separate metrics, or single tagged metric?
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
            for (var i = 0; i < counts.Length; i++)
            {
                var value = counts[i];
                if (value < 0)
                {
                    // handles overflow
                    value = int.MaxValue;
                }

                var metric = (Count)i;
                if (value > 0 && metric.GetName() is { } metricName)
                {
                    data.Add(
                        new MetricData(
                            metricName,
                            points: new MetricSeries { new(timestamp, value) },
                            common: metric.IsCommon(),
                            type: TelemetryMetricType.Count));
                }
            }
        }

        if (gaugesLength > 0)
        {
            for (var i = 0; i < gauges.Length; i++)
            {
                var value = gauges[i];
                var metric = (Gauge)i;
                if (value > 0 && metric.GetName() is { } metricName)
                {
                    data.Add(
                        new MetricData(
                            metricName,
                            points: new MetricSeries { new(timestamp, value) },
                            common: metric.IsCommon(),
                            type: TelemetryMetricType.Gauge));
                }
            }
        }

        if (countsWithTagsLength > 0)
        {
            foreach (var kvp in countsWithTags)
            {
                var helper = kvp.Key;
                var metric = (Count)helper.Metric;
                if (metric.GetName() is { } metricName)
                {
                    data.Add(
                        new MetricData(
                            metricName,
                            points: new MetricSeries { new(timestamp, kvp.Value) },
                            common: metric.IsCommon(),
                            type: TelemetryMetricType.Count) { Tags = helper.GetTags(), });
                }
            }
        }

        if (gaugesWithTagsLength > 0)
        {
            foreach (var kvp in gaugesWithTags)
            {
                var helper = kvp.Key;
                var metric = (Gauge)helper.Metric;
                if (metric.GetName() is { } metricName)
                {
                    data.Add(
                        new MetricData(
                            metricName,
                            points: new MetricSeries { new(timestamp, kvp.Value) },
                            common: metric.IsCommon(),
                            type: TelemetryMetricType.Count) { Tags = helper.GetTags(), });
                }
            }
        }

        return data;
    }

    private static List<DistributionMetricData>? GetDistributionData(
        BoundedConcurrentQueue<double>[] distributions,
        ConcurrentDictionary<MetricKey, BoundedConcurrentQueue<double>> distributionsWithTags)
    {
        var distributionsLength = distributions.Count(x => x.Count > 0);
        var distributionsWithTagsLength = distributionsWithTags.Count(x => x.Value.Count > 0);

        var totalLength = distributionsLength + distributionsWithTagsLength;
        if (totalLength == 0)
        {
            return null;
        }

        var data = new List<DistributionMetricData>(totalLength);

        if (distributionsLength > 0)
        {
            for (var i = 0; i < distributions.Length; i++)
            {
                var queue = distributions[i];
                var metric = (Distribution)i;
                if (queue.Count > 0 && metric.GetName() is { } metricName)
                {
                    data.Add(
                        new DistributionMetricData(
                            metricName,
                            points: queue.ToArray(),
                            common: metric.IsCommon()));
                }
            }
        }

        foreach (var kvp in distributionsWithTags)
        {
            var helper = kvp.Key;
            var metric = (Distribution)helper.Metric;
            if (metric.GetName() is { } metricName)
            {
                data.Add(
                    new DistributionMetricData(
                        metricName,
                        points: kvp.Value.ToArray(),
                        common: metric.IsCommon()) { Tags = helper.GetTags(), });
            }
        }

        return data;
    }

    private void RecordCount(in MetricKey key, int increment)
    {
#if NETCOREAPP
        // we can avoid the closure in .NET Core
        _buffer.CountsWithTags.AddOrUpdate(
            key: key,
            addValueFactory: static (_, arg) => arg,
            updateValueFactory: static (_, old, arg) => (old + arg) < old ? int.MaxValue : (old + arg),
            factoryArgument: increment);
#else
        _buffer.CountsWithTags.AddOrUpdate(
            key,
            increment,
            (_, old) => (old + increment) < old ? int.MaxValue : (old + increment)); // look out for overflow
#endif
    }

    private void RecordGauge(in MetricKey key, int value)
    {
        _buffer.GaugesWithTags[key] = value;
    }

    private void RecordDistribution(in MetricKey key, double value)
    {
        var queue = _buffer.DistributionsWithTags.GetOrAdd(key, _ => MetricBuffer.CreateDistributionQueue());
        queue.TryEnqueue(value);
    }

    private readonly record struct MetricKey
    {
        public readonly int Metric;
        public readonly MetricTags Tag1;
        public readonly MetricTags? Tag2;
        public readonly MetricTags? Tag3;

        public MetricKey(Count count, MetricTags tag1, MetricTags? tag2, MetricTags? tag3)
        {
            Metric = (int)count;
            Tag1 = tag1;
            Tag2 = tag2;
            Tag3 = tag3;
        }

        public MetricKey(Gauge gauge, MetricTags tag1, MetricTags? tag2, MetricTags? tag3)
        {
            Metric = (int)gauge;
            Tag1 = tag1;
            Tag2 = tag2;
            Tag3 = tag3;
        }

        public MetricKey(Distribution distribution, MetricTags tag1, MetricTags? tag2, MetricTags? tag3)
        {
            Metric = (int)distribution;
            Tag1 = tag1;
            Tag2 = tag2;
            Tag3 = tag3;
        }

        public List<string> GetTags()
        {
            // Relies on the fact that we don't set Tag3's value unless we've set Tag2
            var capacity = Tag3.HasValue ? 3 : Tag2.HasValue ? 2 : 1;
            var list = new List<string>(capacity) { Tag1.ToStringFast(), };

            if (Tag2.HasValue)
            {
                list.Add(Tag2.Value.ToStringFast());

                if (Tag3.HasValue)
                {
                    list.Add(Tag3.Value.ToStringFast());
                }
            }

            return list;
        }
    }

    private class MetricBuffer
    {
#pragma warning disable SA1401 // fields should be private
        public readonly int[] PublicApiCounts;
        public readonly int[] Counts;
        public readonly ConcurrentDictionary<MetricKey, int> CountsWithTags;

        public readonly int[] Gauges;
        public readonly ConcurrentDictionary<MetricKey, int> GaugesWithTags;

        public readonly BoundedConcurrentQueue<double>[] Distributions;
        public readonly ConcurrentDictionary<MetricKey, BoundedConcurrentQueue<double>> DistributionsWithTags;
#pragma warning restore SA1401

        public MetricBuffer()
        {
            PublicApiCounts = new int[PublicApiUsageExtensions.Length];
            Counts = new int[CountExtensions.Length];
            CountsWithTags = new();
            Gauges = new int[GaugeExtensions.Length];
            GaugesWithTags = new();
            Distributions = GetInitialDistributions();
            DistributionsWithTags = new();
        }

        public static BoundedConcurrentQueue<double> CreateDistributionQueue() => new(queueLimit: 1000);

        private static BoundedConcurrentQueue<double>[] GetInitialDistributions()
        {
            var d = new BoundedConcurrentQueue<double>[DistributionExtensions.Length];
            for (var i = DistributionExtensions.Length - 1; i >= 0; i--)
            {
                d[i] = CreateDistributionQueue(); // maximum number of points per distribution, per unit time
            }

            return d;
        }

        public void Clear()
        {
            for (var i = 0; i < PublicApiUsageExtensions.Length; i++)
            {
                PublicApiCounts[i] = 0;
            }

            for (var i = 0; i < CountExtensions.Length; i++)
            {
                Counts[i] = 0;
            }

            for (var i = 0; i < GaugeExtensions.Length; i++)
            {
                Gauges[i] = 0;
            }

            for (var i = 0; i < DistributionExtensions.Length; i++)
            {
                Distributions[i].Clear();
            }

            foreach (var kvp in DistributionsWithTags)
            {
                kvp.Value.Clear();
            }
        }
    }
}
