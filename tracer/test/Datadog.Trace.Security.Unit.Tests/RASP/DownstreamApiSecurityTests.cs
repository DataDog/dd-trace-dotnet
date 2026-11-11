// <copyright file="DownstreamApiSecurityTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.AppSec.WafEncoding;
using Datadog.Trace.Security.Unit.Tests.Utils;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests.RASP;

public class DownstreamApiSecurityTests : WafLibraryRequiredTest
{
    private const ulong OutboundCallId = 42;
    private const string McpBrokenAuth = "api.security.mcp.broken_auth";
    private const string LargeResponseUrl = "api.security.large_response.url";

    [Fact]
    public void GivenAnUnauthenticatedMcpCall_WhenRequestAndResponseShareTheSubcontext_ThenBrokenAuthIsTagged()
    {
        using var waf = CreateWaf().Waf!;
        using var context = waf.CreateContext(out _)!;

        context.RunInSubcontext(OutboundCallId, McpRequest(), TimeoutMicroSeconds, isRasp: true, out _);
        var result = context.RunInSubcontext(OutboundCallId, ResponseWithStatus("200"), TimeoutMicroSeconds, isRasp: true, out _);

        result!.WafSpanAttributes.Should().ContainKey(McpBrokenAuth);
    }

    [Fact]
    public void GivenAnUnauthenticatedMcpCall_WhenTheServerAnswers401_ThenBrokenAuthIsNotTagged()
    {
        using var waf = CreateWaf().Waf!;
        using var context = waf.CreateContext(out _)!;

        context.RunInSubcontext(OutboundCallId, McpRequest(), TimeoutMicroSeconds, isRasp: true, out _);
        var result = context.RunInSubcontext(OutboundCallId, ResponseWithStatus("401"), TimeoutMicroSeconds, isRasp: true, out _);

        (result!.WafSpanAttributes?.ContainsKey(McpBrokenAuth) ?? false).Should().BeFalse();
    }

    [Fact]
    public void GivenAnSsrfMatchOnTheRequest_WhenTheResponseIsEvaluatedInTheSameSubcontext_ThenItIsNotReportedAgain()
    {
        using var waf = CreateWaf().Waf!;
        using var context = waf.CreateContext(out _)!;
        context.Run(new Dictionary<string, object> { [AddressesConstants.RequestQuery] = new Dictionary<string, object> { ["host"] = "169.254.169.254" } }, TimeoutMicroSeconds);

        var request = context.RunInSubcontext(OutboundCallId, Request("https://169.254.169.254/somewhere/in/the/app"), TimeoutMicroSeconds, isRasp: true, out _);
        var response = context.RunInSubcontext(OutboundCallId, ResponseWithStatus("200"), TimeoutMicroSeconds, isRasp: true, out _);

        request!.ShouldReportSecurityResult.Should().BeTrue();
        response!.ShouldReportSecurityResult.Should().BeFalse();
    }

    [Fact]
    public void GivenTwoOutboundCalls_WhenTheirBatchesInterleave_ThenEachResponseOnlySeesItsOwnRequest()
    {
        using var waf = CreateWaf().Waf!;
        using var context = waf.CreateContext(out _)!;

        context.RunInSubcontext(1, McpRequest(), TimeoutMicroSeconds, isRasp: true, out _);
        context.RunInSubcontext(2, Request("https://api.example.com/users"), TimeoutMicroSeconds, isRasp: true, out _);

        var second = context.RunInSubcontext(2, ResponseWithStatus("200"), TimeoutMicroSeconds, isRasp: true, out _);
        var first = context.RunInSubcontext(1, ResponseWithStatus("200"), TimeoutMicroSeconds, isRasp: true, out _);

        (second!.WafSpanAttributes?.ContainsKey(McpBrokenAuth) ?? false).Should().BeFalse();
        first!.WafSpanAttributes.Should().ContainKey(McpBrokenAuth);
    }

    [Fact]
    public void GivenAClosedSubcontext_WhenTheSameIdIsReused_ThenThePreviousRequestIsGone()
    {
        using var waf = CreateWaf().Waf!;
        using var context = waf.CreateContext(out _)!;

        context.RunInSubcontext(OutboundCallId, McpRequest(), TimeoutMicroSeconds, isRasp: true, out _);
        context.CloseSubcontext(OutboundCallId);
        var result = context.RunInSubcontext(OutboundCallId, ResponseWithStatus("200"), TimeoutMicroSeconds, isRasp: true, out _);

        (result!.WafSpanAttributes?.ContainsKey(McpBrokenAuth) ?? false).Should().BeFalse();
    }

    [Fact]
    public void GivenAnOpenSubcontext_WhenTheContextIsDisposed_ThenItIsReleasedWithIt()
    {
        using var waf = CreateWaf().Waf!;
        var context = waf.CreateContext(out _)!;

        context.RunInSubcontext(OutboundCallId, McpRequest(), TimeoutMicroSeconds, isRasp: true, out _);

        context.Invoking(c => c.Dispose()).Should().NotThrow();
        context.RunInSubcontext(OutboundCallId, ResponseWithStatus("200"), TimeoutMicroSeconds, isRasp: true, out var outcome)
               .Should().BeNull();
        outcome.Should().Be(WafOutcome.RequestEnded);
    }

    [Fact]
    public void GivenALargeDownstreamResponse_WhenItIsEvaluated_ThenTheUrlIsReportedWithoutSecrets()
    {
        using var waf = CreateWaf().Waf!;
        using var context = waf.CreateContext(out _)!;

        context.RunInSubcontext(OutboundCallId, Request("https://user:pass@api.example.com/files/report?token=s3cr3t#page"), TimeoutMicroSeconds, isRasp: true, out _);
        var response = new Dictionary<string, object>
        {
            [AddressesConstants.DownstreamResponseStatus] = "200",
            [AddressesConstants.DownstreamResponseHeaders] = new Dictionary<string, object> { ["content-length"] = new[] { "12345678" } },
        };
        var result = context.RunInSubcontext(OutboundCallId, response, TimeoutMicroSeconds, isRasp: true, out _);

        result!.WafSpanAttributes.Should().ContainKey(LargeResponseUrl);
        result.WafSpanAttributes![LargeResponseUrl].Should().Be("https://api.example.com/files/report");
        result.WafSpanAttributes["api.security.large_response.length"].Should().Be("12345678");
    }

    [Theory]
    [InlineData(LargeResponseUrl)]
    [InlineData("api.security.redirection.move_target")]
    [InlineData("api.security.redirection.redirect_target")]
    public void GivenAUrlAttribute_WhenTheResultIsBuilt_ThenItIsRedacted(string key)
    {
        var result = CreateResult(key, "https://user:pass@api.example.com/files?token=s3cr3t");

        result.WafSpanAttributes![key].Should().Be("https://api.example.com/files");
    }

    [Fact]
    public void GivenANonUrlAttribute_WhenTheResultIsBuilt_ThenItIsKeptAsIs()
    {
        var result = CreateResult("api.security.jwt.alg", "https://example.com/?a=b");

        result.WafSpanAttributes!["api.security.jwt.alg"].Should().Be("https://example.com/?a=b");
    }

    private static Dictionary<string, object> McpRequest() => Request("https://api.example.com/mcp/tools/list");

    private static Dictionary<string, object> Request(string url) => new()
    {
        [AddressesConstants.DownstreamUrl] = url,
        [AddressesConstants.DownstreamRequestMethod] = "GET",
        [AddressesConstants.DownstreamRequestHeaders] = new Dictionary<string, object> { ["accept"] = new[] { "application/json" } },
    };

    private static Dictionary<string, object> ResponseWithStatus(string status) => new() { [AddressesConstants.DownstreamResponseStatus] = status };

    private static IResult CreateResult(string attribute, string value)
    {
        var returnValues = new Dictionary<string, object?>
        {
            ["keep"] = false,
            ["attributes"] = new Dictionary<string, object?> { [attribute] = value },
        };

        var encoder = new Encoder();
        using var encodedResult = encoder.Encode(returnValues, applySafetyLimits: true);
        var nativeResult = encodedResult.ResultDdwafObject;
        var totals = default(WafRunTotals);
        return new Result(ref nativeResult, WafReturnCode.Match, ref totals);
    }
}
