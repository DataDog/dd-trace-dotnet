// <copyright file="DatadogProvider.Experimental.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Diagnostics.CodeAnalysis;
using System.Threading;
using OpenFeature.Model;

namespace Datadog.FeatureFlags.OpenFeature;

/// <summary>Experimental synchronous resolution methods for the Datadog provider.</summary>
public sealed partial class DatadogProvider
{
    /// <summary>Synchronously resolves a flag as bool. Experimental Datadog extension.</summary>
    /// <remarks>
    /// Initialize the provider asynchronously before evaluation. This method uses only the supplied
    /// context and does not run OpenFeature client hooks, including evaluation metrics and span
    /// enrichment. It has the same resolution contract as <see cref="ResolveBooleanValueAsync"/>.
    /// It will be deprecated when an equivalent stable OpenFeature API is supported by this package.
    /// </remarks>
    /// <param name="flagKey">Requested flag.</param>
    /// <param name="defaultValue">Value returned when the flag cannot be resolved.</param>
    /// <param name="context">Complete evaluation context, including the targeting key.</param>
    /// <param name="cancellationToken">Cancellation token checked before evaluation.</param>
    /// <returns>The resolved value and details, including errors and the caller's default.</returns>
    [Experimental("DDFF001")]
    public ResolutionDetails<bool> ResolveBooleanValue(string flagKey, bool defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return FeatureFlagsSdk.Resolve<bool>(flagKey, Trace.FeatureFlags.ValueType.Boolean, defaultValue, context);
    }

    /// <summary>Synchronously resolves a flag as double. Experimental Datadog extension.</summary>
    /// <remarks>
    /// Initialize the provider asynchronously before evaluation. This method uses only the supplied
    /// context and does not run OpenFeature client hooks, including evaluation metrics and span
    /// enrichment. It has the same resolution contract as <see cref="ResolveDoubleValueAsync"/>.
    /// It will be deprecated when an equivalent stable OpenFeature API is supported by this package.
    /// </remarks>
    /// <param name="flagKey">Requested flag.</param>
    /// <param name="defaultValue">Value returned when the flag cannot be resolved.</param>
    /// <param name="context">Complete evaluation context, including the targeting key.</param>
    /// <param name="cancellationToken">Cancellation token checked before evaluation.</param>
    /// <returns>The resolved value and details, including errors and the caller's default.</returns>
    [Experimental("DDFF001")]
    public ResolutionDetails<double> ResolveDoubleValue(string flagKey, double defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return FeatureFlagsSdk.Resolve<double>(flagKey, Trace.FeatureFlags.ValueType.Numeric, defaultValue, context);
    }

    /// <summary>Synchronously resolves a flag as int. Experimental Datadog extension.</summary>
    /// <remarks>
    /// Initialize the provider asynchronously before evaluation. This method uses only the supplied
    /// context and does not run OpenFeature client hooks, including evaluation metrics and span
    /// enrichment. It has the same resolution contract as <see cref="ResolveIntegerValueAsync"/>.
    /// It will be deprecated when an equivalent stable OpenFeature API is supported by this package.
    /// </remarks>
    /// <param name="flagKey">Requested flag.</param>
    /// <param name="defaultValue">Value returned when the flag cannot be resolved.</param>
    /// <param name="context">Complete evaluation context, including the targeting key.</param>
    /// <param name="cancellationToken">Cancellation token checked before evaluation.</param>
    /// <returns>The resolved value and details, including errors and the caller's default.</returns>
    [Experimental("DDFF001")]
    public ResolutionDetails<int> ResolveIntegerValue(string flagKey, int defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return FeatureFlagsSdk.Resolve<int>(flagKey, Trace.FeatureFlags.ValueType.Integer, defaultValue, context);
    }

    /// <summary>Synchronously resolves a flag as string. Experimental Datadog extension.</summary>
    /// <remarks>
    /// Initialize the provider asynchronously before evaluation. This method uses only the supplied
    /// context and does not run OpenFeature client hooks, including evaluation metrics and span
    /// enrichment. It has the same resolution contract as <see cref="ResolveStringValueAsync"/>.
    /// It will be deprecated when an equivalent stable OpenFeature API is supported by this package.
    /// </remarks>
    /// <param name="flagKey">Requested flag.</param>
    /// <param name="defaultValue">Value returned when the flag cannot be resolved.</param>
    /// <param name="context">Complete evaluation context, including the targeting key.</param>
    /// <param name="cancellationToken">Cancellation token checked before evaluation.</param>
    /// <returns>The resolved value and details, including errors and the caller's default.</returns>
    [Experimental("DDFF001")]
    public ResolutionDetails<string> ResolveStringValue(string flagKey, string defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return FeatureFlagsSdk.Resolve<string>(flagKey, Trace.FeatureFlags.ValueType.String, defaultValue, context);
    }

    /// <summary>Synchronously resolves a flag as Value. Experimental Datadog extension.</summary>
    /// <remarks>
    /// Initialize the provider asynchronously before evaluation. This method uses only the supplied
    /// context and does not run OpenFeature client hooks, including evaluation metrics and span
    /// enrichment. It has the same resolution contract as <see cref="ResolveStructureValueAsync"/>.
    /// It will be deprecated when an equivalent stable OpenFeature API is supported by this package.
    /// </remarks>
    /// <param name="flagKey">Requested flag.</param>
    /// <param name="defaultValue">Value returned when the flag cannot be resolved.</param>
    /// <param name="context">Complete evaluation context, including the targeting key.</param>
    /// <param name="cancellationToken">Cancellation token checked before evaluation.</param>
    /// <returns>The resolved value and details, including errors and the caller's default.</returns>
    [Experimental("DDFF001")]
    public ResolutionDetails<Value> ResolveStructureValue(string flagKey, Value defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return FeatureFlagsSdk.Resolve<Value>(flagKey, Trace.FeatureFlags.ValueType.Json, defaultValue, context);
    }
}
