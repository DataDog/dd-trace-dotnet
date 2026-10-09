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
        private const int MaxFields = 256;
        private const int MaxTextLength = 256;
        private const int MaxContainerItems = 256;
        private const int MaxDepth = 4;
        private const int MaxVisits = 1280;
        private int _visits;
        private bool _stopped;

        internal Dictionary<string, object?> Fields { get; } = new(StringComparer.Ordinal);

        internal ContextOmissionReason Reasons { get; private set; }

        internal void VisitMap(IImmutableDictionary<string, Value> map, string prefix, int depth)
        {
            if (map.Count > MaxContainerItems)
            {
                Reasons |= ContextOmissionReason.MaxStructureProperties;
            }

            var count = Math.Min(map.Count, MaxContainerItems);
            // Bound enumeration without collecting/sorting the input. Rebuilt oversized maps with
            // colliding key hashes can select different subsets; evaluation results are unaffected.
            using var iterator = map.GetEnumerator();
            for (var i = 0; i < count && TryVisit() && iterator.MoveNext(); i++)
            {
                var pair = iterator.Current;

                if (depth == 0 && pair.Key == "targetingKey")
                {
                    continue;
                }

                if (pair.Key.Length > MaxTextLength || (depth != 0 && prefix.Length + 1 + pair.Key.Length > MaxTextLength))
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

            if (Fields.Count == MaxFields)
            {
                Reasons |= ContextOmissionReason.MaxContextFields;
                _stopped = true;
                return false;
            }

            if (_visits == MaxVisits)
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
            if (depth > MaxDepth)
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
                if (list.Count > MaxContainerItems)
                {
                    Reasons |= ContextOmissionReason.MaxListElements;
                }

                var count = Math.Min(list.Count, MaxContainerItems);
                for (var i = 0; i < count && TryVisit(); i++)
                {
                    var childKey = key + "." + i.ToString(CultureInfo.InvariantCulture);
                    if (childKey.Length > MaxTextLength)
                    {
                        Reasons |= ContextOmissionReason.MaxKeyLength;
                        continue;
                    }

                    VisitValue(childKey, list[i], depth + 1);
                }
            }
            else if (value?.AsString is { } text)
            {
                if (text.Length > MaxTextLength)
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
