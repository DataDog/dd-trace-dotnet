// <copyright file="ExposureEvaluation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.FeatureFlags;
using OpenFeature.Model;

namespace Datadog.FeatureFlags.OpenFeature;

/// <summary>
/// A completed flag evaluation and its Datadog exposure decision, handed to an
/// <see cref="ExposureHook"/> callback. The callback runs on the evaluation thread and must not do
/// I/O: copy what is needed and hand it to a background queue.
/// </summary>
public sealed class ExposureEvaluation
{
    private static readonly IReadOnlyDictionary<string, object> EmptyFeatures = new Dictionary<string, object>();

    private readonly bool _evaluationSucceeded;
    private readonly ImmutableMetadata? _metadata;

    internal ExposureEvaluation(
        bool evaluationSucceeded,
        string flagKey,
        string? variant,
        string? allocationKey,
        string? targetingKey,
        EvaluationContext? context,
        ImmutableMetadata? metadata)
    {
        _evaluationSucceeded = evaluationSucceeded;
        _metadata = metadata;
        FlagKey = flagKey;
        Variant = variant;
        AllocationKey = allocationKey;
        TargetingKey = targetingKey;
        Context = context;
        Features = FeatureFlagsSdk.GetHookFeatures(metadata) ?? EmptyFeatures;
    }

    /// <summary> Gets the evaluated flag key. </summary>
    public string FlagKey { get; }

    /// <summary> Gets the selected variant, or null when the evaluation did not select one. </summary>
    public string? Variant { get; }

    /// <summary> Gets the selected allocation key, or null when the evaluation did not select one. </summary>
    public string? AllocationKey { get; }

    /// <summary> Gets the targeting key of the evaluation context. </summary>
    public string? TargetingKey { get; }

    /// <summary> Gets the evaluation context, including the targeting key. </summary>
    public EvaluationContext? Context { get; }

    /// <summary>
    /// Gets a value indicating whether the evaluation succeeded and selected an allocation that logs
    /// exposures, whether or not this subject was already exposed.
    /// </summary>
    public bool IsExposure
        => _evaluationSucceeded
           && Variant is not null
           && AllocationKey is not null
           && _metadata?.GetBool(FeatureFlagMetadataKeys.DoLog) == true;

    /// <summary>
    /// Gets a value indicating whether this exposure repeats one already recorded for this subject:
    /// the same allocation, variant and serial id. False for a new or changed exposure, and for an
    /// evaluation that is not an exposure.
    /// </summary>
    public bool IsCacheHit => IsExposure && _metadata?.GetBool(FeatureFlagMetadataKeys.ExposureCacheHit) == true;

    /// <summary>
    /// Gets a value indicating whether this is a new or changed exposure: exactly the decision the
    /// provider uses for sending the exposure to Datadog.
    /// </summary>
    public bool ShouldSend => IsExposure && _metadata?.GetBool(FeatureFlagMetadataKeys.ExposureCacheHit) == false;

    /// <summary>
    /// Gets the features of the selected split whose destinations include the customer hook, keyed
    /// without the <c>__dd_feature.</c> prefix. Values are a <see cref="string"/>,
    /// <see cref="double"/> or <see cref="bool"/>. The map is empty when the split has none.
    /// </summary>
    public IReadOnlyDictionary<string, object> Features { get; }
}
