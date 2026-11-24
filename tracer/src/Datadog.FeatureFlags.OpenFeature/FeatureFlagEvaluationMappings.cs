// <copyright file="FeatureFlagEvaluationMappings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using OpenFeature.Model;

namespace Datadog.FeatureFlags.OpenFeature;

internal static class FeatureFlagEvaluationMappings
{
    internal static string ToReason(Datadog.Trace.FeatureFlags.EvaluationReason reason) => reason switch
    {
        Datadog.Trace.FeatureFlags.EvaluationReason.Static => "static",
        Datadog.Trace.FeatureFlags.EvaluationReason.Default => "default",
        Datadog.Trace.FeatureFlags.EvaluationReason.TargetingMatch => "targeting_match",
        Datadog.Trace.FeatureFlags.EvaluationReason.Split => "split",
        Datadog.Trace.FeatureFlags.EvaluationReason.Disabled => "disabled",
        Datadog.Trace.FeatureFlags.EvaluationReason.Cached => "cached",
        Datadog.Trace.FeatureFlags.EvaluationReason.Error => "error",
        _ => "unknown",
    };

    internal static ErrorType ToErrorType(string? errorCode) => errorCode switch
    {
        "FLAG_NOT_FOUND" => ErrorType.FlagNotFound,
        "INVALID_CONTEXT" => ErrorType.InvalidContext,
        "PARSE_ERROR" => ErrorType.ParseError,
        "PROVIDER_FATAL" => ErrorType.ProviderFatal,
        "PROVIDER_NOT_READY" => ErrorType.ProviderNotReady,
        "TARGETING_KEY_MISSING" => ErrorType.TargetingKeyMissing,
        "TYPE_MISMATCH" => ErrorType.TypeMismatch,
        "GENERAL" => ErrorType.General,
        _ => ErrorType.None,
    };

    internal static string? ToTelemetryErrorCode(string? errorCode)
    {
        if (string.IsNullOrEmpty(errorCode))
        {
            return null;
        }

        var errorType = ToErrorType(errorCode);
        return errorType == ErrorType.None ? "general" : ToTelemetryErrorCode(errorType);
    }

    internal static string ToTelemetryErrorCode(ErrorType errorType) => errorType switch
    {
        ErrorType.ProviderNotReady => "provider_not_ready",
        ErrorType.FlagNotFound => "flag_not_found",
        ErrorType.ParseError => "parse_error",
        ErrorType.TypeMismatch => "type_mismatch",
        ErrorType.TargetingKeyMissing => "targeting_key_missing",
        ErrorType.InvalidContext => "invalid_context",
        ErrorType.ProviderFatal => "provider_fatal",
        ErrorType.General => "general",
        _ => "general",
    };
}
