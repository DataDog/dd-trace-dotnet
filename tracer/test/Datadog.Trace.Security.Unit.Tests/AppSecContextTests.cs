// <copyright file="AppSecContextTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Configuration;
using Datadog.Trace.Tagging;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests;

public class AppSecContextTests
{
    [InlineData(-2, -2, -1, 0, -2, -1)]
    [InlineData(2, -2, -1, 1, null, -1)]
    [InlineData(2, 2, 1, 2, null, null)]
    [Theory]
    public async Task GivenAQuery_WhenWAFError_ThenSpanHasErrorTags(int raspErrorCode, int wafErrorCode, int wafErrorCode2, int wafTimeouts, int? expectedRaspErrorCode, int? expectedWafErrorCode)
    {
        var settings = TracerSettings.Create(new Dictionary<string, object>());
        await using var tracer = TracerHelper.Create(settings);
        var rootTestScope = (Scope)tracer.StartActive("test.trace");

        var appSecContext = rootTestScope.Span.Context.TraceContext.AppSecRequestContext;
        int timeouts = wafTimeouts;

        appSecContext.CheckWAFError(new MockResult(raspErrorCode, timeouts-- > 0), true);
        appSecContext.CheckWAFError(new MockResult(wafErrorCode, timeouts-- > 0), false);
        appSecContext.CheckWAFError(new MockResult(wafErrorCode2, timeouts-- > 0), false);
        appSecContext.CloseWebSpan(rootTestScope.Span);
        rootTestScope.Span.GetMetric(Metrics.WafError).Should().Be(expectedWafErrorCode);
        rootTestScope.Span.GetMetric(Metrics.RaspWafError).Should().Be(expectedRaspErrorCode);

        if (wafTimeouts > 0)
        {
            rootTestScope.Span.GetMetric(Metrics.WafTimeouts).Should().Be(wafTimeouts);
        }
    }

    private class MockResult : IResult
    {
        public MockResult(int returnCode, bool timeout, bool truncated = false)
        {
            ReturnCode = (WafReturnCode)returnCode;
            Timeout = timeout;
            Truncated = truncated;
        }

        public WafReturnCode ReturnCode { get; }

        public bool Timeout { get; }

        public bool Truncated { get; }

        public bool ShouldBlock => throw new System.NotImplementedException();

        public Dictionary<string, object> BlockInfo => throw new System.NotImplementedException();

        public Dictionary<string, object> RedirectInfo => throw new System.NotImplementedException();

        public Dictionary<string, object> SendStackInfo => throw new System.NotImplementedException();

        public IReadOnlyCollection<object> Data => throw new System.NotImplementedException();

        public Dictionary<string, object> Actions => throw new System.NotImplementedException();

        public ulong AggregatedTotalRuntime => throw new System.NotImplementedException();

        public ulong AggregatedTotalRuntimeWithBindings => throw new System.NotImplementedException();

        public ulong AggregatedTotalRuntimeRasp => throw new System.NotImplementedException();

        public ulong AggregatedTotalRuntimeWithBindingsRasp => throw new System.NotImplementedException();

        public uint RaspRuleEvaluations => throw new System.NotImplementedException();

        public Dictionary<string, object> ExtractSchemaDerivatives => throw new System.NotImplementedException();

        public Dictionary<string, object> FingerprintDerivatives => throw new System.NotImplementedException();

        public bool ShouldReportSecurityResult => throw new System.NotImplementedException();
    }
}
