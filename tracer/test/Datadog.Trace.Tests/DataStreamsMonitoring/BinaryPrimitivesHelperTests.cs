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
    [Fact]
    public void CanRoundTripValue()
    {
        var random = new Random();
        var bytes = new byte[8];
        var expected = GetULong();
        BinaryPrimitivesHelper.WriteUInt64LittleEndian(bytes, expected);
        var actual = BinaryPrimitivesHelper.ReadUInt64LittleEndian(bytes);

        actual.Should().Be(expected);

        ulong GetULong()
        {
            var bytes = new byte[8];
            random.NextBytes(bytes);
            return BitConverter.ToUInt64(bytes, 0);
        }
    }
}
