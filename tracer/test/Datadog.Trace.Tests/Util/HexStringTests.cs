// <copyright file="HexStringTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Util;

public class HexStringTests
{
    [Theory]
    [InlineData("0000000000000000", 0)]
    [InlineData("00000000075bcd15", 123456789)]
    [InlineData("ffffffffffffffff", 18446744073709551615)]
    public void TryParseUInt64_Valid(string hex, ulong expected)
    {
        HexString.TryParseUInt64(hex, out var actual).Should().BeTrue();
        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("gfd")]
    [InlineData("000000000000000z")]
    public void TryParseUInt64_Invalid(string hex)
    {
        HexString.TryParseUInt64(hex, out var actual).Should().BeFalse();
        actual.Should().Be(0);
    }

    [Theory]
    [InlineData("00", 0)]
    [InlineData("0c", 12)]
    [InlineData("ff", 255)]
    public void TryParseByte_Valid(string hex, byte expected)
    {
        HexString.TryParseByte(hex, out var actual).Should().BeTrue();
        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("gfd")]
    [InlineData("0z")]
    public void TryParseByte_Invalid(string hex)
    {
        HexString.TryParseByte(hex, out var actual).Should().BeFalse();
        actual.Should().Be(0);
    }

    [Theory]
    [InlineData(0, "0000000000000000")]
    [InlineData(123456789, "00000000075bcd15")]
    [InlineData(18446744073709551615, "ffffffffffffffff")]
    public void EncodeToHexString(ulong value, string expected)
    {
        HexString.ToHexString(value).Should().Be(expected);
    }
}
