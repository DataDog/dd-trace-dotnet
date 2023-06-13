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
        collector.RecordCountSpanFinished(15);
        collector.RecordCountIntegrationsError(MetricTags.IntegrationName.Aerospike, MetricTags.InstrumentationError.Invoker);
        collector.RecordCountSpanCreated(MetricTags.IntegrationName.Aerospike);
        collector.RecordCountSpanDropped(MetricTags.DropReason.SingleSpanSampling, 23);
        collector.RecordGaugeStatsBuckets(234);
        collector.RecordDistributionInitTime(MetricTags.InitializationComponent.Total, 23);
        collector.RecordDistributionInitTime(MetricTags.InitializationComponent.Total, 46);
        collector.RecordDistributionInitTime(MetricTags.InitializationComponent.Managed, 52);

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
                Tags = new[] { "integrations_name:aerospike", "error_type:invoker" },
            },
            new
            {
                Metric = Count.SpanCreated.GetName(),
                Points = new[] { new { Value = 1 } },
                Type = TelemetryMetricType.Count,
                Tags = new[] { "integrations_name:aerospike" },
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
                Tags = new[] { "reason:single_span_sampling" },
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
                Tags = new[] { "component:total" },
                Points = new[] { 23, 46 },
            },
            new
            {
                Metric = Distribution.InitTime.GetName(),
                Tags = new[] { "component:managed" },
                Points = new[] {  52 },
            },
        });
    }
}
