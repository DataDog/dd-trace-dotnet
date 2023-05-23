// <copyright file="MetricsTelemetryCollectorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Datadog.Trace.Tests.Telemetry.Collectors;

public class MetricsTelemetryCollectorTests
{
    [Fact]
    public void AllMetricsAreReturned()
    {
        var collector = new MetricsTelemetryCollector();
        collector.Record(PublicApiUsage.Tracer_Configure);
        collector.Record(PublicApiUsage.Tracer_Configure);
        collector.Record(PublicApiUsage.Tracer_Ctor);
        collector.Record(Count.SpanFinished, 15);
        // Note that I'm reusing the same metric tags for all the tags here, just because I haven't created the "real" values we need yet
        collector.Record(Count.IntegrationsError, MetricTags.IntegrationName_Aerospike, MetricTags.IntegrationName_Aerospike, MetricTags.IntegrationName_Aerospike);
        collector.Record(Count.SpanCreated, MetricTags.IntegrationName_Aerospike, MetricTags.IntegrationName_Aerospike);
        collector.Record(Count.SpanDropped, MetricTags.DropReason_SingleSpanSampling, 23);
        collector.Record(Gauge.StatsBuckets, 234);
        collector.Record(Distribution.InitTime, MetricTags.Total, 23);
        collector.Record(Distribution.InitTime, MetricTags.Total, 46);
        collector.Record(Distribution.InitTime, MetricTags.Component_Managed, 52);

        using var scope = new AssertionScope();

        var metrics = collector.GetMetrics();

        var metrics2 = collector.GetMetrics();
        metrics2.Metrics.Should().BeNull();
        metrics2.Distributions.Should().BeNull();

        metrics.Metrics.Should().BeEquivalentTo(new[]
        {
            new
            {
                Metric = PublicApiUsage.Tracer_Configure.ToStringFast(),
                Points = new[] { new { Value = 2 } },
                Type = TelemetryMetricType.Count,
                Tags = (string[])null,
            },
            new
            {
                Metric = PublicApiUsage.Tracer_Ctor.ToStringFast(),
                Points = new[] { new { Value = 1 } },
                Type = TelemetryMetricType.Count,
                Tags = (string[])null,
            },
            new
            {
                Metric = Count.IntegrationsError.GetName(),
                Points = new[] { new { Value = 1 } },
                Type = TelemetryMetricType.Count,
                Tags = new[] { MetricTags.IntegrationName_Aerospike.ToStringFast(), MetricTags.IntegrationName_Aerospike.ToStringFast(), MetricTags.IntegrationName_Aerospike.ToStringFast() },
            },
            new
            {
                Metric = Count.SpanCreated.GetName(),
                Points = new[] { new { Value = 1 } },
                Type = TelemetryMetricType.Count,
                Tags = new[] { MetricTags.IntegrationName_Aerospike.ToStringFast(), MetricTags.IntegrationName_Aerospike.ToStringFast() },
            },
            new
            {
                Metric = Count.SpanFinished.GetName(),
                Points = new[] { new { Value = 15 } },
                Type = TelemetryMetricType.Count,
                Tags = (string[])null,
            },
            new
            {
                Metric = Count.SpanDropped.GetName(),
                Points = new[] { new { Value = 23 } },
                Type = TelemetryMetricType.Count,
                Tags = new[] { MetricTags.DropReason_SingleSpanSampling.ToStringFast() },
            },
            new
            {
                Metric = Gauge.StatsBuckets.GetName(),
                Points = new[] { new { Value = 234 } },
                Type = TelemetryMetricType.Gauge,
                Tags = (string[])null,
            },
        });

        metrics.Distributions.Should().BeEquivalentTo(new[]
        {
            new
            {
                Metric = Distribution.InitTime.GetName(),
                Tags = new[] { MetricTags.Total.ToStringFast() },
                Points = new[] { 23, 46 },
            },
            new
            {
                Metric = Distribution.InitTime.GetName(),
                Tags = new[] { MetricTags.Component_Managed.ToStringFast() },
                Points = new[] {  52 },
            },
        });
    }
}
