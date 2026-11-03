// <copyright file="FeatureFlagsSdk.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.FeatureFlags;
using OpenFeature.Constant;
using OpenFeature.Model;

namespace Datadog.FeatureFlags.OpenFeature;

/// <summary>
/// Functions to retrieve FeatureFlags from server
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
[Browsable(false)]
internal static class FeatureFlagsSdk
{
    private static readonly ConditionalWeakTable<ImmutableMetadata, ResultState> Results = new();
    private static readonly string[] FeaturePrefixes = [FeatureFlagMetadataKeys.FeaturePrefix, FeatureFlagMetadataKeys.ExposureFeaturePrefix, FeatureFlagMetadataKeys.EvaluationFeaturePrefix];

    // OpenFeature 2.3 passes the invocation context, not the one a Before hook returned, to Finally.
    private static readonly ConditionalWeakTable<ImmutableMetadata, EvaluationContext> EvaluatedContexts = new();

    internal delegate IEvaluation? EvaluationCallback(string flagKey, Trace.FeatureFlags.ValueType targetType, object? defaultValue, string? targetingKey, IDictionary<string, object?>? attributes);

    /// <summary> Gets a value indicating whether FeatureFlags framework is available or not </summary>
    /// <returns> True if FeatureFlagsSDK is instrumented </returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool IsAvailable() => false;

    /// <summary>Gets a value indicating whether APM span enrichment is enabled.</summary>
    /// <returns> True when the span-enrichment gate is on </returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool IsSpanEnrichmentEnabled() => false;

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool IsEvaluationEventsEnabled() => false;

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool CanEnqueueEVP() => false;

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void EnqueueEVP(string flagKey, string? variant, string? allocationKey, string? targetingKey, long evalTimeMs, string? errorCode, IReadOnlyDictionary<string, object?>? attrs, int flags)
    {
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool CanEnqueueEVPWithFeatures() => false;

    // Called only when the split has EVALUATION features: the ninth argument takes CallTarget's
    // allocating slow path, which every other evaluation avoids by calling EnqueueEVP.
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void EnqueueEVPWithFeatures(string flagKey, string? variant, string? allocationKey, string? targetingKey, long evalTimeMs, string? errorCode, IReadOnlyDictionary<string, object?>? attrs, IReadOnlyDictionary<string, object?>? features, int flags)
    {
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void RecordEVPHookError()
    {
    }

    /// <summary>
    /// Gets a value indicating whether flag configuration is currently held, so the provider can
    /// resolve flags. Goes back to <c>false</c> when configuration is withdrawn.
    /// </summary>
    /// <returns> True while configuration is held </returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool HasConfiguration() => false;

    /// <summary>
    /// Activates flag configuration delivery and waits for the first configuration to arrive.
    /// Delivery only starts here, because requesting configuration is billable and installing the
    /// tracer alone must not do it.
    /// </summary>
    /// <param name="cancellationToken"> Cancellation token. OpenFeature 2.3.0 does not forward one through SetProviderAsync, so only a direct caller supplies it </param>
    /// <returns> A task that completes once configuration has arrived or the initialization timeout has elapsed, and that faults when no source could start delivery at all </returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary> Installs an event handler to be fired when a new config has been received </summary>
    /// <param name="onNewConfig"> Action to be called when the event is fired </param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void RegisterOnNewConfigEventHandler(Action onNewConfig)
    {
    }

    /// <summary>Gets a value indicating whether the tracer supports <see cref="EvaluateDeferred"/> and <see cref="CommitExposure"/>.</summary>
    /// <returns> True when the tracer intercepts both </returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool CanDeferExposures() => false;

    // Like Evaluate, but the tracer keeps the exposure pending instead of recording and sending it.
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static IEvaluation? EvaluateDeferred(string flagKey, Trace.FeatureFlags.ValueType targetType, object? defaultValue, string? targetingKey, IDictionary<string, object?>? attributes)
    {
        if (flagKey is null)
        {
            throw new ArgumentNullException(nameof(flagKey));
        }

        return null;
    }

    // Takes the metadata dictionary instance that EvaluateDeferred returned: the tracer finds the
    // pending exposure by that reference, so a copy would commit nothing.
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static int CommitExposure(IDictionary<string, string> evaluationMetadata) => ExposureCommitResult.NothingPending;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static IEvaluation? Evaluate(string flagKey, Trace.FeatureFlags.ValueType targetType, object? defaultValue, string? targetingKey, IDictionary<string, object?>? attributes)
    {
        if (flagKey is null)
        {
            throw new ArgumentNullException(nameof(flagKey));
        }

        return null;
    }

    /// <summary>Accumulates a single flag evaluation into the active root span's FFE span-enrichment state.</summary>
    /// <param name="serialId"> Split serial id, or null when absent </param>
    /// <param name="doLog"> Whether the allocation authorizes subject logging </param>
    /// <param name="targetingKey"> Evaluation-context targeting key, or null </param>
    /// <param name="hasVariant"> Whether the evaluation produced a non-empty variant </param>
    /// <param name="flagKey"> The flag key (used for runtime defaults) </param>
    /// <param name="value"> The evaluated value (used for runtime defaults) </param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void AccumulateSpanEnrichment(long? serialId, bool doLog, string? targetingKey, bool hasVariant, string flagKey, object? value)
    {
    }

    public static ResolutionDetails<T> Resolve<T>(string flagKey, Trace.FeatureFlags.ValueType targetType, T defaultValue, EvaluationContext? context, EvaluationCallback? evaluate, FlagEvalEVPHook? evaluationEventsHook, bool deferredExposure)
    {
        long? evalTimeMs = evaluationEventsHook is not null ? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() : null;
        var attributes = GetContextAttributes(context);
        var evaluation = evaluate is null
                             ? Evaluate(flagKey, targetType, defaultValue, context?.TargetingKey, attributes)
                             : evaluate(flagKey, targetType, defaultValue, context?.TargetingKey, attributes);
        var resolution = GetResolutionDetails(flagKey, defaultValue, evaluation, evalTimeMs, deferredExposure);
        evaluationEventsHook?.CaptureEvaluation(context, resolution.FlagMetadata);
        // Only exposures need the context in ExposureHook, so other evaluations skip the table.
        if (context is not null && resolution.FlagMetadata is { } metadata && metadata.GetBool(FeatureFlagMetadataKeys.DoLog) == true)
        {
            EvaluatedContexts.Add(metadata, context);
        }

        return resolution;
    }

    private static IDictionary<string, object?>? GetContextAttributes(EvaluationContext? context)
    {
        if (context is null)
        {
            return null;
        }

        return context.AsDictionary().Select(p => new KeyValuePair<string, object?>(p.Key, ToObject(p.Value))).ToDictionary(p => p.Key, p => p.Value);
    }

    private static object? ToObject(Value value) => value switch
    {
        null => null,
        { IsBoolean: true } => value.AsBoolean,
        { IsString: true } => value.AsString,
        { IsNumber: true } => value.AsDouble,
        _ => value.AsObject,
    };

    /// <summary>
    /// The selected split's HOOK-destination features associated with a result's metadata, for
    /// <see cref="ExposureHook"/>. OpenFeature metadata has no enumeration API, so the provider
    /// records the typed map beside the result it built; the entry dies with the result.
    /// </summary>
    internal static IReadOnlyDictionary<string, object>? GetHookFeatures(ImmutableMetadata? metadata)
        => metadata is not null && Results.TryGetValue(metadata, out var state) ? state.HookFeatures : null;

    /// <summary>The selected split's EVALUATION-destination features, for evaluation counts.</summary>
    internal static IReadOnlyDictionary<string, object?>? GetEvaluationFeatures(ImmutableMetadata? metadata)
        => metadata is not null && Results.TryGetValue(metadata, out var state) ? state.EvaluationFeatures : null;

    /// <summary>The context the provider evaluated for an exposure result, or null when none was recorded.</summary>
    internal static EvaluationContext? GetEvaluatedContext(ImmutableMetadata? metadata)
        => metadata is not null && EvaluatedContexts.TryGetValue(metadata, out var context) ? context : null;

    /// <summary>
    /// Commits the exposure that a deferred evaluation left pending, at most once per result, and keeps
    /// the verdict for <see cref="GetExposureCacheHit"/>.
    /// </summary>
    internal static void CommitPendingExposure(ImmutableMetadata? metadata, Func<IDictionary<string, string>, int> commit)
    {
        if (metadata is null || !Results.TryGetValue(metadata, out var state)
            || Interlocked.Exchange(ref state.PendingExposure, null) is not { } evaluationMetadata)
        {
            return;
        }

        state.ExposureVerdict = commit(evaluationMetadata);
    }

    /// <summary>The verdict of the committed exposure, or null when this result committed none.</summary>
    internal static bool? GetExposureCacheHit(ImmutableMetadata? metadata)
        => metadata is not null && Results.TryGetValue(metadata, out var state)
               ? state.ExposureVerdict switch
               {
                   ExposureCommitResult.New => false,
                   ExposureCommitResult.Repeat => true,
                   _ => null,
               }
               : null;

    internal static ResolutionDetails<T> GetResolutionDetails<T>(string flagKey, T defaultValue, Datadog.Trace.FeatureFlags.IEvaluation? evaluation, long? evalTimeMs, bool deferredExposure)
    {
        // OpenFeature substitutes the caller's default only when a provider throws. This provider
        // reports errors as details instead, so every error must carry the default itself.
        if (evaluation is null)
        {
            return new ResolutionDetails<T>(
                        flagKey,
                        defaultValue,
                        ErrorType.ProviderNotReady,
                        default,
                        default,
                        "FeatureFlagsSdk is disabled",
                        ToMetadata(null, evalTimeMs, false));
        }

        var errorType = ToErrorType(evaluation.Reason, evaluation.Error);
        var value = errorType != ErrorType.None
                        ? defaultValue
                        : typeof(T) == typeof(Value) ? (T)(object)JsonToValue(evaluation.Value) : (T)evaluation.Value!;
        var res = new ResolutionDetails<T>(
            evaluation.FlagKey,
            value,
            errorType,
            ReasonToLowerSnakeCase(evaluation.Reason),
            evaluation.Variant,
            evaluation.Error,
            ToMetadata(evaluation.FlagMetadata, evalTimeMs, deferredExposure));
        return res;
    }

    private static ErrorType ToErrorType(Datadog.Trace.FeatureFlags.EvaluationReason reason, string? errorMessage)
    {
        return errorMessage switch
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
    }

    // Converts EvaluationReason enum to lower_snake_case string for OpenFeature Reason field.
    // Uses cached strings to avoid allocation.
    private static string ReasonToLowerSnakeCase(Datadog.Trace.FeatureFlags.EvaluationReason reason) => reason switch
    {
        Datadog.Trace.FeatureFlags.EvaluationReason.Static => "static",
        Datadog.Trace.FeatureFlags.EvaluationReason.Default => "default",
        Datadog.Trace.FeatureFlags.EvaluationReason.TargetingMatch => "targeting_match",
        Datadog.Trace.FeatureFlags.EvaluationReason.Split => "split",
        Datadog.Trace.FeatureFlags.EvaluationReason.Disabled => "disabled",
        Datadog.Trace.FeatureFlags.EvaluationReason.Cached => "cached",
        Datadog.Trace.FeatureFlags.EvaluationReason.Unknown => "unknown",
        Datadog.Trace.FeatureFlags.EvaluationReason.Error => "error",
        _ => "unknown"
    };

    private static ImmutableMetadata? ToMetadata(IDictionary<string, string>? metadata, long? evalTimeMs, bool deferredExposure)
    {
        if (metadata is null && evalTimeMs is null)
        {
            return null;
        }

        var dic = new Dictionary<string, object>();
        Dictionary<string, object>? hookFeatures = null;
        Dictionary<string, object?>? evaluationFeatures = null;
        if (metadata is not null)
        {
            var featureTypes = metadata.TryGetValue(FeatureFlagMetadataKeys.FeatureTypes, out var encodedTypes) ? ParseFeatureTypes(encodedTypes) : null;
            foreach (var pair in metadata)
            {
                if (pair.Key == FeatureFlagMetadataKeys.FeatureTypes)
                {
                    continue;
                }

                if (FeaturePrefixOf(pair.Key) is not { } prefix)
                {
                    dic[pair.Key] = ConvertMetadataValue(pair.Key, pair.Value);
                    continue;
                }

                var featureKey = pair.Key.Substring(prefix.Length);
                var converted = ConvertFeatureValue(pair.Value, featureTypes is not null && featureTypes.TryGetValue(featureKey, out var type) ? type : '\0');
                dic[pair.Key] = converted;
                if (prefix == FeatureFlagMetadataKeys.FeaturePrefix)
                {
                    hookFeatures ??= new Dictionary<string, object>();
                    hookFeatures[featureKey] = converted;
                }
                else if (prefix == FeatureFlagMetadataKeys.EvaluationFeaturePrefix)
                {
                    evaluationFeatures ??= new Dictionary<string, object?>();
                    evaluationFeatures[featureKey] = converted;
                }
            }
        }

        if (evalTimeMs is { } time)
        {
            dic[FeatureFlagMetadataKeys.ObserveFullEvaluationData] =
                metadata is not null && metadata.TryGetValue(FeatureFlagMetadataKeys.ObserveFullEvaluationData, out var consent) && consent == "true";
            // OpenFeature exposes numeric metadata as int or double. Epoch milliseconds are exact as a double.
            dic[FeatureFlagMetadataKeys.EvaluationTimestampMs] = (double)time;
        }
        else
        {
            // The evaluator still captures consent, but no EVP hook consumes these private fields.
            dic.Remove(FeatureFlagMetadataKeys.ObserveFullEvaluationData);
            dic.Remove(FeatureFlagMetadataKeys.EvaluationTimestampMs);
        }

        var immutable = new ImmutableMetadata(dic);
        // Keeps the tracer's own dictionary instance, not a copy, for CommitExposure.
        var pendingExposure = deferredExposure && metadata is not null && metadata.TryGetValue(FeatureFlagMetadataKeys.DoLog, out var doLog) && doLog == "true"
                                  ? metadata
                                  : null;
        if (hookFeatures is not null || evaluationFeatures is not null || pendingExposure is not null)
        {
            Results.Add(immutable, new ResultState(hookFeatures, evaluationFeatures, pendingExposure));
        }

        return immutable;
    }

    // The tracer hands metadata across the package boundary as strings; OpenFeature metadata exposes
    // typed values, so the known non-feature keys are converted here. Features go through ConvertFeatureValue.
    private static object ConvertMetadataValue(string key, string value)
    {
        if (key == FeatureFlagMetadataKeys.DoLog || key == FeatureFlagMetadataKeys.ExposureCacheHit)
        {
            return value == "true";
        }

        if (key == FeatureFlagMetadataKeys.SplitSerialId
            && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var serialId))
        {
            // The serial id comes from a uint32 configuration value; the int form preserves its bits.
            return unchecked((int)serialId);
        }

        return value;
    }

    private static string? FeaturePrefixOf(string metadataKey)
    {
        foreach (var prefix in FeaturePrefixes)
        {
            if (metadataKey.StartsWith(prefix, StringComparison.Ordinal))
            {
                return prefix;
            }
        }

        return null;
    }

    // A value is a boolean or a number only when the tracer listed it as one, so a string feature
    // whose value looks like a boolean or a number stays a string.
    private static object ConvertFeatureValue(string value, char type) => type switch
    {
        'b' when value == "true" => true,
        'b' when value == "false" => false,
        'n' when double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) => number,
        _ => value,
    };

    // Reads FeatureFlagMetadataKeys.FeatureTypes. A malformed item never fails the evaluation: an
    // unknown type letter skips that item, and a broken length prefix ends parsing, so the remaining
    // values stay strings.
    private static Dictionary<string, char>? ParseFeatureTypes(string encoded)
    {
        Dictionary<string, char>? types = null;
        var position = 0;
        while (position < encoded.Length)
        {
            var colon = encoded.IndexOf(':', position);
            if (colon <= position
                || !int.TryParse(encoded.Substring(position, colon - position), NumberStyles.None, CultureInfo.InvariantCulture, out var length)
                || length > encoded.Length - colon - 2)
            {
                break;
            }

            var key = encoded.Substring(colon + 1, length);
            var type = encoded[colon + 1 + length];
            position = colon + 2 + length;
            if (type is 'b' or 'n')
            {
                types ??= new Dictionary<string, char>(StringComparer.Ordinal);
                types[key] = type;
            }
        }

        return types;
    }

    public static Value JsonToValue(object? obj)
    {
        try
        {
            if (obj is null)
            {
                return new Value();
            }

            return ConvertObject(obj);
        }
        catch
        {
            return new Value();
        }
    }

    private static Value ConvertObject(object? obj) => obj switch
    {
        // The evaluator can return the caller's JSON default, which is already an OpenFeature Value.
        Value value => value,
        Dictionary<string, object?> dic => ConvertStructure(dic),
        object?[] arr => ConvertArray(arr),
        long intVal => new Value(intVal),
        double doubleVal => new Value(doubleVal),
        string strVal => new Value(strVal),
        bool boolVal => new Value(boolVal),
        _ => new Value()
    };

    private static Value ConvertStructure(Dictionary<string, object?> structure)
    {
        var dic = structure.ToDictionary(p => p.Key, p => ConvertObject(p.Value));
        return new Value(new Structure(dic));
    }

    private static Value ConvertArray(object?[] array)
    {
        return new Value(array.Select(ConvertObject).ToList());
    }

    private sealed class ResultState(IReadOnlyDictionary<string, object>? hookFeatures, IReadOnlyDictionary<string, object?>? evaluationFeatures, IDictionary<string, string>? pendingExposure)
    {
#pragma warning disable SA1401 // Fields should be private: exchanged atomically, so a commit runs at most once
        internal IDictionary<string, string>? PendingExposure = pendingExposure;
        internal volatile int ExposureVerdict = ExposureCommitResult.NothingPending;
#pragma warning restore SA1401

        internal IReadOnlyDictionary<string, object>? HookFeatures { get; } = hookFeatures;

        internal IReadOnlyDictionary<string, object?>? EvaluationFeatures { get; } = evaluationFeatures;
    }
}
