// <copyright file="FeatureFlagsSdk.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Runtime.CompilerServices;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.FeatureFlags;

/// <summary>
/// Handlers for setting ASM login success / failures events in traces
/// Includes a security scan if asm is enabled
/// </summary>
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
    /// Sets the details of a successful login on the local root span
    /// </summary>
    /// <returns> Returns the evaluation result </returns>
    /// <param name="key">The feature flag key to evaluate</param>
    /// <param name="targetType">The desired result type</param>
    /// <param name="defaultValue">The default value</param>
    /// <param name="context">The evaluation context</param>
    [Instrumented]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static IEvaluation? Evaluate(string key, Type targetType, object? defaultValue, IEvaluationContext? context)
    {
        return null;
    }
}
