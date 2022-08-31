// <copyright file="VarEncodingHelperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Datadog.Trace.DataStreamsMonitoring;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Datadog.Trace.Tests.DataStreamsMonitoring;

public class VarEncodingHelperTests
{
    public static IEnumerable<object[]> UnsignedVarLongs() =>
        new List<object[]>
        {
            Unsigned(0L, 0x00),
            Unsigned(1, 0x01),
            Unsigned(127, 0x7F),
            Unsigned(128, 0x80, 0x01),
            Unsigned(129, 0x81, 0x01),
            Unsigned(255, 0xFF, 0x01),
            Unsigned(256, 0x80, 0x02),
            Unsigned(16383, 0xFF, 0x7F),
            Unsigned(16384, 0x80, 0x80, 0x01),
            Unsigned(16385, 0x81, 0x80, 0x01),
            Unsigned(35_459_249_995_776, 0x80, 0x80, 0x80, 0x80, 0x80, 0x88, 0x08),
            Unsigned(unchecked((ulong)-2), 0xFE, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF),
            Unsigned(unchecked((ulong)-1), 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF),
        };

    public static IEnumerable<object[]> SignedVarLongs() =>
        new List<object[]>
        {
            Signed(0L, 0x00),
            Signed(1L, 0x02),
            Signed(63L, 0x7E),
            Signed(64L, 0x80, 0x01),
            Signed(65L, 0x82, 0x01),
            Signed(127L, 0xFE, 0x01),
            Signed(128L, 0x80, 0x02),
            Signed(8191L, 0xFE, 0x7F),
            Signed(8192L, 0x80, 0x80, 0x01),
            Signed(8193L, 0x82, 0x80, 0x01),
            Signed((long.MaxValue >> 1) - 1L, 0xFC, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F),
            Signed(long.MaxValue >> 1, 0xFE, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F),
            Signed((long.MaxValue >> 1) + 1L, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80),
            Signed(long.MaxValue - 1L, 0xFC, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF),
            Signed(long.MaxValue, 0xFE, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF),
            Signed(-1L, 0x01),
            Signed(-63L, 0x7D),
            Signed(-64L, 0x7F),
            Signed(-65L, 0x81, 0x01),
            Signed(-127L, 0xFD, 0x01),
            Signed(-128L, 0xFF, 0x01),
            Signed(-8191L, 0xFD, 0x7F),
            Signed(-8192L, 0xFF, 0x7F),
            Signed(-8193L, 0x81, 0x80, 0x01),
            Signed((long.MinValue >> 1) + 1L, 0xFD, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F),
            Signed(long.MinValue >> 1, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F),
            Signed((long.MinValue >> 1) - 1L, 0x81, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80),
            Signed(long.MinValue + 1L, 0xFD, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF),
            Signed(long.MinValue, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF),
        };

    public static IEnumerable<object[]> UnsignedVarInts() =>
        new List<object[]>
        {
            UnsignedInt(0, 0x00),
            UnsignedInt(1, 0x01),
            UnsignedInt(127, 0x7F),
            UnsignedInt(128, 0x80, 0x01),
            UnsignedInt(129, 0x81, 0x01),
            UnsignedInt(255, 0xFF, 0x01),
            UnsignedInt(256, 0x80, 0x02),
            UnsignedInt(16383, 0xFF, 0x7F),
            UnsignedInt(16384, 0x80, 0x80, 0x01),
            UnsignedInt(16385, 0x81, 0x80, 0x01),
        };

    public static IEnumerable<object[]> SignedVarInts() =>
        new List<object[]>
        {
            SignedInt(0, 0x00),
            SignedInt(1, 0x02),
            SignedInt(63, 0x7E),
            SignedInt(64, 0x80, 0x01),
            SignedInt(65, 0x82, 0x01),
            SignedInt(127, 0xFE, 0x01),
            SignedInt(128, 0x80, 0x02),
            SignedInt(8191, 0xFE, 0x7F),
            SignedInt(8192, 0x80, 0x80, 0x01),
            SignedInt(8193, 0x82, 0x80, 0x01),
            SignedInt((int.MaxValue >> 1) - 1, 0xFC, 0xFF, 0xFF, 0xFF, 0x07),
            SignedInt(int.MaxValue >> 1, 0xFE, 0xFF, 0xFF, 0xFF, 0x07),
            SignedInt((int.MaxValue >> 1) + 1, 0x80, 0x80, 0x80, 0x80, 0x08),
            SignedInt(int.MaxValue - 1, 0xFC, 0xFF, 0xFF, 0xFF, 0x0F),
            SignedInt(int.MaxValue, 0xFE, 0xFF, 0xFF, 0xFF, 0x0F),
            SignedInt(-1, 0x01),
            SignedInt(-63, 0x7D),
            SignedInt(-64, 0x7F),
            SignedInt(-65, 0x81, 0x01),
            SignedInt(-127, 0xFD, 0x01),
            SignedInt(-128, 0xFF, 0x01),
            SignedInt(-8191, 0xFD, 0x7F),
            SignedInt(-8192, 0xFF, 0x7F),
            SignedInt(-8193, 0x81, 0x80, 0x01),
            SignedInt((int.MinValue >> 1) + 1, 0xFD, 0xFF, 0xFF, 0xFF, 0x07),
            SignedInt(int.MinValue >> 1, 0xFF, 0xFF, 0xFF, 0xFF, 0x07),
            SignedInt((int.MinValue >> 1) - 1, 0x81, 0x80, 0x80, 0x80, 0x08),
            SignedInt(int.MinValue + 1, 0xFD, 0xFF, 0xFF, 0xFF, 0x0F),
            SignedInt(int.MinValue, 0xFF, 0xFF, 0xFF, 0xFF, 0x0F),
        };

    public static IEnumerable<object[]> BadBytes() =>
        new List<object[]>
        {
            new object[] { new byte[] { 0b1000_0000 } },
            new object[] { new byte[] { 0b1111_1111 } },
            new object[] { new byte[] { 0b1000_0000, 0b1000_0000 } },
        };

    [Theory]
    [MemberData(nameof(UnsignedVarLongs))]
    public void WriteVarLong_BytesTest(ulong value, byte[] encoded)
    {
        var bytes = new byte[encoded.Length];
        var bytesWritten = VarEncodingHelper.WriteVarLong(bytes, 0, value);

        using var s = new AssertionScope();
        bytesWritten.Should().Be(bytes.Length);
        bytes.Should().Equal(encoded);
    }

    [Theory]
    [MemberData(nameof(UnsignedVarLongs))]
    public void WriteVarLong_BinaryWriterTest(ulong value, byte[] encoded)
    {
        var bytes = new byte[encoded.Length];
        using var ms = new MemoryStream(bytes);
        using var writer = new BinaryWriter(ms, Encoding.UTF8);
        var bytesWritten = VarEncodingHelper.WriteVarLong(writer, value);

        using var s = new AssertionScope();
        bytesWritten.Should().Be(bytes.Length);
        bytes.Should().Equal(encoded);
    }

#if NETCOREAPP3_1_OR_GREATER
    [Theory]
    [MemberData(nameof(UnsignedVarLongs))]
    public void WriteVarLong_SpanTest(ulong value, byte[] encoded)
    {
        Span<byte> bytes = stackalloc byte[encoded.Length];
        var bytesWritten = VarEncodingHelper.WriteVarLong(bytes, value);

        using var s = new AssertionScope();
        bytesWritten.Should().Be(bytes.Length);
        for (var i = 0; i < encoded.Length; i++)
        {
            var b = encoded[i];
            bytes[i].Should().Be(b, $"the byte at {i} should be equal");
        }
    }
#endif

    [Theory]
    [MemberData(nameof(UnsignedVarLongs))]
    public void ReadVarLong_BytesTest(ulong expected, byte[] encoded)
    {
        var actual = VarEncodingHelper.ReadVarLong(encoded, 0, out var bytesRead);
        actual.Should().Be(expected);
        bytesRead.Should().Be(encoded.Length);
    }

    [Theory]
    [MemberData(nameof(BadBytes))]
    public void ReadVarLong_BytesTest_Error(byte[] bytes)
    {
        VarEncodingHelper.ReadVarLong(bytes, 0, out _).Should().BeNull();
    }

#if NETCOREAPP3_1_OR_GREATER
    [Theory]
    [MemberData(nameof(UnsignedVarLongs))]
    public void ReadVarLong_SpanTest(ulong expected, byte[] encoded)
    {
        var actual = VarEncodingHelper.ReadVarLong(encoded.AsSpan(), out var bytesRead);
        actual.Should().Be(expected);
        bytesRead.Should().Be(encoded.Length);
    }

    [Theory]
    [MemberData(nameof(BadBytes))]
    public void ReadVarLong_SpanTest_Error(byte[] bytes)
    {
        VarEncodingHelper.ReadVarLong(bytes.AsSpan(), out _).Should().BeNull();
    }
#endif

    [Theory]
    [MemberData(nameof(SignedVarLongs))]
    public void WriteVarLongZigZag_BytesTest(long value, byte[] encoded)
    {
        var bytes = new byte[encoded.Length];
        var bytesWritten = VarEncodingHelper.WriteVarLongZigZag(bytes, 0, value);

        using var s = new AssertionScope();
        bytesWritten.Should().Be(bytes.Length);
        bytes.Should().Equal(encoded);
    }

    [Theory]
    [MemberData(nameof(SignedVarLongs))]
    public void WriteVarLongZigZag_BinaryWriterTest(long value, byte[] encoded)
    {
        var bytes = new byte[encoded.Length];
        using var ms = new MemoryStream(bytes);
        using var writer = new BinaryWriter(ms, Encoding.UTF8);
        var bytesWritten = VarEncodingHelper.WriteVarLongZigZag(writer, value);

        using var s = new AssertionScope();
        bytesWritten.Should().Be(bytes.Length);
        bytes.Should().Equal(encoded);
    }

#if NETCOREAPP3_1_OR_GREATER
    [Theory]
    [MemberData(nameof(SignedVarLongs))]
    public void WriteVarLongZigZag_SpanTest(long value, byte[] encoded)
    {
        Span<byte> bytes = stackalloc byte[encoded.Length];
        var bytesWritten = VarEncodingHelper.WriteVarLongZigZag(bytes, value);

        using var s = new AssertionScope();
        bytesWritten.Should().Be(bytes.Length);
        for (var i = 0; i < encoded.Length; i++)
        {
            var b = encoded[i];
            bytes[i].Should().Be(b, $"the byte at {i} should be equal");
        }
    }
#endif

    [Theory]
    [MemberData(nameof(SignedVarLongs))]
    public void ReadVarLongZigZag_BytesTest(long expected, byte[] encoded)
    {
        var actual = VarEncodingHelper.ReadVarLongZigZag(encoded, 0, out var bytesRead);
        actual.Should().Be(expected);
        bytesRead.Should().Be(encoded.Length);
    }

    [Theory]
    [MemberData(nameof(BadBytes))]
    public void ReadVarLongZigZag_BytesTest_Error(byte[] bytes)
    {
        VarEncodingHelper.ReadVarLong(bytes, 0, out _).Should().BeNull();
    }

#if NETCOREAPP3_1_OR_GREATER
    [Theory]
    [MemberData(nameof(SignedVarLongs))]
    public void ReadVarLongZigZag_SpanTest(long expected, byte[] encoded)
    {
        var actual = VarEncodingHelper.ReadVarLongZigZag(encoded.AsSpan(), out var bytesRead);
        actual.Should().Be(expected);
        bytesRead.Should().Be(encoded.Length);
    }

    [Theory]
    [MemberData(nameof(BadBytes))]
    public void ReadVarLongZigZag_SpanTest_Error(byte[] bytes)
    {
        VarEncodingHelper.ReadVarLong(bytes.AsSpan(), out _).Should().BeNull();
    }
#endif

    [Theory]
    [MemberData(nameof(UnsignedVarInts))]
    public void WriteVarInt_BytesTest(uint value, byte[] encoded)
    {
        var bytes = new byte[encoded.Length];
        var bytesWritten = VarEncodingHelper.WriteVarInt(bytes, 0, value);

        using var s = new AssertionScope();
        bytesWritten.Should().Be(bytes.Length);
        bytes.Should().Equal(encoded);
    }

    [Theory]
    [MemberData(nameof(UnsignedVarInts))]
    public void WriteVarInt_BinaryWriterTest(uint value, byte[] encoded)
    {
        var bytes = new byte[encoded.Length];
        using var ms = new MemoryStream(bytes);
        using var writer = new BinaryWriter(ms, Encoding.UTF8);
        var bytesWritten = VarEncodingHelper.WriteVarInt(writer, value);

        using var s = new AssertionScope();
        bytesWritten.Should().Be(bytes.Length);
        bytes.Should().Equal(encoded);
    }

#if NETCOREAPP3_1_OR_GREATER
    [Theory]
    [MemberData(nameof(UnsignedVarInts))]
    public void WriteVarInt_SpanTest(uint value, byte[] encoded)
    {
        Span<byte> bytes = stackalloc byte[encoded.Length];
        var bytesWritten = VarEncodingHelper.WriteVarInt(bytes, value);

        using var s = new AssertionScope();
        bytesWritten.Should().Be(bytes.Length);
        for (var i = 0; i < encoded.Length; i++)
        {
            var b = encoded[i];
            bytes[i].Should().Be(b, $"the byte at {i} should be equal");
        }
    }
#endif

    [Theory]
    [MemberData(nameof(UnsignedVarInts))]
    public void ReadVarInt_BytesTest(uint expected, byte[] encoded)
    {
        var actual = VarEncodingHelper.ReadVarInt(encoded, 0, out var bytesRead);
        actual.Should().Be(expected);
        bytesRead.Should().Be(encoded.Length);
    }

    [Theory]
    [MemberData(nameof(BadBytes))]
    public void ReadVarInt_BytesTest_Error(byte[] bytes)
    {
        VarEncodingHelper.ReadVarInt(bytes, 0, out _).Should().BeNull();
    }

#if NETCOREAPP3_1_OR_GREATER
    [Theory]
    [MemberData(nameof(UnsignedVarInts))]
    public void ReadVarInt_SpanTest(uint expected, byte[] encoded)
    {
        var actual = VarEncodingHelper.ReadVarInt(encoded.AsSpan(), out var bytesRead);
        actual.Should().Be(expected);
        bytesRead.Should().Be(encoded.Length);
    }

    [Theory]
    [MemberData(nameof(BadBytes))]
    public void ReadVarInt_SpanTest_Error(byte[] bytes)
    {
        VarEncodingHelper.ReadVarInt(bytes.AsSpan(), out _).Should().BeNull();
    }
#endif

    [Theory]
    [MemberData(nameof(SignedVarInts))]
    public void WriteVarIntZigZag_BytesTest(int value, byte[] encoded)
    {
        var bytes = new byte[encoded.Length];
        var bytesWritten = VarEncodingHelper.WriteVarIntZigZag(bytes, 0, value);

        using var s = new AssertionScope();
        bytesWritten.Should().Be(bytes.Length);
        bytes.Should().Equal(encoded);
    }

    [Theory]
    [MemberData(nameof(SignedVarInts))]
    public void WriteVarIntZigZag_BinaryWriterTest(int value, byte[] encoded)
    {
        var bytes = new byte[encoded.Length];
        using var ms = new MemoryStream(bytes);
        using var writer = new BinaryWriter(ms, Encoding.UTF8);
        var bytesWritten = VarEncodingHelper.WriteVarIntZigZag(writer, value);

        using var s = new AssertionScope();
        bytesWritten.Should().Be(bytes.Length);
        bytes.Should().Equal(encoded);
    }

#if NETCOREAPP3_1_OR_GREATER
    [Theory]
    [MemberData(nameof(SignedVarInts))]
    public void WriteVarIntZigZag_SpanTest(int value, byte[] encoded)
    {
        Span<byte> bytes = stackalloc byte[encoded.Length];
        var bytesWritten = VarEncodingHelper.WriteVarIntZigZag(bytes, value);

        using var s = new AssertionScope();
        bytesWritten.Should().Be(bytes.Length);
        for (var i = 0; i < encoded.Length; i++)
        {
            var b = encoded[i];
            bytes[i].Should().Be(b, $"the byte at {i} should be equal");
        }
    }
#endif

    [Theory]
    [MemberData(nameof(SignedVarInts))]
    public void ReadVarIntZigZag_BytesTest(int expected, byte[] encoded)
    {
        var actual = VarEncodingHelper.ReadVarIntZigZag(encoded, 0, out var bytesRead);
        actual.Should().Be(expected);
        bytesRead.Should().Be(encoded.Length);
    }

    [Theory]
    [MemberData(nameof(BadBytes))]
    public void ReadVarIntZigZag_BytesTest_Error(byte[] bytes)
    {
        VarEncodingHelper.ReadVarIntZigZag(bytes, 0, out _).Should().BeNull();
    }

#if NETCOREAPP3_1_OR_GREATER
    [Theory]
    [MemberData(nameof(SignedVarInts))]
    public void ReadVarIntZigZag_SpanTest(int expected, byte[] encoded)
    {
        var actual = VarEncodingHelper.ReadVarIntZigZag(encoded.AsSpan(), out var bytesRead);
        actual.Should().Be(expected);
        bytesRead.Should().Be(encoded.Length);
    }

    [Theory]
    [MemberData(nameof(BadBytes))]
    public void ReadVarIntZigZag_SpanTest_Error(byte[] bytes)
    {
        VarEncodingHelper.ReadVarIntZigZag(bytes.AsSpan(), out _).Should().BeNull();
    }
#endif

    [Theory]
    [MemberData(nameof(UnsignedVarLongs))]
    public void VarLongLengthTest(ulong value, byte[] encoded)
    {
        VarEncodingHelper.VarLongLength(value).Should().Be(encoded.Length);
    }

    [Theory]
    [MemberData(nameof(SignedVarLongs))]
    public void VarLongZigZagLengthTest(long value, byte[] encoded)
    {
        VarEncodingHelper.VarLongZigZagLength(value).Should().Be(encoded.Length);
    }

    [Theory]
    [MemberData(nameof(SignedVarInts))]
    public void VarIntZigZagLengthTest(int value, byte[] encoded)
    {
        VarEncodingHelper.VarIntZigZagLength(value).Should().Be(encoded.Length);
    }

    private static object[] Unsigned(ulong value, params byte[] bytes) => new object[] { value, bytes };

    private static object[] Signed(long value, params byte[] bytes) => new object[] { value, bytes };

    private static object[] UnsignedInt(int value, params byte[] bytes) => new object[] { value, bytes };

    private static object[] SignedInt(int value, params byte[] bytes) => new object[] { value, bytes };
}
