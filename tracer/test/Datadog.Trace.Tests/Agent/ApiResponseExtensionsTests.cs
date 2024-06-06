// <copyright file="ApiResponseExtensionsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Agent;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Agent;

public class ApiResponseExtensionsTests
{
    public static TheoryData<string, string, bool> Data => new()
    {
        { "text/plain", "text/plain", true },
        { " text/plain  ", "text/plain", true },
        { " text/plain;", "text/plain", true },
        { " text/plain ;", "text/plain", true },
        { "text/plain ;", "text/plain", true },
        { "text/plain;", "text/plain", true },
        { "text/plain; charset=utf8", "text/plain", true },
        { "text/plain; charset=utf8;boundary=fdsygyh", "text/plain", true },
        { "text/plain ; charset=utf8; boundary=fdsygyh", "text/plain", true },
        { " text/plain ; charset=utf8; boundary=fdsygyh", "text/plain", true },
        { "application/json", "application/json", true },
        { "text/html", "text/html", true },
        { "text/htmlx", "text/html", false },
        { "text/htmlx;", "text/html", false },
        { "pretext/html;", "text/html", false },
        { "pretext/html", "text/html", false },
        { "pre text/html", "text/html", false },
        { "text/ html", "text/html", false },
        { string.Empty, "text/plain", false },
        { null, "text/plain", false },
    };

    [Theory]
    [MemberData(nameof(Data))]
    public void HasMimeType_ReturnsExpectedValues(string rawContentType, string mimeType, bool expectedValue)
    {
        ApiResponseExtensions.HasMimeType(rawContentType, mimeType).Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void HasMimeType_ThrowsForNullMimeType(string mimeType)
    {
        FluentActions.Invoking(() => ApiResponseExtensions.HasMimeType("application/json", mimeType))
                     .Should()
                     .ThrowExactly<ArgumentNullException>();
    }
}
