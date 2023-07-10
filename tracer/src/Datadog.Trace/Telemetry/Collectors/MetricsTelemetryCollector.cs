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
    private readonly string[] _unknownWafVersionTags = { "waf_version:unknown" };
    private readonly AggregatedMetrics _aggregated = new();
    private MetricBuffer _buffer = new();
    private MetricBuffer _reserveBuffer = new();
    private string[]? _wafVersionTags;

    public void Record(PublicApiUsage publicApi)
    {
        // This can technically overflow, but it's _very_ unlikely as we reset every minute
        // Negative values are normalized during polling
        Interlocked.Increment(ref _buffer.PublicApiCounts[(int)publicApi]);
    }

    public void AggregateMetrics()
    {
        var buffer = Interlocked.Exchange(ref _buffer, _reserveBuffer);

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        AggregateMetric(buffer.PublicApiCounts, timestamp, _aggregated.PublicApiCounts);
        AggregateMetric(buffer.Counts, timestamp, _aggregated.Counts);
        AggregateMetric(buffer.Gauges, timestamp, _aggregated.Gauges);
        AggregateDistribution(buffer.Distributions, _aggregated.Distributions);

        // prepare the buffer for next time
        buffer.Clear();
        Interlocked.Exchange(ref _reserveBuffer, buffer);
    }

    public MetricResults GetMetrics()
    {
        var metricData = GetMetricData(_aggregated.PublicApiCounts, _aggregated.Counts, _aggregated.Gauges);
        var distributionData = GetDistributionData(_aggregated.Distributions);

        return new(metricData, distributionData);
    }

    public void SetWafVersion(string wafVersion)
    {
        // Setting this an array so we can reuse it for multiple metrics
        _wafVersionTags = new[] { $"waf_version:{wafVersion}" };
    }

    public void Clear()
    {
        _reserveBuffer.Clear();
        var buffer = Interlocked.Exchange(ref _buffer, _reserveBuffer);
        buffer.Clear();
    }

    private void AggregateMetric(int[] metricValues, long timestamp, AggregatedMetric[] aggregatedMetrics)
    {
        for (var i = metricValues.Length - 1; i >= 0; i--)
        {
            var value = metricValues[i];
            if (value != 0)
            {
                if (value < 0)
                {
                    // handles overflow
                    value = int.MaxValue;
                }

                aggregatedMetrics[i].AddValue(timestamp, value);
            }
        }
    }

    private void AggregateDistribution(BoundedConcurrentQueue<double>[] distributions, AggregatedDistribution[] aggregatedDistributions)
    {
        for (var i = distributions.Length - 1; i >= 0; i--)
        {
            var distribution = distributions[i];
            while (distribution.TryDequeue(out var point))
            {
                aggregatedDistributions[i].AddValue(point);
            }
        }
    }

    /// <summary>
    /// Loop through the aggregated data, looking for any metrics that have points
    /// </summary>
    private List<MetricData>? GetMetricData(AggregatedMetric[] publicApis, AggregatedMetric[] counts, AggregatedMetric[] gauges)
    {
        var apiLength = publicApis.Count(x => x.HasValues);
        var countsLength = counts.Count(x => x.HasValues);
        var gaugesLength = gauges.Count(x => x.HasValues);

        var totalLength = apiLength + countsLength + gaugesLength;
        if (totalLength == 0)
        {
            return null;
        }

        var data = new List<MetricData>(totalLength);

        if (apiLength > 0)
        {
            for (var i = publicApis.Length - 1; i >= 0; i--)
            {
                var publicApi = publicApis[i];
                if (publicApi.GetAndClear() is { } series)
                {
                    data.Add(
                        new MetricData(
                            "public_api",
                            points: series,
                            common: false,
                            type: TelemetryMetricType.Count)
                        {
                            Tags = publicApi.Tags,
                        });
                }
            }
        }

        if (countsLength > 0)
        {
            var index = counts.Length - 1;
            for (var i = CountEntryCounts.Length - 1; i >= 0; i--)
            {
                var metric = (Count)i;
                var metricName = metric.GetName()!;
                var ns = metric.GetNamespace();
                var isCommon = metric.IsCommon();

                var entries = CountEntryCounts[i];
                for (var j = entries - 1; j >= 0; j--)
                {
                    var metricValues = counts[index];
                    if (metricValues.GetAndClear() is { } series)
                    {
                        data.Add(
                            new MetricData(
                                metricName,
                                points: series,
                                common: isCommon,
                                type: TelemetryMetricType.Count)
                            {
                                Namespace = ns,
                                Tags = GetTags(ns, metricValues.Tags),
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
                var metricName = metric.GetName()!;
                var ns = metric.GetNamespace();
                var isCommon = metric.IsCommon();

                var entries = GaugeEntryCounts[i];
                for (var j = entries - 1; j >= 0; j--)
                {
                    var metricValues = gauges[index];
                    if (metricValues.GetAndClear() is { } series)
                    {
                        data.Add(
                            new MetricData(
                                metricName,
                                points: series,
                                common: isCommon,
                                type: TelemetryMetricType.Gauge)
                            {
                                Namespace = ns,
                                Tags = GetTags(ns, metricValues.Tags)
                            });
                    }

                    index--;
                }
            }
        }

        return data;
    }

    private List<DistributionMetricData>? GetDistributionData(AggregatedDistribution[] distributions)
    {
        var distributionsLength = distributions.Count(x => x.HasValues);

        if (distributionsLength == 0)
        {
            return null;
        }

        var data = new List<DistributionMetricData>(distributionsLength);

        var index = distributions.Length - 1;
        for (var i = DistributionEntryCounts.Length - 1; i >= 0; i--)
        {
            var metric = (Distribution)i;
            var metricName = metric.GetName()!;
            var ns = metric.GetNamespace();
            var isCommon = metric.IsCommon();

            var entries = DistributionEntryCounts[i];
            for (var j = entries - 1; j >= 0; j--)
            {
                var metricValues = distributions[index];
                if (metricValues.GetAndClear() is { } values)
                {
                    data.Add(
                        new DistributionMetricData(
                            metricName,
                            points: values,
                            common: isCommon)
                        {
                            Namespace = ns,
                            Tags = GetTags(ns, metricValues.Tags),
                        });
                }

                index--;
            }
        }

        return data;
    }

    private string[]? GetTags(string? ns, string[]? metricKeyTags)
    {
        if (ns != MetricNamespaceConstants.ASM)
        {
            return metricKeyTags;
        }

        if (metricKeyTags is null)
        {
            return _wafVersionTags ?? _unknownWafVersionTags;
        }

        var wafVersionTag = (_wafVersionTags ?? _unknownWafVersionTags)[0];

        metricKeyTags[0] = wafVersionTag;
        return metricKeyTags;
    }

    private struct AggregatedMetric
    {
        private List<MetricDataPoint>? _values;
        public readonly string[]? Tags;

        public AggregatedMetric(string[]? tags)
        {
            Tags = tags;
        }

        public bool HasValues => _values is { Count: > 0 };

        public void AddValue(long timestamp, int value)
        {
            // we set the default capacity to 8 because we expect to get 6 values (steady state)
            // but we may get a couple of extra values initially, and don't want to end up needing to expand
            _values ??= new(capacity: 8);
            _values.Add(new(timestamp, value));
        }

        public MetricSeries? GetAndClear()
        {
            if (_values is { Count: > 0 } values)
            {
                var result = new MetricSeries(values);
                values.Clear();
                return result;
            }

            return null;
        }
    }

    private struct AggregatedDistribution
    {
        private List<double>? _values;
        public readonly string[]? Tags;

        public AggregatedDistribution(string[]? tags)
        {
            Tags = tags;
        }

        public bool HasValues => _values is { Count: > 0 };

        public void AddValue(double value)
        {
            _values ??= new();
            _values.Add(value);
        }

        public List<double>? GetAndClear()
        {
            if (_values is { Count: > 0 } values)
            {
                var result = new List<double>(_values);
                _values.Clear();
                return result;
            }

            return null;
        }
    }

    private class AggregatedMetrics
    {
#pragma warning disable SA1401 // fields should be private
        public readonly AggregatedMetric[] PublicApiCounts;
        public readonly AggregatedMetric[] Counts;
        public readonly AggregatedMetric[] Gauges;
        public readonly AggregatedDistribution[] Distributions;
#pragma warning restore SA1401

        public AggregatedMetrics()
        {
            PublicApiCounts = new AggregatedMetric[PublicApiUsageExtensions.Length];
            for (var i = PublicApiUsageExtensions.Length - 1; i >= 0; i--)
            {
                PublicApiCounts[i] = new(new[] { ((PublicApiUsage)i).ToStringFast() });
            }

            Counts = GetCountBuffer();
            Gauges = GetGaugeBuffer();
            Distributions = GetDistributionBuffer();
        }
    }

    private class MetricBuffer
    {
#pragma warning disable SA1401 // fields should be private
        public readonly int[] PublicApiCounts;
        public readonly int[] Counts;
        public readonly int[] Gauges;
        public readonly BoundedConcurrentQueue<double>[] Distributions;

#pragma warning restore SA1401

        public MetricBuffer()
        {
            PublicApiCounts = new int[PublicApiUsageExtensions.Length];
            Counts = new int[_countsLength];
            Gauges = new int[_gaugesLength];
            Distributions = new BoundedConcurrentQueue<double>[_distributionsLength];
            for (var i = _distributionsLength - 1; i >= 0; i--)
            {
                Distributions[i] = new BoundedConcurrentQueue<double>(queueLimit: 1000);
            }
        }

        public void Clear()
        {
            for (var i = 0; i < PublicApiCounts.Length; i++)
            {
                PublicApiCounts[i] = 0;
            }

            for (var i = 0; i < Counts.Length; i++)
            {
                Counts[i] = 0;
            }

            for (var i = 0; i < Gauges.Length; i++)
            {
                Gauges[i] = 0;
            }

            for (var i = 0; i < Distributions.Length; i++)
            {
                while (Distributions[i].TryDequeue(out _)) { }
            }
        }
    }
}
