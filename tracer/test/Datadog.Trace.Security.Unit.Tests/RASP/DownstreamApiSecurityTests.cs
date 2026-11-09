// <copyright file="DownstreamApiSecurityTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
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
    [InlineData("https://api.example.com/files", "https://api.example.com/files")]
    [InlineData("https://api.example.com/files?token=s3cr3t", "https://api.example.com/files")]
    [InlineData("https://api.example.com/files#fragment", "https://api.example.com/files")]
    [InlineData("https://user:pass@api.example.com:8443/files?a=1", "https://api.example.com:8443/files")]
    [InlineData("https://user@api.example.com", "https://api.example.com/")]
    [InlineData("http://api.example.com?key=1", "http://api.example.com/")]
    [InlineData("https://user:p#ss@api.example.com/files", "https://")]
    [InlineData("https://user:p?ss@api.example.com/files", "https://")]
    [InlineData("//api.example.com?next=user@example.net", "//")]
    [InlineData("//api.example.com#user@example.net", "//")]
    [InlineData("https://user:1234?ss@api.example.com/files", "https://")]
    [InlineData("\\\\user:pass@api.example.com/next?a=1", "\\\\api.example.com/next")]
    [InlineData("/login?next=https://user:pass@x.example.com", "/login")]
    [InlineData("https://api.example.com/path/with@sign", "https://api.example.com/path/with@sign")]
    [InlineData("https://[::1]:8080/files?a=1", "https://[::1]:8080/files")]
    [InlineData("//user:pass@api.example.com/next?a=1", "//api.example.com/next")]
    [InlineData("/login?next=/home&token=s3cr3t", "/login")]
    [InlineData("login#top", "login")]
    [InlineData("", "")]
    public void GivenAUrl_WhenItIsRedacted_ThenUserInfoQueryAndFragmentAreRemoved(string url, string expected)
    {
        WafAttributeRedaction.RemoveUrlSecrets(url).Should().Be(expected);
    }

    [Theory]
    [InlineData("https://user:", 'a', WafConstants.MaxStringLength)]
    [InlineData("https://user:p?ss", 'a', WafConstants.MaxStringLength)]
    [InlineData("https://user:pa@ss", 'a', WafConstants.MaxStringLength)]
    [InlineData("//user:", 'a', WafConstants.MaxStringLength)]
    [InlineData("https://user:", (char)0xE4, WafConstants.MaxStringLength * 2 / 3)]
    public void GivenAUrlTruncatedInsideItsAuthority_WhenItIsRedacted_ThenTheAuthorityIsDropped(string prefix, char filler, int length)
    {
        // the encoder truncates strings to MaxStringLength chars or UTF-8 bytes, which may cut a long user-info before its '@'
        var url = (prefix + new string(filler, length)).Substring(0, length);

        var redacted = WafAttributeRedaction.RemoveUrlSecrets(url);

        redacted.Should().Be(prefix.Substring(0, prefix.IndexOf("//", StringComparison.Ordinal) + 2));
    }

    [Fact]
    public void GivenALongUrlWithAPath_WhenItIsRedacted_ThenTheHostIsKept()
    {
        var url = "https://user:pass@api.example.com/files?token=" + new string('a', WafConstants.MaxStringLength);

        WafAttributeRedaction.RemoveUrlSecrets(url.Substring(0, WafConstants.MaxStringLength)).Should().Be("https://api.example.com/files");
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
        ulong duration = 0;
        return new Result(ref nativeResult, WafReturnCode.Match, ref duration, 0);
    }
}
