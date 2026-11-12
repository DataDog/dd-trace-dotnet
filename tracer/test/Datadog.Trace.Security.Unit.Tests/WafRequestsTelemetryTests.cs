// <copyright file="WafRequestsTelemetryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Coordinator;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using FluentAssertions;
using Xunit;

using static Datadog.Trace.Security.Unit.Tests.SecurityReporterWafTelemetryTests;

namespace Datadog.Trace.Security.Unit.Tests;

public class WafRequestsTelemetryTests
{
    [Fact]
    public async Task GivenSeveralWafRuns_WhenTheRequestEnds_ThenOneWafRequestsPointSumsThemUp()
    {
        var requestContext = new AppSecRequestContext();
        Record(requestContext, CreateResult());
        Record(requestContext, CreateResult(ruleTriggered: true));
        Record(requestContext, CreateResult(truncated: true));

        var tags = await CloseAndGetWafRequestsTagsAsync(requestContext);

        tags.Should().Contain(["rule_triggered:true", "request_blocked:false", "block_failure:false", "input_truncated:true", "waf_timeout:false", "waf_error:false", "rate_limited:false"]);
    }

    [Fact]
    public async Task GivenATimeoutAndAnErrorInDifferentRuns_WhenTheRequestEnds_ThenBothAreReported()
    {
        var requestContext = new AppSecRequestContext();
        Record(requestContext, CreateResult(timeout: true));
        Record(requestContext, CreateResult(-3));

        var tags = await CloseAndGetWafRequestsTagsAsync(requestContext);

        tags.Should().Contain(["waf_timeout:true", "waf_error:true"]);
    }

    [Fact]
    public async Task GivenNoWafRun_WhenTheRequestEnds_ThenNoWafRequestsPointIsReported()
    {
        var metrics = await CloseAsync(new AppSecRequestContext());

        metrics.Should().NotContain(m => m.Name == "waf.requests");
    }

    [Fact]
    public async Task GivenABlockThatWasApplied_WhenTheRequestEnds_ThenTheRequestIsReportedAsBlocked()
    {
        var requestContext = new AppSecRequestContext();
        Record(requestContext, CreateResult(block: true));

        var (metrics, span) = await CloseWithSpanAsync(requestContext);

        GetWafRequestsTags(metrics).Should().Contain(["rule_triggered:true", "request_blocked:true", "block_failure:false"]);
        span.GetMetric(Metrics.AppSecBlockFailed).Should().BeNull();
    }

    [Fact]
    public async Task GivenABlockThatFailed_WhenTheRequestEnds_ThenTheBlockFailureIsReported()
    {
        var requestContext = new AppSecRequestContext();
        Record(requestContext, CreateResult(block: true));
        requestContext.MarkBlockFailed();

        var (metrics, span) = await CloseWithSpanAsync(requestContext);

        GetWafRequestsTags(metrics).Should().Contain(["rule_triggered:true", "request_blocked:false", "block_failure:true"]);
        span.GetMetric(Metrics.AppSecBlockFailed).Should().Be(1);
    }

    [Fact]
    public async Task GivenARaspBlockThatFailed_WhenTheRequestEnds_ThenOnlyTheSpanReportsIt()
    {
        var requestContext = new AppSecRequestContext();
        Record(requestContext, CreateResult());
        requestContext.MarkBlockFailed();

        var (metrics, span) = await CloseWithSpanAsync(requestContext);

        // the tags of waf.requests only cover the non-RASP runs, which didn't ask for a block
        GetWafRequestsTags(metrics).Should().Contain(["rule_triggered:false", "request_blocked:false", "block_failure:false"]);
        span.GetMetric(Metrics.AppSecBlockFailed).Should().Be(1);
    }

    [Fact]
    public async Task GivenATraceTheRateLimiterRefused_WhenTheRequestEnds_ThenItIsReportedAsRateLimited()
    {
        var requestContext = new AppSecRequestContext();
        Record(requestContext, CreateResult(ruleTriggered: true));
        requestContext.MarkTraceRateLimited();

        var tags = await CloseAndGetWafRequestsTagsAsync(requestContext);

        tags.Should().Contain("rate_limited:true");
    }

    [Fact]
    public async Task GivenATraceTheRateLimiterAlreadyKept_WhenALaterCallIsRefused_ThenItIsNotReportedAsRateLimited()
    {
        var requestContext = new AppSecRequestContext();
        Record(requestContext, CreateResult(ruleTriggered: true));
        requestContext.MarkTraceKept();
        requestContext.MarkTraceRateLimited();

        var tags = await CloseAndGetWafRequestsTagsAsync(requestContext);

        tags.Should().Contain("rate_limited:false");
    }

    [Fact]
    public async Task GivenTheWebSpanIsClosedTwice_WhenTheRequestEnds_ThenOnlyOnePointIsReported()
    {
        var requestContext = new AppSecRequestContext();
        Record(requestContext, CreateResult());
        var span = CreateSpan();
        var collector = new MetricsTelemetryCollector(Timeout.InfiniteTimeSpan);

        requestContext.CloseWebSpan(span, collector);
        requestContext.CloseWebSpan(span, collector);

        var metrics = await GetMetricsAsync(collector);
        metrics.Should().ContainSingle(m => m.Name == "waf.requests").Which.Points.Should().Be(1);
    }

    [Fact]
    public void GivenTheWafAnalysisValues_WhenTheirTagsAreRead_ThenTheyFollowTheOutcomeAndFlagsLayout()
    {
        // AppSecRequestContext computes the value as (int)outcome * 16 + flags, so the order of the values matters
        var values = (MetricTags.WafAnalysis[])Enum.GetValues(typeof(MetricTags.WafAnalysis));
        values.Should().HaveCount(64);

        foreach (var value in values)
        {
            var index = (int)value;
            var outcome = index / 16;
            var tags = typeof(MetricTags.WafAnalysis).GetField(value.ToString())!.GetCustomAttribute<DescriptionAttribute>()!.Description.Split(';');

            tags.Should().Equal(
                "waf_version",
                "event_rules_version",
                $"rule_triggered:{Bool(outcome != 0)}",
                $"request_blocked:{Bool(outcome == 2)}",
                $"waf_timeout:{Bool((index & 1) != 0)}",
                $"block_failure:{Bool(outcome == 3)}",
                $"rate_limited:{Bool((index & 2) != 0)}",
                $"input_truncated:{Bool((index & 4) != 0)}",
                $"waf_error:{Bool((index & 8) != 0)}");
        }

        static string Bool(bool value) => value ? "true" : "false";
    }

    internal static async Task<List<(string Name, string[] Tags, int Points)>> CloseAsync(AppSecRequestContext requestContext)
        => (await CloseWithSpanAsync(requestContext)).Metrics;

    private static void Record(AppSecRequestContext requestContext, IResult result)
        => SecurityReporter.RecordWafTelemetry(result, isRasp: false, requestContext, new MetricsTelemetryCollector(Timeout.InfiniteTimeSpan));

    private static async Task<string[]> CloseAndGetWafRequestsTagsAsync(AppSecRequestContext requestContext)
        => GetWafRequestsTags(await CloseAsync(requestContext));

    private static string[] GetWafRequestsTags(List<(string Name, string[] Tags, int Points)> metrics)
        => metrics.Should().ContainSingle(m => m.Name == "waf.requests").Which.Tags;

    private static async Task<(List<(string Name, string[] Tags, int Points)> Metrics, Span Span)> CloseWithSpanAsync(AppSecRequestContext requestContext)
    {
        var span = CreateSpan();
        var collector = new MetricsTelemetryCollector(Timeout.InfiniteTimeSpan);
        requestContext.CloseWebSpan(span, collector);
        return (await GetMetricsAsync(collector), span);
    }

    private static Span CreateSpan() => new(new SpanContext(SpanContext.None, null, "Test"), DateTimeOffset.Now);

    private static async Task<List<(string Name, string[] Tags, int Points)>> GetMetricsAsync(MetricsTelemetryCollector collector)
    {
        await collector.DisposeAsync();
        return collector.GetMetrics().Metrics?
                        .Select(m => (m.Metric, m.Tags ?? [], m.Points.Sum(p => (int)p.Value)))
                        .ToList()
            ?? [];
    }
}
