// <copyright file="FeatureFlagTypeCodecTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.FeatureFlags;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.FeatureFlags;

public class FeatureFlagTypeCodecTests
{
    [Fact]
    public void ParseReturnsAllSupportedEntries()
        => FeatureFlagTypeCodec.Parse("1:ab3:foon")
                               .Should()
                               .BeEquivalentTo(new Dictionary<string, char> { ["a"] = 'b', ["foo"] = 'n' });

    [Theory]
    [InlineData("")]
    [InlineData("x:ab")]
    [InlineData("-1:ab")]
    [InlineData("999999999999999999999:ab")]
    [InlineData("3:ab")]
    [InlineData("1:a?")]
    public void ParseIgnoresMalformedOrUnsupportedEntries(string encoded)
        => FeatureFlagTypeCodec.Parse(encoded).Should().BeNull();

    [Fact]
    public void ParseContinuesAfterAnUnsupportedType()
        => FeatureFlagTypeCodec.Parse("1:a?1:bn")
                               .Should()
                               .BeEquivalentTo(new Dictionary<string, char> { ["b"] = 'n' });

    [Theory]
    [InlineData("true", 'b', true)]
    [InlineData("false", 'b', false)]
    [InlineData("1.5", 'n', 1.5)]
    [InlineData("not-a-number", 'n', "not-a-number")]
    [InlineData("true", '?', "true")]
    public void DecodeValueUsesTheRecordedType(string encoded, char type, object expected)
        => FeatureFlagTypeCodec.DecodeValue(encoded, type).Should().Be(expected);
}
