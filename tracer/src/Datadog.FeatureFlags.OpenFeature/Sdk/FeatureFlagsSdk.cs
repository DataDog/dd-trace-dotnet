// <copyright file="FeatureFlagsSdk.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.FeatureFlags;
using Datadog.Trace.FeatureFlags.FlagEvaluation;
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
    private const int MaxContextContainerItems = 256;
    private const int MaxContextDepth = 4;
    private const int MaxContextNodes = 1_280;

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

    public static ResolutionDetails<T> Resolve<T>(string flagKey, Trace.FeatureFlags.ValueType targetType, T defaultValue, EvaluationContext? context, EvaluationCallback? evaluate = null, FlagEvalEVPHook? evaluationEventsHook = null, ExposureCommitHook? exposureCommitHook = null)
    {
        long? evalTimeMs = evaluationEventsHook is not null ? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() : null;
        var attributes = GetContextAttributes(context);
        var evaluation = evaluate is null
                             ? Evaluate(flagKey, targetType, defaultValue, context?.TargetingKey, attributes)
                             : evaluate(flagKey, targetType, defaultValue, context?.TargetingKey, attributes);
        var exposureCommit = GetExposureCommit(evaluation?.FlagMetadata);
        var resolution = GetResolutionDetails(flagKey, defaultValue, evaluation, evalTimeMs);
        exposureCommitHook?.Capture(resolution.FlagMetadata, exposureCommit);
        evaluationEventsHook?.CaptureEvaluation(context, resolution.FlagMetadata);
        return resolution;
    }

    private static IDictionary<string, object?>? GetContextAttributes(EvaluationContext? context)
    {
        if (context == null)
        {
            return null;
        }

        var result = new Dictionary<string, object?>();
        var omissionReasons = ContextOmissionReason.None;
        foreach (var pair in context.AsDictionary())
        {
            // Targeting must see every top-level attribute. Bound only recursive expansion of each
            // nested value; a large structure must not consume the budget of a later scalar.
            var remainingNodes = MaxContextNodes;
            result[pair.Key] = ToObject(pair.Value, depth: 1, ref remainingNodes, ref omissionReasons);
        }

        return new ContextAttributes(result, omissionReasons);
    }

    private static object? ToObject(Value value, int depth, ref int remainingNodes, ref ContextOmissionReason omissionReasons)
    {
        if (remainingNodes-- <= 0)
        {
            omissionReasons |= ContextOmissionReason.MaxVisitedNodes;
            return null;
        }

        if (depth > MaxContextDepth)
        {
            omissionReasons |= ContextOmissionReason.MaxSnapshotDepth;
            return null;
        }

        if (value is null) { return null; }
        if (value.IsNull) { return null; }
        if (value.IsBoolean) { return value.AsBoolean; }
        if (value.IsString) { return value.AsString; }
        if (value.IsNumber) { return value.AsDouble; }
        if (value.AsDateTime is { } dateTime) { return dateTime; }
        if (value.IsStructure) { return ConvertStructure(value.AsStructure!.AsDictionary(), depth, ref remainingNodes, ref omissionReasons); }
        if (value.IsList) { return ConvertList(value.AsList!, depth, ref remainingNodes, ref omissionReasons); }
        omissionReasons |= ContextOmissionReason.UnsupportedValue;
        return null;
    }

    private static Dictionary<string, object?> ConvertStructure(IEnumerable<KeyValuePair<string, Value>> structure, int depth, ref int remainingNodes, ref ContextOmissionReason omissionReasons)
    {
        var result = new Dictionary<string, object?>();
        var count = 0;
        foreach (var pair in structure)
        {
            if (count++ >= MaxContextContainerItems)
            {
                omissionReasons |= ContextOmissionReason.MaxStructureProperties;
                break;
            }

            if (remainingNodes <= 0)
            {
                omissionReasons |= ContextOmissionReason.MaxVisitedNodes;
                break;
            }

            result[pair.Key] = ToObject(pair.Value, depth + 1, ref remainingNodes, ref omissionReasons);
        }

        return result;
    }

    private static object?[] ConvertList(IReadOnlyList<Value> list, int depth, ref int remainingNodes, ref ContextOmissionReason omissionReasons)
    {
        if (list.Count > MaxContextContainerItems)
        {
            omissionReasons |= ContextOmissionReason.MaxListElements;
        }

        var count = Math.Min(list.Count, MaxContextContainerItems);
        var result = new List<object?>(count);
        for (var i = 0; i < count && remainingNodes > 0; i++)
        {
            result.Add(ToObject(list[i], depth + 1, ref remainingNodes, ref omissionReasons));
        }

        if (result.Count < count)
        {
            omissionReasons |= ContextOmissionReason.MaxVisitedNodes;
        }

        return result.ToArray();
    }

    internal static ResolutionDetails<T> GetResolutionDetails<T>(string flagKey, T defaultValue, Datadog.Trace.FeatureFlags.IEvaluation? evaluation, long? evalTimeMs)
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
                        ToMetadata(null, evalTimeMs));
        }

        var errorType = ToErrorType(evaluation.Reason, evaluation.Error);
        var value = errorType != ErrorType.None
                        ? defaultValue
                        : typeof(T) == typeof(Value) ? (T)(object)JsonToValue(evaluation.Value) : (T)evaluation.Value!;
        var res = new ResolutionDetails<T>(
            evaluation.FlagKey,
            value,
            errorType,
            ToOpenFeatureReason(evaluation.Reason),
            evaluation.Variant,
            evaluation.Error,
            ToMetadata(evaluation.FlagMetadata, evalTimeMs));
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

    private static string ToOpenFeatureReason(Datadog.Trace.FeatureFlags.EvaluationReason reason) => reason switch
    {
        Datadog.Trace.FeatureFlags.EvaluationReason.Static => Reason.Static,
        Datadog.Trace.FeatureFlags.EvaluationReason.Default => Reason.Default,
        Datadog.Trace.FeatureFlags.EvaluationReason.TargetingMatch => Reason.TargetingMatch,
        Datadog.Trace.FeatureFlags.EvaluationReason.Split => Reason.Split,
        Datadog.Trace.FeatureFlags.EvaluationReason.Disabled => Reason.Disabled,
        Datadog.Trace.FeatureFlags.EvaluationReason.Cached => Reason.Cached,
        Datadog.Trace.FeatureFlags.EvaluationReason.Unknown => Reason.Unknown,
        Datadog.Trace.FeatureFlags.EvaluationReason.Error => Reason.Error,
        _ => Reason.Unknown
    };

    private static ImmutableMetadata? ToMetadata(IDictionary<string, string>? metadata, long? evalTimeMs)
    {
        if (metadata is null && evalTimeMs is null)
        {
            return null;
        }

        var dic = (metadata ?? new Dictionary<string, string>()).ToDictionary(p => p.Key, p => (object)p.Value);
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

        return new ImmutableMetadata(dic);
    }

    private static Action? GetExposureCommit(IDictionary<string, string>? metadata)
    {
        try
        {
            var type = metadata?.GetType();
            if (type?.FullName != "Datadog.Trace.FeatureFlags.ExposureCommitMetadata" ||
                type.Assembly.GetName().Name != "Datadog.Trace")
            {
                return null;
            }

            return type.GetProperty("ExposureCommit")?.GetValue(metadata) as Action;
        }
        catch
        {
            return null;
        }
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

    internal sealed class ContextAttributes(Dictionary<string, object?> attributes, ContextOmissionReason omissionReasons) : Dictionary<string, object?>(attributes)
    {
        // Read by the tracer integration through duck typing. Keeping this on the dictionary avoids
        // changing the instrumented Evaluate signature and preserves compatibility with older tracers.
        public int ContextOmissionReasons { get; } = (int)omissionReasons;
    }
}
