// <copyright file="ApiResponseExtensionsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
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

    [Theory]
    [MemberData(nameof(CharsetData), DisableDiscoveryEnumeration = true)]
    public void GetCharsetEncoding_ReturnsExpectedValues(string rawContentType, Encoding expected)
    {
        ApiResponseExtensions.GetCharsetEncoding(rawContentType)
                             .Should()
                             .BeSameAs(expected, $"content-type {rawContentType} should be extracted as encoding {expected.EncodingName}");
    }
}
