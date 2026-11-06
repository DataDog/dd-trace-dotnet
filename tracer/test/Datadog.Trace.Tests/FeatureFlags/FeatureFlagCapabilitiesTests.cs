// <copyright file="FeatureFlagCapabilitiesTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

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
    public void GatesRemoteConfigurationCapability(string? capability, bool supported)
    {
        string[] capabilities = capability is null ? [] : [capability];
        FeatureFlagCapabilities.RemoteConfigMask(capabilities).Should().Be(new BigInteger((1UL << 46) | (supported ? 1UL << 50 : 0)));
    }

    [Fact]
    public void CurrentEvaluatorAdvertisesDependentFlags()
    {
        FeatureFlagsEvaluator.GetCapabilities().Should().Equal(FeatureFlagCapabilities.DependentFlagVariantKeyV1);
        FeatureFlagCapabilities.RemoteConfigMask(FeatureFlagsEvaluator.GetCapabilities()).Should().Be(new BigInteger((1UL << 46) | (1UL << 50)));
    }
}
