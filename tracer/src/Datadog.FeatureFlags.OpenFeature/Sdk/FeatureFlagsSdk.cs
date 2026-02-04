// <copyright file="FeatureFlagsSdk.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Datadog.Trace.FeatureFlags;
using Newtonsoft.Json.Linq;
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
    /// <summary> Gets a value indicating whether FeatureFlags framework is available or not </summary>
    /// <returns> True if FeatureFlagsSDK is instrumented </returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool IsAvailable() => false;

    /// <summary> Installs an event handler to be fired when a new config has been received </summary>
    /// <param name="onNewConfig"> Action to be called when the event is fired </param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void RegisterOnNewConfigEventHandler(Action onNewConfig)
    {
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static IEvaluation? Evaluate(string flagKey, Trace.FeatureFlags.ValueType targetType, object? defaultValue, string? targetingKey, IDictionary<string, object?>? attributes)
    {
        if (flagKey is null)
        {
            throw new ArgumentNullException(nameof(flagKey));
        }

        return null;
    }

    public static ResolutionDetails<T> Resolve<T>(string flagKey, Trace.FeatureFlags.ValueType targetType, object? defaultValue, EvaluationContext? context) =>
        GetResolutionDetails<T>(Evaluate(flagKey, targetType, defaultValue, context?.TargetingKey, GetContextAttributes(context)));

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
                        string.Empty,
                        default!,
                        ErrorType.ProviderNotReady,
                        default,
                        default,
                        "FeatureFlagsSdk is disabled",
                        null);
        }

        var value = typeof(T) == typeof(Value) ? JsonToValue(evaluation.Value as string) : evaluation.Value!;
        var res = new ResolutionDetails<T>(
            evaluation.FlagKey,
            (T)value,
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
        var dic = (metadata ?? new Dictionary<string, string>()).ToDictionary(p => p.Key, p => (object)p.Value);
        return new ImmutableMetadata(dic);
    }

    public static Value JsonToValue(string? json)
    {
        try
        {
            if (json is null)
            {
                return new Value();
            }

            var token = JToken.Parse(json);
            return ConvertToken(token);
        }
        catch
        {
            return new Value();
        }
    }

    private static Value ConvertToken(JToken token)
    {
        switch (token.Type)
        {
            case JTokenType.Object:
                return new Value(ConvertObject((JObject)token));
            case JTokenType.Array:
                return new Value(ConvertArray((JArray)token));
            case JTokenType.Integer:
                return new Value((long)token);
            case JTokenType.Float:
                return new Value((double)token);
            case JTokenType.String:
                return new Value((string)token);
            case JTokenType.Boolean:
                return new Value((bool)token);
            case JTokenType.Null:
                return new Value();
            default:
                return new Value();
        }
    }

    private static Structure ConvertObject(JObject obj)
    {
        var dict = new Dictionary<string, Value>();
        foreach (var property in obj.Properties())
        {
            dict.Add(property.Name, ConvertToken(property.Value));
        }

        return new Structure(dict);
    }

    private static List<Value> ConvertArray(JArray array)
    {
        return array.Select(ConvertToken).ToList();
    }
}
