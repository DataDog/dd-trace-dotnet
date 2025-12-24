// <copyright file="FeatureFlagsSdk.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.FeatureFlags;

/// <summary>
/// Functions to retrieve FeatureFlags from server
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
[Browsable(false)]
public static class FeatureFlagsSdk
{
    /// <summary> Gets a value indicating wether FeatureFlags framework is available or not </summary>
    /// <returns> True if FeatureFlagsSDK is instrumented </returns>
    [Instrumented]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool IsAvailable() => false;

    /// <summary> Installs an event handler to be fired when a new config has been received </summary>
    /// <param name="onNewConfig"> Action to be called when the event is fired </param>
    [Instrumented]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void RegisterOnNewConfigEventHandler(Action onNewConfig)
    {
    }

    /// <summary>
    /// Returns the evaluation of the requested flag key
    /// </summary>
    /// <returns> Returns the evaluation result </returns>
    /// <param name="flagKey">The feature flag key to evaluate</param>
    /// <param name="targetType">The desired result type</param>
    /// <param name="defaultValue">The default value</param>
    /// <param name="context">The evaluation context</param>
    public static IEvaluation? Evaluate(string flagKey, ValueType targetType, object? defaultValue, EvaluationContext? context)
        => Evaluate(flagKey, targetType, defaultValue, context?.TargetingKey, context?.Attributes);

    [Instrumented]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static IEvaluation? Evaluate(string flagKey, ValueType targetType, object? defaultValue, string? targetingKey, IDictionary<string, object?>? attributes)
    {
        if (flagKey is null)
        {
            throw new ArgumentNullException(nameof(flagKey));
        }

        return null;
    }
}
