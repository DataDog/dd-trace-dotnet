// <copyright file="HttpMessageTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Text;
using Datadog.Trace.HttpOverStreams;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.HttpOverStreams;

public class HttpMessageTests
{
    [Fact]
    public void Json()
    {
        var headers = new HttpHeaders { { "Content-Type", "application/json" } };
        var httpResponse = new HttpResponse(200, "OK", headers, null);

        httpResponse.GetContentEncoding().Should().Be(new UTF8Encoding(false, true));
    }

    [Fact]
    public void NoContentType()
    {
        var httpResponse = new HttpResponse(200, "OK", new HttpHeaders(), null);

        httpResponse.GetContentEncoding().Should().Be(new UTF8Encoding(false, true));
    }

    [Fact]
    public void CustomEncoding()
    {
        var headers = new HttpHeaders { { "Content-Type", "text/plain; charset=iso-8859-1" } };
        var httpResponse = new HttpResponse(200, "OK", headers, null);

        httpResponse.GetContentEncoding().Should().Be(Encoding.GetEncoding("ISO-8859-1"));
    }

    [Fact]
    public void Ascii()
    {
        var headers = new HttpHeaders { { "Content-Type", "text/plain; charset=us-ascii" } };
        var httpResponse = new HttpResponse(200, "OK", headers, null);

        httpResponse.GetContentEncoding().Should().Be(Encoding.ASCII);
    }

    [Fact]
    public void InvalidHeader()
    {
        var headers = new HttpHeaders { { "Content-Type", "text/plain; charset=us-ascii=" } };
        var httpResponse = new HttpResponse(200, "OK", headers, null);

        httpResponse.GetContentEncoding().Should().Be(new UTF8Encoding(false, true));
    }
}
