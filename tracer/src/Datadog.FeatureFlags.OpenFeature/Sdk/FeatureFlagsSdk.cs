// <copyright file="FeatureFlagsSdk.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.FeatureFlags;
using Datadog.Trace.FeatureFlags.FlagEvaluation;
using Datadog.Trace.SourceGenerators;
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
    private static readonly object BoxedTrue = true;
    private static readonly object BoxedFalse = false;

    // Set by the first ExposureHook. Until then, skip work that only ExposureHook needs.
    private static volatile bool _exposureHookCreated;

    internal delegate IEvaluation? EvaluationCallback(string flagKey, Trace.FeatureFlags.ValueType targetType, object? defaultValue, string? targetingKey, IDictionary<string, object?>? attributes);

    internal delegate IReadOnlyList<IDictionary<string, object?>>? DependencyTelemetryCallback(IDictionary<string, string> evaluationMetadata);

    internal static bool ExposureHookCreated => _exposureHookCreated;

    // Lets tests reset the process-wide flag.
    [TestingOnly]
    internal static bool ExposureHookCreatedForTests
    {
        get => _exposureHookCreated;
        set => _exposureHookCreated = value;
    }

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

    /// <summary> Hands a provider's flag selection to the tracer, which claims it for the process </summary>
    /// <param name="tags"> The selected tags, or <c>null</c> when none are set </param>
    /// <param name="keyPatterns"> The selected key patterns, or <c>null</c> when none are set </param>
    /// <returns> False with a tracer that predates flag selection, which then loads every flag </returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool SetFlagSelection(string?[]? tags, string?[]? keyPatterns) => false;

    /// <summary> Releases the claim <see cref="SetFlagSelection"/> made, once the provider is shut down or disposed </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ReleaseFlagSelection()
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

    // New tracers attach a detached prerequisite batch to their private metadata object. Older
    // tracers leave this stub unintercepted, so the provider remains backwards compatible.
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static IReadOnlyList<IDictionary<string, object?>>? GetDependencyTelemetry(IDictionary<string, string> evaluationMetadata) => null;

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

    public static ResolutionDetails<T> Resolve<T>(string flagKey, Trace.FeatureFlags.ValueType targetType, T defaultValue, EvaluationContext? context, bool deferredExposure, EvaluationCallback? evaluate = null, FlagEvalEVPHook? evaluationEventsHook = null, DependencyTelemetryCallback? getDependencyTelemetry = null)
    {
        long? evalTimeMs = evaluationEventsHook is not null ? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() : null;
        var attributes = GetContextAttributes(context);
        var evaluation = evaluate is null
                             ? Evaluate(flagKey, targetType, defaultValue, context?.TargetingKey, attributes)
                             : evaluate(flagKey, targetType, defaultValue, context?.TargetingKey, attributes);
        var resolution = GetResolutionDetails(flagKey, defaultValue, evaluation, evalTimeMs, deferredExposure: deferredExposure, getDependencyTelemetry: getDependencyTelemetry);
        evaluationEventsHook?.CaptureEvaluation(context, resolution.FlagMetadata);
        // OpenFeature 2.3 passes the invocation context, not the one a Before hook returned, to Finally.
        // Only ExposureHook reads it, and only for exposures.
        if (_exposureHookCreated && context is not null && resolution.FlagMetadata is { } metadata)
        {
            var result = GetResult(metadata);
            if (metadata.GetBool(FeatureFlagMetadataKeys.DoLog) == true || result?.HasDependencyExposures == true)
            {
                (result ?? Results.GetValue(metadata, static _ => new ResultState(null, null, null, null))).EvaluatedContext = context;
            }
        }

        return resolution;
    }

    internal static void OnExposureHookCreated()
    {
        // Write once: every Resolve reads this flag.
        if (!_exposureHookCreated)
        {
            _exposureHookCreated = true;
        }
    }

    private static IDictionary<string, object?>? GetContextAttributes(EvaluationContext? context)
    {
        if (context == null)
        {
            return null;
        }

        var source = context.AsDictionary();
        var result = new ContextAttributes(source.Count);
        var omissionReasons = ContextOmissionReason.None;
        var remainingNodes = ContextSnapshotLimits.Visits;
        foreach (var pair in source)
        {
            // Targeting must see every top-level scalar. Share one budget across recursive values so
            // many nested attributes cannot multiply the conversion work before tracer evaluation.
            var consumesRecursiveBudget = pair.Value is not null && (pair.Value.IsStructure || pair.Value.IsList);
            result[pair.Key] = ToObject(pair.Value, depth: 1, ref remainingNodes, ref omissionReasons, consumesRecursiveBudget);
        }

        result.ContextOmissionReasons = (int)omissionReasons;
        return result;
    }

    private static object? ToObject(Value? value, int depth, ref int remainingNodes, ref ContextOmissionReason omissionReasons, bool consumeNode)
    {
        if (consumeNode && remainingNodes <= 0)
        {
            omissionReasons |= ContextOmissionReason.MaxVisitedNodes;
            // Preserve whether a supplied top-level container is null when the bounded snapshot
            // budget is exhausted. Targeting still needs to distinguish an omitted expansion from
            // an explicitly null attribute (for example, for IS_NULL conditions).
            return EmptyContainer(value);
        }

        if (consumeNode)
        {
            remainingNodes--;
        }

        if (depth > ContextSnapshotLimits.Depth)
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
        if (value.AsStructure is { } structure) { return ConvertStructure(structure.AsDictionary(), depth, ref remainingNodes, ref omissionReasons); }
        if (value.AsList is { } list) { return ConvertList(list, depth, ref remainingNodes, ref omissionReasons); }
        omissionReasons |= ContextOmissionReason.UnsupportedValue;
        return null;
    }

    private static Dictionary<string, object?> ConvertStructure(IEnumerable<KeyValuePair<string, Value>> structure, int depth, ref int remainingNodes, ref ContextOmissionReason omissionReasons)
    {
        var result = new Dictionary<string, object?>();
        var count = 0;
        foreach (var pair in structure)
        {
            if (count++ >= ContextSnapshotLimits.ContainerItems)
            {
                omissionReasons |= ContextOmissionReason.MaxStructureProperties;
                break;
            }

            if (remainingNodes <= 0)
            {
                omissionReasons |= ContextOmissionReason.MaxVisitedNodes;
                break;
            }

            result[pair.Key] = ToObject(pair.Value, depth + 1, ref remainingNodes, ref omissionReasons, consumeNode: true);
        }

        return result;
    }

    private static object? EmptyContainer(Value? value)
        => value?.IsStructure == true ? new Dictionary<string, object?>()
         : value?.IsList == true ? Array.Empty<object?>()
         : null;

    private static object?[] ConvertList(IReadOnlyList<Value> list, int depth, ref int remainingNodes, ref ContextOmissionReason omissionReasons)
    {
        if (list.Count > ContextSnapshotLimits.ContainerItems)
        {
            omissionReasons |= ContextOmissionReason.MaxListElements;
        }

        var count = Math.Min(list.Count, ContextSnapshotLimits.ContainerItems);
        var result = new object?[count];
        var converted = 0;
        while (converted < count && remainingNodes > 0)
        {
            result[converted] = ToObject(list[converted], depth + 1, ref remainingNodes, ref omissionReasons, consumeNode: true);
            converted++;
        }

        if (converted < count)
        {
            omissionReasons |= ContextOmissionReason.MaxVisitedNodes;
            Array.Resize(ref result, converted);
        }

        return result;
    }

    /// <summary>
    /// The selected split's HOOK-destination features associated with a result's metadata, for
    /// <see cref="ExposureHook"/>. OpenFeature metadata has no enumeration API, so the provider
    /// records the typed map beside the result it built; the entry dies with the result.
    /// </summary>
    [TestingOnly]
    internal static IReadOnlyDictionary<string, object>? GetHookFeatures(ImmutableMetadata? metadata)
        => GetResult(metadata)?.HookFeatures;

    /// <summary>The selected split's EVALUATION-destination features, for evaluation counts.</summary>
    internal static IReadOnlyDictionary<string, object?>? GetEvaluationFeatures(ImmutableMetadata? metadata)
        => GetResult(metadata)?.EvaluationFeatures;

    /// <summary>The context the provider evaluated for an exposure result, or null when none was recorded.</summary>
    [TestingOnly]
    internal static EvaluationContext? GetEvaluatedContext(ImmutableMetadata? metadata)
        => GetResult(metadata)?.EvaluatedContext;

    /// <summary>
    /// The state recorded beside a result, or null. One lookup serves all <see cref="ExposureHook"/> reads
    /// (each lookup locks on .NET Framework).
    /// </summary>
    internal static ResultState? GetResult(ImmutableMetadata? metadata)
        => metadata is not null && Results.TryGetValue(metadata, out var state) ? state : null;

    /// <summary>
    /// Commits the exposure that a deferred evaluation left pending, at most once per result, and keeps
    /// the verdict for <see cref="ResultState.TryGetCommittedVerdict"/>.
    /// </summary>
    internal static void CommitPendingExposure(ImmutableMetadata? metadata, Func<IDictionary<string, string>, int> commit)
    {
        if (metadata is null || !Results.TryGetValue(metadata, out var state)
            || Interlocked.Exchange(ref state.PendingExposure, null) is not { } evaluationMetadata)
        {
            return;
        }

        state.ExposureVerdict = commit(evaluationMetadata);
        Volatile.Write(ref state.ExposureCommitCompleted, 1);
    }

    internal static ResolutionDetails<T> GetResolutionDetails<T>(
        string flagKey,
        T defaultValue,
        Datadog.Trace.FeatureFlags.IEvaluation? evaluation,
        long? evalTimeMs,
        bool deferredExposure,
        IReadOnlyList<IDictionary<string, object?>>? dependencyTelemetryOverride = null,
        DependencyTelemetryCallback? getDependencyTelemetry = null)
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
                        ToMetadata(null, evalTimeMs, false, dependencyTelemetry: null));
        }

        var errorType = FeatureFlagEvaluationMappings.ToErrorType(evaluation.Error);
        var value = errorType != ErrorType.None
                        ? defaultValue
                        : typeof(T) == typeof(Value) ? (T)(object)JsonToValue(evaluation.Value) : (T)evaluation.Value!;
        var dependencyTelemetry = dependencyTelemetryOverride;
        if (dependencyTelemetry is null && deferredExposure && evaluation.FlagMetadata is { } evaluationMetadata)
        {
            try
            {
                dependencyTelemetry = getDependencyTelemetry is null
                                          ? GetDependencyTelemetry(evaluationMetadata)
                                          : getDependencyTelemetry(evaluationMetadata);
            }
            catch
            {
                // An optional telemetry bridge must never affect resolution.
            }
        }

        var res = new ResolutionDetails<T>(
            evaluation.FlagKey,
            value,
            errorType,
            FeatureFlagEvaluationMappings.ToReason(evaluation.Reason),
            evaluation.Variant,
            evaluation.Error,
            ToMetadata(evaluation.FlagMetadata, evalTimeMs, deferredExposure, dependencyTelemetry));
        return res;
    }

    private static ImmutableMetadata? ToMetadata(
        IDictionary<string, string>? metadata,
        long? evalTimeMs,
        bool deferredExposure,
        IReadOnlyList<IDictionary<string, object?>>? dependencyTelemetry)
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
            var featureTypes = metadata.TryGetValue(FeatureFlagMetadataKeys.FeatureTypes, out var encodedTypes) ? FeatureFlagTypeCodec.Parse(encodedTypes) : null;
            foreach (var pair in metadata)
            {
                if (pair.Key == FeatureFlagMetadataKeys.FeatureTypes || pair.Key == FeatureFlagMetadataKeys.PendingExposure)
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
        var pendingExposure = deferredExposure && metadata is not null
                           && ((metadata.TryGetValue(FeatureFlagMetadataKeys.PendingExposure, out var pending) && pending == "true")
                            || (metadata.TryGetValue(FeatureFlagMetadataKeys.DoLog, out var doLog) && doLog == "true"))
                                  ? metadata
                                  : null;
        var dependencies = ParseDependencyTelemetry(dependencyTelemetry);
        if (hookFeatures is not null || evaluationFeatures is not null || pendingExposure is not null || dependencies is not null)
        {
            Results.Add(immutable, new ResultState(hookFeatures, evaluationFeatures, pendingExposure, dependencies));
        }

        return immutable;
    }

    /// <summary>The selected split's serial id, or null when the metadata has none.</summary>
    internal static long? GetSplitSerialId(ImmutableMetadata? metadata)
        // The int holds the uint32 bits.
        => metadata?.GetInt(FeatureFlagMetadataKeys.SplitSerialId) is { } serialBits ? unchecked((uint)serialBits) : null;

    // The tracer hands metadata across the package boundary as strings; OpenFeature metadata exposes
    // typed values, so the known non-feature keys are converted here. Features go through ConvertFeatureValue.
    private static object ConvertMetadataValue(string key, string value)
    {
        if (key == FeatureFlagMetadataKeys.DoLog || key == FeatureFlagMetadataKeys.ExposureCacheHit)
        {
            return value == "true" ? BoxedTrue : BoxedFalse;
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
    private static object ConvertFeatureValue(string value, char type) => FeatureFlagTypeCodec.DecodeValue(value, type);

    private static IReadOnlyList<DependencyEvaluationState>? ParseDependencyTelemetry(IReadOnlyList<IDictionary<string, object?>>? records)
    {
        if (records is not { Count: > 0 })
        {
            return null;
        }

        var dependencies = new List<DependencyEvaluationState>(records.Count);
        for (var i = 0; i < records.Count; i++)
        {
            try
            {
                var record = records[i];
                if (!record.TryGetValue("flagKey", out var flagKeyValue) || flagKeyValue is not string flagKey || flagKey.Length == 0)
                {
                    continue;
                }

                var variant = Value<string>(record, "variant");
                var allocationKey = Value<string>(record, "allocationKey");
                var reason = Value<int>(record, "reason");
                var errorCode = Value<string>(record, "errorCode");
                var evaluationTimeMs = Value<long>(record, "evaluationTimeMs");
                var consent = Value<bool>(record, "observeFullEvaluationData");
                var targetingKey = Value<string>(record, "targetingKey");
                var contextOmissions = Value<int>(record, "contextOmissions");
                var attributes = CopyNullableMap(record, "contextAttributes");
                var evaluationFeatures = CopyNullableMap(record, "evaluationFeatures");
                var hookFeatures = CopyHookFeatures(record);
                DependencyExposureState? exposure = null;
                if (Value<bool>(record, "exposureEligible") && variant is not null && allocationKey is not null)
                {
                    exposure = new DependencyExposureState(
                        flagKey,
                        variant,
                        allocationKey,
                        targetingKey,
                        Value<long?>(record, "serialId"),
                        hookFeatures,
                        record);
                }

                dependencies.Add(new DependencyEvaluationState(
                    flagKey,
                    variant,
                    allocationKey,
                    reason,
                    errorCode,
                    evaluationTimeMs,
                    consent,
                    targetingKey,
                    attributes,
                    contextOmissions,
                    evaluationFeatures,
                    exposure));
            }
            catch
            {
                // One malformed bridge record must not hide the remaining dependency telemetry.
            }
        }

        return dependencies.Count == 0 ? null : dependencies;

        static T? Value<T>(IDictionary<string, object?> record, string key)
            => record.TryGetValue(key, out var value) && value is T typed ? typed : default;

        static IReadOnlyDictionary<string, object?>? CopyNullableMap(IDictionary<string, object?> record, string key)
        {
            if (!record.TryGetValue(key, out var value) || value is not IReadOnlyDictionary<string, object?> source || source.Count == 0)
            {
                return null;
            }

            var copy = new Dictionary<string, object?>(source.Count, StringComparer.Ordinal);
            foreach (var pair in source)
            {
                copy[pair.Key] = pair.Value;
            }

            return new ReadOnlyDictionary<string, object?>(copy);
        }

        static IReadOnlyDictionary<string, object> CopyHookFeatures(IDictionary<string, object?> record)
        {
            if (!record.TryGetValue("hookFeatures", out var value) || value is not IReadOnlyDictionary<string, object?> source || source.Count == 0)
            {
                return new ReadOnlyDictionary<string, object>(new Dictionary<string, object>());
            }

            var copy = new Dictionary<string, object>(source.Count, StringComparer.Ordinal);
            foreach (var pair in source)
            {
                if (pair.Value is not null)
                {
                    copy[pair.Key] = pair.Value;
                }
            }

            return new ReadOnlyDictionary<string, object>(copy);
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

    internal sealed class ContextAttributes(int capacity) : Dictionary<string, object?>(capacity)
    {
        // Read by the tracer integration through duck typing. Keeping this on the dictionary avoids
        // changing the instrumented Evaluate signature and preserves compatibility with older tracers.
        public int ContextOmissionReasons { get; internal set; }
    }

    internal sealed class ResultState(
        IReadOnlyDictionary<string, object>? hookFeatures,
        IReadOnlyDictionary<string, object?>? evaluationFeatures,
        IDictionary<string, string>? pendingExposure,
        IReadOnlyList<DependencyEvaluationState>? dependencyEvaluations)
    {
#pragma warning disable SA1401 // Fields should be private: exchanged atomically, so a commit runs at most once
        internal IDictionary<string, string>? PendingExposure = pendingExposure;
        internal volatile int ExposureVerdict = ExposureCommitResult.NothingPending;
        internal int ExposureCommitCompleted;
        internal int DependencyMetricsRecorded;
#pragma warning restore SA1401

        internal IReadOnlyDictionary<string, object>? HookFeatures { get; } = hookFeatures;

        internal IReadOnlyDictionary<string, object?>? EvaluationFeatures { get; } = evaluationFeatures;

        internal IReadOnlyList<DependencyEvaluationState>? DependencyEvaluations { get; } = dependencyEvaluations;

        internal bool HasDependencyExposures
        {
            get
            {
                if (DependencyEvaluations is { } dependencies)
                {
                    for (var i = 0; i < dependencies.Count; i++)
                    {
                        if (dependencies[i].Exposure is not null)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
        }

        // True when the commit decides this result's verdict.
        internal bool OwesCommit { get; } = pendingExposure is not null;

        internal EvaluationContext? EvaluatedContext { get; set; }

        internal bool TryTakeDependencyEvaluations(out IReadOnlyList<DependencyEvaluationState>? dependencies)
        {
            dependencies = DependencyEvaluations;
            return dependencies is not null && Interlocked.Exchange(ref DependencyMetricsRecorded, 1) == 0;
        }

        /// <summary>
        /// Gets the committed verdict: false for new, true for repeat, null when not recorded. Returns false
        /// when the result owed no commit; the caller then uses the stamp.
        /// </summary>
        internal bool TryGetCommittedVerdict(out bool? verdict)
        {
            if (!OwesCommit)
            {
                verdict = null;
                return false;
            }

            verdict = ExposureVerdict switch
            {
                ExposureCommitResult.New => false,
                ExposureCommitResult.Repeat => true,
                _ => null,
            };
            return true;
        }
    }

    internal sealed class DependencyEvaluationState(
        string flagKey,
        string? variant,
        string? allocationKey,
        int reason,
        string? errorCode,
        long evaluationTimeMs,
        bool observeFullEvaluationData,
        string? targetingKey,
        IReadOnlyDictionary<string, object?>? contextAttributes,
        int contextOmissions,
        IReadOnlyDictionary<string, object?>? evaluationFeatures,
        DependencyExposureState? exposure)
    {
        internal string FlagKey { get; } = flagKey;

        internal string? Variant { get; } = variant;

        internal string? AllocationKey { get; } = allocationKey;

        internal int Reason { get; } = reason;

        internal string? ErrorCode { get; } = errorCode;

        internal long EvaluationTimeMs { get; } = evaluationTimeMs;

        internal bool ObserveFullEvaluationData { get; } = observeFullEvaluationData;

        internal string? TargetingKey { get; } = targetingKey;

        internal IReadOnlyDictionary<string, object?>? ContextAttributes { get; } = contextAttributes;

        internal int ContextOmissions { get; } = contextOmissions;

        internal IReadOnlyDictionary<string, object?>? EvaluationFeatures { get; } = evaluationFeatures;

        internal DependencyExposureState? Exposure { get; } = exposure;
    }

    internal sealed class DependencyExposureState(
        string flagKey,
        string variant,
        string allocationKey,
        string? targetingKey,
        long? serialId,
        IReadOnlyDictionary<string, object> hookFeatures,
        IDictionary<string, object?> verdict)
    {
        private readonly IDictionary<string, object?> _verdict = verdict;

        internal string FlagKey { get; } = flagKey;

        internal string Variant { get; } = variant;

        internal string AllocationKey { get; } = allocationKey;

        internal string? TargetingKey { get; } = targetingKey;

        internal long? SerialId { get; } = serialId;

        internal IReadOnlyDictionary<string, object> HookFeatures { get; } = hookFeatures;

        internal bool? CacheHit
            => _verdict.TryGetValue("exposureCommitted", out var committed) && committed is true
            && _verdict.TryGetValue("exposureCacheHit", out var cacheHit) && cacheHit is bool value
                   ? value
                   : null;
    }
}
