// <copyright file="FlagEvalLoggingHook.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OpenFeature;
using OpenFeature.Constant;
using OpenFeature.Model;

namespace Datadog.FeatureFlags.OpenFeature;

/// <summary>
/// OpenFeature hook that enqueues flag evaluation events for EVP <c>flagevaluation</c> aggregation.
/// Uses the FinallyAsync stage so it fires for every evaluation path (success, error, and default).
/// Does ONLY cheap capture + non-blocking enqueue on the eval hot path — NO inline aggregation.
/// Routes through FeatureFlagsSdk.EnqueueEVP (static delegate bridge wired by FeatureFlagsModule
/// in the auto-instrumentation side) to avoid a cross-assembly reference to FlagEvaluationApi.
/// The existing OTel FlagEvalMetricsHook is left unmodified (no regression to that metric path).
/// </summary>
internal sealed class FlagEvalLoggingHook : Hook
{
    /// <summary>
    /// Metadata key for the evaluation timestamp stamped by the provider at eval entry.
    /// Stored as a string in the metadata dictionary (ImmutableMetadata converts strings).
    /// Falls back to DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() when absent.
    /// </summary>
    private const string MetadataEvalTimeKey = "dd.eval.timestamp_ms";

    /// <summary>
    /// Metadata key for the allocation key.
    /// Matches FlagEvalMetrics.MetadataAllocationKey intentionally.
    /// </summary>
    private const string MetadataAllocationKey = "__dd_allocation_key";

    private const int MaxContextFields = 256;
    private const int MaxFieldLength = 256;

    /// <summary>
    /// Initializes a new instance of the <see cref="FlagEvalLoggingHook"/> class.
    /// </summary>
    public FlagEvalLoggingHook()
    {
    }

    /// <inheritdoc/>
    /// <remarks>
    /// FinallyAsync fires after all hook stages (Before/After/Error) on every evaluation path
    /// including error and default paths — this ensures error/default evaluations are counted, not
    /// just successful ones. The body does only cheap scalar extraction and a non-blocking call to
    /// FeatureFlagsSdk.EnqueueEVP; aggregation happens on the background send loop.
    /// </remarks>
    public override ValueTask FinallyAsync<T>(
        HookContext<T> context,
        FlagEvaluationDetails<T> details,
        IReadOnlyDictionary<string, object>? hints = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var flagKey = context.FlagKey;

            // Variant: an absent (null/empty) variant means the runtime default was used. Pass null
            // explicitly so the aggregator marks runtime_default_used rather than keying on "".
            string? variant = string.IsNullOrEmpty(details.Variant) ? null : details.Variant;

            // Allocation key from metadata.
            string? allocationKey = details.FlagMetadata?.GetString(MetadataAllocationKey);

            // Targeting key from the evaluation context (cheap string read).
            string? targetingKey = context.EvaluationContext?.TargetingKey;

            string? errorMessage = details.ErrorType == ErrorType.None ? null : ErrorTypeToString(details.ErrorType);

            // Eval time: prefer provider-stamped timestamp for accuracy; fall back to hook-fire time.
            // The evaluator stores metadata as string, so GetString and parse.
            long evalTimeMs = 0;
            string? evalTimeStr = details.FlagMetadata?.GetString(MetadataEvalTimeKey);
            if (!string.IsNullOrEmpty(evalTimeStr) &&
                long.TryParse(evalTimeStr, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out long parsedMs))
            {
                evalTimeMs = parsedMs;
            }

            if (evalTimeMs == 0)
            {
                evalTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }

            // Flatten and bound context attributes before handing them to the enqueue bridge.
            IDictionary<string, object?>? contextAttrs = ExtractContextAttrs(context.EvaluationContext);

            // Route via the static delegate bridge (wired from FeatureFlagsModule when EVP is enabled).
            // No-op when the bridge is null (EVP disabled or tracer not initialized).
            FeatureFlagsSdk.EnqueueEVP(flagKey, variant, allocationKey, targetingKey, errorMessage, evalTimeMs, contextAttrs);
        }
        catch (Exception ex)
        {
            // EVP recording must never break flag evaluation.
            System.Diagnostics.Debug.WriteLine($"[Datadog] FlagEvalLoggingHook.FinallyAsync failed: {ex}");
        }

        return default;
    }

    /// <summary>
    /// Extracts context attributes from the OpenFeature evaluation context as a bounded plain object map.
    /// Converts OpenFeature <see cref="Value"/> to native types for the aggregation layer.
    /// </summary>
    private static IDictionary<string, object?>? ExtractContextAttrs(EvaluationContext? ctx)
    {
        if (ctx is null)
        {
            return null;
        }

        var pairs = ctx.AsDictionary();
        if (pairs is null)
        {
            return null;
        }

        var flattened = new Dictionary<string, object?>();
        foreach (var kv in pairs)
        {
            // Skip the targeting_key entry — it is captured separately via context.TargetingKey.
            // The key used in AsDictionary() is "targetingKey" (the internal TargetingKeyIndex constant).
            if (kv.Key == "targetingKey")
            {
                continue;
            }

            FlattenValue(kv.Key, kv.Value, flattened);
        }

        if (flattened.Count == 0)
        {
            return null;
        }

        var keys = new List<string>(flattened.Keys);
        keys.Sort(StringComparer.Ordinal);

        var result = new Dictionary<string, object?>(Math.Min(flattened.Count, MaxContextFields));
        foreach (string key in keys)
        {
            if (result.Count >= MaxContextFields)
            {
                break;
            }

            object? value = flattened[key];
            if (value is string s && s.Length > MaxFieldLength)
            {
                continue;
            }

            result[key] = value;
        }

        return result.Count > 0 ? result : null;
    }

    private static void FlattenValue(string prefix, Value? value, Dictionary<string, object?> output)
    {
        if (value is null || value.IsNull)
        {
            output[prefix] = null;
            return;
        }

        if (value.IsStructure && value.AsStructure is { } structure)
        {
            foreach (var kv in structure.AsDictionary())
            {
                FlattenValue(prefix + "." + kv.Key, kv.Value, output);
            }

            return;
        }

        if (value.IsList && value.AsList is { } list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                FlattenValue(prefix + "." + i, list[i], output);
            }

            return;
        }

        output[prefix] = ValueToObject(value);
    }

    private static object? ValueToObject(Value value)
    {
        if (value.IsBoolean)
        {
            return value.AsBoolean;
        }

        if (value.IsString)
        {
            return value.AsString;
        }

        if (value.IsNumber)
        {
            return value.AsDouble;
        }

        if (value.IsDateTime)
        {
            return value.AsDateTime;
        }

        return value.AsObject?.ToString();
    }

    private static string ErrorTypeToString(ErrorType errorType) => errorType switch
    {
        ErrorType.ProviderNotReady => "provider_not_ready",
        ErrorType.FlagNotFound => "flag_not_found",
        ErrorType.ParseError => "parse_error",
        ErrorType.TypeMismatch => "type_mismatch",
        ErrorType.TargetingKeyMissing => "targeting_key_missing",
        ErrorType.InvalidContext => "invalid_context",
        ErrorType.ProviderFatal => "provider_fatal",
        ErrorType.General => "general",
        _ => "unknown"
    };
}
