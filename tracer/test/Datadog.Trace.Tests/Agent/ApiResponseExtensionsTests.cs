// <copyright file="ApiResponseExtensionsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Datadog.Trace.Agent;
using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Agent;

public class ApiResponseExtensionsTests
{
    private static readonly Encoding Latin1 = Encoding.GetEncoding("ISO-8859-1");

    public static TheoryData<string, Encoding> CharsetData => new()
    {
        // Not provided
        { "text/plain", EncodingHelpers.Utf8NoBom },
        { " text/plain  ", EncodingHelpers.Utf8NoBom },
        { " text/plain;", EncodingHelpers.Utf8NoBom },
        { " text/plain ;", EncodingHelpers.Utf8NoBom },
        { "text/plain ;", EncodingHelpers.Utf8NoBom },
        { "text/plain;", EncodingHelpers.Utf8NoBom },
        { "text/html", EncodingHelpers.Utf8NoBom },
        { "text/htmlx", EncodingHelpers.Utf8NoBom },
        { "text/htmlx;", EncodingHelpers.Utf8NoBom },
        { "pretext/html;", EncodingHelpers.Utf8NoBom },
        { "pretext/html", EncodingHelpers.Utf8NoBom },
        { "pre text/html", EncodingHelpers.Utf8NoBom },
        { "text/ html", EncodingHelpers.Utf8NoBom },

        // default recognized content-types
        { "application/json", EncodingHelpers.Utf8NoBom },

        // null content-types
        { string.Empty, EncodingHelpers.Utf8NoBom },
        { null, EncodingHelpers.Utf8NoBom },

        // Well known values
        { " charset=utf-8", EncodingHelpers.Utf8NoBom },
        { "text/plain;charset=utf-8", EncodingHelpers.Utf8NoBom },
        { "text/plain; charset=utf-8", EncodingHelpers.Utf8NoBom },
        { "text/plain; charset=utf-8 ", EncodingHelpers.Utf8NoBom },
        { "text/plain; charset=utf-8 ;boundary=fdsygyh", EncodingHelpers.Utf8NoBom },
        { "text/plain ; charset=utf-8; boundary=fdsygyh", EncodingHelpers.Utf8NoBom },
        { " text/plain ; charset=utf-8; boundary=fdsygyh", EncodingHelpers.Utf8NoBom },
        { "text/plain;charset=us-ascii", Encoding.ASCII },
        { "text/plain; charset=us-ascii", Encoding.ASCII },
        { "text/plain; charset=us-ascii ", Encoding.ASCII },
        { "text/plain; charset=us-ascii ;boundary=fdsygyh", Encoding.ASCII },
        { "text/plain ; charset=us-ascii; boundary=fdsygyh", Encoding.ASCII },
        { " text/plain ; charset=us-ascii; boundary=fdsygyh", Encoding.ASCII },
        { " text/plain ; charset=us-ascii; boundary=fdsygyh", Encoding.ASCII },

        // Converted values
        { " charset=ISO-8859-1", Latin1 },
        { "text/plain;charset=ISO-8859-1", Latin1 },
        { "text/plain; charset=ISO-8859-1", Latin1 },
        { "text/plain; charset=ISO-8859-1 ", Latin1 },
        { "text/plain; charset=ISO-8859-1 ;boundary=fdsygyh", Latin1 },
        { "text/plain ; charset=ISO-8859-1; boundary=fdsygyh", Latin1 },
        { " text/plain ; charset=ISO-8859-1; boundary=fdsygyh", Latin1 },
        { " text/plain ; charset=ISO-8859-1; boundary=fdsygyh", Latin1 },
        { " charset=utf-16", Encoding.Unicode },
        { "text/plain;charset=utf-16", Encoding.Unicode },
        { "text/plain; charset=utf-16", Encoding.Unicode },
        { "text/plain; charset=utf-16 ", Encoding.Unicode },
        { "text/plain; charset=utf-16 ;boundary=fdsygyh", Encoding.Unicode },
        { "text/plain ; charset=utf-16; boundary=fdsygyh", Encoding.Unicode },
        { " text/plain ; charset=utf-16; boundary=fdsygyh", Encoding.Unicode },
        { " text/plain ; charset=utf-16; boundary=fdsygyh", Encoding.Unicode },

        // Unknown values
        { "text/plain;charset=meep", EncodingHelpers.Utf8NoBom },
        { "text/plain; charset=meep", EncodingHelpers.Utf8NoBom },
        { "text/plain; charset=meep ", EncodingHelpers.Utf8NoBom },
        { "text/plain; charset=meep ;boundary=fdsygyh", EncodingHelpers.Utf8NoBom },
        { "text/plain ; charset=meep; boundary=fdsygyh", EncodingHelpers.Utf8NoBom },
        { " text/plain ; charset=meep; boundary=fdsygyh", EncodingHelpers.Utf8NoBom },
        { " text/plain ; charset=meep; boundary=fdsygyh", EncodingHelpers.Utf8NoBom },
    };

    public static TheoryData<string, int> ContentEncodingData => new()
    {
        { null, (int)ContentEncodingType.None },
        { string.Empty, (int)ContentEncodingType.None },
        { "br", (int)ContentEncodingType.Brotli },
        { "gzip", (int)ContentEncodingType.GZip },
        { "GZip", (int)ContentEncodingType.GZip },
        { "compress", (int)ContentEncodingType.Compress },
        { "deflate", (int)ContentEncodingType.Deflate },
        { "zstd", (int)ContentEncodingType.Other },
        { "br,", (int)ContentEncodingType.Multiple }, // not technically, but edge case we don't care about
        { ",", (int)ContentEncodingType.Multiple }, // not technically, but edge case we don't care about
        { ",,", (int)ContentEncodingType.Multiple }, // not technically, but edge case we don't care about
        { "br,gzip", (int)ContentEncodingType.Multiple },
        { " br,gzip ", (int)ContentEncodingType.Multiple },
        { "br, gzip", (int)ContentEncodingType.Multiple },
        { " br ", (int)ContentEncodingType.Brotli },
    };

    [Theory]
    [MemberData(nameof(CharsetData), DisableDiscoveryEnumeration = true)]
    public void GetCharsetEncoding_ReturnsExpectedValues(string rawContentType, Encoding expected)
    {
        ApiResponseExtensions.GetCharsetEncoding(rawContentType)
                             .Should()
                             .BeSameAs(expected, $"content-type {rawContentType} should be extracted as encoding {expected.EncodingName}");
    }

#if NETCOREAPP3_1_OR_GREATER
    [SkippableTheory]
    [MemberData(nameof(CharsetData), DisableDiscoveryEnumeration = true)]
    public void HttpClientResponse_ReturnsExpectedValues(string rawContentType, Encoding expected)
    {
        if (!MediaTypeHeaderValue.TryParse(rawContentType, out var parsed))
        {
            // this is unforgiving, so just bail out as we can't test it
            throw new SkipException();
        }

        var httpClientResponse = new HttpResponseMessage();
        var content = new StringContent("Some content");
        content.Headers.ContentType = parsed;
        httpClientResponse.Content = content;

        var response = new Datadog.Trace.Agent.Transports.HttpClientResponse(httpClientResponse);
        response.GetCharsetEncoding()
                .Should()
                .BeSameAs(expected, $"content-type {rawContentType} should be extracted as encoding {expected.EncodingName}");
    }
#endif

    [Theory]
    [MemberData(nameof(ContentEncodingData), DisableDiscoveryEnumeration = true)]
    public void GetContentEncodingType_ReturnsExpectedValues(string contentEncoding, int type)
    {
        ApiResponseExtensions.GetContentEncodingType(contentEncoding)
                             .Should()
                             .Be((ContentEncodingType)type);
    }
}
