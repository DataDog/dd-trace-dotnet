// <copyright file="RaspModuleTelemetryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Rasp;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Telemetry;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests.RASP;

public class RaspModuleTelemetryTests
{
    public static IEnumerable<object[]> RaspAddresses
        => new List<object[]>
        {
            new object[] { AddressesConstants.FileAccess, new[] { "rule_type:lfi" } },
            new object[] { AddressesConstants.DownstreamUrl, new[] { "rule_type:ssrf" } },
            new object[] { AddressesConstants.DBStatement, new[] { "rule_type:sql_injection" } },
            new object[] { AddressesConstants.ShellInjection, new[] { "rule_type:command_injection", "rule_variant:shell" } },
            new object[] { AddressesConstants.CommandInjection, new[] { "rule_type:command_injection", "rule_variant:exec" } },
        };

    [Theory]
    [MemberData(nameof(RaspAddresses))]
    public async Task GivenASkippedRaspCall_WhenTheReasonIsAfterRequest_ThenTheRuleTypeAndReasonAreReported(string address, string[] expectedRuleTags)
    {
        var metrics = await RecordAsync(collector => RaspModule.RecordRaspSkipped(address, RaspModule.SkipReason.AfterRequest, collector));

        var tags = metrics.Should().ContainSingle(m => m.Name == "rasp.rule.skipped").Which.Tags;
        tags.Should().Contain("reason:after-request").And.Contain(expectedRuleTags);
    }

    [Theory]
    [MemberData(nameof(RaspAddresses))]
    public async Task GivenASkippedRaspCall_WhenTheReasonIsOutOfRequest_ThenTheRuleTypeAndReasonAreReported(string address, string[] expectedRuleTags)
    {
        var metrics = await RecordAsync(collector => RaspModule.RecordRaspSkipped(address, RaspModule.SkipReason.OutOfRequest, collector));

        var tags = metrics.Should().ContainSingle(m => m.Name == "rasp.rule.skipped").Which.Tags;
        tags.Should().Contain("reason:out-of-request").And.Contain(expectedRuleTags);
    }

    [Fact]
    public async Task GivenASkippedRaspCall_WhenTheMetricIsReported_ThenNoWafVersionTagsAreAttached()
    {
        // rasp.rule.skipped is specified without waf_version/event_rules_version, and the placeholder
        // substitution in the collector would leak "unknown" values if they were declared
        var metrics = await RecordAsync(collector => RaspModule.RecordRaspSkipped(AddressesConstants.FileAccess, RaspModule.SkipReason.OutOfRequest, collector));

        var tags = metrics.Should().ContainSingle(m => m.Name == "rasp.rule.skipped").Which.Tags;
        tags.Should().NotContain(t => t.StartsWith("waf_version")).And.NotContain(t => t.StartsWith("event_rules_version"));
    }

    [Fact]
    public async Task GivenASkippedRaspCall_WhenTheAddressIsUnknown_ThenNothingIsReported()
    {
        var metrics = await RecordAsync(collector => RaspModule.RecordRaspSkipped("server.request.body", RaspModule.SkipReason.OutOfRequest, collector));

        metrics.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(RaspAddresses))]
    public async Task GivenANullResult_WhenTheErrorIsRecorded_ThenABindingErrorIsReported(string address, string[] expectedRuleTags)
    {
        var metrics = await RecordAsync(collector => RaspModule.RecordRaspError(address, result: null, collector));

        var tags = metrics.Should().ContainSingle(m => m.Name == "rasp.error").Which.Tags;
        tags.Should().Contain("waf_error:-127").And.Contain(expectedRuleTags);
    }

    [Theory]
    [InlineData(-3, "waf_error:-3")]
    [InlineData(-2, "waf_error:-2")]
    [InlineData(-1, "waf_error:-1")]
    public async Task GivenAFailedRaspRun_WhenTheErrorIsRecorded_ThenTheReturnCodeIsReported(int returnCode, string expectedErrorTag)
    {
        var metrics = await RecordAsync(collector => RaspModule.RecordRaspError(AddressesConstants.DBStatement, CreateResult(returnCode), collector));

        var tags = metrics.Should().ContainSingle(m => m.Name == "rasp.error").Which.Tags;
        tags.Should().Contain(expectedErrorTag).And.Contain("rule_type:sql_injection");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task GivenASuccessfulRaspRun_WhenTheErrorIsRecorded_ThenNothingIsReported(int returnCode)
    {
        var metrics = await RecordAsync(collector => RaspModule.RecordRaspError(AddressesConstants.DBStatement, CreateResult(returnCode), collector));

        metrics.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-2)]
    [InlineData(-3)]
    public async Task GivenATimedOutRaspRun_WhenTheErrorIsRecorded_ThenItIsNotReportedAsAnError(int returnCode)
    {
        // the timeout is already reported through rasp.timeout, so it must win over the return code
        var metrics = await RecordAsync(collector => RaspModule.RecordRaspError(AddressesConstants.DBStatement, CreateResult(returnCode, timeout: true), collector));

        metrics.Should().BeEmpty();
    }

    [Fact]
    public async Task GivenAFailedRaspRun_WhenTheAddressIsUnknown_ThenNothingIsReported()
    {
        var metrics = await RecordAsync(collector => RaspModule.RecordRaspError("server.request.body", CreateResult(-3), collector));

        metrics.Should().BeEmpty();
    }

    [Fact]
    public async Task GivenANullResult_WhenTheContextWasAlreadyDisposed_ThenTheRunIsReportedAsSkipped()
    {
        // the request can end between the lifecycle check and the WAF call: nothing was evaluated,
        // so this is a skip rather than a binding error
        var rootSpan = CreateRootSpan(disposeAdditiveContext: true);

        var metrics = await RecordAsync(collector => RaspModule.RecordRaspRunOutcome(AddressesConstants.DBStatement, result: null, rootSpan, collector));

        var tags = metrics.Should().ContainSingle(m => m.Name == "rasp.rule.skipped").Which.Tags;
        tags.Should().Contain("reason:after-request").And.Contain("rule_type:sql_injection");
    }

    [Fact]
    public async Task GivenANullResult_WhenTheContextIsStillAlive_ThenTheRunIsReportedAsABindingError()
    {
        var rootSpan = CreateRootSpan(disposeAdditiveContext: false);

        var metrics = await RecordAsync(collector => RaspModule.RecordRaspRunOutcome(AddressesConstants.DBStatement, result: null, rootSpan, collector));

        var tags = metrics.Should().ContainSingle(m => m.Name == "rasp.error").Which.Tags;
        tags.Should().Contain("waf_error:-127").And.Contain("rule_type:sql_injection");
    }

    [Fact]
    public async Task GivenAFailedResult_WhenTheContextWasAlreadyDisposed_ThenTheErrorIsStillReported()
    {
        // the WAF did run and did return an error, so the disposal that happened afterwards is irrelevant
        var rootSpan = CreateRootSpan(disposeAdditiveContext: true);

        var metrics = await RecordAsync(collector => RaspModule.RecordRaspRunOutcome(AddressesConstants.DBStatement, CreateResult(-3), rootSpan, collector));

        var tags = metrics.Should().ContainSingle(m => m.Name == "rasp.error").Which.Tags;
        tags.Should().Contain("waf_error:-3").And.Contain("rule_type:sql_injection");
    }

    [Fact]
    public async Task GivenASuccessfulResult_WhenTheOutcomeIsRecorded_ThenNothingIsReported()
    {
        var rootSpan = CreateRootSpan(disposeAdditiveContext: false);

        var metrics = await RecordAsync(collector => RaspModule.RecordRaspRunOutcome(AddressesConstants.DBStatement, CreateResult(0), rootSpan, collector));

        metrics.Should().BeEmpty();
    }

    private static Span CreateRootSpan(bool disposeAdditiveContext)
    {
        var traceContext = new TraceContext(new EmptyDatadogTracer());

        if (disposeAdditiveContext)
        {
            traceContext.AppSecRequestContext.DisposeAdditiveContext();
        }

        var spanContext = new SpanContext(parent: null, traceContext, serviceName: "My Service Name", traceId: (TraceId)100, spanId: 200);
        return new Span(spanContext, DateTimeOffset.Now);
    }

    private static async Task<List<(string Name, string[] Tags)>> RecordAsync(Action<IMetricsTelemetryCollector> record)
    {
        var collector = new MetricsTelemetryCollector(Timeout.InfiniteTimeSpan);
        record(collector);
        await collector.DisposeAsync();

        return collector.GetMetrics().Metrics?
                        .Select(m => (m.Metric, m.Tags ?? []))
                        .ToList()
            ?? [];
    }

    private static IResult CreateResult(int returnCode, bool timeout = false)
    {
        var result = new Mock<IResult>();
        result.SetupGet(x => x.ReturnCode).Returns((WafReturnCode)returnCode);
        result.SetupGet(x => x.Timeout).Returns(timeout);
        return result.Object;
    }
}
