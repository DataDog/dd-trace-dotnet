// <copyright file="DatadogProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenFeature.Constant;
using OpenFeature.Model;

namespace Datadog.Trace.OpenFeature;

/// <summary>
/// OpenFeature V2.0.0+ Provider for Datadog
/// </summary>
public class DatadogProvider : global::OpenFeature.FeatureProvider
{
    private Metadata _metadata = new Metadata("datadog-openfeature-provider");

    /// <summary> Gets provider metadata </summary>
    /// <returns> Returns provider metadata </returns>
    public override Metadata? GetMetadata() => _metadata;

    /// <summary> Resolve flag as boolean </summary>
    /// <param name="flagKey"> Requested flag </param>
    /// <param name="defaultValue"> Default value </param>
    /// <param name="context"> Evaluation context </param>
    /// <param name="cancellationToken"> Async cancelation token </param>
    /// <returns> Returns the evaluation result </returns>
    public override Task<ResolutionDetails<bool>> ResolveBooleanValueAsync(string flagKey, bool defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () =>
            {
                var res = Datadog.Trace.FeatureFlags.FeatureFlagsSdk.Evaluate(flagKey, typeof(bool), defaultValue, GetContext(context));
                return GetResolutionDetails<bool>(res);
            },
            cancellationToken);
    }

    /// <summary> Resolve flag as double </summary>
    /// <param name="flagKey"> Requested flag </param>
    /// <param name="defaultValue"> Default value </param>
    /// <param name="context"> Evaluation context </param>
    /// <param name="cancellationToken"> Async cancelation token </param>
    /// <returns> Returns the evaluation result </returns>
    public override Task<ResolutionDetails<double>> ResolveDoubleValueAsync(string flagKey, double defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () =>
            {
                var res = Datadog.Trace.FeatureFlags.FeatureFlagsSdk.Evaluate(flagKey, typeof(double), defaultValue, GetContext(context));
                return GetResolutionDetails<double>(res);
            },
            cancellationToken);
    }

    /// <summary> Resolve flag as integer </summary>
    /// <param name="flagKey"> Requested flag </param>
    /// <param name="defaultValue"> Default value </param>
    /// <param name="context"> Evaluation context </param>
    /// <param name="cancellationToken"> Async cancelation token </param>
    /// <returns> Returns the evaluation result </returns>
    public override Task<ResolutionDetails<int>> ResolveIntegerValueAsync(string flagKey, int defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () =>
            {
                var res = Datadog.Trace.FeatureFlags.FeatureFlagsSdk.Evaluate(flagKey, typeof(int), defaultValue, GetContext(context));
                return GetResolutionDetails<int>(res);
            },
            cancellationToken);
    }

    /// <summary> Resolve flag as string </summary>
    /// <param name="flagKey"> Requested flag </param>
    /// <param name="defaultValue"> Default value </param>
    /// <param name="context"> Evaluation context </param>
    /// <param name="cancellationToken"> Async cancelation token </param>
    /// <returns> Returns the evaluation result </returns>
    public override Task<ResolutionDetails<string>> ResolveStringValueAsync(string flagKey, string defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () =>
            {
                var res = Datadog.Trace.FeatureFlags.FeatureFlagsSdk.Evaluate(flagKey, typeof(string), defaultValue, GetContext(context));
                return GetResolutionDetails<string>(res);
            },
            cancellationToken);
    }

    /// <summary> Resolve flag as Value </summary>
    /// <param name="flagKey"> Requested flag </param>
    /// <param name="defaultValue"> Default value </param>
    /// <param name="context"> Evaluation context </param>
    /// <param name="cancellationToken"> Async cancelation token </param>
    /// <returns> Returns the evaluation result </returns>
    public override Task<ResolutionDetails<Value>> ResolveStructureValueAsync(string flagKey, Value defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () =>
            {
                var res = Datadog.Trace.FeatureFlags.FeatureFlagsSdk.Evaluate(flagKey, typeof(Value), defaultValue, GetContext(context));
                return GetResolutionDetails<Value>(res);
            },
            cancellationToken);
    }

    private static Datadog.Trace.FeatureFlags.IEvaluationContext? GetContext(EvaluationContext? context)
    {
        if (context == null) { return new Datadog.Trace.FeatureFlags.EvaluationContext(string.Empty); }
        var values = context.AsDictionary().Select(p => new KeyValuePair<string, object?>(p.Key, ToObject(p.Value))).ToDictionary(p => p.Key, p => p.Value);
        var res = new Datadog.Trace.FeatureFlags.EvaluationContext(context.TargetingKey!, values);
        return res;
    }

    private static object? ToObject(Value value)
    {
        if (value == null) { return null; }
        else if (value.IsBoolean) { return value.AsBoolean; }
        else if (value.IsString) { return value.AsString; }
        else if (value.IsNumber) { return value.AsDouble; }
        else { return value.AsObject; }
    }

    private static ResolutionDetails<T> GetResolutionDetails<T>(Datadog.Trace.FeatureFlags.IEvaluation? evaluation)
    {
        if (evaluation == null) { return default!; }
        var res = new ResolutionDetails<T>(
            evaluation.FlagKey,
            (T)(evaluation.Value ?? default(T)!),
            ToErrorType(evaluation.Reason),
            evaluation.Reason.ToString(),
            evaluation.Variant,
            evaluation.Error,
            ToMetadata(evaluation.FlagMetadata!));
        return res;
    }

    private static ErrorType ToErrorType(Datadog.Trace.FeatureFlags.EvaluationReason reason)
    {
        return ErrorType.None; // TODO: Map error types properly
    }

    private static ImmutableMetadata ToMetadata(IDictionary<string, string> metadata)
    {
        return default!; // TODO: Map metadata properly
    }
}
