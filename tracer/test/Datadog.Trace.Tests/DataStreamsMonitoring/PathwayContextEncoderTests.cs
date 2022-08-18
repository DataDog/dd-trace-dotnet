// <copyright file="PathwayContextEncoderTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DataStreamsMonitoring.Hashes;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.DataStreamsMonitoring;

public class PathwayContextEncoderTests
{
    private static readonly Random Random = new();

    [Fact]
    public void TestRandomValues()
    {
        for (var i = 0; i < 1000; i++)
        {
            EncodeTest(unchecked((ulong)GetLong()), Math.Abs(GetLong()), Math.Abs(GetLong()));
        }
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(ulong.MaxValue, long.MaxValue, long.MaxValue)]
    [InlineData(ulong.MinValue, long.MinValue, long.MinValue)]
    public void TestEdgeCases(ulong hash, long pathwayStartNs, long edgeStartNs)
        => EncodeTest(hash, pathwayStartNs, edgeStartNs);

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
        decoded.Value.EdgeStart.Should().Be((pathway.EdgeStart / 1000) * 1000);
        decoded.Value.PathwayStart.Should().Be((pathway.PathwayStart / 1000) * 1000);
    }

    private static long GetLong()
    {
        var bytes = new byte[8];
        Random.NextBytes(bytes);
        return BitConverter.ToInt64(bytes, 0);
    }
}
