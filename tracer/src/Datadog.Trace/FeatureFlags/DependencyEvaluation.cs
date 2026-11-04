// <copyright file="DependencyEvaluation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Datadog.Trace.FeatureFlags.FlagEvaluation;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.FeatureFlags;

internal sealed class DependencyEvaluation(
    Evaluation evaluation,
    DateTime evaluationTime,
    string? targetingKey,
    IReadOnlyDictionary<string, object?>? contextAttributes,
    ContextOmissionReason contextOmissions)
{
    private const int MaxContextKeyLength = 256;

    public Evaluation Evaluation { get; } = evaluation;

    public DateTime EvaluationTime { get; } = evaluationTime;

    public string? TargetingKey { get; } = targetingKey;

    public IReadOnlyDictionary<string, object?>? ContextAttributes { get; } = contextAttributes;

    public ContextOmissionReason ContextOmissions { get; } = contextOmissions;

    public static DependencyEvaluation Snapshot(
        Evaluation evaluation,
        DateTime evaluationTime,
        string? targetingKey,
        IReadOnlyDictionary<string, object?>? contextAttributes,
        ContextOmissionReason contextOmissions)
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
            return new DependencyEvaluation(detached, evaluationTime, targetingKey, contextAttributes, contextOmissions);
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
            return new DependencyEvaluation(detached, evaluationTime, targetingKey, null, contextOmissions | ContextOmissionReason.SnapshotError);

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
        => SnapshotContext(context, includeContext, out _);

    public static IReadOnlyDictionary<string, object?>? SnapshotContext(EvaluationContext? context, bool includeContext, out ContextOmissionReason omissions)
    {
        omissions = ContextOmissionReason.None;
        if (!includeContext || context is null)
        {
            return null;
        }

        try
        {
            omissions = context.ContextOmissions;
            var remainingNodes = 1_280;
            var attributes = new Dictionary<string, object?>();
            const string targetingKey = "targetingKey";
            var attributeCount = context.Attributes.Count - (context.Attributes.ContainsKey(targetingKey) ? 1 : 0);
            if (attributeCount > 256)
            {
                omissions |= ContextOmissionReason.MaxContextFields;
            }

            var remainingAttributes = Math.Min(attributeCount, 256);
            foreach (var pair in context.Attributes)
            {
                // OpenFeature exposes the root targeting key in AsDictionary(), but dependency
                // telemetry reports it separately and it must not consume the attribute budget.
                if (pair.Key == targetingKey)
                {
                    continue;
                }

                if (remainingAttributes-- <= 0)
                {
                    break;
                }

                if (remainingNodes <= 0)
                {
                    omissions |= ContextOmissionReason.MaxVisitedNodes;
                    break;
                }

                if (pair.Key.Length > MaxContextKeyLength)
                {
                    omissions |= ContextOmissionReason.MaxKeyLength;
                    remainingNodes--;
                    continue;
                }

                attributes[pair.Key] = Detach(pair.Value, 0, ref remainingNodes, ref omissions);
            }

            return attributes;
        }
        catch
        {
            omissions |= ContextOmissionReason.SnapshotError;
            return null;
        }
    }

    internal static IReadOnlyDictionary<string, object?>? FlattenContext(IReadOnlyDictionary<string, object?>? attributes)
        => FlattenContext(attributes, out _);

    internal static IReadOnlyDictionary<string, object?>? FlattenContext(IReadOnlyDictionary<string, object?>? attributes, out ContextOmissionReason omissions)
    {
        var reasons = ContextOmissionReason.None;
        if (attributes is null)
        {
            omissions = reasons;
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
            reasons |= ContextOmissionReason.SnapshotError;
            // The context was already detached, but telemetry construction must remain best effort.
        }

        omissions = reasons;
        return flattened;

        void VisitDictionary(IEnumerable<KeyValuePair<string, object?>> dictionary, string prefix, int depth)
        {
            var count = 0;
            foreach (var pair in dictionary)
            {
                if (count++ >= 256)
                {
                    reasons |= depth == 0 ? ContextOmissionReason.MaxContextFields : ContextOmissionReason.MaxStructureProperties;
                    break;
                }

                if (remainingNodes-- <= 0)
                {
                    reasons |= ContextOmissionReason.MaxVisitedNodes;
                    break;
                }

                if (flattened.Count >= 256)
                {
                    reasons |= ContextOmissionReason.MaxContextFields;
                    break;
                }

                var separatorLength = prefix.Length == 0 ? 0 : 1;
                if (pair.Key.Length > MaxContextKeyLength
                 || prefix.Length > MaxContextKeyLength - separatorLength - pair.Key.Length)
                {
                    reasons |= ContextOmissionReason.MaxKeyLength;
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
                reasons |= depth > 4 ? ContextOmissionReason.MaxSnapshotDepth : ContextOmissionReason.MaxContextFields;
                return;
            }

            switch (value)
            {
                case IReadOnlyDictionary<string, object?> dictionary:
                    VisitDictionary(dictionary, key, depth);
                    break;
                case object?[] array:
                    if (array.Length > 256)
                    {
                        reasons |= ContextOmissionReason.MaxListElements;
                    }

                    var count = Math.Min(array.Length, 256);
                    for (var i = 0; i < count; i++)
                    {
                        if (remainingNodes-- <= 0)
                        {
                            reasons |= ContextOmissionReason.MaxVisitedNodes;
                            break;
                        }

                        if (flattened.Count >= 256)
                        {
                            reasons |= ContextOmissionReason.MaxContextFields;
                            break;
                        }

                        var index = i.ToString(CultureInfo.InvariantCulture);
                        if (key.Length > MaxContextKeyLength - 1 - index.Length)
                        {
                            reasons |= ContextOmissionReason.MaxKeyLength;
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

    private static object? Detach(object? value, int depth, ref int remainingNodes, ref ContextOmissionReason omissions)
    {
        if (remainingNodes-- <= 0)
        {
            omissions |= ContextOmissionReason.MaxVisitedNodes;
            return null;
        }

        if (depth >= 32)
        {
            omissions |= ContextOmissionReason.MaxSnapshotDepth;
            return null;
        }

        return value switch
        {
            null or string or bool or byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal or char or DateTime or DateTimeOffset or Guid => value,
            JValue valueToken => Detach(valueToken.Value, depth + 1, ref remainingNodes, ref omissions),
            JObject objectToken => DetachObject(objectToken, depth, ref remainingNodes, ref omissions),
            JArray arrayToken => DetachArray(arrayToken, depth, ref remainingNodes, ref omissions),
            JToken => Unsupported(ref omissions),
            IDictionary<string, object?> dictionary => DetachDictionary(dictionary, depth, ref remainingNodes, ref omissions),
            IDictionary dictionary => DetachDictionary(dictionary, depth, ref remainingNodes, ref omissions),
            IEnumerable enumerable => DetachEnumerable(enumerable, depth, ref remainingNodes, ref omissions),
            _ => Unsupported(ref omissions),
        };
    }

    private static object? Unsupported(ref ContextOmissionReason omissions)
    {
        omissions |= ContextOmissionReason.UnsupportedValue;
        return null;
    }

    private static Dictionary<string, object?> DetachDictionary(
        IEnumerable<KeyValuePair<string, object?>> dictionary,
        int depth,
        ref int remainingNodes,
        ref ContextOmissionReason omissions)
    {
        var detached = new Dictionary<string, object?>();
        var count = 0;
        foreach (var pair in dictionary)
        {
            if (count++ >= 256)
            {
                omissions |= ContextOmissionReason.MaxStructureProperties;
                break;
            }

            if (remainingNodes <= 0)
            {
                omissions |= ContextOmissionReason.MaxVisitedNodes;
                break;
            }

            if (pair.Key.Length > MaxContextKeyLength)
            {
                omissions |= ContextOmissionReason.MaxKeyLength;
                remainingNodes--;
                continue;
            }

            detached[pair.Key] = Detach(pair.Value, depth + 1, ref remainingNodes, ref omissions);
        }

        return detached;
    }

    private static Dictionary<string, object?> DetachDictionary(IDictionary dictionary, int depth, ref int remainingNodes, ref ContextOmissionReason omissions)
    {
        var detached = new Dictionary<string, object?>();
        var count = 0;
        foreach (var item in dictionary)
        {
            if (count++ >= 256)
            {
                omissions |= ContextOmissionReason.MaxStructureProperties;
                break;
            }

            if (item is not DictionaryEntry entry)
            {
                continue;
            }

            if (remainingNodes <= 0)
            {
                omissions |= ContextOmissionReason.MaxVisitedNodes;
                break;
            }

            if (entry.Key is not string key)
            {
                omissions |= ContextOmissionReason.UnsupportedValue;
                remainingNodes--;
                continue;
            }

            if (key.Length > MaxContextKeyLength)
            {
                omissions |= ContextOmissionReason.MaxKeyLength;
                remainingNodes--;
                continue;
            }

            detached[key] = Detach(entry.Value, depth + 1, ref remainingNodes, ref omissions);
        }

        return detached;
    }

    private static object?[] DetachEnumerable(IEnumerable enumerable, int depth, ref int remainingNodes, ref ContextOmissionReason omissions)
    {
        var detached = new List<object?>();
        var count = 0;
        foreach (var item in enumerable)
        {
            if (count++ >= 256)
            {
                omissions |= ContextOmissionReason.MaxListElements;
                break;
            }

            if (remainingNodes <= 0)
            {
                omissions |= ContextOmissionReason.MaxVisitedNodes;
                break;
            }

            detached.Add(Detach(item, depth + 1, ref remainingNodes, ref omissions));
        }

        return detached.ToArray();
    }

    private static Dictionary<string, object?> DetachObject(JObject value, int depth, ref int remainingNodes, ref ContextOmissionReason omissions)
    {
        var detached = new Dictionary<string, object?>();
        var count = 0;
        foreach (var property in value.Properties())
        {
            if (count++ >= 256)
            {
                omissions |= ContextOmissionReason.MaxStructureProperties;
                break;
            }

            if (remainingNodes <= 0)
            {
                omissions |= ContextOmissionReason.MaxVisitedNodes;
                break;
            }

            if (property.Name.Length > MaxContextKeyLength)
            {
                omissions |= ContextOmissionReason.MaxKeyLength;
                remainingNodes--;
                continue;
            }

            detached[property.Name] = Detach(property.Value, depth + 1, ref remainingNodes, ref omissions);
        }

        return detached;
    }

    private static object?[] DetachArray(JArray value, int depth, ref int remainingNodes, ref ContextOmissionReason omissions)
    {
        var detached = new List<object?>();
        var count = 0;
        foreach (var item in value)
        {
            if (count++ >= 256)
            {
                omissions |= ContextOmissionReason.MaxListElements;
                break;
            }

            if (remainingNodes <= 0)
            {
                omissions |= ContextOmissionReason.MaxVisitedNodes;
                break;
            }

            detached.Add(Detach(item, depth + 1, ref remainingNodes, ref omissions));
        }

        return detached.ToArray();
    }
}
