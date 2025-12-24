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
using Datadog.Trace.FeatureFlags;
using OpenFeature.Constant;
using OpenFeature.Model;

namespace Datadog.FeatureFlags.OpenFeature;

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
        cancellationToken.ThrowIfCancellationRequested();
        var res = Datadog.Trace.FeatureFlags.FeatureFlagsSdk.Evaluate(flagKey, Trace.FeatureFlags.ValueType.Boolean, defaultValue, context?.TargetingKey, GetContextAttributes(context));
        return Task.FromResult(GetResolutionDetails<bool>(res));
    }

    /// <summary> Resolve flag as double </summary>
    /// <param name="flagKey"> Requested flag </param>
    /// <param name="defaultValue"> Default value </param>
    /// <param name="context"> Evaluation context </param>
    /// <param name="cancellationToken"> Async cancelation token </param>
    /// <returns> Returns the evaluation result </returns>
    public override Task<ResolutionDetails<double>> ResolveDoubleValueAsync(string flagKey, double defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var res = Datadog.Trace.FeatureFlags.FeatureFlagsSdk.Evaluate(flagKey, Trace.FeatureFlags.ValueType.Numeric, defaultValue, context?.TargetingKey, GetContextAttributes(context));
        return Task.FromResult(GetResolutionDetails<double>(res));
    }

    /// <summary> Resolve flag as integer </summary>
    /// <param name="flagKey"> Requested flag </param>
    /// <param name="defaultValue"> Default value </param>
    /// <param name="context"> Evaluation context </param>
    /// <param name="cancellationToken"> Async cancelation token </param>
    /// <returns> Returns the evaluation result </returns>
    public override Task<ResolutionDetails<int>> ResolveIntegerValueAsync(string flagKey, int defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var res = Datadog.Trace.FeatureFlags.FeatureFlagsSdk.Evaluate(flagKey, Trace.FeatureFlags.ValueType.Integer, defaultValue, context?.TargetingKey, GetContextAttributes(context));
        return Task.FromResult(GetResolutionDetails<int>(res));
    }

    /// <summary> Resolve flag as string </summary>
    /// <param name="flagKey"> Requested flag </param>
    /// <param name="defaultValue"> Default value </param>
    /// <param name="context"> Evaluation context </param>
    /// <param name="cancellationToken"> Async cancelation token </param>
    /// <returns> Returns the evaluation result </returns>
    public override Task<ResolutionDetails<string>> ResolveStringValueAsync(string flagKey, string defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var res = Datadog.Trace.FeatureFlags.FeatureFlagsSdk.Evaluate(flagKey, Trace.FeatureFlags.ValueType.String, defaultValue, context?.TargetingKey, GetContextAttributes(context));
        return Task.FromResult(GetResolutionDetails<string>(res));
    }

    /// <summary> Resolve flag as Value </summary>
    /// <param name="flagKey"> Requested flag </param>
    /// <param name="defaultValue"> Default value </param>
    /// <param name="context"> Evaluation context </param>
    /// <param name="cancellationToken"> Async cancelation token </param>
    /// <returns> Returns the evaluation result </returns>
    public override Task<ResolutionDetails<Value>> ResolveStructureValueAsync(string flagKey, Value defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var res = Datadog.Trace.FeatureFlags.FeatureFlagsSdk.Evaluate(flagKey, Trace.FeatureFlags.ValueType.Json, defaultValue, context?.TargetingKey, GetContextAttributes(context));
        return Task.FromResult(GetResolutionDetails<Value>(res));
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

    private static ResolutionDetails<T> GetResolutionDetails<T>(Datadog.Trace.FeatureFlags.IEvaluation? evaluation)
    {
        if (evaluation is null) 
        {
            return new ResolutionDetails<T>(
                        null,
                        default,
                        ErrorType.ProviderNotReady,
                        default,
                        default,
                        "FeatureFlagsSdk is disabled",
                        null);
        }

        var res = new ResolutionDetails<T>(
            evaluation.FlagKey,
            (T)(evaluation.Value!),
            ToErrorType(evaluation.Reason, evaluation.Error),
            evaluation.Reason.ToString(),
            evaluation.Variant,
            evaluation.Error,
            ToMetadata(evaluation.FlagMetadata));
        return res;
    }

    private static ErrorType ToErrorType(Datadog.Trace.FeatureFlags.EvaluationReason reason, string? errorMessage)
    {
        return errorMessage switch
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
    }

    private static ImmutableMetadata ToMetadata(IDictionary<string, string>? metadata)
    {
        var dic = (metadata ?? new Dictionary<string, string>()).ToDictionary(p =>  p.Key, p => (object)p.Value);
        return new ImmutableMetadata(dic);
    }
}
