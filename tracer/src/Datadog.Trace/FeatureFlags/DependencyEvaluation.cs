// <copyright file="DependencyEvaluation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.FeatureFlags;

internal sealed class DependencyEvaluation(
    Evaluation evaluation,
    DateTime evaluationTime,
    string? targetingKey,
    IReadOnlyDictionary<string, object?>? contextAttributes)
{
    private const int MaxContextKeyLength = 256;

    public Evaluation Evaluation { get; } = evaluation;

    public DateTime EvaluationTime { get; } = evaluationTime;

    public string? TargetingKey { get; } = targetingKey;

    public IReadOnlyDictionary<string, object?>? ContextAttributes { get; } = contextAttributes;

    public static DependencyEvaluation Snapshot(
        Evaluation evaluation,
        DateTime evaluationTime,
        string? targetingKey,
        IReadOnlyDictionary<string, object?>? contextAttributes)
    {
        try
        {
            var metadata = evaluation.FlagMetadata is null ? null : new Dictionary<string, string>(evaluation.FlagMetadata);
            var detached = new Evaluation(
                evaluation.FlagKey,
                null,
                evaluation.Reason,
                evaluation.Variant,
                evaluation.Error,
                metadata);
            return new DependencyEvaluation(detached, evaluationTime, targetingKey, contextAttributes);
        }
        catch
        {
            // Telemetry detachment is best effort. Retain the dependency result without potentially
            // unsafe values or attributes so instrumentation cannot affect flag resolution.
            var metadata = new Dictionary<string, string>();
            CopyMetadata(FeatureFlagMetadataKeys.ObserveFullEvaluationData);
            CopyMetadata(FeatureFlagsEvaluator.MetadataAllocationKey);
            CopyMetadata(FeatureFlagsEvaluator.MetadataErrorCode);
            var detached = new Evaluation(
                evaluation.FlagKey,
                null,
                evaluation.Reason,
                evaluation.Variant,
                evaluation.Error,
                metadata);
            return new DependencyEvaluation(detached, evaluationTime, targetingKey, null);

            void CopyMetadata(string key)
            {
                try
                {
                    if (evaluation.FlagMetadata?.TryGetValue(key, out var value) == true)
                    {
                        metadata[key] = value;
                    }
                }
                catch
                {
                    // The minimal fallback must remain non-throwing even if a future evaluation
                    // implementation supplies custom metadata.
                }
            }
        }
    }

    public static IReadOnlyDictionary<string, object?>? SnapshotContext(EvaluationContext? context, bool includeContext)
    {
        if (!includeContext || context is null)
        {
            return null;
        }

        try
        {
            var remainingNodes = 1_280;
            var attributes = new Dictionary<string, object?>();
            foreach (var pair in context.Attributes)
            {
                if (remainingNodes <= 0)
                {
                    break;
                }

                attributes[pair.Key] = Detach(pair.Value, 0, ref remainingNodes);
            }

            return attributes;
        }
        catch
        {
            return null;
        }
    }

    internal static IReadOnlyDictionary<string, object?>? FlattenContext(IReadOnlyDictionary<string, object?>? attributes)
    {
        if (attributes is null)
        {
            return null;
        }

        var flattened = new Dictionary<string, object?>();
        var remainingNodes = 1_280;
        try
        {
            VisitDictionary(attributes, string.Empty, 0);
        }
        catch
        {
            // The context was already detached, but telemetry construction must remain best effort.
        }

        return flattened;

        void VisitDictionary(IEnumerable<KeyValuePair<string, object?>> dictionary, string prefix, int depth)
        {
            var count = 0;
            foreach (var pair in dictionary)
            {
                if (count++ >= 256 || remainingNodes-- <= 0 || flattened.Count >= 256)
                {
                    break;
                }

                var separatorLength = prefix.Length == 0 ? 0 : 1;
                if (pair.Key.Length > MaxContextKeyLength
                 || prefix.Length > MaxContextKeyLength - separatorLength - pair.Key.Length)
                {
                    continue;
                }

                var key = prefix.Length == 0 ? pair.Key : prefix + "." + pair.Key;
                VisitValue(key, pair.Value, depth + 1);
            }
        }

        void VisitValue(string key, object? value, int depth)
        {
            if (depth > 4 || flattened.Count >= 256)
            {
                return;
            }

            switch (value)
            {
                case IReadOnlyDictionary<string, object?> dictionary:
                    VisitDictionary(dictionary, key, depth);
                    break;
                case object?[] array:
                    var count = Math.Min(array.Length, 256);
                    for (var i = 0; i < count && remainingNodes-- > 0 && flattened.Count < 256; i++)
                    {
                        var index = i.ToString(CultureInfo.InvariantCulture);
                        if (key.Length > MaxContextKeyLength - 1 - index.Length)
                        {
                            continue;
                        }

                        VisitValue(key + "." + index, array[i], depth + 1);
                    }

                    break;
                default:
                    flattened[key] = value;
                    break;
            }
        }
    }

    private static object? Detach(object? value, int depth, ref int remainingNodes)
    {
        if (remainingNodes-- <= 0 || depth >= 32)
        {
            return null;
        }

        return value switch
        {
            null or string or bool or byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal or char or DateTime or DateTimeOffset or Guid => value,
            JValue valueToken => Detach(valueToken.Value, depth + 1, ref remainingNodes),
            JObject objectToken => DetachObject(objectToken, depth, ref remainingNodes),
            JArray arrayToken => DetachArray(arrayToken, depth, ref remainingNodes),
            JToken => null,
            IDictionary<string, object?> dictionary => DetachDictionary(dictionary, depth, ref remainingNodes),
            IDictionary dictionary => DetachDictionary(dictionary, depth, ref remainingNodes),
            IEnumerable enumerable => DetachEnumerable(enumerable, depth, ref remainingNodes),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture),
        };
    }

    private static Dictionary<string, object?> DetachDictionary(
        IEnumerable<KeyValuePair<string, object?>> dictionary,
        int depth,
        ref int remainingNodes)
    {
        var detached = new Dictionary<string, object?>();
        foreach (var pair in dictionary)
        {
            if (remainingNodes <= 0)
            {
                break;
            }

            detached[pair.Key] = Detach(pair.Value, depth + 1, ref remainingNodes);
        }

        return detached;
    }

    private static Dictionary<string, object?> DetachDictionary(IDictionary dictionary, int depth, ref int remainingNodes)
    {
        var detached = new Dictionary<string, object?>();
        foreach (var item in dictionary)
        {
            if (item is not DictionaryEntry entry)
            {
                continue;
            }

            if (remainingNodes <= 0)
            {
                break;
            }

            detached[Convert.ToString(entry.Key) ?? string.Empty] = Detach(entry.Value, depth + 1, ref remainingNodes);
        }

        return detached;
    }

    private static object?[] DetachEnumerable(IEnumerable enumerable, int depth, ref int remainingNodes)
    {
        var detached = new List<object?>();
        foreach (var item in enumerable)
        {
            if (remainingNodes <= 0)
            {
                break;
            }

            detached.Add(Detach(item, depth + 1, ref remainingNodes));
        }

        return detached.ToArray();
    }

    private static Dictionary<string, object?> DetachObject(JObject value, int depth, ref int remainingNodes)
    {
        var detached = new Dictionary<string, object?>();
        foreach (var property in value.Properties())
        {
            if (remainingNodes <= 0)
            {
                break;
            }

            detached[property.Name] = Detach(property.Value, depth + 1, ref remainingNodes);
        }

        return detached;
    }

    private static object?[] DetachArray(JArray value, int depth, ref int remainingNodes)
    {
        var detached = new List<object?>();
        foreach (var item in value)
        {
            if (remainingNodes <= 0)
            {
                break;
            }

            detached.Add(Detach(item, depth + 1, ref remainingNodes));
        }

        return detached.ToArray();
    }
}
