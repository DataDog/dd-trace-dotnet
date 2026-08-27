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
    /// <summary>
    /// Metadata key carrying the evaluation timestamp (Unix milliseconds), stamped at provider
    /// entry so the EVP hook records evaluation time rather than the later hook-fire time.
    /// </summary>
    internal const string MetadataEvalTimeKey = "dd.eval.timestamp_ms";

    /// <summary>
    /// Enqueues one flag evaluation into the auto-instrumented EVP aggregation pipeline.
    /// This stub is a no-op when only the standalone package is loaded.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void EnqueueEVP(
        string flagKey,
        string? variant,
        string? allocationKey,
        string? targetingKey,
        string? errorMessage,
        long evalTimeMs,
        bool observeFullEvaluationData,
        IDictionary<string, object?>? contextAttrs)
    {
    }

    /// <summary> Gets a value indicating whether FeatureFlags framework is available or not </summary>
    /// <returns> True if FeatureFlagsSDK is instrumented </returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool IsAvailable() => false;

    /// <summary>Gets a value indicating whether APM span enrichment is enabled.</summary>
    /// <returns> True when the span-enrichment gate is on </returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool IsSpanEnrichmentEnabled() => false;

    /// <summary>
    /// Activates flag configuration delivery and waits for the first configuration to arrive.
    /// Agentless delivery only starts here, because those requests are billable and installing the
    /// tracer alone must not make them. With the Remote Configuration source, configuration is
    /// already being received by this point and this waits for the first update.
    /// </summary>
    /// <param name="cancellationToken"> Cancellation token supplied by OpenFeature </param>
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

    public static ResolutionDetails<T> Resolve<T>(string flagKey, Trace.FeatureFlags.ValueType targetType, object? defaultValue, EvaluationContext? context)
    {
        var evalTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return GetResolutionDetails<T>(Evaluate(flagKey, targetType, defaultValue, context?.TargetingKey, GetContextAttributes(context)), evalTimeMs);
    }

    private static IDictionary<string, object?>? GetContextAttributes(EvaluationContext? context)
    {
        if (context == null)
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

    private static ResolutionDetails<T> GetResolutionDetails<T>(Datadog.Trace.FeatureFlags.IEvaluation? evaluation, long evalTimeMs)
    {
        if (evaluation is null)
        {
            return new ResolutionDetails<T>(
                        string.Empty,
                        default!,
                        ErrorType.ProviderNotReady,
                        default,
                        default,
                        "FeatureFlagsSdk is disabled",
                        ToMetadata(null, evalTimeMs));
        }

        var value = typeof(T) == typeof(Value) ? JsonToValue(evaluation.Value) : evaluation.Value!;
        string? metadataErrorCode = null;
        evaluation.FlagMetadata?.TryGetValue("errorCode", out metadataErrorCode);
        var res = new ResolutionDetails<T>(
            evaluation.FlagKey,
            (T)value,
            ToErrorType(evaluation.Reason, metadataErrorCode, evaluation.Error),
            ReasonToLowerSnakeCase(evaluation.Reason),
            evaluation.Variant,
            evaluation.Error,
            ToMetadata(evaluation.FlagMetadata, evalTimeMs));
        return res;
    }

    private static ErrorType ToErrorType(Datadog.Trace.FeatureFlags.EvaluationReason reason, string? metadataErrorCode, string? errorMessage)
    {
        var metadataErrorType = StableCodeToErrorType(metadataErrorCode);
        if (metadataErrorType != ErrorType.None)
        {
            return metadataErrorType;
        }

        var messageErrorType = StableCodeToErrorType(errorMessage);
        if (messageErrorType != ErrorType.None)
        {
            return messageErrorType;
        }

        return reason == Datadog.Trace.FeatureFlags.EvaluationReason.Error ? ErrorType.General : ErrorType.None;
    }

    private static ErrorType StableCodeToErrorType(string? errorCode) => errorCode switch
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

    private static ImmutableMetadata ToMetadata(IDictionary<string, string>? metadata, long evalTimeMs)
    {
        var dic = (metadata ?? new Dictionary<string, string>())
                 .ToDictionary(
                      pair => pair.Key,
                      pair => pair.Key == FeatureFlagMetadataKeys.ObserveFullEvaluationData
                                  ? (object)string.Equals(pair.Value, "true", StringComparison.Ordinal)
                                  : pair.Value);
        dic[MetadataEvalTimeKey] = evalTimeMs.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return new ImmutableMetadata(dic);
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
}
