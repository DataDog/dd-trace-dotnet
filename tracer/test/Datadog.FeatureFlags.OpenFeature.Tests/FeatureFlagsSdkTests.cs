// <copyright file="FeatureFlagsSdkTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.FeatureFlags;
using FluentAssertions;
using OpenFeature.Constant;
using Xunit;

namespace Datadog.FeatureFlags.OpenFeature.Tests;

public class FeatureFlagsSdkTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("false", false)]
    [InlineData("true", true)]
    [InlineData("True", false)]
    [InlineData("1", false)]
    [InlineData(" true ", false)]
    public void ConsentIsStrictAndTyped(string? consent, bool expected)
    {
        var metadata = new Dictionary<string, string> { [FeatureFlagMetadataKeys.DoLog] = "true" };
        if (consent is not null)
        {
            metadata[FeatureFlagMetadataKeys.ObserveFullEvaluationData] = consent;
        }

        var result = FeatureFlagsSdk.GetResolutionDetails("flag", false, new TestEvaluation(metadata), 1234);

        result.FlagMetadata.Should().NotBeNull();
        result.FlagMetadata!.GetBool(FeatureFlagMetadataKeys.ObserveFullEvaluationData).Should().Be(expected);
        result.FlagMetadata.GetBool(FeatureFlagMetadataKeys.DoLog).Should().BeTrue();
        result.FlagMetadata.GetString(FeatureFlagMetadataKeys.DoLog).Should().BeNull();
        result.FlagMetadata.GetDouble("__dd_eval_timestamp_ms").Should().Be(1234);
        metadata.Should().NotContainKey("__dd_eval_timestamp_ms");
    }

    [Fact]
    public void OldTracerWithoutMetadataDefaultsToProtected()
    {
        var result = FeatureFlagsSdk.GetResolutionDetails("flag", false, new TestEvaluation(null), 1234);

        result.Value.Should().BeTrue();
        result.FlagMetadata!.GetBool(FeatureFlagMetadataKeys.ObserveFullEvaluationData).Should().BeFalse();
        result.FlagMetadata.GetDouble("__dd_eval_timestamp_ms").Should().Be(1234);
    }

    [Fact]
    public void ResolutionWithoutEventHookPreservesDefaultAndOmitsPrivateMetadata()
    {
        var result = FeatureFlagsSdk.Resolve("flag", Trace.FeatureFlags.ValueType.Boolean, false, null);

        result.Value.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.ProviderNotReady);
        result.FlagMetadata.Should().BeNull();
    }

    [Theory]
    [InlineData("FLAG_NOT_FOUND")]
    [InlineData("INVALID_CONTEXT")]
    [InlineData("PARSE_ERROR")]
    [InlineData("PROVIDER_FATAL")]
    [InlineData("PROVIDER_NOT_READY")]
    [InlineData("TARGETING_KEY_MISSING")]
    [InlineData("TYPE_MISMATCH")]
    [InlineData("GENERAL")]
    public void ErrorsPreserveCallerDefaultAndEvaluationMetadata(string error)
    {
        var metadata = new Dictionary<string, string> { [FeatureFlagMetadataKeys.ObserveFullEvaluationData] = "true" };
        var result = FeatureFlagsSdk.GetResolutionDetails("flag", false, new TestEvaluation(metadata, error), 1234);

        result.Value.Should().BeFalse();
        result.ErrorType.Should().NotBe(ErrorType.None);
        result.FlagMetadata!.GetBool(FeatureFlagMetadataKeys.ObserveFullEvaluationData).Should().BeTrue();
        result.FlagMetadata.GetDouble("__dd_eval_timestamp_ms").Should().Be(1234);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("FLAG_NOT_FOUND")]
    public void DisabledEventsOmitOnlyPrivateEvpMetadata(string? error)
    {
        var metadata = new Dictionary<string, string>
        {
            [FeatureFlagMetadataKeys.ObserveFullEvaluationData] = "true",
            [FeatureFlagMetadataKeys.EvaluationTimestampMs] = "1234",
            [FeatureFlagMetadataKeys.DoLog] = "true",
            ["__dd_allocation_key"] = "allocation",
            ["errorCode"] = error ?? string.Empty,
        };

        var result = FeatureFlagsSdk.GetResolutionDetails("flag", false, new TestEvaluation(metadata, error), null);

        result.Value.Should().Be(error is null);
        result.FlagMetadata!.GetBool(FeatureFlagMetadataKeys.DoLog).Should().BeTrue();
        result.FlagMetadata.GetString("__dd_allocation_key").Should().Be("allocation");
        result.FlagMetadata.GetString("errorCode").Should().Be(error ?? string.Empty);
        result.FlagMetadata.GetBool(FeatureFlagMetadataKeys.ObserveFullEvaluationData).Should().BeNull();
        result.FlagMetadata.GetString(FeatureFlagMetadataKeys.ObserveFullEvaluationData).Should().BeNull();
        result.FlagMetadata.GetDouble(FeatureFlagMetadataKeys.EvaluationTimestampMs).Should().BeNull();
        result.FlagMetadata.GetString(FeatureFlagMetadataKeys.EvaluationTimestampMs).Should().BeNull();
        metadata[FeatureFlagMetadataKeys.ObserveFullEvaluationData].Should().Be("true");
        metadata[FeatureFlagMetadataKeys.EvaluationTimestampMs].Should().Be("1234");
    }

    [Fact]
    public void DisabledEventsDoNotInventMetadataForAnOldTracer()
    {
        var result = FeatureFlagsSdk.GetResolutionDetails("flag", false, new TestEvaluation(null), null);

        result.Value.Should().BeTrue();
        result.FlagMetadata.Should().BeNull();
    }

    [Fact]
    public void MetadataConvertsKnownKeysToTheirTypes()
    {
        var metadata = new Dictionary<string, string>
        {
            ["__dd_allocation_key"] = "alloc",
            [FeatureFlagMetadataKeys.DoLog] = "true",
            [FeatureFlagMetadataKeys.ExposureCacheHit] = "false",
            [FeatureFlagMetadataKeys.SplitSerialId] = "1001",
            ["__dd_feature.holdout.key"] = "06ad",
            ["__dd_feature.holdout.should_include_in_holdout_analysis"] = "false",
            ["__dd_feature.internal.count"] = "3.5",
            [FeatureFlagMetadataKeys.FeatureTypes] = "42:holdout.should_include_in_holdout_analysisb14:internal.countn",
            ["errorCode"] = string.Empty,
        };

        var result = FeatureFlagsSdk.GetResolutionDetails("flag", false, new TestEvaluation(metadata), null);

        var converted = result.FlagMetadata!;
        converted.GetBool(FeatureFlagMetadataKeys.DoLog).Should().BeTrue();
        converted.GetString(FeatureFlagMetadataKeys.DoLog).Should().BeNull();
        converted.GetBool(FeatureFlagMetadataKeys.ExposureCacheHit).Should().BeFalse();
        converted.GetInt(FeatureFlagMetadataKeys.SplitSerialId).Should().Be(1001);
        converted.GetString("__dd_allocation_key").Should().Be("alloc");
        converted.GetString("errorCode").Should().BeEmpty();
        converted.GetString("__dd_feature.holdout.key").Should().Be("06ad");
        converted.GetBool("__dd_feature.holdout.should_include_in_holdout_analysis").Should().BeFalse();
        converted.GetDouble("__dd_feature.internal.count").Should().Be(3.5);
        converted.GetString(FeatureFlagMetadataKeys.FeatureTypes).Should().BeNull("the type marker is internal to the provider");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("4:flagb")]
    public void StringFeaturesThatLookTypedStayStrings(string? types)
    {
        var metadata = new Dictionary<string, string>
        {
            ["__dd_feature.flag"] = "true",
            ["__dd_feature.year"] = "2026",
            ["__dd_evaluation_feature.year"] = "2026",
        };
        if (types is not null)
        {
            metadata[FeatureFlagMetadataKeys.FeatureTypes] = types;
        }

        var result = FeatureFlagsSdk.GetResolutionDetails("flag", false, new TestEvaluation(metadata), 1);

        var converted = result.FlagMetadata!;
        converted.GetString("__dd_feature.flag").Should().Be(types == "4:flagb" ? null : "true");
        converted.GetString("__dd_feature.year").Should().Be("2026");
        FeatureFlagsSdk.GetHookFeatures(converted)!["year"].Should().Be("2026");
        FeatureFlagsSdk.GetEvaluationFeatures(converted)!["year"].Should().Be("2026");
        converted.GetString(FeatureFlagMetadataKeys.FeatureTypes).Should().BeNull();
    }

    [Theory]
    [InlineData("4:flag", "a truncated item")]
    [InlineData("99:flagb", "a length beyond the entry")]
    [InlineData("x:flagb", "a non-numeric length")]
    [InlineData("-4:flagb", "a signed length")]
    [InlineData("4flagb", "a missing separator")]
    [InlineData("99999999999999999999:flagb", "an overflowing length")]
    [InlineData("4:flagz", "an unknown type letter")]
    [InlineData("7:missingb", "a listed key with no matching feature")]
    [InlineData("4:yearb", "a value that is not of the listed type")]
    public void AMalformedTypeMarkerFallsBackToStrings(string types, string because)
    {
        var metadata = new Dictionary<string, string>
        {
            ["__dd_feature.flag"] = "true",
            ["__dd_feature.year"] = "2026",
            [FeatureFlagMetadataKeys.FeatureTypes] = types,
        };

        var result = FeatureFlagsSdk.GetResolutionDetails("flag", false, new TestEvaluation(metadata), null);

        result.ErrorType.Should().Be(ErrorType.None, because);
        result.FlagMetadata!.GetString("__dd_feature.flag").Should().Be("true", because);
        result.FlagMetadata!.GetString("__dd_feature.year").Should().Be("2026", because);
        result.FlagMetadata!.GetString(FeatureFlagMetadataKeys.FeatureTypes).Should().BeNull(because);
    }

    [Fact]
    public void AnUnknownTypeLetterSkipsOnlyItsOwnItem()
    {
        var metadata = new Dictionary<string, string>
        {
            ["__dd_feature.flag"] = "true",
            ["__dd_feature.year"] = "2026",
            [FeatureFlagMetadataKeys.FeatureTypes] = "4:flagz4:yearn",
        };

        var result = FeatureFlagsSdk.GetResolutionDetails("flag", false, new TestEvaluation(metadata), null);

        result.FlagMetadata!.GetString("__dd_feature.flag").Should().Be("true");
        result.FlagMetadata!.GetDouble("__dd_feature.year").Should().Be(2026);
    }

    [Fact]
    public void ASerialIdAboveTheIntRangeKeepsItsWireBits()
    {
        var metadata = new Dictionary<string, string> { [FeatureFlagMetadataKeys.SplitSerialId] = "4294967295" };

        var result = FeatureFlagsSdk.GetResolutionDetails("flag", false, new TestEvaluation(metadata), null);

        result.FlagMetadata!.GetInt(FeatureFlagMetadataKeys.SplitSerialId).Should().Be(-1);
    }

    [Fact]
    public void HookFeaturesAreStashedWithTheResultMetadata()
    {
        var metadata = new Dictionary<string, string>
        {
            ["__dd_feature.holdout.key"] = "06ad",
            ["__dd_feature.holdout.should_include_in_holdout_analysis"] = "false",
            ["__dd_exposure_feature.holdout.key"] = "06ad",
            [FeatureFlagMetadataKeys.FeatureTypes] = "42:holdout.should_include_in_holdout_analysisb",
        };

        var result = FeatureFlagsSdk.GetResolutionDetails("flag", false, new TestEvaluation(metadata), null);

        var features = FeatureFlagsSdk.GetHookFeatures(result.FlagMetadata);
        features.Should().NotBeNull();
        features!.Count.Should().Be(2, "only HOOK features are exposed to the exposure hook");
        features["holdout.key"].Should().Be("06ad");
        features["holdout.should_include_in_holdout_analysis"].Should().Be(false);
    }

    [Fact]
    public void OnlyEvaluationDestinationFeaturesAreCounted()
    {
        var metadata = new Dictionary<string, string> { ["__dd_exposure_feature.holdout.key"] = "06ad", ["__dd_feature.hook"] = "x" };

        var converted = FeatureFlagsSdk.GetResolutionDetails("flag", false, new TestEvaluation(metadata), null).FlagMetadata!;

        FeatureFlagsSdk.GetEvaluationFeatures(converted).Should().BeNull("exposure and hook features do not split evaluation counts");
        FeatureFlagsSdk.GetHookFeatures(converted)!.Should().ContainKey("hook");
    }

    [Fact]
    public void HookFeaturesAreAbsentWithoutFeatureMetadata()
    {
        var result = FeatureFlagsSdk.GetResolutionDetails("flag", false, new TestEvaluation(new Dictionary<string, string> { ["__dd_allocation_key"] = "alloc" }), null);

        FeatureFlagsSdk.GetHookFeatures(result.FlagMetadata).Should().BeNull();
    }

    private sealed class TestEvaluation(IDictionary<string, string>? metadata, string? error = null) : IEvaluation
    {
        public string FlagKey => "flag";

        public object Value => true;

        public EvaluationReason Reason => error is null ? EvaluationReason.Static : EvaluationReason.Error;

        public string? Variant => "on";

        public string? Error => error;

        public IDictionary<string, string>? FlagMetadata => metadata;
    }
}
