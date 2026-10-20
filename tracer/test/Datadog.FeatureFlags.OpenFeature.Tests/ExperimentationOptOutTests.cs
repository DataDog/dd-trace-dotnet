// <copyright file="ExperimentationOptOutTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

extern alias Tracer;

using System;
using System.Collections.Generic;
using Datadog.Trace.FeatureFlags;
using OpenFeature.Constant;
using Xunit;
using TracerFlags = Tracer::Datadog.Trace.FeatureFlags;

namespace Datadog.FeatureFlags.OpenFeature.Tests;

[Collection("Environment configuration")]
public class ExperimentationOptOutTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("disabled", false)]
    [InlineData("immediate", true)]
    [InlineData(" IMMEDIATE ", true)]
    public void DefaultUsesConfiguration(string? configured, bool expected)
    {
        Assert.Equal(expected, DatadogProvider.ResolveExperimentationOptOutMode(ExperimentationOptOutMode.Default, configured));
    }

    [Theory]
    [InlineData("")]
    [InlineData("disabled")]
    [InlineData("immediate")]
    [InlineData("unsupported")]
    public void ExplicitModeOverridesConfiguration(string configured)
    {
        Assert.False(DatadogProvider.ResolveExperimentationOptOutMode(ExperimentationOptOutMode.Disabled, configured));
        Assert.True(DatadogProvider.ResolveExperimentationOptOutMode(ExperimentationOptOutMode.Immediate, configured));
    }

    [Fact]
    public void UnsupportedModesAreRejected()
    {
        Assert.Throws<ArgumentException>(() => DatadogProvider.ResolveExperimentationOptOutMode(ExperimentationOptOutMode.Default, "unsupported"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DatadogProvider((ExperimentationOptOutMode)99));
    }

    [Fact]
    public void EnvironmentIsNotIgnoredWithoutTheTracerBridge()
    {
        const string key = "DD_FEATURE_FLAGS_EXPERIMENTATION_OPT_OUT_MODE";
        var previous = Environment.GetEnvironmentVariable(key);
        try
        {
            Environment.SetEnvironmentVariable(key, "immediate");
            Assert.True(DatadogProvider.ResolveExperimentationOptOutMode(ExperimentationOptOutMode.Default));
            Assert.False(DatadogProvider.ResolveExperimentationOptOutMode(ExperimentationOptOutMode.Disabled));
            Environment.SetEnvironmentVariable(key, "unsupported");
            Assert.Throws<ArgumentException>(() => new DatadogProvider());
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, previous);
        }
    }

    [Theory]
    [InlineData("2026-10-19T15:30:00Z")]
    [InlineData("2026-10-19T15:30:00.123456789123Z")]
    [InlineData("2026-10-19T15:30:00+05:30")]
    [InlineData("2026-10-19T15:30:00-04:00")]
    [InlineData("9999-12-31T23:59:59Z")]
    [InlineData("0000-02-29T00:00:00Z")]
    [InlineData("2024-02-29T00:00:00Z")]
    public void ValidTimestampExcludesImmediately(string timestamp)
    {
        Assert.True(TracerFlags.ExperimentationOptOut.TryExclude(
            new Dictionary<string, object?> { [TracerFlags.ExperimentationOptOut.Attribute] = timestamp }, out var excluded));
        Assert.True(excluded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a timestamp")]
    [InlineData("2026-10-19")]
    [InlineData("2026-10-19T15:30:00")]
    [InlineData("2026-02-30T00:00:00Z")]
    [InlineData("2026-10-19T24:00:00Z")]
    [InlineData("2026-10-19T15:30:60Z")]
    [InlineData("2026-10-19T15:30:00+99:00")]
    [InlineData(" 2026-10-19T15:30:00Z")]
    [InlineData(true)]
    [InlineData(42)]
    public void InvalidTimestampsAreRejected(object? value)
    {
        Assert.False(TracerFlags.ExperimentationOptOut.TryExclude(
            new Dictionary<string, object?> { [TracerFlags.ExperimentationOptOut.Attribute] = value }, out var excluded));
        Assert.False(excluded);
    }

    [Fact]
    public void MissingAttributePreservesParticipation()
    {
        Assert.True(TracerFlags.ExperimentationOptOut.TryExclude(new Dictionary<string, object?>(), out var excluded));
        Assert.False(excluded);
    }

    [Fact]
    public void ImmediateModeDoesNotFallBackToOldTracerEvaluation()
    {
        var result = FeatureFlagsSdk.Resolve(
            "flag", Trace.FeatureFlags.ValueType.Boolean, false, null, experimentationOptOutImmediate: true);
        Assert.False(result.Value);
        Assert.Equal(ErrorType.ProviderNotReady, result.ErrorType);
    }

    [Fact]
    public void SharedFixturesIncludeImmediateCapability()
    {
        Assert.Equal(36, System.Linq.Enumerable.Count(CanonicalFixtureTests.Cases(), row => (bool)row[3]));
    }
}
