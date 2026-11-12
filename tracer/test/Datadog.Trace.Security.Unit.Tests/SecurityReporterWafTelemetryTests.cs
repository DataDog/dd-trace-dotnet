// <copyright file="SecurityReporterWafTelemetryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Coordinator;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Telemetry;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests;

public class SecurityReporterWafTelemetryTests
{
    public static IEnumerable<object[]> ErrorCodes
        => new List<object[]>
        {
            new object[] { -3, "waf_error:-3" },
            new object[] { -2, "waf_error:-2" },
            new object[] { -1, "waf_error:-1" },
        };

    [Theory]
    [MemberData(nameof(ErrorCodes))]
    public async Task GivenAFailedWafRun_WhenTheTelemetryIsRecorded_ThenWafErrorIsReportedForTheRunAndWafRequestsOnlyAtTheEndOfTheRequest(int returnCode, string expectedErrorTag)
    {
        var requestContext = new AppSecRequestContext();

        var runMetrics = await RecordAsync(CreateResult(returnCode), isRasp: false, requestContext);
        var requestMetrics = await WafRequestsTelemetryTests.CloseAsync(requestContext);

        runMetrics.Should().ContainSingle(m => m.Name == "waf.error")
                  .Which.Tags.Should().Contain(expectedErrorTag);
        runMetrics.Should().NotContain(m => m.Name == "waf.requests");
        requestMetrics.Should().ContainSingle(m => m.Name == "waf.requests")
                      .Which.Tags.Should().Contain("waf_error:true");
    }

    [Theory]
    [InlineData(-3)]
    [InlineData(0)]
    public async Task GivenARaspRun_WhenTheTelemetryIsRecorded_ThenNeitherWafErrorNorWafRequestsCountIt(int returnCode)
    {
        var requestContext = new AppSecRequestContext();

        var runMetrics = await RecordAsync(CreateResult(returnCode), isRasp: true, requestContext);
        var requestMetrics = await WafRequestsTelemetryTests.CloseAsync(requestContext);

        runMetrics.Should().BeEmpty();
        requestMetrics.Should().NotContain(m => m.Name == "waf.requests");
    }

    [Fact]
    public async Task GivenATimedOutWafRun_WhenTheTelemetryIsRecorded_ThenItIsNotReportedAsAnError()
    {
        var requestContext = new AppSecRequestContext();

        var runMetrics = await RecordAsync(CreateResult(-3, timeout: true), isRasp: false, requestContext);
        var requestMetrics = await WafRequestsTelemetryTests.CloseAsync(requestContext);

        runMetrics.Should().NotContain(m => m.Name == "waf.error");
        requestMetrics.Should().ContainSingle(m => m.Name == "waf.requests")
                      .Which.Tags.Should().Contain("waf_timeout:true").And.Contain("waf_error:false");
    }

    [Fact]
    public async Task GivenNoResult_WhenTheTelemetryIsRecorded_ThenNothingIsReported()
    {
        var requestContext = new AppSecRequestContext();

        var runMetrics = await RecordAsync(result: null, isRasp: false, requestContext);
        var requestMetrics = await WafRequestsTelemetryTests.CloseAsync(requestContext);

        runMetrics.Should().BeEmpty();
        requestMetrics.Should().NotContain(m => m.Name == "waf.requests");
    }

    internal static IResult CreateResult(int returnCode = 0, bool timeout = false, bool truncated = false, bool ruleTriggered = false, bool block = false)
    {
        var result = new Mock<IResult>();
        result.SetupGet(x => x.ReturnCode).Returns((WafReturnCode)returnCode);
        result.SetupGet(x => x.Timeout).Returns(timeout);
        result.SetupGet(x => x.Truncated).Returns(truncated);
        result.SetupGet(x => x.ShouldReportSecurityResult).Returns(ruleTriggered || block);
        result.SetupGet(x => x.ShouldBlock).Returns(block);
        return result.Object;
    }

    private static async Task<List<(string Name, string[] Tags)>> RecordAsync(IResult? result, bool isRasp, AppSecRequestContext requestContext)
    {
        var collector = new MetricsTelemetryCollector(Timeout.InfiniteTimeSpan);
        SecurityReporter.RecordWafTelemetry(result, isRasp, requestContext, collector);
        await collector.DisposeAsync();

        return collector.GetMetrics().Metrics?
                        .Select(m => (m.Metric, m.Tags ?? []))
                        .ToList()
            ?? [];
    }
}
