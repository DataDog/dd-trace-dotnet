// <copyright file="ULeb128EncoderTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.FeatureFlags;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.FeatureFlags;

/// <summary>
/// Input/output tests for the frozen FFE ULEB128 delta-varint + base64 codec: dedupe → sort →
/// delta-from-previous → unsigned LEB128 → base64. Output must stay byte-identical across SDKs.
/// </summary>
public class ULeb128EncoderTests
{
    private const string GoldenBase64 = "ZAgUAg==";

    [Fact]
    public void GoldenVector_EncodesToZAgUAg()
    {
        // {100,108,128,130} -> deltas [100,8,20,2] -> ULEB128 [0x64,0x08,0x14,0x02] -> base64 "ZAgUAg=="
        ULeb128Encoder.EncodeDeltaVarint(new long[] { 100, 108, 128, 130 }).Should().Be(GoldenBase64);
    }

    [Fact]
    public void Empty_ReturnsEmptyString()
    {
        ULeb128Encoder.EncodeDeltaVarint(new long[0]).Should().Be(string.Empty);
    }

    [Fact]
    public void RoundTrip_DecodesBackToSortedDedupedIds()
    {
        var input = new long[] { 130, 100, 108, 128, 100 }; // unsorted + duplicate
        var encoded = ULeb128Encoder.EncodeDeltaVarint(input);
        encoded.Should().Be(GoldenBase64);

        DecodeDeltaVarint(encoded).Should().Equal(new long[] { 100, 108, 128, 130 });
    }

    [Fact]
    public void MultiByteVarint_RoundTrips()
    {
        // 200 needs 2 ULEB128 bytes (0xC8 0x01); exercise the continuation-bit path.
        var input = new long[] { 5, 205, 500 };
        DecodeDeltaVarint(ULeb128Encoder.EncodeDeltaVarint(input)).Should().Equal(input);
    }

    // Decode side mirrors the cross-SDK codec (system-tests test_ffe/utils.py) — the round-trip oracle.
    private static List<long> DecodeDeltaVarint(string base64)
    {
        var result = new List<long>();
        if (string.IsNullOrEmpty(base64))
        {
            return result;
        }

        var bytes = System.Convert.FromBase64String(base64);
        long prev = 0;
        var i = 0;
        while (i < bytes.Length)
        {
            long value = 0;
            var shift = 0;
            while (true)
            {
                var b = bytes[i++];
                value |= (long)(b & 0x7F) << shift;
                if ((b & 0x80) == 0)
                {
                    break;
                }

                shift += 7;
            }

            prev += value;
            result.Add(prev);
        }

        return result;
    }
}
