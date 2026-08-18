// <copyright file="SecurityReporterWafTelemetryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.AppSec.Coordinator;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using FluentAssertions;
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
    public async Task GivenAFailedWafRun_WhenTheTelemetryIsRecorded_ThenBothWafErrorAndWafRequestsAreReported(int returnCode, string expectedErrorTag)
    {
        var metrics = await RecordAsync(new MockResult(returnCode), isRasp: false);

        metrics.Should().ContainSingle(m => m.Name == "waf.error")
               .Which.Tags.Should().Contain(expectedErrorTag);
        metrics.Should().ContainSingle(m => m.Name == "waf.requests")
               .Which.Tags.Should().Contain("waf_error:true").And.Contain("input_truncated:false");
    }

    [Theory]
    [InlineData(-3)]
    [InlineData(-2)]
    [InlineData(-1)]
    public async Task GivenAFailedRaspRun_WhenTheTelemetryIsRecorded_ThenNeitherWafErrorNorTheWafRequestsTagIsSet(int returnCode)
    {
        var metrics = await RecordAsync(new MockResult(returnCode), isRasp: true);

        metrics.Should().NotContain(m => m.Name == "waf.error");
        metrics.Should().ContainSingle(m => m.Name == "waf.requests")
               .Which.Tags.Should().Contain("waf_error:false");
    }

    [Fact]
    public async Task GivenAFailedTruncatedWafRun_WhenTheTelemetryIsRecorded_ThenTheTruncatedTagIsUsed()
    {
        var metrics = await RecordAsync(new MockResult(-3, truncated: true), isRasp: false);

        metrics.Should().ContainSingle(m => m.Name == "waf.requests")
               .Which.Tags.Should().Contain("waf_error:true").And.Contain("input_truncated:true");
    }

    [Fact]
    public async Task GivenATimedOutWafRun_WhenTheTelemetryIsRecorded_ThenItIsNotReportedAsAnError()
    {
        var metrics = await RecordAsync(new MockResult(0, timeout: true), isRasp: false);

        metrics.Should().NotContain(m => m.Name == "waf.error");
        metrics.Should().ContainSingle(m => m.Name == "waf.requests")
               .Which.Tags.Should().Contain("waf_timeout:true").And.Contain("waf_error:false");
    }

    [Fact]
    public async Task GivenASuccessfulWafRun_WhenTheTelemetryIsRecorded_ThenNoErrorIsReported()
    {
        var metrics = await RecordAsync(new MockResult(0), isRasp: false);

        metrics.Should().NotContain(m => m.Name == "waf.error");
        metrics.Should().ContainSingle(m => m.Name == "waf.requests")
               .Which.Tags.Should().Contain("waf_error:false");
    }

    [Fact]
    public async Task GivenNoResult_WhenTheTelemetryIsRecorded_ThenNothingIsReported()
    {
        var metrics = await RecordAsync(result: null, isRasp: false);

        metrics.Should().BeEmpty();
    }

    private static async Task<List<(string Name, string[] Tags)>> RecordAsync(IResult? result, bool isRasp)
    {
        var collector = new MetricsTelemetryCollector(Timeout.InfiniteTimeSpan);
        SecurityReporter.RecordWafTelemetry(result, isRasp, collector);
        await collector.DisposeAsync();

        return collector.GetMetrics().Metrics?
                        .Select(m => (m.Metric, m.Tags ?? []))
                        .ToList()
            ?? [];
    }

    private class MockResult : IResult
    {
        public MockResult(int returnCode, bool timeout = false, bool truncated = false)
        {
            ReturnCode = (WafReturnCode)returnCode;
            Timeout = timeout;
            Truncated = truncated;
        }

        public WafReturnCode ReturnCode { get; }

        public bool Timeout { get; }

        public bool Truncated { get; }

        public bool ShouldBlock => false;

        public bool ShouldReportSecurityResult => false;

        public Dictionary<string, object?>? BlockInfo => throw new System.NotImplementedException();

        public Dictionary<string, object?>? RedirectInfo => throw new System.NotImplementedException();

        public Dictionary<string, object?>? SendStackInfo => throw new System.NotImplementedException();

        public IReadOnlyCollection<object>? Data => throw new System.NotImplementedException();

        public Dictionary<string, object?>? Actions => throw new System.NotImplementedException();

        public ulong AggregatedTotalRuntime => throw new System.NotImplementedException();

        public ulong AggregatedTotalRuntimeWithBindings => throw new System.NotImplementedException();

        public ulong AggregatedTotalRuntimeRasp => throw new System.NotImplementedException();

        public ulong AggregatedTotalRuntimeWithBindingsRasp => throw new System.NotImplementedException();

        public uint RaspRuleEvaluations => throw new System.NotImplementedException();

        public Dictionary<string, object?>? ExtractSchemaDerivatives => throw new System.NotImplementedException();

        public Dictionary<string, object?>? FingerprintDerivatives => throw new System.NotImplementedException();

        public Dictionary<string, object?>? WafSpanAttributes => throw new System.NotImplementedException();

        public bool Keep => throw new System.NotImplementedException();

        public bool HasKeep => throw new System.NotImplementedException();
    }
}
