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

        var result = FeatureFlagsSdk.GetResolutionDetails("flag", false, new TestEvaluation(metadata), 1234);

        result.FlagMetadata.Should().NotBeNull();
        result.FlagMetadata!.GetBool(FeatureFlagMetadataKeys.ObserveFullEvaluationData).Should().Be(expected);
        result.FlagMetadata.GetString(FeatureFlagMetadataKeys.DoLog).Should().Be("true");
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
        result.FlagMetadata!.GetString(FeatureFlagMetadataKeys.DoLog).Should().Be("true");
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
