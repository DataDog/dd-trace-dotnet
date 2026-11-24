// <copyright file="FlagEvaluationContextSnapshot.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using Datadog.Trace.FeatureFlags.FlagEvaluation;
using OpenFeature.Model;

namespace Datadog.FeatureFlags.OpenFeature;

internal static class FlagEvaluationContextSnapshot
{
    internal static IReadOnlyDictionary<string, object?> Capture(EvaluationContext? context, out int omissionReasons)
    {
        var snapshot = new Snapshot();
        if (context is not null)
        {
            snapshot.VisitMap(context.AsDictionary(), string.Empty, 0);
        }

        omissionReasons = (int)snapshot.Reasons;
        return snapshot.Fields;
    }

    private sealed class Snapshot
    {
        private const string TargetingKey = "targetingKey";
        private int _visits;
        private bool _stopped;

        internal Dictionary<string, object?> Fields { get; } = new(StringComparer.Ordinal);

        internal ContextOmissionReason Reasons { get; private set; }

        internal void VisitMap(IImmutableDictionary<string, Value> map, string prefix, int depth)
        {
            // The root targetingKey is never captured as an attribute, so it does not consume the
            // attribute budget. A string value is reported separately as the event's targeting key.
            var attributeCount = map.Count - (depth == 0 && map.ContainsKey(TargetingKey) ? 1 : 0);
            if (attributeCount > ContextSnapshotLimits.ContainerItems)
            {
                Reasons |= depth == 0 ? ContextOmissionReason.MaxContextFields : ContextOmissionReason.MaxStructureProperties;
            }

            var remaining = Math.Min(attributeCount, ContextSnapshotLimits.ContainerItems);
            // Bound enumeration without collecting/sorting the input. Rebuilt oversized maps with
            // colliding key hashes can select different subsets; evaluation results are unaffected.
            using var iterator = map.GetEnumerator();
            while (remaining > 0 && iterator.MoveNext())
            {
                var pair = iterator.Current;

                if (depth == 0 && pair.Key == TargetingKey)
                {
                    continue;
                }

                remaining--;
                if (!TryVisit())
                {
                    break;
                }

                if (pair.Key.Length > ContextSnapshotLimits.TextLength || (depth != 0 && prefix.Length + 1 + pair.Key.Length > ContextSnapshotLimits.TextLength))
                {
                    Reasons |= ContextOmissionReason.MaxKeyLength;
                    continue;
                }

                var key = depth == 0 ? pair.Key : prefix + "." + pair.Key;
                VisitValue(key, pair.Value, depth + 1);
            }
        }

        private bool TryVisit()
        {
            if (_stopped)
            {
                return false;
            }

            if (Fields.Count == ContextSnapshotLimits.Fields)
            {
                Reasons |= ContextOmissionReason.MaxContextFields;
                _stopped = true;
                return false;
            }

            if (_visits == ContextSnapshotLimits.Visits)
            {
                Reasons |= ContextOmissionReason.MaxVisitedNodes;
                _stopped = true;
                return false;
            }

            _visits++;
            return true;
        }

        private void VisitValue(string key, Value? value, int depth)
        {
            if (depth > ContextSnapshotLimits.Depth)
            {
                Reasons |= ContextOmissionReason.MaxSnapshotDepth;
                return;
            }

            if (value?.AsStructure is { } structure)
            {
                VisitMap(structure.AsDictionary(), key, depth);
            }
            else if (value?.AsList is { } list)
            {
                if (list.Count > ContextSnapshotLimits.ContainerItems)
                {
                    Reasons |= ContextOmissionReason.MaxListElements;
                }

                var count = Math.Min(list.Count, ContextSnapshotLimits.ContainerItems);
                for (var i = 0; i < count && TryVisit(); i++)
                {
                    var childKey = key + "." + i.ToString(CultureInfo.InvariantCulture);
                    if (childKey.Length > ContextSnapshotLimits.TextLength)
                    {
                        Reasons |= ContextOmissionReason.MaxKeyLength;
                        continue;
                    }

                    VisitValue(childKey, list[i], depth + 1);
                }
            }
            else if (value?.AsString is { } text)
            {
                if (text.Length > ContextSnapshotLimits.TextLength)
                {
                    Reasons |= ContextOmissionReason.MaxValueLength;
                }
                else
                {
                    Fields[key] = text;
                }
            }
            else if (value?.AsBoolean is { } boolean)
            {
                Fields[key] = boolean;
            }
            else if (value?.AsDouble is { } number && !double.IsNaN(number) && !double.IsInfinity(number))
            {
                Fields[key] = number;
            }
            else
            {
                Reasons |= ContextOmissionReason.UnsupportedValue;
            }
        }
    }
}
