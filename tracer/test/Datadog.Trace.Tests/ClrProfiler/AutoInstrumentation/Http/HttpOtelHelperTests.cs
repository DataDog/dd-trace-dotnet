// <copyright file="HttpOtelHelperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.ClrProfiler.AutoInstrumentation.Http;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.ClrProfiler.AutoInstrumentation.Http;

public class HttpOtelHelperTests
{
    // ─── SetRequestMethod ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    [InlineData("TRACE")]
    [InlineData("PATCH")]
    [InlineData("CONNECT")]
    [InlineData("QUERY")]
    public void SetRequestMethod_KnownMethod_SetsTagWithoutOriginal(string method)
    {
        var span = SpanMock();
        HttpOtelHelper.SetRequestMethod(span.Object, method);

        span.Verify(s => s.SetTag("http.request.method", method), Times.Once);
        span.Verify(s => s.SetTag("http.request.method_original", It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData("PROPFIND")]
    [InlineData("BOGUS")]
    [InlineData("MKCOL")]
    public void SetRequestMethod_UnknownMethod_SetsOtherAndPreservesOriginal(string method)
    {
        var span = SpanMock();
        HttpOtelHelper.SetRequestMethod(span.Object, method);

        span.Verify(s => s.SetTag("http.request.method", "_OTHER"), Times.Once);
        span.Verify(s => s.SetTag("http.request.method_original", method), Times.Once);
        span.Verify(s => s.SetTag("http.request.method", method), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void SetRequestMethod_NullOrEmpty_SetsNoTag(string method)
    {
        var span = SpanMock();
        HttpOtelHelper.SetRequestMethod(span.Object, method);

        span.Verify(s => s.SetTag(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    // ─── SetClientUrl ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("http://host:8080/p?q=1", "http://host:8080/p?q=1")]
    [InlineData("https://host/p", "https://host/p")]
    public void SetClientUrl_NoCredentials_SetsUrlFullAsIs(string rawUrl, string expectedFull)
    {
        var span = SpanMock();
        HttpOtelHelper.SetClientUrl(span.Object, rawUrl);

        span.Verify(s => s.SetTag("url.full", expectedFull), Times.Once);
    }

    [Theory]
    [InlineData("http://user:pass@host/p", "http://host/p")]
    [InlineData("https://user:secret@host:8443/p?q=1", "https://host:8443/p?q=1")]
    [InlineData("http://user@host/p", "http://host/p")]
    public void SetClientUrl_CredentialsPresent_RedactsFromUrlFull(string rawUrl, string expectedFull)
    {
        var span = SpanMock();
        HttpOtelHelper.SetClientUrl(span.Object, rawUrl);

        span.Verify(s => s.SetTag("url.full", expectedFull), Times.Once);
    }

    [Theory]
    [InlineData("http://host/p", "http")]
    [InlineData("https://host/p", "https")]
    [InlineData("http://host:8080/p", "http")]
    public void SetClientUrl_SetsScheme(string rawUrl, string expectedScheme)
    {
        var span = SpanMock();
        HttpOtelHelper.SetClientUrl(span.Object, rawUrl);

        span.Verify(s => s.SetTag("url.scheme", expectedScheme), Times.Once);
    }

    [Fact]
    public void SetClientUrl_NonDefaultPort_SetsServerPort()
    {
        var span = SpanMock();
        HttpOtelHelper.SetClientUrl(span.Object, "http://host:8080/p");

        span.Verify(s => s.SetTag("server.port", "8080"), Times.Once);
    }

    [Theory]
    [InlineData("http://host/p")]    // port 80 is default for http
    [InlineData("https://host/p")]   // port 443 is default for https
    public void SetClientUrl_DefaultPort_DoesNotSetServerPort(string rawUrl)
    {
        var span = SpanMock();
        HttpOtelHelper.SetClientUrl(span.Object, rawUrl);

        span.Verify(s => s.SetTag("server.port", It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void SetClientUrl_NullOrEmpty_SetsNoTag(string rawUrl)
    {
        var span = SpanMock();
        HttpOtelHelper.SetClientUrl(span.Object, rawUrl);

        span.Verify(s => s.SetTag(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    // ─── SetServerUrl ────────────────────────────────────────────────────────────

    [Fact]
    public void SetServerUrl_SetsSchemePathAndQuery()
    {
        var span = SpanMock();
        HttpOtelHelper.SetServerUrl(span.Object, "http://host:8080/p/q?token=secret");

        span.Verify(s => s.SetTag("url.scheme", "http"), Times.Once);
        span.Verify(s => s.SetTag("url.path", "/p/q"), Times.Once);
        span.Verify(s => s.SetTag("url.query", "token=secret"), Times.Once);
        span.Verify(s => s.SetTag("server.port", "8080"), Times.Once);
    }

    [Fact]
    public void SetServerUrl_DefaultPort_DoesNotSetServerPort()
    {
        var span = SpanMock();
        HttpOtelHelper.SetServerUrl(span.Object, "https://host/p");

        span.Verify(s => s.SetTag("server.port", It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void SetServerUrl_NoQuery_DoesNotSetUrlQuery()
    {
        var span = SpanMock();
        HttpOtelHelper.SetServerUrl(span.Object, "http://host/p");

        span.Verify(s => s.SetTag("url.query", It.IsAny<string>()), Times.Never);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────────

    private static Mock<ISpan> SpanMock(string existingErrorType = null)
    {
        var mock = new Mock<ISpan>();
        mock.Setup(s => s.SetTag(It.IsAny<string>(), It.IsAny<string>())).Returns(mock.Object);
        mock.Setup(s => s.GetTag("error.type")).Returns(existingErrorType);
        return mock;
    }
}
