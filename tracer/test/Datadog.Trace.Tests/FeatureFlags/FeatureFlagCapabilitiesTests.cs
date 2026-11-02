// <copyright file="FeatureFlagCapabilitiesTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Numerics;
using Datadog.Trace.FeatureFlags;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.FeatureFlags;

public class FeatureFlagCapabilitiesTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("unknown-v1", false)]
    [InlineData("dependent-flag-variant-key-v1", true)]
    public void GatesBothTransports(string? capability, bool supported)
    {
        var capabilities = capability is null ? Array.Empty<string>() : new[] { capability };
        FeatureFlagCapabilities.HeaderValue(capabilities).Should().Be(supported ? "dependent-flag-variant-key-v1" : null);
        FeatureFlagCapabilities.RemoteConfigMask(capabilities).Should().Be(new BigInteger((1UL << 46) | (supported ? 1UL << 50 : 0)));
    }

    [Fact]
    public void CurrentEvaluatorDoesNotAdvertiseDependentFlags()
    {
        FeatureFlagsEvaluator.GetCapabilities().Should().BeEmpty();
        FeatureFlagCapabilities.HeaderValue(FeatureFlagsEvaluator.GetCapabilities()).Should().BeNull();
        FeatureFlagCapabilities.RemoteConfigMask(FeatureFlagsEvaluator.GetCapabilities()).Should().Be(new BigInteger(1UL << 46));
    }
}
