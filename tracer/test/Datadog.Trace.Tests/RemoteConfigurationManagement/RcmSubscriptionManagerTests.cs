// <copyright file="RcmSubscriptionManagerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using Datadog.Trace.RemoteConfigurationManagement;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.RemoteConfigurationManagement;

public class RcmSubscriptionManagerTests
{
    // some values (noted below) can trigger BigInteger.ToByteArray() to an an extra 0x00 byte,
    // so we're testing a few values to make sure the conversion is correct
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    // ...
    [InlineData(6)]
    [InlineData(7)] // 7 is the last index of the first byte
    [InlineData(8)]
    // ...
    [InlineData(14)]
    [InlineData(15)] // 15 is the last index of the second byte
    [InlineData(16)]
    // ...
    [InlineData(22)]
    [InlineData(23)] // 23 is the last index of the third byte
    [InlineData(24)]
    // ...
    [InlineData(30)]
    [InlineData(31)] // 31 is the last index of the fourth byte
    [InlineData(32)]
    public void GetCapabilities(int capabilityIndex)
    {
        var byteCount = (capabilityIndex / 8) + 1;
        var expectedBytes = new byte[byteCount];
        var bits = new BitArray(expectedBytes) { [capabilityIndex] = true };
        bits.CopyTo(expectedBytes, 0);
        Array.Reverse(expectedBytes);

        var subscriptionManager = new RcmSubscriptionManager();
        subscriptionManager.SetCapability(1UL << capabilityIndex, true);
        subscriptionManager.GetCapabilities().Should().BeEquivalentTo(expectedBytes);
    }
}
