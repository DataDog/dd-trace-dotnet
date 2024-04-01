// <copyright file="HexStringTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Util;

public class HexStringTests
{
    public static TheoryData<byte[], bool, string> BytesToString => new()
    {
        { [], /* lowerCase */ true,  string.Empty },
        { [], /* lowerCase */ false, string.Empty },
        { [0x01, 0x02, 0xab], /* lowerCase */ true,  "0102ab" },
        { [0x01, 0x02, 0xab], /* lowerCase */ false, "0102AB" },
        { [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00], /* lowerCase */ true,  "0000000000000000" },
        { [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00], /* lowerCase */ false, "0000000000000000" },
        { [0x12, 0x34, 0x56, 0x78, 0x90, 0xab, 0xcd, 0xef], /* lowerCase */ true,  "1234567890abcdef" },
        { [0x12, 0x34, 0x56, 0x78, 0x90, 0xab, 0xcd, 0xef], /* lowerCase */ false, "1234567890ABCDEF" },
        { [0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff], /* lowerCase */ true,  "ffffffffffffffff" },
        { [0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff], /* lowerCase */ false, "FFFFFFFFFFFFFFFF" },
    };

    [Theory]
    [MemberData(nameof(BytesToString))]
    public void ToHexChars(byte[] bytes, bool lowerCase, string expected)
    {
        var actual = new char[bytes.Length * 2];
        HexString.ToHexChars(new ArraySegment<byte>(bytes, offset: 0, count: bytes.Length), actual, lowerCase);

        actual.Should().BeEquivalentTo(expected.ToCharArray());
    }

    [Theory]
    [MemberData(nameof(BytesToString))]
    public void ToHexString_Bytes(byte[] bytes, bool lowerCase, string expected)
    {
        var actual = HexString.ToHexString(bytes, lowerCase);
        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData(0x0000000000000000, /* lowerCase */ true,  "0000000000000000")]
    [InlineData(0x0000000000000000, /* lowerCase */ false, "0000000000000000")]
    [InlineData(0x1234567890abcdef, /* lowerCase */ true,  "1234567890abcdef")]
    [InlineData(0x1234567890abcdef, /* lowerCase */ false, "1234567890ABCDEF")]
    [InlineData(0xffffffffffffffff, /* lowerCase */ true,  "ffffffffffffffff")]
    [InlineData(0xffffffffffffffff, /* lowerCase */ false, "FFFFFFFFFFFFFFFF")]
    public void ToHexString_UInt64(ulong value, bool lowerCase, string expected)
    {
        var actual = HexString.ToHexString(value, lowerCase);
        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData(0x0000000000000000, 0x0000000000000000, /* lowerCase */ true,  "00000000000000000000000000000000")]
    [InlineData(0x1234567890abcdef, 0x1122334455667788, /* lowerCase */ true,  "1234567890abcdef1122334455667788")]
    [InlineData(0x1234567890abcdef, 0x1122334455667788, /* lowerCase */ false, "1234567890ABCDEF1122334455667788")]
    [InlineData(0xffffffffffffffff, 0xffffffffffffffff, /* lowerCase */ true,  "ffffffffffffffffffffffffffffffff")]
    [InlineData(0xffffffffffffffff, 0xffffffffffffffff, /* lowerCase */ false, "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF")]
    public void ToHexString_TraceId128(ulong upper, ulong lower, bool lowerCase, string expected)
    {
        var traceId = new TraceId(upper, lower);
        var actual = HexString.ToHexString(traceId, pad16To32: true, lowerCase);
        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData(0x0000000000000000, 0x0000000000000000, /* pad16To32 */ true,  "00000000000000000000000000000000")]
    [InlineData(0x0000000000000000, 0x0000000000000000, /* pad16To32 */ false, "0000000000000000")]
    [InlineData(0x0000000000000000, 0x1234567890abcdef, /* pad16To32 */ true,  "00000000000000001234567890abcdef")]
    [InlineData(0x0000000000000000, 0x1234567890abcdef, /* pad16To32 */ false, "1234567890abcdef")]
    [InlineData(0x0000000000000000, 0xffffffffffffffff, /* pad16To32 */ true,  "0000000000000000ffffffffffffffff")]
    [InlineData(0x0000000000000000, 0xffffffffffffffff, /* pad16To32 */ false, "ffffffffffffffff")]
    [InlineData(0xffffffffffffffff, 0x0000000000000000, /* pad16To32 */ true,  "ffffffffffffffff0000000000000000")]
    [InlineData(0xffffffffffffffff, 0x0000000000000000, /* pad16To32 */ false, "ffffffffffffffff0000000000000000")]
    public void ToHexString_TraceId64(ulong upper, ulong lower, bool pad16To32, string expected)
    {
        var traceId = new TraceId(upper, lower);
        var actual = HexString.ToHexString(traceId, pad16To32);
        actual.Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(BytesToString))]
    public void TryParseBytes_ValidString(byte[] expected, bool lowerCase, string hex)
    {
        _ = lowerCase; // analyzer will complain if not used

        var actual = new byte[hex.Length / 2];
        HexString.TryParseBytes(hex, actual).Should().BeTrue();
        actual.Should().BeEquivalentTo(expected);
    }

    [Theory]
    // null
    [InlineData(null, 0)]
    // invalid chars
    [InlineData("gh", 1)]
    [InlineData("12abxy", 3)]
    [InlineData("12  ab", 3)]
    // invalid string length (odd)
    [InlineData("0", 0)]
    [InlineData("0", 1)]
    [InlineData("12a", 1)]
    [InlineData("12a", 2)]
    // invalid buffer length (not hex.Length / 2)
    [InlineData("12ab", 1)]
    [InlineData("12ab", 3)]
    public void TryParseBytes_InvalidStringLength(string hex, int bufferLength)
    {
        var actual = new byte[bufferLength];
        HexString.TryParseBytes(hex, actual).Should().BeFalse();
    }

#if NETCOREAPP3_1_OR_GREATER
    [Theory]
    [MemberData(nameof(BytesToString))]
    public void TryParseBytes_ValidSpan(byte[] expected, bool lowerCase, string hex)
    {
        _ = lowerCase; // analyzer will complain if not used

        ReadOnlySpan<char> chars = hex;
        var actual = new byte[hex.Length / 2];

        HexString.TryParseBytes(hex, actual).Should().BeTrue();
        actual.Should().BeEquivalentTo(expected);
    }

    [Theory]
    // invalid chars
    [InlineData("gh", 1)]
    [InlineData("12abxy", 3)]
    [InlineData("12  ab", 3)]
    // invalid string length (odd)
    [InlineData("0", 0)]
    [InlineData("0", 1)]
    [InlineData("12a", 1)]
    [InlineData("12a", 2)]
    // invalid buffer length (not hex.Length / 2)
    [InlineData("12ab", 1)]
    [InlineData("12ab", 3)]
    public void TryParseBytes_InvalidSpanLength(string hex, int bufferLength)
    {
        ReadOnlySpan<char> chars = hex;
        var actual = new byte[bufferLength];

        HexString.TryParseBytes(chars, actual).Should().BeFalse();
    }
#endif

    [Theory]
    // 64-bits trace ids
    [InlineData("0000000000000000", 0, 0x0000000000000000)]
    [InlineData("1234567890abcdef", 0, 0x1234567890abcdef)]
    [InlineData("1234567890ABCDEF", 0, 0x1234567890abcdef)]
    [InlineData("ffffffffffffffff", 0, 0xffffffffffffffff)]
    [InlineData("FFFFFFFFFFFFFFFF", 0, 0xffffffffffffffff)]
    // 128-bits trace ids
    [InlineData("00000000000000000000000000000000", 0x0000000000000000, 0x0000000000000000)]
    [InlineData("1234567890abcdef1122334455667788", 0x1234567890abcdef, 0x1122334455667788)]
    [InlineData("1234567890ABCDEF1122334455667788", 0x1234567890abcdef, 0x1122334455667788)]
    [InlineData("ffffffffffffffffffffffffffffffff", 0xffffffffffffffff, 0xffffffffffffffff)]
    [InlineData("FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF", 0xffffffffffffffff, 0xffffffffffffffff)]
    public void TryParseTraceId_Valid(string hex, ulong expectedUpper, ulong expectedLower)
    {
        HexString.TryParseTraceId(hex, out var actual).Should().BeTrue();
        actual.Should().Be(new TraceId(expectedUpper, expectedLower));
    }

    [Theory]
    // null
    [InlineData(null)]
    // invalid chars
    [InlineData("0000000000000000000000000000000z")]
    [InlineData("y0000000000000000000000000000000")]
    [InlineData("000000000000000 0000000000000000")]
    [InlineData("000000000000000-0000000000000000")]
    // invalid string length (not 16 or 32)
    [InlineData("")]                                  // length:  0
    [InlineData("000000000000000")]                   // length: 15
    [InlineData("00000000000000000")]                 // length: 17
    [InlineData("0000000000000000000000000000000")]   // length: 31
    [InlineData("000000000000000000000000000000000")] // length: 33
    // "0x" prefix
    [InlineData("0x00000000000000")]                   // prefix + 14 chars (valid length, but invalid chars)
    [InlineData("0x0000000000000000")]                 // prefix + 16 chars (both invalid length and chars)
    [InlineData("0x000000000000000000000000000000")]   // prefix + 30 chars (valid length, but invalid chars)
    [InlineData("0x00000000000000000000000000000000")] // prefix + 32 chars (both invalid length and chars)
    public void TryParseTraceId_Invalid(string hex)
    {
        HexString.TryParseTraceId(hex, out var actual).Should().BeFalse();
        actual.Should().Be(new TraceId(0, 0));
    }

    [Theory]
    [InlineData("0000000000000000", 0x0000000000000000)]
    [InlineData("1234567890abcdef", 0x1234567890abcdef)]
    [InlineData("1234567890ABCDEF", 0x1234567890abcdef)]
    [InlineData("ffffffffffffffff", 0xffffffffffffffff)]
    [InlineData("FFFFFFFFFFFFFFFF", 0xffffffffffffffff)]
    public void TryParseUInt64_Valid(string hex, ulong expected)
    {
        HexString.TryParseUInt64(hex, out var actual).Should().BeTrue();
        actual.Should().Be(expected);
    }

    [Theory]
    // null
    [InlineData(null)]
    // invalid chars
    [InlineData("000000000000000z")]
    [InlineData("y000000000000000")]
    [InlineData("000000 00000000")]
    [InlineData("0x0000000000000")]
    // invalid string length (not 16)
    [InlineData("")]
    [InlineData("000000000000000")]
    [InlineData("00000000000000000")]
    public void TryParseUInt64_Invalid(string hex)
    {
        HexString.TryParseUInt64(hex, out var actual).Should().BeFalse();
        actual.Should().Be(0);
    }

    [Theory]
    [InlineData("00", 0x00)]
    [InlineData("12", 0x12)]
    [InlineData("ab", 0xab)]
    [InlineData("AB", 0xab)]
    [InlineData("ff", 0xff)]
    [InlineData("FF", 0xff)]
    public void TryParseByte_Valid(string hex, byte expected)
    {
        HexString.TryParseByte(hex, out var actual).Should().BeTrue();
        actual.Should().Be(expected);
    }

    [Theory]
    // null
    [InlineData(null)]
    // // invalid chars
    [InlineData("0x")]
    [InlineData(" 0")]
    [InlineData("0 ")]
    // invalid string length (not 2)
    [InlineData("")]
    [InlineData("0")]
    [InlineData("12a")]
    public void TryParseByte_Invalid(string hex)
    {
        HexString.TryParseByte(hex, out var actual).Should().BeFalse();
        actual.Should().Be(0);
    }
}
