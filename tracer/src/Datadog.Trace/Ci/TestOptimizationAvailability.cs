// <copyright file="TestOptimizationAvailability.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.Ci;

/// <summary>
/// Whether the application can run Test Optimization (CI Visibility). The Datadog.Trace.Aot targets set the feature switch
/// to false in NativeAOT applications, which Test Optimization doesn't support yet: ILC then leaves its code out (see
/// ILLink.Substitutions.xml), so the code outside Test Optimization checks this before using it.
/// </summary>
internal static class TestOptimizationAvailability
{
    /// <summary>
    /// The feature switch.
    /// </summary>
    internal const string FeatureSwitch = "Datadog.Trace.TestOptimization.IsSupported";

    // Read once: span serialization checks it.
    private static readonly bool Supported = !AppContext.TryGetSwitch(FeatureSwitch, out var isSupported) || isSupported;

    /// <summary>
    /// Gets a value indicating whether the application can run Test Optimization: false in a NativeAOT application.
    /// </summary>
    public static bool IsSupported => Supported;

    /// <summary>
    /// Gets a value indicating whether Test Optimization runs in agentless mode: its service-entry spans then don't have the
    /// runtime id and top-level tags.
    /// </summary>
    /// <returns>true when Test Optimization runs in agentless mode; otherwise, false.</returns>
    public static bool IsRunningAgentless()
        => IsSupported && TestOptimization.Instance is { IsRunning: true } testOptimization && testOptimization.Settings.Agentless;
}
