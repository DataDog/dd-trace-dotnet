// <copyright file="FlagEvalEVPHook.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.FeatureFlags;
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
internal sealed class FlagEvalEVPHook : Hook
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
    private const int MaxContextDepth = 4;
    private const int MaxTopLevelFieldsWalked = 256;
    private const int MaxListElementsWalked = 256;
    private const int MaxStructurePropertiesWalked = 256;

    /// <summary>
    /// Initializes a new instance of the <see cref="FlagEvalEVPHook"/> class.
    /// </summary>
    public FlagEvalEVPHook()
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

            // Preserve null versus empty exactly: only null means the runtime default was used.
            // Empty is a real schema-visible variant identity and must remain a separate bucket.
            string? variant = details.Variant;

            // Allocation key from metadata.
            string? allocationKey = details.FlagMetadata?.GetString(MetadataAllocationKey);

            var observeFullEvaluationData = HasFullEvaluationDataConsent(details.FlagMetadata);

            string? targetingKey = FeatureEvaluationPrivacy.ProtectTargetingKey(
                context.EvaluationContext?.TargetingKey,
                observeFullEvaluationData);

            string? diagnosticError = null;
            if (observeFullEvaluationData && details.ErrorType != ErrorType.None)
            {
                try
                {
                    diagnosticError = details.FlagMetadata?.GetString("message");
                }
                catch
                {
                    // Wrong-typed metadata is not consent and must not affect flag evaluation.
                }

                if (string.IsNullOrEmpty(diagnosticError))
                {
                    diagnosticError = details.ErrorMessage;
                }
            }

            string? errorMessage = details.ErrorType == ErrorType.None
                                       ? null
                                       : FeatureEvaluationPrivacy.ProtectErrorDetails(
                                           ErrorTypeToString(details.ErrorType),
                                           diagnosticError,
                                           observeFullEvaluationData);

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

            // Protected mode never captures evaluation context. Full-data mode snapshots and bounds
            // it here, before the asynchronous hand-off, so later config changes cannot affect it.
            IDictionary<string, object?>? contextAttrs = observeFullEvaluationData
                                                              ? ExtractContextAttrs(context.EvaluationContext)
                                                              : null;

            // Route via the static delegate bridge (wired from FeatureFlagsModule when EVP is enabled).
            // No-op when the bridge is null (EVP disabled or tracer not initialized).
            FeatureFlagsSdk.EnqueueEVP(
                flagKey,
                variant,
                allocationKey,
                targetingKey,
                errorMessage,
                evalTimeMs,
                observeFullEvaluationData,
                contextAttrs);
        }
        catch (Exception ex)
        {
            // EVP recording must never break flag evaluation.
            System.Diagnostics.Debug.WriteLine($"[Datadog] FlagEvalEVPHook.FinallyAsync failed: {ex}");
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
        var walked = 0;
        foreach (var kv in pairs)
        {
            if (walked++ >= MaxTopLevelFieldsWalked)
            {
                break;
            }

            // Skip the targeting_key entry — it is captured separately via context.TargetingKey.
            // The key used in AsDictionary() is "targetingKey" (the internal TargetingKeyIndex constant).
            if (kv.Key == "targetingKey")
            {
                continue;
            }

            FlattenValue(kv.Key, kv.Value, flattened, depth: 0);
            if (flattened.Count >= MaxContextFields)
            {
                break;
            }
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

    private static void FlattenValue(string prefix, Value? value, Dictionary<string, object?> output, int depth)
    {
        if (output.Count >= MaxContextFields || prefix.Length > MaxFieldLength)
        {
            return;
        }

        if (value is null || value.IsNull)
        {
            output[prefix] = null;
            return;
        }

        if (value.IsStructure && value.AsStructure is { } structure)
        {
            if (depth >= MaxContextDepth)
            {
                return;
            }

            var walked = 0;
            foreach (var kv in structure.AsDictionary())
            {
                if (walked++ >= MaxStructurePropertiesWalked)
                {
                    break;
                }

                FlattenValue(prefix + "." + kv.Key, kv.Value, output, depth + 1);
                if (output.Count >= MaxContextFields)
                {
                    break;
                }
            }

            return;
        }

        if (value.IsList && value.AsList is { } list)
        {
            if (depth >= MaxContextDepth)
            {
                return;
            }

            var count = Math.Min(list.Count, MaxListElementsWalked);
            for (int i = 0; i < count; i++)
            {
                FlattenValue(prefix + "[" + i + "]", list[i], output, depth + 1);
                if (output.Count >= MaxContextFields)
                {
                    break;
                }
            }

            return;
        }

        var plainValue = ValueToObject(value);
        if (plainValue is not string text || text.Length <= MaxFieldLength)
        {
            output[prefix] = plainValue;
        }
    }

    private static bool HasFullEvaluationDataConsent(ImmutableMetadata? metadata)
    {
        try
        {
            return metadata?.GetBool(FeatureFlagMetadataKeys.ObserveFullEvaluationData) == true;
        }
        catch
        {
            return false;
        }
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
