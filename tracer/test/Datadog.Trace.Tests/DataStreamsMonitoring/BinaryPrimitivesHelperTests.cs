// <copyright file="BinaryPrimitivesHelperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DataStreamsMonitoring.Utils;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.DataStreamsMonitoring;

public class BinaryPrimitivesHelperTests
{
    private static readonly Random Random = new();

    [Fact]
    public void CanRoundTripValue()
    {
        var bytes = new byte[8];
        var expected = GetULong();
        BinaryPrimitivesHelper.WriteUInt64LittleEndian(bytes, expected);
        var actual = BinaryPrimitivesHelper.ReadUInt64LittleEndian(bytes);

        actual.Should().Be(expected);

        ulong GetULong()
        {
            var bytes = new byte[8];
            Random.NextBytes(bytes);
            return BitConverter.ToUInt64(bytes, 0);
        }
    }

    [Fact]
    public void CanRoundTripValueWithZeroLowerByte()
    {
        var bytes = new byte[8];
        var expected = GetZeroedUlong();

        BinaryPrimitivesHelper.WriteUInt64LittleEndian(bytes, expected);
        bytes[0].Should().Be(0);

        var actual = BinaryPrimitivesHelper.ReadUInt64LittleEndian(bytes);

        actual.Should().Be(expected);

        ulong GetZeroedUlong()
        {
            var bytes = new byte[8];
            Random.NextBytes(bytes);
            bytes[0] = 0x00;
            return BitConverter.ToUInt64(bytes, 0);
        }
    }
}
