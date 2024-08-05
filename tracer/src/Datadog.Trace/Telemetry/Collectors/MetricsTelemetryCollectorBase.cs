// <copyright file="MetricsTelemetryCollectorBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Util;

namespace Datadog.Trace.Telemetry;

internal abstract partial class MetricsTelemetryCollectorBase
{
    private readonly TimeSpan _aggregationInterval;
    private readonly Action? _aggregationNotification;
    private readonly string[] _unknownWafVersionTags = { "waf_version:unknown" };
    private readonly Task _aggregateTask;
    private readonly TaskCompletionSource<bool> _processExit = new();
    private string[]? _wafVersionTags;

    protected MetricsTelemetryCollectorBase()
        : this(TimeSpan.FromSeconds(10))
    {
    }

    protected MetricsTelemetryCollectorBase(TimeSpan aggregationInterval, Action? aggregationNotification = null)
    {
        _aggregationInterval = aggregationInterval;
        _aggregationNotification = aggregationNotification;
        _aggregateTask = Task.Run(AggregateMetricsLoopAsync);
        _aggregateTask
           .ContinueWith(
                t =>
                {
                    // There's a complex relationship between metrics and logs initialization, so we don't log anything in this case
                },
                TaskContinuationOptions.OnlyOnFaulted);
    }

    /// <summary>
    /// Internal for testing
    /// </summary>
    internal abstract void AggregateMetrics();

    internal abstract void Clear();

    public Task DisposeAsync()
    {
        _processExit.TrySetResult(true);
        return _aggregateTask;
    }

    public void SetWafVersion(string wafVersion)
    {
        // Setting this an array so we can reuse it for multiple metrics
        _wafVersionTags = new[] { $"waf_version:{wafVersion}" };
    }

    protected static AggregatedMetric[] GetPublicApiCountBuffer()
    {
        var publicApiCounts = new AggregatedMetric[PublicApiUsageExtensions.Length];
        for (var i = PublicApiUsageExtensions.Length - 1; i >= 0; i--)
        {
            publicApiCounts[i] = new(new[] { ((PublicApiUsage)i).ToStringFast() });
        }

        return publicApiCounts;
    }

    protected static void AggregateMetric(int[] metricValues, long timestamp, AggregatedMetric[] aggregatedMetrics)
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

                // Be careful here to avoid copying - AggregatedMetric is a stuct
                // We may refactor this later to be less dangerous
                aggregatedMetrics[i].AddValue(timestamp, value);
            }
        }
    }

    protected static void AggregateDistribution(BoundedConcurrentQueue<double>[] distributions, AggregatedDistribution[] aggregatedDistributions)
    {
        for (var i = distributions.Length - 1; i >= 0; i--)
        {
            var distribution = distributions[i];
            while (distribution.TryDequeue(out var point))
            {
                // Be careful here to avoid copying - AggregatedDistribution is a stuct
                // We may refactor this later to be less dangerous
                aggregatedDistributions[i].AddValue(point);
            }
        }
    }

    protected void AddPublicApiMetricData(AggregatedMetric[] publicApis, List<MetricData> data)
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

    protected void AddMetricData(
        string metricType,
        AggregatedMetric[] values,
        List<MetricData> data,
        int[] cardinalityPerMetric,
        Func<int, MetricDetails> getMetricDetails)
    {
        var index = values.Length - 1;
        for (var i = cardinalityPerMetric.Length - 1; i >= 0; i--)
        {
            var metric = getMetricDetails(i);
            var entries = cardinalityPerMetric[i];
            for (var j = entries - 1; j >= 0; j--)
            {
                var metricValues = values[index];
                if (metricValues.GetAndClear() is { } series)
                {
                    data.Add(
                        new MetricData(
                            metric.Name,
                            points: series,
                            common: metric.IsCommon,
                            type: metricType)
                        {
                            Namespace = metric.NameSpace,
                            Tags = GetTags(metric.NameSpace, metricValues.Tags)
                        });
                }

                index--;
            }
        }
    }

    protected void AddDistributionData(
        AggregatedDistribution[] distributions,
        List<DistributionMetricData> data,
        int[] cardinalityPerDistribution,
        Func<int, MetricDetails> getMetricDetails)
    {
        var index = distributions.Length - 1;
        for (var i = cardinalityPerDistribution.Length - 1; i >= 0; i--)
        {
            var metric = getMetricDetails(i);

            var entries = cardinalityPerDistribution[i];
            for (var j = entries - 1; j >= 0; j--)
            {
                var metricValues = distributions[index];
                if (metricValues.GetAndClear() is { } values)
                {
                    data.Add(
                        new DistributionMetricData(
                            metric.Name,
                            points: values,
                            common: metric.IsCommon)
                        {
                            Namespace = metric.NameSpace,
                            Tags = GetTags(metric.NameSpace, metricValues.Tags),
                        });
                }

                index--;
            }
        }
    }

    private async Task AggregateMetricsLoopAsync()
    {
        var tasks = new Task[2];
        tasks[0] = _processExit.Task;
        while (true)
        {
            tasks[1] = Task.Delay(_aggregationInterval);
            await Task.WhenAny(tasks).ConfigureAwait(false);

            // The process may have exited, but we want to do a final aggregation before process end anyway
            AggregateMetrics();
            _aggregationNotification?.Invoke();

            if (_processExit.Task.IsCompleted)
            {
                return;
            }
        }
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

    protected struct AggregatedMetric
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

    protected struct AggregatedDistribution
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

    protected readonly struct MetricDetails
    {
        public readonly string Name;
        public readonly string? NameSpace;
        public readonly bool IsCommon;

        public MetricDetails(string name, string? nameSpace, bool isCommon)
        {
            Name = name;
            NameSpace = nameSpace;
            IsCommon = isCommon;
        }
    }
}
