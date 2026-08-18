// <copyright file="DebuggerGuardrailMetricTestHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Linq;
using System.Threading;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Xunit;

namespace Datadog.Trace.Tests.Debugger;

internal static class DebuggerGuardrailMetricTestHelpers
{
    internal static IDisposable OverrideMetrics(out MetricsTelemetryCollector collector)
    {
        // Tracer startup can replace/clear TelemetryFactory.Metrics. Initialize it first so
        // capture/finalize during the test does not drop recordings from this collector.
        _ = Datadog.Trace.Tracer.Instance;
        collector = new MetricsTelemetryCollector(Timeout.InfiniteTimeSpan);
        var previous = TelemetryFactory.SetMetricsForTesting(collector);
        return new RestoreMetrics(previous, collector);
    }

    internal static void AssertHasCount(this MetricsTelemetryCollector collector, string metricName, string reasonTag, string eventTypeTag, int expected = 1)
    {
        collector.AggregateMetrics();
        var metrics = collector.GetMetrics().Metrics;
        Assert.NotNull(metrics);
        var match = metrics.SingleOrDefault(
            metric => metric.Metric == metricName
                   && metric.Tags != null
                   && metric.Tags.Contains(reasonTag)
                   && metric.Tags.Contains(eventTypeTag));
        Assert.NotNull(match);
        Assert.Equal(expected, match!.Points[0].Value);
    }

    internal static void AssertHasCounts(this MetricsTelemetryCollector collector, string metricName, string eventTypeTag, params string[] reasonTags)
    {
        collector.AggregateMetrics();
        var metrics = collector.GetMetrics().Metrics;
        Assert.NotNull(metrics);
        foreach (var reasonTag in reasonTags)
        {
            Assert.Contains(
                metrics,
                metric => metric.Metric == metricName
                       && metric.Tags != null
                       && metric.Tags.Contains(reasonTag)
                       && metric.Tags.Contains(eventTypeTag));
        }
    }

    internal static void AssertDoesNotHave(this MetricsTelemetryCollector collector, string metricName)
    {
        collector.AggregateMetrics();
        var metrics = collector.GetMetrics().Metrics;
        Assert.True(metrics is null || metrics.All(metric => metric.Metric != metricName));
    }

    private sealed class RestoreMetrics(IMetricsTelemetryCollector previous, MetricsTelemetryCollector collector) : IDisposable
    {
        public void Dispose()
        {
            try
            {
                TelemetryFactory.SetMetricsForTesting(previous);
            }
            finally
            {
                collector.DisposeAsync().GetAwaiter().GetResult();
            }
        }
    }
}
