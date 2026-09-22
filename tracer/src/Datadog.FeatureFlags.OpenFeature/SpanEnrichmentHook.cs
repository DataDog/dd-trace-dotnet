// <copyright file="SpanEnrichmentHook.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.FeatureFlags;
using OpenFeature;
using OpenFeature.Model;

namespace Datadog.FeatureFlags.OpenFeature;

internal sealed class SpanEnrichmentHook : Hook
{
    /// <inheritdoc/>
    public override ValueTask FinallyAsync<T>(
        HookContext<T> context,
        FlagEvaluationDetails<T> details,
        IReadOnlyDictionary<string, object>? hints = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var metadata = details.FlagMetadata;

            long? serialId = null;
            var serialIdStr = metadata?.GetString(FeatureFlagMetadataKeys.SplitSerialId);
            if (!string.IsNullOrEmpty(serialIdStr) &&
                long.TryParse(serialIdStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                serialId = parsed;
            }

            var doLogStr = metadata?.GetString(FeatureFlagMetadataKeys.DoLog);
            var doLog = string.Equals(doLogStr, "true", StringComparison.OrdinalIgnoreCase);

            var targetingKey = context.EvaluationContext?.TargetingKey;
            var hasVariant = !string.IsNullOrEmpty(details.Variant);

            // The value is only recorded as a runtime default (no serial id and no variant); skip the
            // ToPlainObject conversion + boxing in every other case, where it would be ignored.
            object? runtimeValue = null;
            if (serialId is null && !hasVariant)
            {
                runtimeValue = context.DefaultValue is Value defaultValue ? ToPlainObject(defaultValue) : details.Value;
            }

            FeatureFlagsSdk.AccumulateSpanEnrichment(serialId, doLog, targetingKey, hasVariant, context.FlagKey, runtimeValue);
        }
        catch (Exception)
        {
            // Enrichment must never break flag evaluation.
        }

        return default;
    }

    private static object? ToPlainObject(Value? value)
    {
        if (value is null || value.IsNull)
        {
            return null;
        }

        if (value.AsStructure is { } structure)
        {
            var orig = structure.AsDictionary();
            var dict = new Dictionary<string, object?>(orig.Count);
            foreach (var pair in orig)
            {
                dict[pair.Key] = ToPlainObject(pair.Value);
            }

            return dict;
        }

        if (value.AsList is { } listValue)
        {
            var list = new List<object?>(listValue.Count);
            foreach (var item in listValue)
            {
                list.Add(ToPlainObject(item));
            }

            return list;
        }

        if (value.AsBoolean is { } boolValue)
        {
            return boolValue;
        }

        if (value.AsString is { } stringValue)
        {
            return stringValue;
        }

        if (value.AsDouble is { } d)
        {
            if (!double.IsNaN(d) && !double.IsInfinity(d) && d == Math.Floor(d) && Math.Abs(d) < 9.007199254740992E15)
            {
                return (long)d;
            }

            return d;
        }

        if (value.AsDateTime is { } dateTimeValue)
        {
            return dateTimeValue;
        }

        return null;
    }
}
