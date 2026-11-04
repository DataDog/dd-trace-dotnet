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
    public void GatesBothTransports(string? capability, bool supported)
    {
        string[] capabilities = capability is null ? [] : [capability];
        var uri = new Uri("https://example.test/ufc?token=preserved");
        FeatureFlagCapabilities.AddToUri(uri, capabilities)
                               .Should()
                               .Be(supported ? new Uri(uri + "&cap_dependent-flags-variant-key=v1") : uri);
        FeatureFlagCapabilities.RemoteConfigMask(capabilities).Should().Be(new BigInteger((1UL << 46) | (supported ? 1UL << 50 : 0)));
    }

    [Fact]
    public void CurrentEvaluatorDoesNotAdvertiseDependentFlags()
    {
        FeatureFlagsEvaluator.GetCapabilities().Should().BeEmpty();
        FeatureFlagCapabilities.AddToUri(new Uri("https://example.test/ufc"), FeatureFlagsEvaluator.GetCapabilities())
                               .Should()
                               .Be(new Uri("https://example.test/ufc"));
        FeatureFlagCapabilities.RemoteConfigMask(FeatureFlagsEvaluator.GetCapabilities()).Should().Be(new BigInteger(1UL << 46));
    }

    [Fact]
    public void ReplacesConfiguredDependentFlagsParameter()
        => FeatureFlagCapabilities.AddToUri(
                                      new Uri("https://example.test/ufc?cap_dependent-flags-variant-key=old&token=preserved"),
                                      [FeatureFlagCapabilities.DependentFlagVariantKeyV1])
                                  .Should()
                                  .Be(new Uri("https://example.test/ufc?token=preserved&cap_dependent-flags-variant-key=v1"));
}
