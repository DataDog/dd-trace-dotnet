// <copyright file="FeatureFlagsSdkTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

extern alias Tracer;

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.FeatureFlags;
using Datadog.Trace.FeatureFlags.FlagEvaluation;
using FluentAssertions;
using OpenFeature.Constant;
using OpenFeature.Model;
using Xunit;

namespace Datadog.FeatureFlags.OpenFeature.Tests;

public class FeatureFlagsSdkTests
{
    [Fact]
    public void TracerDetectsBridgesFromSdkTargetAssembly()
    {
        Tracer::Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.OpenFeature.OpenFeatureSdkEvaluateIntegration
              .HasExposureCommitBridge(typeof(DatadogProvider)).Should().BeTrue();
        Tracer::Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.OpenFeature.OpenFeatureSdkEvaluateIntegration
              .HasEvaluationEventBridge(typeof(DatadogProvider)).Should().BeTrue();
        Tracer::Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.OpenFeature.OpenFeatureSdkEvaluateIntegration
              .HasExposureCommitBridge(typeof(FeatureFlagsSdkTests)).Should().BeFalse();
        Tracer::Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.OpenFeature.OpenFeatureSdkEvaluateIntegration
              .HasEvaluationEventBridge(typeof(FeatureFlagsSdkTests)).Should().BeFalse();
    }

    [Theory]
    [InlineData((int)EvaluationReason.Static, Reason.Static)]
    [InlineData((int)EvaluationReason.Default, Reason.Default)]
    [InlineData((int)EvaluationReason.TargetingMatch, Reason.TargetingMatch)]
    [InlineData((int)EvaluationReason.Split, Reason.Split)]
    [InlineData((int)EvaluationReason.Disabled, Reason.Disabled)]
    [InlineData((int)EvaluationReason.Cached, Reason.Cached)]
    [InlineData((int)EvaluationReason.Unknown, Reason.Unknown)]
    [InlineData((int)EvaluationReason.Error, Reason.Error)]
    public void ResolutionUsesCanonicalOpenFeatureReason(int evaluationReason, string expected)
    {
        var result = FeatureFlagsSdk.GetResolutionDetails("flag", false, new TestEvaluation(null, reason: (EvaluationReason)evaluationReason), null);

        result.Reason.Should().Be(expected);
    }

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

        var result = FeatureFlagsSdk.GetResolutionDetails("flag", false, new TestEvaluation(metadata), 1234, deferredExposure: false);

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
        var result = FeatureFlagsSdk.GetResolutionDetails("flag", false, new TestEvaluation(null), 1234, deferredExposure: false);

        result.Value.Should().BeTrue();
        result.FlagMetadata!.GetBool(FeatureFlagMetadataKeys.ObserveFullEvaluationData).Should().BeFalse();
        result.FlagMetadata.GetDouble("__dd_eval_timestamp_ms").Should().Be(1234);
    }

    [Fact]
    public void ResolutionWithoutEventHookPreservesDefaultAndOmitsPrivateMetadata()
    {
        var result = FeatureFlagsSdk.Resolve("flag", Trace.FeatureFlags.ValueType.Boolean, false, null, evaluate: null, evaluationEventsHook: null, deferredExposure: false);

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
        var result = FeatureFlagsSdk.GetResolutionDetails("flag", false, new TestEvaluation(metadata, error), 1234, deferredExposure: false);

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

        var result = FeatureFlagsSdk.GetResolutionDetails("flag", false, new TestEvaluation(metadata, error), null, deferredExposure: false);

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
        var result = FeatureFlagsSdk.GetResolutionDetails("flag", false, new TestEvaluation(null), null, deferredExposure: false);

        result.Value.Should().BeTrue();
        result.FlagMetadata.Should().BeNull();
    }

    [Fact]
    public void NestedContextValuesAreNormalizedBeforeTracerEvaluation()
    {
        IDictionary<string, object?>? captured = null;
        var createdAt = new DateTime(2026, 11, 4, 1, 2, 3, DateTimeKind.Utc);
        var context = global::OpenFeature.Model.EvaluationContext.Builder()
                                       .Set("createdAt", new Value(createdAt))
                                       .Set("user", new Value(new Structure(new Dictionary<string, Value>
                                       {
                                           ["country"] = new("US"),
                                           ["tags"] = new(new List<Value> { new("paid"), new(false) }),
                                       })))
                                       .Build();

        FeatureFlagsSdk.Resolve(
            "flag",
            Trace.FeatureFlags.ValueType.Boolean,
            false,
            context,
            evaluate: (_, _, _, _, attributes) =>
            {
                captured = attributes;
                return new TestEvaluation(null);
            });

        captured.Should().NotBeNull();
        captured!["createdAt"].Should().Be(createdAt);
        captured!["user"].Should().BeEquivalentTo(new Dictionary<string, object?>
        {
            ["country"] = "US",
            ["tags"] = new object?[] { "paid", false },
        });
    }

    [Fact]
    public void NestedContextNormalizationBoundsDepthContainersAndNodes()
    {
        var deep = new Value("leaf");
        for (var i = 0; i < 10; i++)
        {
            deep = new Value(new Structure(new Dictionary<string, Value> { ["child"] = deep }));
        }

        var wide = new Value(Enumerable.Repeat(new Value("item"), 300).ToList());
        var branches = new Dictionary<string, Value>();
        for (var i = 0; i < 256; i++)
        {
            branches[i.ToString()] = new Value(Enumerable.Repeat(new Value("item"), 256).ToList());
        }

        var capturedDeep = Capture(deep);
        var capturedWide = Capture(wide);
        var capturedBranches = Capture(new Value(new Structure(branches)));

        MaximumDepth(capturedDeep["value"]).Should().BeLessOrEqualTo(5, "the root attribute is depth one");
        ((object?[])capturedWide["value"]!).Should().HaveCountLessOrEqualTo(256);
        CountNodes(capturedBranches).Should().BeLessOrEqualTo(1_281, "the root dictionary is not an OpenFeature Value node");
        OmissionReasons(capturedDeep).Should().HaveFlag(ContextOmissionReason.MaxSnapshotDepth);
        OmissionReasons(capturedWide).Should().HaveFlag(ContextOmissionReason.MaxListElements);
        OmissionReasons(capturedBranches).Should().HaveFlag(ContextOmissionReason.MaxVisitedNodes);

        static IDictionary<string, object?> Capture(Value value)
        {
            IDictionary<string, object?>? captured = null;
            var context = global::OpenFeature.Model.EvaluationContext.Builder().Set("value", value).Build();
            FeatureFlagsSdk.Resolve(
                "flag",
                Trace.FeatureFlags.ValueType.Boolean,
                false,
                context,
                evaluate: (_, _, _, _, attributes) =>
                {
                    captured = attributes;
                    return new TestEvaluation(null);
                });

            return captured!;
        }

        static ContextOmissionReason OmissionReasons(IDictionary<string, object?> attributes)
        {
            var sdkReasons = ((FeatureFlagsSdk.ContextAttributes)attributes).ContextOmissionReasons;
            var tracerReasons = Tracer::Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.OpenFeature.OpenFeatureSdkEvaluateIntegration.GetContextOmissionReasons(attributes);
            tracerReasons.Should().Be(sdkReasons, "the profiler bridge must preserve SDK normalization omissions");
            return (ContextOmissionReason)sdkReasons;
        }

        static int CountNodes(object? value)
        {
            if (value is IDictionary<string, object?> dictionary)
            {
                return 1 + dictionary.Values.Sum(CountNodes);
            }

            if (value is object?[] array)
            {
                return 1 + array.Sum(CountNodes);
            }

            return 1;
        }

        static int MaximumDepth(object? value)
        {
            if (value is IDictionary<string, object?> dictionary && dictionary.Count > 0)
            {
                return 1 + dictionary.Values.Max(MaximumDepth);
            }

            if (value is object?[] array && array.Length > 0)
            {
                return 1 + array.Max(MaximumDepth);
            }

            return 1;
        }
    }

    [Fact]
    public void NestedContextNormalizationSharesNodeBudgetAndRetainsLaterScalars()
    {
        IDictionary<string, object?>? captured = null;
        var builder = global::OpenFeature.Model.EvaluationContext.Builder();
        for (var i = 0; i < 10; i++)
        {
            builder.Set($"nested-{i}", new Value(Enumerable.Repeat(new Value("item"), 256).ToList()));
        }

        var context = builder.Set("later-scalar", new Value("retained")).Build();
        FeatureFlagsSdk.Resolve(
            "flag",
            Trace.FeatureFlags.ValueType.Boolean,
            false,
            context,
            evaluate: (_, _, _, _, attributes) =>
            {
                captured = attributes;
                return new TestEvaluation(null);
            });

        captured.Should().NotBeNull();
        captured!["later-scalar"].Should().Be("retained");
        captured.Where(pair => pair.Key.StartsWith("nested-", StringComparison.Ordinal))
                .Sum(pair => CountExpandedNodes(pair.Value))
                .Should().BeLessOrEqualTo(1_280);
        ((ContextOmissionReason)((FeatureFlagsSdk.ContextAttributes)captured).ContextOmissionReasons)
           .Should().HaveFlag(ContextOmissionReason.MaxVisitedNodes);

        static int CountExpandedNodes(object? value)
        {
            if (value is null)
            {
                return 0;
            }

            if (value is IDictionary<string, object?> dictionary)
            {
                return 1 + dictionary.Values.Sum(CountExpandedNodes);
            }

            if (value is object?[] array)
            {
                return 1 + array.Sum(CountExpandedNodes);
            }

            return 1;
        }
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

        var result = FeatureFlagsSdk.GetResolutionDetails("flag", false, new TestEvaluation(metadata), null, deferredExposure: false);

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

        var result = FeatureFlagsSdk.GetResolutionDetails("flag", false, new TestEvaluation(metadata), 1, deferredExposure: false);

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

        var result = FeatureFlagsSdk.GetResolutionDetails("flag", false, new TestEvaluation(metadata), null, deferredExposure: false);

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

        var result = FeatureFlagsSdk.GetResolutionDetails("flag", false, new TestEvaluation(metadata), null, deferredExposure: false);

        result.FlagMetadata!.GetString("__dd_feature.flag").Should().Be("true");
        result.FlagMetadata!.GetDouble("__dd_feature.year").Should().Be(2026);
    }

    [Fact]
    public void ASerialIdAboveTheIntRangeKeepsItsWireBits()
    {
        var metadata = new Dictionary<string, string> { [FeatureFlagMetadataKeys.SplitSerialId] = "4294967295" };

        var result = FeatureFlagsSdk.GetResolutionDetails("flag", false, new TestEvaluation(metadata), null, deferredExposure: false);

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

        var result = FeatureFlagsSdk.GetResolutionDetails("flag", false, new TestEvaluation(metadata), null, deferredExposure: false);

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

        var converted = FeatureFlagsSdk.GetResolutionDetails("flag", false, new TestEvaluation(metadata), null, deferredExposure: false).FlagMetadata!;

        FeatureFlagsSdk.GetEvaluationFeatures(converted).Should().BeNull("exposure and hook features do not split evaluation counts");
        FeatureFlagsSdk.GetHookFeatures(converted)!.Should().ContainKey("hook");
    }

    [Fact]
    public void HookFeaturesAreAbsentWithoutFeatureMetadata()
    {
        var result = FeatureFlagsSdk.GetResolutionDetails("flag", false, new TestEvaluation(new Dictionary<string, string> { ["__dd_allocation_key"] = "alloc" }), null, deferredExposure: false);

        FeatureFlagsSdk.GetHookFeatures(result.FlagMetadata).Should().BeNull();
    }

    private sealed class TestEvaluation(IDictionary<string, string>? metadata, string? error = null, EvaluationReason? reason = null) : IEvaluation
    {
        public string FlagKey => "flag";

        public object Value => true;

        public EvaluationReason Reason => reason ?? (error is null ? EvaluationReason.Static : EvaluationReason.Error);

        public string? Variant => "on";

        public string? Error => error;

        public IDictionary<string, string>? FlagMetadata => metadata;
    }
}
