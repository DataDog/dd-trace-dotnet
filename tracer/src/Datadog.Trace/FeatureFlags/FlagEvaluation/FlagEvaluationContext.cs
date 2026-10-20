// <copyright file="FlagEvaluationContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Datadog.Trace.FeatureFlags.FlagEvaluation;

internal static class FlagEvaluationContext
{
    private const int MaxFields = 256;
    private const int MaxTextLength = 256;

    internal static Dictionary<string, object?>? Copy(IReadOnlyDictionary<string, object?>? attributes, out ContextOmissionReason omissions)
    {
        omissions = ContextOmissionReason.None;
        if (attributes is null)
        {
            return null;
        }

        try
        {
            var count = attributes.Count;
            if (count == 0)
            {
                return null;
            }

            if (count > MaxFields)
            {
                omissions |= ContextOmissionReason.MaxContextFields;
            }

            var result = new Dictionary<string, object?>(Math.Min(count, MaxFields), StringComparer.Ordinal);
            using var iterator = attributes.GetEnumerator();
            for (var i = 0; i < MaxFields && iterator.MoveNext(); i++)
            {
                var pair = iterator.Current;
                if (pair.Key is null)
                {
                    omissions |= ContextOmissionReason.UnsupportedValue;
                    continue;
                }

                if (pair.Key.Length > MaxTextLength)
                {
                    omissions |= ContextOmissionReason.MaxKeyLength;
                    continue;
                }

                if (!FlagEvaluationPrivacy.IsValidText(pair.Key))
                {
                    omissions |= ContextOmissionReason.UnsupportedValue;
                    continue;
                }

                if (pair.Value is string text && text.Length > MaxTextLength)
                {
                    omissions |= ContextOmissionReason.MaxValueLength;
                    continue;
                }

                if (!IsScalar(pair.Value))
                {
                    omissions |= ContextOmissionReason.UnsupportedValue;
                    continue;
                }

                result[pair.Key] = pair.Value;
            }

            return result.Count == 0 ? null : result;
        }
        catch (Exception)
        {
            omissions = ContextOmissionReason.SnapshotError;
            return null;
        }
    }

    internal static string CanonicalKey(Dictionary<string, object?>? attributes)
    {
        if (attributes is null || attributes.Count == 0)
        {
            return string.Empty;
        }

        var keys = new List<string>(attributes.Keys);
        keys.Sort(StringComparer.Ordinal);
        var builder = new StringBuilder();
        foreach (var key in keys)
        {
            var value = attributes[key]!;
            // Copy admits only primitives. Type tags keep 1, 1d, true and "1" distinct;
            // character-length prefixes prevent keys or values from forging field boundaries.
            builder.Append(key.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(key);
            builder.Append(((int)Type.GetTypeCode(value.GetType())).ToString(CultureInfo.InvariantCulture)).Append(':');
            var text = value switch
            {
                string s => s,
                bool b => b ? "true" : "false",
                double d => d.ToString("R", CultureInfo.InvariantCulture),
                float f => f.ToString("R", CultureInfo.InvariantCulture),
                _ => Convert.ToString(value, CultureInfo.InvariantCulture)!,
            };
            builder.Append(text.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(text);
        }

        return builder.ToString();
    }

    private static bool IsScalar(object? value) => value switch
    {
        string text => FlagEvaluationPrivacy.IsValidText(text),
        bool or byte or sbyte or short or ushort or int or uint or long or ulong or decimal => true,
        double number => !double.IsNaN(number) && !double.IsInfinity(number),
        float number => !float.IsNaN(number) && !float.IsInfinity(number),
        _ => false,
    };
}
