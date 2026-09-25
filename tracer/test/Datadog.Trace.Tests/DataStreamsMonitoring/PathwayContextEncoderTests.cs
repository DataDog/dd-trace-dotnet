// <copyright file="PathwayContextEncoderTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Text;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DataStreamsMonitoring.Hashes;
using Datadog.Trace.ExtensionMethods;
using FluentAssertions;
using FluentAssertions.Extensions;
using Xunit;

namespace Datadog.Trace.Tests.DataStreamsMonitoring;

public class PathwayContextEncoderTests
{
    private static readonly Random Random = new();

    [Fact]
    public void TestRandomValues()
    {
        var nowNs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000;
        for (var i = 0; i < 1000; i++)
        {
            // Mask off the sign bit to guarantee non-negative — Math.Abs(long.MinValue) returns long.MinValue
            // (overflow), which would feed a negative timestamp into EncodeTest and fail the overflow guard.
            EncodeTest(unchecked((ulong)GetLong()), GetNonNegativeLongInThePast(nowNs), GetNonNegativeLongInThePast(nowNs));
        }

        static long GetNonNegativeLongInThePast(long nowNs) => (GetLong() & long.MaxValue) % nowNs;
    }

    [Theory]
    [InlineData(0, 0, 0)]
    public void TestEdgeCases(ulong hash, long pathwayStartNs, long edgeStartNs)
        => EncodeTest(hash, pathwayStartNs, edgeStartNs);

    [Theory]
    [InlineData(ulong.MaxValue, long.MaxValue, long.MaxValue)]
    public void DecoderFailure_OverflowTimestamps(ulong hash, long pathwayStartNs, long edgeStartNs)
    {
        var pathway = new PathwayContext(new PathwayHash(hash), pathwayStartNs, edgeStartNs);

        var bytes = PathwayContextEncoder.Encode(pathway);
        var decoded = PathwayContextEncoder.Decode(bytes);

        decoded.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    public void DecoderFailure_InsufficientBytes(int byteCount)
    {
        // Minimum byte count is 10 bytes:
        // first 8 bytes are the hash (0), 1 byte min for pathway, 1 byte min for edge
        var bytes = new byte[byteCount];

        var decoded = PathwayContextEncoder.Decode(bytes);
        decoded.Should().BeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DecoderFailure_TrailingBytes(bool base64Text)
    {
        // The Base64 text contains two plausible one-byte timestamps at offsets 8 and 9.
        // Ignoring the remaining bytes would accept it as a completely different pathway.
        var bytes = base64Text ? Encoding.UTF8.GetBytes("AAAAAAAABBAAAA==") : new byte[11];

        PathwayContextEncoder.Decode(bytes).Should().BeNull();
#if NETCOREAPP3_1_OR_GREATER
        PathwayContextEncoder.Decode(bytes.AsSpan()).Should().BeNull();
#endif
    }

    [Theory]
    [InlineData("AAAAAAAABBAAAAAAAAAAAAAAAAA=", 0, 0)]
    [InlineData("AAAAAAAABBAC0K+r/vliAAAAAAA=", 1_000_000, 1_700_000_001_000_000_000)]
    [InlineData("AAAAAAAABBCAgICACNCvq/75YgA=", 1_073_741_824_000_000, 1_700_000_001_000_000_000)]
    public void DecoderAcceptsNodeJsPadding(string base64, long pathwayStartNs, long edgeStartNs)
    {
        // Node.js pads these 10-, 15-, and 19-byte encodings to 20 bytes.
        var bytes = Convert.FromBase64String(base64);
        var expected = new PathwayContext(new PathwayHash(0x1004000000000000), pathwayStartNs, edgeStartNs);

        PathwayContextEncoder.Decode(bytes).Should().Be(expected);
#if NETCOREAPP3_1_OR_GREATER
        PathwayContextEncoder.Decode(bytes.AsSpan()).Should().Be(expected);
#endif
    }

    [Theory]
    [InlineData(19, -1)]
    [InlineData(21, -1)]
    [InlineData(20, 10)]
    [InlineData(20, 14)]
    [InlineData(20, 19)]
    public void DecoderFailure_UnexpectedPadding(int byteCount, int nonZeroOffset)
    {
        // A zero hash and two zero timestamps occupy 10 bytes; the rest is padding.
        var bytes = new byte[byteCount];
        if (nonZeroOffset >= 0)
        {
            bytes[nonZeroOffset] = 1;
        }

        PathwayContextEncoder.Decode(bytes).Should().BeNull();
#if NETCOREAPP3_1_OR_GREATER
        PathwayContextEncoder.Decode(bytes.AsSpan()).Should().BeNull();
#endif
    }

    [Fact]
    public void DecoderFailure_InvalidEdgeBytes()
    {
        // first 8 bytes are the hash (0)
        // 9th byte is valid pathway  (0)
        // 10th byte is invalid edge bytes (Pattern 1xxx_xxxx is invalid except in 9th byte)
        var bytes = new byte[10];
        bytes[9] = 0b1000_0000;

        var decoded = PathwayContextEncoder.Decode(bytes);
        decoded.Should().BeNull();
    }

    [Fact]
    public void DecoderFailure_InvalidPathwayBytes()
    {
        // first 8 bytes are the hash (0)
        // 9th byte is invalid pathway (0)
        var bytes = new byte[9];
        bytes[8] = 0b1000_0000;

        var decoded = PathwayContextEncoder.Decode(bytes);
        decoded.Should().BeNull();
    }

    [Fact]
    public void DecoderFailure_OutOfRangeEdgeBytes()
    {
        // first 8 bytes are the hash (0)
        // 9th byte is valid pathway  (0)
        // 10-18 byte is large edge value (ulong.max, can never be written as we truncate)
        var bytes = new byte[18];
        bytes[9] = 0b0111_1111;
        for (var i = 10; i < 18; i++)
        {
            bytes[i] = 0b1111_1111;
        }

        var decoded = PathwayContextEncoder.Decode(bytes);
        decoded.Should().BeNull();
    }

    [Fact]
    public void DecoderFailure_OutOfRangePathwayBytes()
    {
        // first 8 bytes are the hash (0)
        // 9-17 byte is large edge value (ulong.max, can never be written as we truncate)
        // 18 byte is valid edge (0)
        var bytes = new byte[18];
        bytes[8] = 0b0111_1111;
        for (var i = 10; i < 17; i++)
        {
            bytes[i] = 0b1111_1111;
        }

        var decoded = PathwayContextEncoder.Decode(bytes);
        decoded.Should().BeNull();
    }

    [Fact]
    public void DecoderFailure_FutureTimestamps()
    {
        var futureNs = DateTimeOffset.UtcNow
                                     .AddNanoseconds(PathwayContextEncoder.MaxClockSkewNs * 5)
                                     .ToUnixTimeNanoseconds();
        var pathway = new PathwayContext(new PathwayHash(123), pathwayStartNs: futureNs, edgeStartNs: futureNs);

        var bytes = PathwayContextEncoder.Encode(pathway);
        var decoded = PathwayContextEncoder.Decode(bytes);

        decoded.Should().BeNull();
    }

    [Fact]
    public void DecodeSucceeds_TimestampExactlyNow()
    {
        // The check uses strict >, so a timestamp equal to nowNs (within tolerance) must round-trip.
        var nowNs = DateTimeOffset.UtcNow.ToUnixTimeNanoseconds();
        EncodeTest(hash: 123, pathwayStartNs: nowNs, edgeStartNs: nowNs);
    }

    [Fact]
    public void DecodeSucceeds_TimestampWithinSkewTolerance()
    {
        var futureNs = DateTimeOffset.UtcNow
                                     .AddNanoseconds(PathwayContextEncoder.MaxClockSkewNs / 2)
                                     .ToUnixTimeNanoseconds();
        EncodeTest(hash: 123, pathwayStartNs: futureNs, edgeStartNs: futureNs);
    }

#if NETCOREAPP3_1_OR_GREATER
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(12345, 1_000_000_000L, 2_000_000_000L)]
    public void EncodeInto_ProducesSameBytesAsEncode(ulong hash, long pathwayStartNs, long edgeStartNs)
    {
        var pathway = new PathwayContext(new PathwayHash(hash), pathwayStartNs, edgeStartNs);

        var expected = PathwayContextEncoder.Encode(pathway);

        Span<byte> buffer = stackalloc byte[PathwayContextEncoder.MaxEncodedSize];
        var bytesWritten = PathwayContextEncoder.EncodeInto(pathway, buffer);

        buffer.Slice(0, bytesWritten).ToArray().Should().Equal(expected);
    }

    [Fact]
    public void DecoderFailure_FutureTimestamps_Span()
    {
        var futureNs = DateTimeOffset.UtcNow
                                     .AddNanoseconds(PathwayContextEncoder.MaxClockSkewNs * 5)
                                     .ToUnixTimeNanoseconds();
        var pathway = new PathwayContext(new PathwayHash(123), pathwayStartNs: futureNs, edgeStartNs: futureNs);

        Span<byte> buffer = stackalloc byte[PathwayContextEncoder.MaxEncodedSize];
        var bytesWritten = PathwayContextEncoder.EncodeInto(pathway, buffer);

        var decoded = PathwayContextEncoder.Decode(buffer.Slice(0, bytesWritten));

        decoded.Should().BeNull();
    }
#endif

    private static void EncodeTest(ulong hash, long pathwayStartNs, long edgeStartNs)
    {
        var pathway = new PathwayContext(
            new PathwayHash(hash),
            pathwayStartNs: pathwayStartNs,
            edgeStartNs: edgeStartNs);

        var encoded = PathwayContextEncoder.Encode(pathway);

        encoded.Should().NotBeNullOrEmpty();

        var decoded = PathwayContextEncoder.Decode(encoded);

        decoded.Should().NotBeNull();
        // can't compare directly, because encoding and decoding truncates the ns values to be ms
        decoded.Value.Hash.Should().Be(pathway.Hash);
        decoded.Value.EdgeStart.Should().Be((pathway.EdgeStart / 1_000_000) * 1_000_000);
        decoded.Value.PathwayStart.Should().Be((pathway.PathwayStart / 1_000_000) * 1_000_000);
    }

    private static long GetLong()
    {
        var bytes = new byte[8];
        Random.NextBytes(bytes);
        return BitConverter.ToInt64(bytes, 0);
    }
}
