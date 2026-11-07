// <copyright file="DatadogProvider.Experimental.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
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
    /// context. It runs the hooks from <see cref="GetProviderHooks"/> and any <see cref="ExposureHook"/> on the
    /// OpenFeature API, and returns after they complete. Other API or client hooks do not run.
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
        => ResolveSync(flagKey, Trace.FeatureFlags.ValueType.Boolean, FlagValueType.Boolean, defaultValue, context, cancellationToken);

    /// <summary>Synchronously resolves a flag as double. Experimental Datadog extension.</summary>
    /// <remarks>
    /// Initialize the provider asynchronously before evaluation. This method uses only the supplied
    /// context. It runs the hooks from <see cref="GetProviderHooks"/> and any <see cref="ExposureHook"/> on the
    /// OpenFeature API, and returns after they complete. Other API or client hooks do not run.
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
        => ResolveSync(flagKey, Trace.FeatureFlags.ValueType.Numeric, FlagValueType.Number, defaultValue, context, cancellationToken);

    /// <summary>Synchronously resolves a flag as int. Experimental Datadog extension.</summary>
    /// <remarks>
    /// Initialize the provider asynchronously before evaluation. This method uses only the supplied
    /// context. It runs the hooks from <see cref="GetProviderHooks"/> and any <see cref="ExposureHook"/> on the
    /// OpenFeature API, and returns after they complete. Other API or client hooks do not run.
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
        => ResolveSync(flagKey, Trace.FeatureFlags.ValueType.Integer, FlagValueType.Number, defaultValue, context, cancellationToken);

    /// <summary>Synchronously resolves a flag as string. Experimental Datadog extension.</summary>
    /// <remarks>
    /// Initialize the provider asynchronously before evaluation. This method uses only the supplied
    /// context. It runs the hooks from <see cref="GetProviderHooks"/> and any <see cref="ExposureHook"/> on the
    /// OpenFeature API, and returns after they complete. Other API or client hooks do not run.
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
        => ResolveSync(flagKey, Trace.FeatureFlags.ValueType.String, FlagValueType.String, defaultValue, context, cancellationToken);

    /// <summary>Synchronously resolves a flag as Value. Experimental Datadog extension.</summary>
    /// <remarks>
    /// Initialize the provider asynchronously before evaluation. This method uses only the supplied
    /// context. It runs the hooks from <see cref="GetProviderHooks"/> and any <see cref="ExposureHook"/> on the
    /// OpenFeature API, and returns after they complete. Other API or client hooks do not run.
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
        => ResolveSync(flagKey, Trace.FeatureFlags.ValueType.Json, FlagValueType.Object, defaultValue, context, cancellationToken);

    // FeatureClient runs these hooks around the async methods. The sync methods bypass the client, so they run the
    // client's post-resolution stages here, in its order (last hook first) and with its failure handling, and return
    // only after every hook completes. Before hooks are skipped: they could change the given context.
    internal static ResolutionDetails<T> RunPostResolutionHooks<T>(IReadOnlyList<Hook> hooks, HookContext<T> context, ResolutionDetails<T> resolution)
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

    // Adds API ExposureHooks, which miss sync evaluations otherwise (no client). They go first because hooks run last
    // first, so their finally stage runs after the provider's commit. Other API hooks stay out: they would run without
    // their before stage, and could block on async work.
    private static IReadOnlyList<Hook> WithApiExposureHooks(IImmutableList<Hook> providerHooks)
    {
        // Api.GetHooks() allocates; skip it until an ExposureHook exists.
        if (!FeatureFlagsSdk.ExposureHookCreated)
        {
            return providerHooks;
        }

        List<Hook>? hooks = null;
        foreach (var hook in global::OpenFeature.Api.Instance.GetHooks())
        {
            if (hook is ExposureHook)
            {
                (hooks ??= new List<Hook>(providerHooks.Count + 1)).Add(hook);
            }
        }

        if (hooks is null)
        {
            return providerHooks;
        }

        hooks.AddRange(providerHooks);
        return hooks;
    }

    private ResolutionDetails<T> ResolveSync<T>(string flagKey, Trace.FeatureFlags.ValueType valueType, FlagValueType flagValueType, T defaultValue, EvaluationContext? context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var resolution = FeatureFlagsSdk.Resolve(flagKey, valueType, defaultValue, context, Evaluator(), _evpHook, _deferExposures);
        var hooks = WithApiExposureHooks(_providerHooks);
        return hooks.Count == 0
            ? resolution
            : RunPostResolutionHooks(hooks, new HookContext<T>(flagKey, defaultValue, flagValueType, DirectClientMetadata, _metadata, context ?? EvaluationContext.Empty), resolution);
    }
}
