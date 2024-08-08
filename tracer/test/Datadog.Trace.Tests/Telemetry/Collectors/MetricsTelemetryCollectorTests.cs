// <copyright file="MetricsTelemetryCollectorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;
using Xunit.Abstractions;
using NS = Datadog.Trace.Telemetry.MetricNamespaceConstants;

namespace Datadog.Trace.Tests.Telemetry.Collectors;

public class MetricsTelemetryCollectorTests
{
    [Fact]
    public async Task AggregatingMultipleTimes_GivesNoStats()
    {
        var collector = new MetricsTelemetryCollector(Timeout.InfiniteTimeSpan);
        collector.AggregateMetrics();
        collector.AggregateMetrics();
        collector.AggregateMetrics();
        var metrics = collector.GetMetrics();
        metrics.Metrics.Should().BeNull();
        metrics.Distributions.Should().BeNull();
        await collector.DisposeAsync();
    }

    [Fact]
    public async Task WithoutAggregation_HasNoStats()
    {
        var collector = new MetricsTelemetryCollector(Timeout.InfiniteTimeSpan);
        collector.Record(PublicApiUsage.Tracer_Configure);
        await Task.Delay(TimeSpan.FromSeconds(1));
        // Shouldn't have any stats, as no aggregation
        var metrics = collector.GetMetrics();
        metrics.Metrics.Should().BeNull();
        metrics.Distributions.Should().BeNull();
        await collector.DisposeAsync();
    }

    [Fact]
    public async Task AggregatesOnShutdown()
    {
        var collector = new MetricsTelemetryCollector(Timeout.InfiniteTimeSpan);
        collector.Record(PublicApiUsage.Tracer_Configure);
        collector.RecordDistributionSharedInitTime(MetricTags.InitializationComponent.Managed, 22);

        await collector.DisposeAsync();
        var metrics = collector.GetMetrics();

        metrics.Metrics.Should().BeEquivalentTo(new[]
        {
            new
            {
                Metric = "public_api",
                Points = new[] { new { Value = 1 } },
                Type = TelemetryMetricType.Count,
                Tags = new[] { PublicApiUsage.Tracer_Configure.ToStringFast() },
                Common = false,
                Namespace = (string)null,
            },
        });

        metrics.Distributions.Should().BeEquivalentTo(new[]
        {
            new
            {
                Metric = DistributionShared.InitTime.GetName(),
                Tags = new[] { "component:managed" },
                Points = new[] {  22 },
                Common = true,
                Namespace = NS.General,
            },
        });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("1.2.3")]
    public async Task AllMetricsAreReturned_ForMetricsTelemetryCollector(string wafVersion)
    {
        static void IncrementOpenTelemetryConfigMetrics(MetricsTelemetryCollector collector, string openTelemetryKey)
        {
            OpenTelemetryHelpers.GetConfigurationMetricTags(openTelemetryKey, out var openTelemetryConfig, out var datadogConfig);
            collector.RecordCountOpenTelemetryConfigHiddenByDatadogConfig(datadogConfig, openTelemetryConfig);
            collector.RecordCountOpenTelemetryConfigInvalid(datadogConfig, openTelemetryConfig);
        }

        var collector = new MetricsTelemetryCollector(Timeout.InfiniteTimeSpan);
        collector.Record(PublicApiUsage.Tracer_Configure);
        collector.Record(PublicApiUsage.Tracer_Configure);
        collector.Record(PublicApiUsage.Tracer_Ctor);
        collector.RecordCountSpanFinished(15);
        collector.RecordCountSharedIntegrationsError(MetricTags.IntegrationName.Aerospike, MetricTags.InstrumentationError.Invoker);
        collector.RecordCountSpanCreated(MetricTags.IntegrationName.Aerospike);
        collector.RecordCountSpanDropped(MetricTags.DropReason.P0Drop, 23);
        collector.RecordCountLogCreated(MetricTags.LogLevel.Debug, 3);
        collector.RecordCountWafInit(4);
        collector.RecordCountWafRequests(MetricTags.WafAnalysis.Normal, 5);
        collector.RecordCountRaspRuleEval(MetricTags.RaspRuleType.Lfi, 5);
        collector.RecordCountRaspRuleMatch(MetricTags.RaspRuleType.Lfi, 3);
        collector.RecordCountRaspTimeout(MetricTags.RaspRuleType.Lfi, 2);
        collector.RecordGaugeStatsBuckets(234);
        collector.RecordDistributionSharedInitTime(MetricTags.InitializationComponent.Total, 23);
        collector.RecordDistributionSharedInitTime(MetricTags.InitializationComponent.Total, 46);
        collector.RecordDistributionSharedInitTime(MetricTags.InitializationComponent.Managed, 52);

        // Record OpenTelemetry => Datadog configuration error metrics
        IncrementOpenTelemetryConfigMetrics(collector, "OTEL_SERVICE_NAME");
        IncrementOpenTelemetryConfigMetrics(collector, "OTEL_LOG_LEVEL");
        IncrementOpenTelemetryConfigMetrics(collector, "OTEL_PROPAGATORS");
        IncrementOpenTelemetryConfigMetrics(collector, "OTEL_TRACES_SAMPLER");
        IncrementOpenTelemetryConfigMetrics(collector, "OTEL_TRACES_SAMPLER_ARG");
        IncrementOpenTelemetryConfigMetrics(collector, "OTEL_TRACES_EXPORTER");
        IncrementOpenTelemetryConfigMetrics(collector, "OTEL_METRICS_EXPORTER");
        IncrementOpenTelemetryConfigMetrics(collector, "OTEL_RESOURCE_ATTRIBUTES");
        IncrementOpenTelemetryConfigMetrics(collector, "OTEL_SDK_DISABLED");

        // These aren't applicable in non-ci visibility
        collector.RecordCountCIVisibilityITRSkipped(MetricTags.CIVisibilityTestingEventType.Test, 123);
        collector.RecordCountCIVisibilityEventCreated(MetricTags.CIVisibilityTestFramework.XUnit, MetricTags.CIVisibilityTestingEventTypeWithCodeOwnerAndSupportedCiAndBenchmark.Test);
        collector.RecordCountCIVisibilityEventFinished(MetricTags.CIVisibilityTestFramework.XUnit, MetricTags.CIVisibilityTestingEventTypeWithCodeOwnerAndSupportedCiAndBenchmarkAndEarlyFlakeDetectionAndRum.Test);

        collector.AggregateMetrics();

        collector.Record(PublicApiUsage.Tracer_Ctor);
        collector.Record(PublicApiUsage.Tracer_Ctor);
        collector.Record(PublicApiUsage.TracerSettings_Build);
        collector.RecordCountSpanFinished(3);
        collector.RecordCountTraceSegmentCreated(MetricTags.TraceContinuation.New, 2);
        collector.RecordGaugeStatsBuckets(15);
        collector.RecordGaugeDirectLogQueue(7);
        collector.RecordDistributionSharedInitTime(MetricTags.InitializationComponent.Managed, 22);
        collector.RecordDistributionSharedInitTime(MetricTags.InitializationComponent.Rcm, 15);

        // These aren't applicable in non-ci visibility
        collector.RecordDistributionCIVisibilityGitCommandMs(MetricTags.CIVisibilityCommands.PackObjects, 125);

        collector.AggregateMetrics();

        var expectedWafTag = "waf_version:unknown";

        if (wafVersion is not null)
        {
            collector.SetWafVersion(wafVersion);
            expectedWafTag = $"waf_version:{wafVersion}";
        }

        using var scope = new AssertionScope();
        scope.FormattingOptions.MaxLines = 1000;

        var metrics = collector.GetMetrics();

        var metrics2 = collector.GetMetrics();
        metrics2.Metrics.Should().BeNull();
        metrics2.Distributions.Should().BeNull();

        metrics.Metrics.Should().BeEquivalentTo(new[]
        {
            new
            {
                Metric = "public_api",
                Points = new[] { new { Value = 2 } },
                Type = TelemetryMetricType.Count,
                Tags = new[] { PublicApiUsage.Tracer_Configure.ToStringFast() },
                Common = false,
                Namespace = (string)null,
            },
            new
            {
                Metric = "public_api",
                Points = new[] { new { Value = 1 }, new { Value = 2 } },
                Type = TelemetryMetricType.Count,
                Tags = new[] { PublicApiUsage.Tracer_Ctor.ToStringFast() },
                Common = false,
                Namespace = (string)null,
            },
            new
            {
                Metric = "public_api",
                Points = new[] { new { Value = 1 } },
                Type = TelemetryMetricType.Count,
                Tags = new[] { PublicApiUsage.TracerSettings_Build.ToStringFast() },
                Common = false,
                Namespace = (string)null,
            },
            new
            {
                Metric = CountShared.IntegrationsError.GetName(),
                Points = new[] { new { Value = 1 } },
                Type = TelemetryMetricType.Count,
                Tags = new[] { "integration_name:aerospike", "error_type:invoker" },
                Common = true,
                Namespace = (string)null,
            },
            new
            {
                Metric = Count.SpanCreated.GetName(),
                Points = new[] { new { Value = 1 } },
                Type = TelemetryMetricType.Count,
                Tags = new[] { "integration_name:aerospike" },
                Common = true,
                Namespace = (string)null,
            },
            new
            {
                Metric = Count.SpanFinished.GetName(),
                Points = new[] { new { Value = 15 }, new { Value = 3 } },
                Type = TelemetryMetricType.Count,
                Tags = (string[])null,
                Common = true,
                Namespace = (string)null,
            },
            new
            {
                Metric = Count.SpanDropped.GetName(),
                Points = new[] { new { Value = 23 } },
                Type = TelemetryMetricType.Count,
                Tags = new[] { "reason:p0_drop" },
                Common = true,
                Namespace = (string)null,
            },
            new
            {
                Metric = Count.LogCreated.GetName(),
                Points = new[] { new { Value = 3 } },
                Type = TelemetryMetricType.Count,
                Tags = new[] { "level:debug" },
                Common = true,
                Namespace = NS.General,
            },
            new
            {
                Metric = Count.WafInit.GetName(),
                Points = new[] { new { Value = 4 } },
                Type = TelemetryMetricType.Count,
                Tags = new[] { expectedWafTag },
                Common = true,
                Namespace = NS.ASM,
            },
            new
            {
                Metric = Count.WafRequests.GetName(),
                Points = new[] { new { Value = 5 } },
                Type = TelemetryMetricType.Count,
                Tags = new[] { expectedWafTag, "rule_triggered:false", "request_blocked:false", "waf_timeout:false", "request_excluded:false" },
                Common = true,
                Namespace = NS.ASM,
            },
            new
            {
                Metric = Count.RaspRuleEval.GetName(),
                Points = new[] { new { Value = 5 } },
                Type = TelemetryMetricType.Count,
                Tags = new[] { expectedWafTag, "rule_type:lfi" },
                Common = true,
                Namespace = NS.ASM,
            },
            new
            {
                Metric = Count.RaspRuleMatch.GetName(),
                Points = new[] { new { Value = 3 } },
                Type = TelemetryMetricType.Count,
                Tags = new[] { expectedWafTag, "rule_type:lfi" },
                Common = true,
                Namespace = NS.ASM,
            },
            new
            {
                Metric = Count.RaspTimeout.GetName(),
                Points = new[] { new { Value = 2 } },
                Type = TelemetryMetricType.Count,
                Tags = new[] { expectedWafTag, "rule_type:lfi" },
                Common = true,
                Namespace = NS.ASM,
            },
            new
            {
                Metric = Count.TraceSegmentCreated.GetName(),
                Points = new[] { new { Value = 2 } },
                Type = TelemetryMetricType.Count,
                Tags = new[] { "new_continued:new" },
                Common = true,
                Namespace = (string)null,
            },
            new
            {
                Metric = Gauge.StatsBuckets.GetName(),
                Points = new[] { new { Value = 234 }, new { Value = 15 } },
                Type = TelemetryMetricType.Gauge,
                Tags = (string[])null,
                Common = true,
                Namespace = (string)null,
            },
            new
            {
                Metric = Gauge.DirectLogQueue.GetName(),
                Points = new[] { new { Value = 7 } },
                Type = TelemetryMetricType.Gauge,
                Tags = (string[])null,
                Common = false,
                Namespace = (string)null,
            },
            new
            {
                Metric = Count.OpenTelemetryConfigHiddenByDatadogConfig.GetName(),
                Points = new[] { new { Value = 1 } },
                Type = TelemetryMetricType.Count,
                Tags = new[] { "config_opentelemetry:otel_service_name", "config_datadog:dd_service" },
                Common = true,
                Namespace = "tracers",
            },
            new
            {
                Metric = Count.OpenTelemetryConfigInvalid.GetName(),
                Points = new[] { new { Value = 1 } },
                Type = TelemetryMetricType.Count,
                Tags = new[] { "config_opentelemetry:otel_service_name", "config_datadog:dd_service" },
                Common = true,
                Namespace = "tracers",
            },
            new
            {
                Metric = Count.OpenTelemetryConfigHiddenByDatadogConfig.GetName(),
                Points = new[] { new { Value = 1 } },
                Type = TelemetryMetricType.Count,
                Tags = new[] { "config_opentelemetry:otel_log_level", "config_datadog:dd_trace_debug" },
                Common = true,
                Namespace = "tracers",
            },
            new
            {
                Metric = Count.OpenTelemetryConfigInvalid.GetName(),
                Points = new[] { new { Value = 1 } },
                Type = TelemetryMetricType.Count,
                Tags = new[] { "config_opentelemetry:otel_log_level", "config_datadog:dd_trace_debug" },
                Common = true,
                Namespace = "tracers",
            },
            new
            {
                Metric = Count.OpenTelemetryConfigHiddenByDatadogConfig.GetName(),
                Points = new[] { new { Value = 1 } },
                Type = TelemetryMetricType.Count,
                Tags = new[] { "config_opentelemetry:otel_propagators", "config_datadog:dd_trace_propagation_style" },
                Common = true,
                Namespace = "tracers",
            },
            new
            {
                Metric = Count.OpenTelemetryConfigInvalid.GetName(),
                Points = new[] { new { Value = 1 } },
                Type = TelemetryMetricType.Count,
                Tags = new[] { "config_opentelemetry:otel_propagators", "config_datadog:dd_trace_propagation_style" },
                Common = true,
                Namespace = "tracers",
            },
            new
            {
                Metric = Count.OpenTelemetryConfigHiddenByDatadogConfig.GetName(),
                Points = new[] { new { Value = 1 } },
                Type = TelemetryMetricType.Count,
                Tags = new[] { "config_opentelemetry:otel_traces_sampler", "config_datadog:dd_trace_sample_rate" },
                Common = true,
                Namespace = "tracers",
            },
            new
            {
                Metric = Count.OpenTelemetryConfigInvalid.GetName(),
                Points = new[] { new { Value = 1 } },
                Type = TelemetryMetricType.Count,
                Tags = new[] { "config_opentelemetry:otel_traces_sampler", "config_datadog:dd_trace_sample_rate" },
                Common = true,
                Namespace = "tracers",
            },
            new
            {
                Metric = Count.OpenTelemetryConfigHiddenByDatadogConfig.GetName(),
                Points = new[] { new { Value = 1 } },
                Type = TelemetryMetricType.Count,
                Tags = new[] { "config_opentelemetry:otel_traces_sampler_arg", "config_datadog:dd_trace_sample_rate" },
                Common = true,
                Namespace = "tracers",
            },
            new
            {
                Metric = Count.OpenTelemetryConfigInvalid.GetName(),
                Points = new[] { new { Value = 1 } },
                Type = TelemetryMetricType.Count,
                Tags = new[] { "config_opentelemetry:otel_traces_sampler_arg", "config_datadog:dd_trace_sample_rate" },
                Common = true,
                Namespace = "tracers",
            },
            new
            {
                Metric = Count.OpenTelemetryConfigHiddenByDatadogConfig.GetName(),
                Points = new[] { new { Value = 1 } },
                Type = TelemetryMetricType.Count,
                Tags = new[] { "config_opentelemetry:otel_traces_exporter", "config_datadog:dd_trace_enabled" },
                Common = true,
                Namespace = "tracers",
            },
            new
            {
                Metric = Count.OpenTelemetryConfigInvalid.GetName(),
                Points = new[] { new { Value = 1 } },
                Type = TelemetryMetricType.Count,
                Tags = new[] { "config_opentelemetry:otel_traces_exporter", "config_datadog:dd_trace_enabled" },
                Common = true,
                Namespace = "tracers",
            },
            new
            {
                Metric = Count.OpenTelemetryConfigHiddenByDatadogConfig.GetName(),
                Points = new[] { new { Value = 1 } },
                Type = TelemetryMetricType.Count,
                Tags = new[] { "config_opentelemetry:otel_metrics_exporter", "config_datadog:dd_runtime_metrics_enabled" },
                Common = true,
                Namespace = "tracers",
            },
            new
            {
                Metric = Count.OpenTelemetryConfigInvalid.GetName(),
                Points = new[] { new { Value = 1 } },
                Type = TelemetryMetricType.Count,
                Tags = new[] { "config_opentelemetry:otel_metrics_exporter", "config_datadog:dd_runtime_metrics_enabled" },
                Common = true,
                Namespace = "tracers",
            },
            new
            {
                Metric = Count.OpenTelemetryConfigHiddenByDatadogConfig.GetName(),
                Points = new[] { new { Value = 1 } },
                Type = TelemetryMetricType.Count,
                Tags = new[] { "config_opentelemetry:otel_resource_attributes", "config_datadog:dd_tags" },
                Common = true,
                Namespace = "tracers",
            },
            new
            {
                Metric = Count.OpenTelemetryConfigInvalid.GetName(),
                Points = new[] { new { Value = 1 } },
                Type = TelemetryMetricType.Count,
                Tags = new[] { "config_opentelemetry:otel_resource_attributes", "config_datadog:dd_tags" },
                Common = true,
                Namespace = "tracers",
            },
            new
            {
                Metric = Count.OpenTelemetryConfigHiddenByDatadogConfig.GetName(),
                Points = new[] { new { Value = 1 } },
                Type = TelemetryMetricType.Count,
                Tags = new[] { "config_opentelemetry:otel_sdk_disabled", "config_datadog:dd_trace_otel_enabled" },
                Common = true,
                Namespace = "tracers",
            },
            new
            {
                Metric = Count.OpenTelemetryConfigInvalid.GetName(),
                Points = new[] { new { Value = 1 } },
                Type = TelemetryMetricType.Count,
                Tags = new[] { "config_opentelemetry:otel_sdk_disabled", "config_datadog:dd_trace_otel_enabled" },
                Common = true,
                Namespace = "tracers",
            },
        });

        metrics.Distributions.Should().BeEquivalentTo(new[]
        {
            new
            {
                Metric = DistributionShared.InitTime.GetName(),
                Tags = new[] { "component:total" },
                Points = new[] { 23, 46 },
                Common = true,
                Namespace = NS.General,
            },
            new
            {
                Metric = DistributionShared.InitTime.GetName(),
                Tags = new[] { "component:managed" },
                Points = new[] {  52, 22 },
                Common = true,
                Namespace = NS.General,
            },
            new
            {
                Metric = DistributionShared.InitTime.GetName(),
                Tags = new[] { "component:rcm" },
                Points = new[] {  15 },
                Common = true,
                Namespace = NS.General,
            },
        });
        await collector.DisposeAsync();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("1.2.3")]
    public async Task AllMetricsAreReturned_ForCiVisibilityCollector(string wafVersion)
    {
        var collector = new CiVisibilityMetricsTelemetryCollector(Timeout.InfiniteTimeSpan);
        collector.Record(PublicApiUsage.Tracer_Configure);
        collector.Record(PublicApiUsage.Tracer_Configure);
        collector.Record(PublicApiUsage.Tracer_Ctor);
        collector.RecordCountSharedIntegrationsError(MetricTags.IntegrationName.Aerospike, MetricTags.InstrumentationError.Invoker);

        // These aren't recorded for ci visibility
        collector.RecordCountSpanFinished(15);
        collector.RecordCountSpanCreated(MetricTags.IntegrationName.Aerospike);
        collector.RecordCountSpanDropped(MetricTags.DropReason.P0Drop, 23);
        collector.RecordCountLogCreated(MetricTags.LogLevel.Debug, 3);
        collector.RecordCountWafInit(4);
        collector.RecordCountWafRequests(MetricTags.WafAnalysis.Normal, 5);
        collector.RecordGaugeStatsBuckets(234);
        collector.RecordDistributionSharedInitTime(MetricTags.InitializationComponent.Total, 23);
        collector.RecordDistributionSharedInitTime(MetricTags.InitializationComponent.Total, 46);
        collector.RecordDistributionSharedInitTime(MetricTags.InitializationComponent.Managed, 52);

        // these ones are
        collector.RecordCountCIVisibilityITRSkipped(MetricTags.CIVisibilityTestingEventType.Test, 123);
        collector.RecordCountCIVisibilityEventCreated(MetricTags.CIVisibilityTestFramework.XUnit, MetricTags.CIVisibilityTestingEventTypeWithCodeOwnerAndSupportedCiAndBenchmark.Test);
        collector.RecordCountCIVisibilityEventFinished(MetricTags.CIVisibilityTestFramework.XUnit, MetricTags.CIVisibilityTestingEventTypeWithCodeOwnerAndSupportedCiAndBenchmarkAndEarlyFlakeDetectionAndRum.Test);

        collector.AggregateMetrics();

        collector.Record(PublicApiUsage.Tracer_Ctor);
        collector.Record(PublicApiUsage.Tracer_Ctor);
        collector.Record(PublicApiUsage.TracerSettings_Build);

        // These aren't recorded for ci visibility
        collector.RecordCountSpanFinished(3);
        collector.RecordCountTraceSegmentCreated(MetricTags.TraceContinuation.New, 2);
        collector.RecordGaugeStatsBuckets(15);
        collector.RecordGaugeDirectLogQueue(7);

        // these ones are
        collector.RecordDistributionSharedInitTime(MetricTags.InitializationComponent.Managed, 22);
        collector.RecordDistributionSharedInitTime(MetricTags.InitializationComponent.Rcm, 15);
        collector.RecordCountCIVisibilityITRSkipped(MetricTags.CIVisibilityTestingEventType.Test, 3);
        collector.RecordDistributionCIVisibilityGitCommandMs(MetricTags.CIVisibilityCommands.PackObjects, 125);

        collector.AggregateMetrics();

        var expectedWafTag = "waf_version:unknown";

        if (wafVersion is not null)
        {
            collector.SetWafVersion(wafVersion);
            expectedWafTag = $"waf_version:{wafVersion}";
        }

        using var scope = new AssertionScope();
        scope.FormattingOptions.MaxLines = 1000;

        var metrics = collector.GetMetrics();

        var metrics2 = collector.GetMetrics();
        metrics2.Metrics.Should().BeNull();
        metrics2.Distributions.Should().BeNull();

        metrics.Metrics.Should().BeEquivalentTo(new[]
        {
            new
            {
                Metric = "public_api",
                Points = new[] { new { Value = 2 } },
                Type = TelemetryMetricType.Count,
                Tags = new[] { PublicApiUsage.Tracer_Configure.ToStringFast() },
                Common = false,
                Namespace = (string)null,
            },
            new
            {
                Metric = "public_api",
                Points = new[] { new { Value = 1 }, new { Value = 2 } },
                Type = TelemetryMetricType.Count,
                Tags = new[] { PublicApiUsage.Tracer_Ctor.ToStringFast() },
                Common = false,
                Namespace = (string)null,
            },
            new
            {
                Metric = "public_api",
                Points = new[] { new { Value = 1 } },
                Type = TelemetryMetricType.Count,
                Tags = new[] { PublicApiUsage.TracerSettings_Build.ToStringFast() },
                Common = false,
                Namespace = (string)null,
            },
            new
            {
                Metric = CountShared.IntegrationsError.GetName(),
                Points = new[] { new { Value = 1 } },
                Type = TelemetryMetricType.Count,
                Tags = new[] { "integration_name:aerospike", "error_type:invoker" },
                Common = true,
                Namespace = (string)null,
            },
            new
            {
                Metric = CountCIVisibility.ITRSkipped.GetName(),
                Points = new[] { new { Value = 123 }, new { Value = 3 } },
                Type = TelemetryMetricType.Count,
                Tags = new[] { "event_type:test" },
                Common = true,
                Namespace = NS.CIVisibility,
            },
            new
            {
                Metric = CountCIVisibility.EventCreated.GetName(),
                Points = new[] { new { Value = 1 } },
                Type = TelemetryMetricType.Count,
                Tags = new[] { "test_framework:xunit", "event_type:test" },
                Common = true,
                Namespace = NS.CIVisibility,
            },
            new
            {
                Metric = CountCIVisibility.EventFinished.GetName(),
                Points = new[] { new { Value = 1 } },
                Type = TelemetryMetricType.Count,
                Tags = new[] { "test_framework:xunit", "event_type:test" },
                Common = true,
                Namespace = NS.CIVisibility,
            },
        });

        metrics.Distributions.Should().BeEquivalentTo(new[]
        {
            new
            {
                Metric = DistributionShared.InitTime.GetName(),
                Tags = new[] { "component:total" },
                Points = new[] { 23, 46 },
                Common = true,
                Namespace = NS.General,
            },
            new
            {
                Metric = DistributionShared.InitTime.GetName(),
                Tags = new[] { "component:managed" },
                Points = new[] {  52, 22 },
                Common = true,
                Namespace = NS.General,
            },
            new
            {
                Metric = DistributionShared.InitTime.GetName(),
                Tags = new[] { "component:rcm" },
                Points = new[] {  15 },
                Common = true,
                Namespace = NS.General,
            },
            new
            {
                Metric = DistributionCIVisibility.GitCommandMs.GetName(),
                Tags = new[] { "command:pack_objects" },
                Points = new[] {  125 },
                Common = true,
                Namespace = NS.CIVisibility,
            },
        });
        await collector.DisposeAsync();
    }

    [Fact]
    public async Task ShouldAggregateMetricsAutomatically()
    {
        var aggregationPeriod = TimeSpan.FromMilliseconds(500);
        var mutex = new ManualResetEventSlim();

        var collector = new MetricsTelemetryCollector(
            aggregationPeriod,
            () =>
            {
                if (!mutex.IsSet)
                {
                    mutex.Set();
                }
            });

        // theoretically ~4 aggregations in this time period
        var count = 0;
        while (count < 20)
        {
            collector.RecordCountSpanFinished(1);
            await Task.Delay(100);
            count++;
        }

        mutex.Wait(TimeSpan.FromSeconds(60)).Should().BeTrue();
        var metrics = collector.GetMetrics();
        metrics.Metrics.Should()
               .ContainSingle(x => x.Metric == Count.SpanFinished.GetName())
               .Which.Points.Should()
               .NotBeEmpty(); // we expect ~10 points, but don't assert that number to avoid flakiness
        await collector.DisposeAsync();
    }
}
