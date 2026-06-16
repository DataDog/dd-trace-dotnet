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
/// enrichment (NET-01). Mirrors <see cref="FlagEvalMetricsHook"/>: it runs in the Finally
/// stage on every evaluation (success + error), reads <c>__dd_split_serial_id</c> /
/// <c>__dd_do_log</c> from the flag metadata and the targeting key from the context, and
/// applies the frozen Node branch (serial id present → accumulate id, plus a subject when
/// do_log + targeting key; otherwise a missing variant → runtime default).
///
/// <para>This shim assembly cannot reference the core tracer, so it forwards to the
/// <see cref="FeatureFlagsSdk.AccumulateSpanEnrichment"/> stub, which CallTarget
/// auto-instrumentation rewrites to resolve the active root span and store the data in
/// <c>Datadog.Trace.FeatureFlags.SpanEnrichmentStore</c>. The hook is only constructed when
/// the gate is on (DG-005) and is disposed on <c>DatadogProvider.Dispose()</c>.</para>
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

            FeatureFlagsSdk.AccumulateSpanEnrichment(serialId, doLog, targetingKey, hasVariant, context.FlagKey, details.Value);
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
}
#endif
