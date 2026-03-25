// <copyright file="FlagEvalMetricsHook.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if NET6_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OpenFeature;
using OpenFeature.Constant;
using OpenFeature.Model;

namespace Datadog.FeatureFlags.OpenFeature;

/// <summary>
/// OpenFeature hook that records flag evaluation metrics.
/// Uses the Finally hook stage so metrics are recorded after all evaluation logic completes,
/// including type conversion errors that happen after evaluate() returns.
/// </summary>
internal sealed class FlagEvalMetricsHook : Hook, IDisposable
{
    private readonly FlagEvalMetrics _metrics;

    /// <summary>
    /// Initializes a new instance of the <see cref="FlagEvalMetricsHook"/> class.
    /// </summary>
    public FlagEvalMetricsHook()
    {
        _metrics = new FlagEvalMetrics();
    }

    /// <inheritdoc/>
    public override ValueTask FinallyAsync<T>(
        HookContext<T> context,
        FlagEvaluationDetails<T> details,
        IReadOnlyDictionary<string, object>? hints = null,
        CancellationToken cancellationToken = default)
    {
        var flagKey = context.FlagKey;
        var variant = details.Variant ?? string.Empty;

        // Use "unknown" as fallback for missing reason (matches OpenFeature SDK telemetry convention)
        var reason = details.Reason;
        if (string.IsNullOrEmpty(reason))
        {
            reason = "unknown";
        }
        else
        {
            reason = reason.ToLowerInvariant();
        }

        // Extract error type if present
        string? errorType = null;
        if (details.ErrorType != ErrorType.None)
        {
            errorType = ErrorTypeToString(details.ErrorType);
        }

        // Extract allocation key from metadata if present
        string? allocationKey = null;
        if (details.FlagMetadata != null)
        {
            allocationKey = details.FlagMetadata.GetString(FlagEvalMetrics.MetadataAllocationKey);
        }

        _metrics.Record(flagKey, variant, reason, errorType, allocationKey);

        return default;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _metrics.Dispose();
    }

    private static string ErrorTypeToString(ErrorType errorType)
    {
        return errorType switch
        {
            ErrorType.ProviderNotReady => "provider_not_ready",
            ErrorType.FlagNotFound => "flag_not_found",
            ErrorType.ParseError => "parse_error",
            ErrorType.TypeMismatch => "type_mismatch",
            ErrorType.TargetingKeyMissing => "targeting_key_missing",
            ErrorType.InvalidContext => "invalid_context",
            ErrorType.ProviderFatal => "provider_fatal",
            ErrorType.General => "general",
            _ => "general"
        };
    }
}
#endif
