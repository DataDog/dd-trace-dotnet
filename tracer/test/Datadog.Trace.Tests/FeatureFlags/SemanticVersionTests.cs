// <copyright file="SemanticVersionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.FeatureFlags;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.FeatureFlags;

public class SemanticVersionTests
{
    [Theory]
    [InlineData("18.0.0.0.0.0", "18.0.0.0.0.1", -1)]
    [InlineData("18.0.0.0.0.1", "18.0.0.0.0.0", 1)]
    [InlineData("18.0.0.0.0.0", "18.0.0.0.0", 0)]
    public void CompareToSupportsSixOrMoreCoreComponents(string left, string right, int expected)
    {
        SemanticVersion.TryParse(left, out var leftVersion).Should().BeTrue();
        SemanticVersion.TryParse(right, out var rightVersion).Should().BeTrue();

        leftVersion.CompareTo(rightVersion).Should().Be(expected);
    }

    [Fact]
    public void TryParseSupportsAnArbitraryNumberOfCoreComponents()
    {
        const string lowerVersion = "1.2.3.4.5.6.7.8.9.10.11.12";
        const string higherVersion = "1.2.3.4.5.6.7.8.9.10.11.13";

        SemanticVersion.TryParse(lowerVersion, out var lower).Should().BeTrue();
        SemanticVersion.TryParse(higherVersion, out var higher).Should().BeTrue();

        lower.CompareTo(higher).Should().Be(-1);
    }

    [Fact]
    public void TryParseRejectsLeadingZerosInExtendedCoreComponents()
    {
        SemanticVersion.TryParse("1.2.3.4.5.06", out _).Should().BeFalse();
    }
}
