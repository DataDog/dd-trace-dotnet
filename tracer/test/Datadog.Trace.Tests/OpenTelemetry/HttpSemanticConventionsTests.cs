// <copyright file="HttpSemanticConventionsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.OpenTelemetry;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util.Http;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.OpenTelemetry;

public class HttpSemanticConventionsTests
{
    private static readonly QueryStringManager QueryStringManager = new(
        reportQueryString: true,
        timeout: 30_000,
        maxSizeBeforeObfuscation: 5000,
        pattern: TracerSettingsConstants.DefaultObfuscationQueryStringRegex);

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
        HttpSemanticConventions.GetNetworkProtocolVersion((Version)null).Should().BeNull();
    }

    [Theory]
    // The protocols we expect a server to report, in the "HTTP/1.1" form both System.Web (through
    // the SERVER_PROTOCOL variable) and ASP.NET Core (through HttpRequest.Protocol) use. Note that
    // the minor version is only reported for HTTP/1.x
    [InlineData("HTTP/1.0", "1.0")]
    [InlineData("HTTP/1.1", "1.1")]
    [InlineData("HTTP/2", "2")]
    [InlineData("HTTP/2.0", "2")]
    [InlineData("HTTP/3", "3")]
    [InlineData("HTTP/3.0", "3")]

    // A version we don't know about is still reported, without the protocol name
    [InlineData("HTTP/1.2", "1.2")]
    [InlineData("http/1.1", "1.1")]

    // Nothing to report, rather than something the conventions don't describe
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("SPDY/3", null)]
    [InlineData("1.1", null)]
    public void GetNetworkProtocolVersion_ReportsTheVersionWithoutTheProtocolName(string protocol, string expected)
    {
        HttpSemanticConventions.GetNetworkProtocolVersion(protocol).Should().Be(expected);
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

    [Theory]
    // "{method} {target}" when the request matched a route
    [InlineData("GET", "/users/{id}", "GET /users/{id}")]
    [InlineData("POST", "{controller}/{action}/{id}", "POST {controller}/{action}/{id}")]

    // Just "{method}" when it didn't. Note that we must not fall back to the URI path, as that
    // would make the span name high-cardinality.
    [InlineData("GET", null, "GET")]
    [InlineData("GET", "", "GET")]

    // An unrecognized method is reported as "_OTHER", but the span name says "HTTP"
    [InlineData("_OTHER", "/users/{id}", "HTTP /users/{id}")]
    [InlineData("_OTHER", null, "HTTP")]
    public void GetServerResourceName_IsMethodAndRoute(string requestMethod, string route, string expected)
    {
        HttpSemanticConventions.GetServerResourceName(requestMethod, route).Should().Be(expected);
    }

    [Theory]
    [InlineData("get", "GET")]
    [InlineData("FOO", "HTTP")]
    [InlineData(null, "HTTP")]
    public void GetServerResourceNameFromRawMethod_NormalizesTheMethod(string httpMethod, string expected)
    {
        HttpSemanticConventions.GetServerResourceNameFromRawMethod(httpMethod).Should().Be(expected);
    }

    [Theory]
    // A Host header with a port
    [InlineData("example.com:8080", "example.com", 8080)]
    [InlineData("127.0.0.1:5000", "127.0.0.1", 5000)]
    [InlineData("[::1]:5000", "::1", 5000)]

    // A Host header without a port: the client used the scheme's default, and guessing which one
    // would be wrong when the request went through a proxy, so no port is reported
    [InlineData("example.com", "example.com", null)]
    [InlineData("[::1]", "::1", null)]

    // An unparseable port is dropped, but it is still a port: it must not end up in the address,
    // which the Host header lets the client set to anything
    [InlineData("example.com:", "example.com", null)]
    [InlineData("example.com:not-a-port", "example.com", null)]
    [InlineData("example.com:99999", "example.com", null)]
    [InlineData("[::1]:", "::1", null)]

    // An unterminated bracket is a malformed address rather than a "host:port" pair, so splitting it
    // at a colon would report half an IPv6 address
    [InlineData("[::1", "[::1", null)]

    // The specification only allows "server.port" alongside "server.address"
    [InlineData(":8080", null, null)]
    public void SetServerAddressAndPort_SplitsTheHostHeader(string hostHeader, string expectedAddress, int? expectedPort)
    {
        var tags = new WebTags();

        HttpSemanticConventions.SetServerAddressAndPort(tags, hostHeader, requestUri: null);

        tags.ServerAddress.Should().Be(expectedAddress);
        tags.ServerPort.Should().Be(expectedPort);
    }

    [Fact]
    public void SetServerAddressAndPort_FallsBackToTheRequestUriWithoutAHostHeader()
    {
        var tags = new WebTags();

        HttpSemanticConventions.SetServerAddressAndPort(tags, hostHeader: null, new Uri("https://example.com/users/1"));

        tags.ServerAddress.Should().Be("example.com");
        tags.ServerPort.Should().Be(443);
    }

    [Fact]
    public void SetServerAddressAndPort_ReportsNothingWhenThereIsNeither()
    {
        var tags = new WebTags();

        HttpSemanticConventions.SetServerAddressAndPort(tags, hostHeader: string.Empty, requestUri: null);

        tags.ServerAddress.Should().BeNull();
        tags.ServerPort.Should().BeNull();
    }

    [Fact]
    public void SetHttpServerRequestValues_SplitsTheUrlAndObfuscatesTheQuery()
    {
        var span = CreateSpan();
        var tags = new WebTags();
        var uri = new Uri("https://localhost:8080/store/checkout?token=SECRET&page=2");

        HttpSemanticConventions.SetHttpServerRequestValues(span, tags, resourceName: null, "GET", userAgent: null, protocol: "HTTP/1.1", hostHeader: "localhost:8080", uri, QueryStringManager);

        tags.HttpMethod.Should().Be("GET");
        tags.HttpRequestMethodOriginal.Should().BeNull();
        tags.NetworkProtocolVersion.Should().Be("1.1");
        tags.UrlScheme.Should().Be("https");
        tags.UrlPath.Should().Be("/store/checkout");

        // The leading '?' belongs to "url.full", not to "url.query"
        tags.UrlQuery.Should().Be("<redacted>&page=2");
        tags.ServerAddress.Should().Be("localhost");
        tags.ServerPort.Should().Be(8080);

        // OpenTelemetry replaces these with the attributes above
        tags.HttpUrl.Should().BeNull();
        tags.HttpRequestHeadersHost.Should().BeNull();
    }

    [Fact]
    public void SetHttpServerRequestValues_OmitsTheQueryWhenThereIsNone()
    {
        var span = CreateSpan();
        var tags = new WebTags();

        HttpSemanticConventions.SetHttpServerRequestValues(span, tags, resourceName: null, "GET", userAgent: null, protocol: "HTTP/1.1", hostHeader: "localhost", new Uri("http://localhost/ping"), QueryStringManager);

        tags.UrlPath.Should().Be("/ping");
        tags.UrlQuery.Should().BeNull();
    }

    [Fact]
    public void SetHttpServerRequestValues_OmitsTheProtocolVersionWhenTheHostDoesNotReportIt()
    {
        var span = CreateSpan();
        var tags = new WebTags();

        HttpSemanticConventions.SetHttpServerRequestValues(span, tags, resourceName: null, "GET", userAgent: null, protocol: null, hostHeader: "localhost", new Uri("http://localhost/ping"), QueryStringManager);

        tags.NetworkProtocolVersion.Should().BeNull();
    }

    [Theory]
    // Reported verbatim when it is already canonical
    [InlineData("GET", "GET", null)]

    // A non-canonical casing is reported as canonical
    [InlineData("get", "GET", null)]

    // An unrecognized method becomes "_OTHER", and the original is kept
    [InlineData("FOO", "_OTHER", "FOO")]
    public void SetHttpServerRequestValues_RecordsTheOriginalMethodOnlyWhenItDiffers(string httpMethod, string expectedMethod, string expectedOriginal)
    {
        var span = CreateSpan();
        var tags = new WebTags();

        HttpSemanticConventions.SetHttpServerRequestValues(span, tags, resourceName: null, httpMethod, userAgent: null, protocol: null, hostHeader: null, requestUri: null, QueryStringManager);

        tags.HttpMethod.Should().Be(expectedMethod);
        tags.HttpRequestMethodOriginal.Should().Be(expectedOriginal);
    }

    [Fact]
    public void SetHttpRoute_KeepsTheFirstRoute()
    {
        var span = CreateSpan();

        HttpSemanticConventions.SetHttpRoute(span, "/users/{id}");
        HttpSemanticConventions.SetHttpRoute(span, "/something/else");

        span.GetTag(Tags.HttpRoute).Should().Be("/users/{id}");
    }

    [Fact]
    public void SetHttpRoute_IgnoresAnEmptyRoute()
    {
        var span = CreateSpan();

        HttpSemanticConventions.SetHttpRoute(span, null);
        HttpSemanticConventions.SetHttpRoute(span, string.Empty);

        span.GetTag(Tags.HttpRoute).Should().BeNull();
    }

    private static Span CreateSpan()
        => new Span(new SpanContext(null, new TraceContext(Tracer.Instance), serviceName: null), DateTimeOffset.UtcNow, new WebTags());
}
