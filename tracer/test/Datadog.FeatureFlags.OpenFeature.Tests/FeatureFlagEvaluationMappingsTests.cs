// <copyright file="FeatureFlagEvaluationMappingsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

extern alias Tracer;

using FluentAssertions;
using OpenFeature.Constant;
using OpenFeature.Model;
using Xunit;

namespace Datadog.FeatureFlags.OpenFeature.Tests;

public class FeatureFlagEvaluationMappingsTests
{
    [Theory]
    [InlineData((int)Tracer::Datadog.Trace.FeatureFlags.EvaluationReason.Static, "static")]
    [InlineData((int)Tracer::Datadog.Trace.FeatureFlags.EvaluationReason.Default, "default")]
    [InlineData((int)Tracer::Datadog.Trace.FeatureFlags.EvaluationReason.TargetingMatch, "targeting_match")]
    [InlineData((int)Tracer::Datadog.Trace.FeatureFlags.EvaluationReason.Split, "split")]
    [InlineData((int)Tracer::Datadog.Trace.FeatureFlags.EvaluationReason.Disabled, "disabled")]
    [InlineData((int)Tracer::Datadog.Trace.FeatureFlags.EvaluationReason.Cached, "cached")]
    [InlineData((int)Tracer::Datadog.Trace.FeatureFlags.EvaluationReason.Unknown, "unknown")]
    [InlineData((int)Tracer::Datadog.Trace.FeatureFlags.EvaluationReason.Error, "error")]
    [InlineData(int.MaxValue, "unknown")]
    public void MapsEvaluationReasons(int reason, string expected)
        => FeatureFlagEvaluationMappings.ToReason((Tracer::Datadog.Trace.FeatureFlags.EvaluationReason)reason).Should().Be(expected);

    [Theory]
    [InlineData("PROVIDER_NOT_READY", (int)ErrorType.ProviderNotReady)]
    [InlineData("FLAG_NOT_FOUND", (int)ErrorType.FlagNotFound)]
    [InlineData("PARSE_ERROR", (int)ErrorType.ParseError)]
    [InlineData("TYPE_MISMATCH", (int)ErrorType.TypeMismatch)]
    [InlineData("TARGETING_KEY_MISSING", (int)ErrorType.TargetingKeyMissing)]
    [InlineData("INVALID_CONTEXT", (int)ErrorType.InvalidContext)]
    [InlineData("PROVIDER_FATAL", (int)ErrorType.ProviderFatal)]
    [InlineData("GENERAL", (int)ErrorType.General)]
    [InlineData(null, (int)ErrorType.None)]
    [InlineData("UNKNOWN", (int)ErrorType.None)]
    public void MapsEvaluatorErrorCodes(string? errorCode, int expected)
        => FeatureFlagEvaluationMappings.ToErrorType(errorCode).Should().Be((ErrorType)expected);

    [Theory]
    [InlineData("PROVIDER_NOT_READY", "provider_not_ready")]
    [InlineData("FLAG_NOT_FOUND", "flag_not_found")]
    [InlineData("PARSE_ERROR", "parse_error")]
    [InlineData("TYPE_MISMATCH", "type_mismatch")]
    [InlineData("TARGETING_KEY_MISSING", "targeting_key_missing")]
    [InlineData("INVALID_CONTEXT", "invalid_context")]
    [InlineData("PROVIDER_FATAL", "provider_fatal")]
    [InlineData("GENERAL", "general")]
    [InlineData("UNKNOWN", "general")]
    [InlineData(null, null)]
    [InlineData("", null)]
    public void MapsEvaluatorErrorsToTelemetry(string? errorCode, string? expected)
        => FeatureFlagEvaluationMappings.ToTelemetryErrorCode(errorCode).Should().Be(expected);

    [Theory]
    [InlineData((int)ErrorType.ProviderNotReady, "provider_not_ready")]
    [InlineData((int)ErrorType.FlagNotFound, "flag_not_found")]
    [InlineData((int)ErrorType.ParseError, "parse_error")]
    [InlineData((int)ErrorType.TypeMismatch, "type_mismatch")]
    [InlineData((int)ErrorType.TargetingKeyMissing, "targeting_key_missing")]
    [InlineData((int)ErrorType.InvalidContext, "invalid_context")]
    [InlineData((int)ErrorType.ProviderFatal, "provider_fatal")]
    [InlineData((int)ErrorType.General, "general")]
    [InlineData((int)ErrorType.None, "general")]
    public void MapsOpenFeatureErrorsToTelemetry(int errorType, string expected)
        => FeatureFlagEvaluationMappings.ToTelemetryErrorCode((ErrorType)errorType).Should().Be(expected);
}
