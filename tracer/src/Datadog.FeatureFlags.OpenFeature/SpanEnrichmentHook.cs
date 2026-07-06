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

internal sealed class SpanEnrichmentHook : Hook, IDisposable
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

            object? runtimeValue = details.Value;
            if (context.DefaultValue is Value defaultValue)
            {
                runtimeValue = ToPlainObject(defaultValue);
            }

            FeatureFlagsSdk.AccumulateSpanEnrichment(serialId, doLog, targetingKey, hasVariant, context.FlagKey, runtimeValue);
        }
        catch (Exception ex)
        {
            // Enrichment must never break flag evaluation.
            System.Diagnostics.Debug.WriteLine($"[Datadog] SpanEnrichmentHook.FinallyAsync failed: {ex}");
        }

        return default;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // No owned resources; per-trace enrichment state is released with its trace context.
    }

    private static object? ToPlainObject(Value? value)
    {
        if (value is null || value.IsNull)
        {
            return null;
        }

        if (value.IsStructure)
        {
            var dict = new Dictionary<string, object?>();
            var structure = value.AsStructure!;
            foreach (var key in structure.Keys)
            {
                dict[key] = ToPlainObject(structure[key]);
            }

            return dict;
        }

        if (value.IsList)
        {
            var list = new List<object?>();
            foreach (var item in value.AsList!)
            {
                list.Add(ToPlainObject(item));
            }

            return list;
        }

        if (value.IsBoolean)
        {
            return value.AsBoolean;
        }

        if (value.IsString)
        {
            return value.AsString;
        }

        if (value.IsNumber)
        {
            var d = value.AsDouble ?? 0d;

            if (!double.IsNaN(d) && !double.IsInfinity(d) && d == Math.Floor(d) && Math.Abs(d) < 9.007199254740992E15)
            {
                return (long)d;
            }

            return d;
        }

        if (value.IsDateTime)
        {
            return value.AsDateTime;
        }

        return null;
    }
}
