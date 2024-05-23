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
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    // ...
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    // ...
    [InlineData(9)]
    [InlineData(15)]
    [InlineData(16)]
    // ...
    [InlineData(17)]
    [InlineData(23)]
    [InlineData(24)]
    public void GetCapabilities(int capabilityIndex)
    {
        var byteCount = (capabilityIndex / 8) + 1;
        var expectedBytes = new byte[byteCount];
        var bits = new BitArray(expectedBytes) { [capabilityIndex] = true };
        bits.CopyTo(expectedBytes, 0);
        Array.Reverse(expectedBytes);

        var subscriptionManager = new RcmSubscriptionManager();
        subscriptionManager.SetCapability(1 << capabilityIndex, true);
        subscriptionManager.GetCapabilities().Should().BeEquivalentTo(expectedBytes);
    }
}
