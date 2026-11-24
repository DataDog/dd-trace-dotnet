// <copyright file="FlagEvalMetricsHook.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if NET6_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Globalization;
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
    public override ValueTask AfterAsync<T>(
        HookContext<T> context,
        FlagEvaluationDetails<T> details,
        IReadOnlyDictionary<string, object>? hints = null,
        CancellationToken cancellationToken = default)
    {
        RecordDependencies(details.FlagMetadata);
        return default;
    }

    /// <inheritdoc/>
    public override ValueTask FinallyAsync<T>(
        HookContext<T> context,
        FlagEvaluationDetails<T> details,
        IReadOnlyDictionary<string, object>? hints = null,
        CancellationToken cancellationToken = default)
    {
        RecordDependencies(details.FlagMetadata);

        try
        {
            var flagKey = context.FlagKey;
            var variant = details.Variant ?? string.Empty;

            var reason = ToTelemetryReason(details.Reason);

            // Extract error type if present
            string? errorType = null;
            if (details.ErrorType != ErrorType.None)
            {
                errorType = FeatureFlagEvaluationMappings.ToTelemetryErrorCode(details.ErrorType);
            }

            // Extract allocation key from metadata if present
            var allocationKey = details.FlagMetadata?.GetString(FlagEvalMetrics.MetadataAllocationKey);

            _metrics.Record(flagKey, variant, reason, errorType, allocationKey);
        }
        catch (Exception)
        {
            // Metrics recording should never break flag evaluation.
        }

        return default;
    }

    private void RecordDependencies(ImmutableMetadata? metadata)
    {
        if (FeatureFlagsSdk.GetResult(metadata) is not { } state
         || !state.TryTakeDependencyEvaluations(out var dependencies)
         || dependencies is null)
        {
            return;
        }

        for (var i = 0; i < dependencies.Count; i++)
        {
            try
            {
                var dependency = dependencies[i];
                _metrics.Record(
                    dependency.FlagKey,
                    dependency.Variant,
                    FeatureFlagEvaluationMappings.ToReason((Datadog.Trace.FeatureFlags.EvaluationReason)dependency.Reason),
                    FeatureFlagEvaluationMappings.ToTelemetryErrorCode(dependency.ErrorCode),
                    dependency.AllocationKey);
            }
            catch (Exception)
            {
                // A malformed dependency record must not suppress later dependencies or the root.
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _metrics.Dispose();
    }

    // OpenFeature reasons are canonical uppercase strings, while the Datadog telemetry
    // convention uses lower_snake_case tags. Cache the standard mappings on this hot path.
    private static string ToTelemetryReason(string? reason) => reason switch
    {
        null or "" => "unknown",
        Reason.TargetingMatch => "targeting_match",
        Reason.Split => "split",
        Reason.Disabled => "disabled",
        Reason.Default => "default",
        Reason.Static => "static",
        Reason.Cached => "cached",
        Reason.Unknown => "unknown",
        Reason.Error => "error",
        _ => reason.ToLower(CultureInfo.InvariantCulture)
    };
}
#endif
