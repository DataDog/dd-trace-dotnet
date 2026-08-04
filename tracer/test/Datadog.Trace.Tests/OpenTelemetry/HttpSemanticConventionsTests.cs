// <copyright file="HttpSemanticConventionsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.OpenTelemetry;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.OpenTelemetry;

public class HttpSemanticConventionsTests
{
    public static TheoryData<string> AllKnownMethods() =>
        new() { "CONNECT", "DELETE", "GET", "HEAD", "OPTIONS", "PATCH", "POST", "PUT", "QUERY", "TRACE" };

    [Theory]
    // The RFC 9110 methods, plus PATCH and QUERY
    [InlineData("CONNECT", "CONNECT")]
    [InlineData("DELETE", "DELETE")]
    [InlineData("GET", "GET")]
    [InlineData("HEAD", "HEAD")]
    [InlineData("OPTIONS", "OPTIONS")]
    [InlineData("PATCH", "PATCH")]
    [InlineData("POST", "POST")]
    [InlineData("PUT", "PUT")]
    [InlineData("QUERY", "QUERY")]
    [InlineData("TRACE", "TRACE")]

    // Known methods are converted to their canonical form
    [InlineData("get", "GET")]
    [InlineData("Post", "POST")]
    [InlineData("pAtCh", "PATCH")]

    // Anything else is _OTHER
    [InlineData("FOO", "_OTHER")]
    [InlineData("GETS", "_OTHER")]
    [InlineData("GE", "_OTHER")]
    [InlineData("_OTHER", "_OTHER")]
    [InlineData(" GET", "_OTHER")]
    [InlineData("", "_OTHER")]
    [InlineData(null, "_OTHER")]
    public void NormalizeRequestMethod_ReturnsKnownMethodOrOther(string httpMethod, string expected)
    {
        HttpSemanticConventions.NormalizeRequestMethod(httpMethod).Should().Be(expected);
    }

    [Theory]
    [InlineData("GET", "GET")]
    [InlineData("POST", "POST")]
    [InlineData("_OTHER", "HTTP")]
    public void GetResourceName_UsesHttpForUnknownMethods(string requestMethod, string expected)
    {
        HttpSemanticConventions.GetResourceName(requestMethod).Should().Be(expected);
    }

    // NormalizeRequestMethod holds the known methods twice: an ordinal switch for the fast path,
    // and a case-insensitive dictionary for the fallback. Exercising every method in both its
    // canonical and its lower-case form covers both, so the two cannot drift apart unnoticed.
    [Theory]
    [MemberData(nameof(AllKnownMethods))]
    public void NormalizeRequestMethod_TakesTheSameDecisionOnBothPaths(string canonicalMethod)
    {
        HttpSemanticConventions.NormalizeRequestMethod(canonicalMethod).Should().Be(canonicalMethod);
        HttpSemanticConventions.NormalizeRequestMethod(canonicalMethod.ToLowerInvariant()).Should().Be(canonicalMethod);
    }

    [Theory]
    // The versions we expect to see. Note that the minor version is only reported for HTTP/1.x
    [InlineData(1, 0, "1.0")]
    [InlineData(1, 1, "1.1")]
    [InlineData(2, 0, "2")]
    [InlineData(3, 0, "3")]

    // Anything else falls back to the version as-is
    [InlineData(1, 2, "1.2")]
    [InlineData(2, 1, "2.1")]
    [InlineData(4, 0, "4.0")]
    public void GetNetworkProtocolVersion_OmitsTheMinorVersionForHttp2AndAbove(int major, int minor, string expected)
    {
        HttpSemanticConventions.GetNetworkProtocolVersion(new Version(major, minor)).Should().Be(expected);
    }

    [Fact]
    public void GetNetworkProtocolVersion_ReturnsNullWhenThereIsNoResponse()
    {
        HttpSemanticConventions.GetNetworkProtocolVersion(null).Should().BeNull();
    }

    [Theory]
    // IPv4 addresses and regular hostnames are unaffected
    [InlineData("example.com", "example.com")]
    [InlineData("127.0.0.1", "127.0.0.1")]

    // Uri.Host wraps IPv6 addresses in brackets (RFC 2732); those must be stripped, since
    // OpenTelemetry expects the address itself in "server.address"
    [InlineData("[::1]", "::1")]
    [InlineData("[2001:db8::1]", "2001:db8::1")]

    // Edge cases: no host, or a value that merely looks bracket-like
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("[", "[")]
    [InlineData("]", "]")]
    public void GetServerAddress_StripsIPv6Brackets(string host, string expected)
    {
        HttpSemanticConventions.GetServerAddress(host).Should().Be(expected);
    }

    // A Version can carry a build and a revision, which the major/minor patterns ignore. We must
    // not fall through to Version.ToString() for those, or an HTTP/1.1 response constructed as a
    // four-component Version would be reported as "1.1.0.0".
    [Theory]
    [InlineData(1, 1, 0, 0, "1.1")]
    [InlineData(1, 1, 1, 1, "1.1")]
    [InlineData(2, 0, 0, 0, "2")]
    public void GetNetworkProtocolVersion_IgnoresTheBuildAndRevision(int major, int minor, int build, int revision, string expected)
    {
        HttpSemanticConventions.GetNetworkProtocolVersion(new Version(major, minor, build, revision)).Should().Be(expected);
    }
}
