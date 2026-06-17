// <copyright file="SpanEnrichmentHook.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if NET6_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using OpenFeature;
using OpenFeature.Model;

namespace Datadog.FeatureFlags.OpenFeature;

/// <summary>
/// OpenFeature Finally hook that captures feature-flag evaluation metadata for APM span
/// enrichment. Mirrors <see cref="FlagEvalMetricsHook"/>: it runs in the Finally
/// stage on every evaluation (success + error), reads <c>__dd_split_serial_id</c> /
/// <c>__dd_do_log</c> from the flag metadata and the targeting key from the context, and
/// applies the frozen Node branch (serial id present → accumulate id, plus a subject when
/// do_log + targeting key; otherwise a missing variant → runtime default).
///
/// <para>This shim assembly cannot reference the core tracer, so it forwards to the
/// <see cref="FeatureFlagsSdk.AccumulateSpanEnrichment"/> stub, which CallTarget
/// auto-instrumentation rewrites to resolve the active root span and store the data in
/// <c>Datadog.Trace.FeatureFlags.SpanEnrichmentStore</c>. The hook is only constructed when
/// the gate is on and is disposed on <c>DatadogProvider.Dispose()</c>.</para>
/// </summary>
internal sealed class SpanEnrichmentHook : Hook, IDisposable
{
    // Frozen-contract metadata keys (dd-trace-js#8343). Mirror of the values set in the core
    // evaluator's FlagMetadata; duplicated here because this shim assembly does not link the
    // core FeatureFlagsEvaluator type.
    private const string MetadataSplitSerialId = "__dd_split_serial_id";
    private const string MetadataDoLog = "__dd_do_log";

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
            var serialIdStr = metadata?.GetString(MetadataSplitSerialId);
            if (!string.IsNullOrEmpty(serialIdStr) &&
                long.TryParse(serialIdStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                serialId = parsed;
            }

            var doLogStr = metadata?.GetString(MetadataDoLog);
            var doLog = string.Equals(doLogStr, "true", StringComparison.OrdinalIgnoreCase);

            var targetingKey = context.EvaluationContext?.TargetingKey;
            var hasVariant = !string.IsNullOrEmpty(details.Variant);

            // Value to record when there is no variant (a runtime default). For scalar flags
            // (bool/string/number) details.Value already carries the default and the core tracer
            // stringifies it directly. Object/structure flags differ on two counts: (1) the provider
            // returns an empty OpenFeature Value on the not-found path, so details.Value cannot
            // reproduce the caller's object default; and (2) the core tracer cannot reference
            // OpenFeature.Model.Value, so handing it a raw Value makes Newtonsoft reflection-serialize
            // the wrapper's properties ({"IsNull":...,"IsBoolean":...}) instead of the structure.
            // For object flags we therefore record the caller's ORIGINAL default, unwrapped from the
            // OpenFeature Value tree into plain CLR objects, so the core tracer JSON-stringifies the
            // real structure as raw UTF-8 — byte-parity with the frozen Node reference (JSON.stringify).
            object? runtimeValue = details.Value;
            if ((object?)context.DefaultValue is Value defaultValue)
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
        // No owned resources; provider-close cleanup of accumulated state is performed by
        // DatadogProvider.Dispose() via the SpanEnrichmentStore bridge.
    }

    /// <summary>
    /// Recursively converts an OpenFeature <see cref="Value"/> tree into plain CLR objects
    /// (<see cref="Dictionary{TKey,TValue}"/>, <see cref="List{T}"/>, and scalars) so the core
    /// tracer can JSON-stringify the real structure rather than the <see cref="Value"/> wrapper's
    /// reflected properties. Integral numbers are emitted as <see cref="long"/> so the JSON renders
    /// without a trailing decimal point, matching the frozen Node reference's <c>JSON.stringify</c>.
    /// </summary>
    private static object? ToPlainObject(Value? value)
    {
        if (value is null || value.IsNull)
        {
            return null;
        }

        if (value.IsStructure)
        {
            var dict = new Dictionary<string, object?>();
            foreach (var pair in value.AsStructure!.AsDictionary())
            {
                dict[pair.Key] = ToPlainObject(pair.Value);
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

            // Render integral numbers without a decimal point (JSON.stringify(3) -> "3", not "3.0").
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
#endif
