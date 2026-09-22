// <copyright file="HttpSemanticConventionsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.OpenTelemetry;
using Datadog.Trace.Tagging;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.OpenTelemetry;

public class HttpSemanticConventionsTests
{
    public static TheoryData<string> AllKnownMethods() =>
        new() { "CONNECT", "DELETE", "GET", "HEAD", "OPTIONS", "PATCH", "POST", "PUT", "QUERY", "TRACE" };

    [Theory]
    // The RFC 9110 methods, plus PATCH and QUERY
    [InlineData("CONNECT", "CONNECT", null)]
    [InlineData("DELETE", "DELETE", null)]
    [InlineData("GET", "GET", null)]
    [InlineData("HEAD", "HEAD", null)]
    [InlineData("OPTIONS", "OPTIONS", null)]
    [InlineData("PATCH", "PATCH", null)]
    [InlineData("POST", "POST", null)]
    [InlineData("PUT", "PUT", null)]
    [InlineData("QUERY", "QUERY", null)]
    [InlineData("TRACE", "TRACE", null)]

    // Known methods are converted to their canonical form
    [InlineData("get", "GET", null)]
    [InlineData("Post", "POST", null)]
    [InlineData("pAtCh", "PATCH", null)]

    // Anything else is _OTHER
    [InlineData("FOO", "_OTHER", "FOO")]
    [InlineData("GETS", "_OTHER", "GETS")]
    [InlineData("GE", "_OTHER", "GE")]
    [InlineData("_OTHER", "_OTHER", "_OTHER")]
    [InlineData(" GET", "_OTHER", " GET")]
    [InlineData("", "_OTHER", "")]
    [InlineData(null, "_OTHER", null)]
    public void GetRequestMethodAttributeValues_ReturnsKnownMethodOrOther(string httpMethod, string expectedMethod, string expectedMethodOriginal)
    {
        HttpSemanticConventions.GetRequestMethodAttributeValues(httpMethod, out var method, out var methodOriginal);
        method.Should().Be(expectedMethod);
        methodOriginal.Should().Be(expectedMethodOriginal);
    }

    [Theory]
    [InlineData("GET", "GET")]
    [InlineData("POST", "POST")]
    [InlineData("_OTHER", "HTTP")]
    public void GetResourceName_UsesHttpForUnknownMethods(string requestMethod, string expected)
    {
        HttpSemanticConventions.GetResourceName(requestMethod).Should().Be(expected);
    }

    [Theory]
    // A template is reported exactly as the server stored it, including its casing, whether or not
    // it has a leading slash, and inline defaults and constraints.
    [InlineData("/api/delay/{seconds}", "/api/delay/{seconds}")]
    [InlineData("api/delay/{seconds}", "api/delay/{seconds}")]
    [InlineData("status-code/{statusCode}", "status-code/{statusCode}")]
    [InlineData("{controller=Home}/{action=Index}/{id?}", "{controller=Home}/{action=Index}/{id?}")]
    // ...except that a template matching the application root is stored as the empty string, and
    // reporting it verbatim would emit an empty attribute and a span name with a trailing space.
    [InlineData("", "/")]
    [InlineData("/", "/")]
    // No route matched, so http.route must be omitted rather than substituted.
    [InlineData(null, null)]
    public void GetHttpRoute_ReportsTheTemplateVerbatimExceptForTheApplicationRoot(string routeTemplate, string expected)
    {
        HttpSemanticConventions.GetHttpRoute(routeTemplate).Should().Be(expected);
    }

    // Exercise every method in both its
    // canonical and its lower-case form covers both, so the two cannot drift apart unnoticed.
    [Theory]
    [MemberData(nameof(AllKnownMethods))]
    public void GetRequestMethodAttributeValues_TakesTheSameDecisionOnBothPaths(string canonicalMethod)
    {
        HttpSemanticConventions.GetRequestMethodAttributeValues(canonicalMethod, out string method, out _);
        method.Should().Be(canonicalMethod);

        HttpSemanticConventions.GetRequestMethodAttributeValues(canonicalMethod.ToLowerInvariant(), out string methodLower, out _);
        methodLower.Should().Be(canonicalMethod);
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

    [Theory]
    // ASP.NET Core's HostString.Host wraps IPv6 addresses in brackets too, so
    // SetHttpServerUrlTags (used for server spans) must strip them the same way
    // GetServerAddress does for client spans
    [InlineData("[::1]", "::1")]
    [InlineData("[2001:db8::1]", "2001:db8::1")]

    // Regular hostnames are unaffected
    [InlineData("example.com", "example.com")]
    public void SetHttpServerUrlTags_StripsIPv6BracketsFromServerAddress(string host, string expected)
    {
        var tags = new WebTags();

        HttpSemanticConventions.SetHttpServerUrlTags(
            tags,
            scheme: "https",
            host: host,
            port: 443,
            pathBase: null,
            path: "/",
            queryString: null,
            queryStringManager: null);

        tags.ServerAddress.Should().Be(expected);
    }

    [Theory]
    // An explicit port is reported as-is
    [InlineData("http://example.com:8080", 8080)]
    [InlineData("https://example.com:8443", 8443)]

    // No explicit port falls back to the scheme's default port
    [InlineData("http://example.com", 80)]
    [InlineData("https://example.com", 443)]

    // A scheme with no registered default port and no explicit port has no valid port to report,
    // as Uri.Port is -1 in that case (for example, a custom HttpMessageHandler using a
    // "http+unix://socket/path" style URI)
    [InlineData("http+unix://socket/path", null)]
    public void GetServerPort_OmitsThePortWhenTheUriHasNone(string uri, int? expected)
    {
        HttpSemanticConventions.GetServerPort(new Uri(uri)).Should().Be(expected);
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
