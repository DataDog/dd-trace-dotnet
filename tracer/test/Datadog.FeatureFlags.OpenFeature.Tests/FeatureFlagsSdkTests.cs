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
    public void UninstrumentedProviderPreservesDefaultAndCapturesTimestamp()
    {
        var before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var result = FeatureFlagsSdk.Resolve("flag", Trace.FeatureFlags.ValueType.Boolean, false, null);
        var after = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        result.Value.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.ProviderNotReady);
        result.FlagMetadata!.GetBool(FeatureFlagMetadataKeys.ObserveFullEvaluationData).Should().BeFalse();
        result.FlagMetadata.GetDouble("__dd_eval_timestamp_ms").Should().BeInRange(before, after);
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
