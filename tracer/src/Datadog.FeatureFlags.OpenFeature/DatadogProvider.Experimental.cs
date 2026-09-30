// <copyright file="DatadogProvider.Experimental.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using OpenFeature;
using OpenFeature.Constant;
using OpenFeature.Error;
using OpenFeature.Model;

namespace Datadog.FeatureFlags.OpenFeature;

/// <summary>Experimental synchronous resolution methods for the Datadog provider.</summary>
public sealed partial class DatadogProvider
{
    private static readonly ClientMetadata DirectClientMetadata = new(null, null);

    /// <summary>Synchronously resolves a flag as bool. Experimental Datadog extension.</summary>
    /// <remarks>
    /// Initialize the provider asynchronously before evaluation. This method uses only the supplied
    /// context. It runs the hooks from <see cref="GetProviderHooks"/> and returns after they complete.
    /// Hooks registered on the OpenFeature API or client do not run.
    /// It has the same resolution contract as <see cref="ResolveBooleanValueAsync"/>.
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
        var resolution = FeatureFlagsSdk.Resolve<bool>(flagKey, Trace.FeatureFlags.ValueType.Boolean, defaultValue, context, _evaluate);
        return RunProviderHooks(GetProviderHooks(), new HookContext<bool>(flagKey, defaultValue, FlagValueType.Boolean, DirectClientMetadata, _metadata, context ?? EvaluationContext.Empty), resolution);
    }

    /// <summary>Synchronously resolves a flag as double. Experimental Datadog extension.</summary>
    /// <remarks>
    /// Initialize the provider asynchronously before evaluation. This method uses only the supplied
    /// context. It runs the hooks from <see cref="GetProviderHooks"/> and returns after they complete.
    /// Hooks registered on the OpenFeature API or client do not run.
    /// It has the same resolution contract as <see cref="ResolveDoubleValueAsync"/>.
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
        var resolution = FeatureFlagsSdk.Resolve<double>(flagKey, Trace.FeatureFlags.ValueType.Numeric, defaultValue, context, _evaluate);
        return RunProviderHooks(GetProviderHooks(), new HookContext<double>(flagKey, defaultValue, FlagValueType.Number, DirectClientMetadata, _metadata, context ?? EvaluationContext.Empty), resolution);
    }

    /// <summary>Synchronously resolves a flag as int. Experimental Datadog extension.</summary>
    /// <remarks>
    /// Initialize the provider asynchronously before evaluation. This method uses only the supplied
    /// context. It runs the hooks from <see cref="GetProviderHooks"/> and returns after they complete.
    /// Hooks registered on the OpenFeature API or client do not run.
    /// It has the same resolution contract as <see cref="ResolveIntegerValueAsync"/>.
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
        var resolution = FeatureFlagsSdk.Resolve<int>(flagKey, Trace.FeatureFlags.ValueType.Integer, defaultValue, context, _evaluate);
        return RunProviderHooks(GetProviderHooks(), new HookContext<int>(flagKey, defaultValue, FlagValueType.Number, DirectClientMetadata, _metadata, context ?? EvaluationContext.Empty), resolution);
    }

    /// <summary>Synchronously resolves a flag as string. Experimental Datadog extension.</summary>
    /// <remarks>
    /// Initialize the provider asynchronously before evaluation. This method uses only the supplied
    /// context. It runs the hooks from <see cref="GetProviderHooks"/> and returns after they complete.
    /// Hooks registered on the OpenFeature API or client do not run.
    /// It has the same resolution contract as <see cref="ResolveStringValueAsync"/>.
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
        var resolution = FeatureFlagsSdk.Resolve<string>(flagKey, Trace.FeatureFlags.ValueType.String, defaultValue, context, _evaluate);
        return RunProviderHooks(GetProviderHooks(), new HookContext<string>(flagKey, defaultValue, FlagValueType.String, DirectClientMetadata, _metadata, context ?? EvaluationContext.Empty), resolution);
    }

    /// <summary>Synchronously resolves a flag as Value. Experimental Datadog extension.</summary>
    /// <remarks>
    /// Initialize the provider asynchronously before evaluation. This method uses only the supplied
    /// context. It runs the hooks from <see cref="GetProviderHooks"/> and returns after they complete.
    /// Hooks registered on the OpenFeature API or client do not run.
    /// It has the same resolution contract as <see cref="ResolveStructureValueAsync"/>.
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
        var resolution = FeatureFlagsSdk.Resolve<Value>(flagKey, Trace.FeatureFlags.ValueType.Json, defaultValue, context, _evaluate);
        return RunProviderHooks(GetProviderHooks(), new HookContext<Value>(flagKey, defaultValue, FlagValueType.Object, DirectClientMetadata, _metadata, context ?? EvaluationContext.Empty), resolution);
    }

    // FeatureClient runs these hooks around the async methods. The sync methods bypass the client, so they run the
    // client's post-resolution stages here, in its order and with its failure handling, and return only after every
    // hook completes. Before hooks are skipped because they can change the context, which these methods take as given.
    internal static ResolutionDetails<T> RunProviderHooks<T>(IImmutableList<Hook> hooks, HookContext<T> context, ResolutionDetails<T> resolution)
    {
        if (hooks.Count == 0)
        {
            return resolution;
        }

        var details = ToDetails(resolution);
        Exception? error = resolution.ErrorType == ErrorType.None ? null : new FeatureProviderException(resolution.ErrorType, resolution.ErrorMessage);
        if (error is null)
        {
            try
            {
                for (var i = hooks.Count - 1; i >= 0; i--)
                {
                    Wait(hooks[i].AfterAsync(context, details));
                }
            }
            catch (Exception ex)
            {
                // Like FeatureClient, an After hook failure turns the evaluation into an error with the default value.
                error = ex;
                var errorType = ex is FeatureProviderException providerException ? providerException.ErrorType
                              : ex is InvalidCastException ? ErrorType.TypeMismatch
                              : ErrorType.General;
                resolution = new ResolutionDetails<T>(resolution.FlagKey, context.DefaultValue, errorType, Reason.Error, string.Empty, ex.Message);
                details = ToDetails(resolution);
            }
        }

        if (error is not null)
        {
            for (var i = hooks.Count - 1; i >= 0; i--)
            {
                try
                {
                    Wait(hooks[i].ErrorAsync(context, error));
                }
                catch
                {
                    // A failing Error hook must not stop the remaining hooks.
                }
            }
        }

        for (var i = hooks.Count - 1; i >= 0; i--)
        {
            try
            {
                Wait(hooks[i].FinallyAsync(context, details));
            }
            catch
            {
                // Telemetry failures must not break evaluation or skip other Finally hooks.
            }
        }

        return resolution;
    }

    private static FlagEvaluationDetails<T> ToDetails<T>(ResolutionDetails<T> resolution)
        => new(resolution.FlagKey, resolution.Value, resolution.ErrorType, resolution.Reason, resolution.Variant, resolution.ErrorMessage, resolution.FlagMetadata);

    private static void Wait(ValueTask task)
    {
        if (task.IsCompleted)
        {
            task.GetAwaiter().GetResult();
        }
        else
        {
            task.AsTask().GetAwaiter().GetResult();
        }
    }
}
