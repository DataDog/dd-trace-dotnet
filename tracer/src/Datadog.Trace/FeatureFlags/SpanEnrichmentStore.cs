// <copyright file="SpanEnrichmentStore.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Threading;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.FeatureFlags
{
    internal sealed class SpanEnrichmentStore
    {
        internal const int MaxTrackedRootSpans = 10_000;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(SpanEnrichmentStore));

        private readonly bool _isEnabled;

        private ConcurrentDictionary<ulong, SpanEnrichmentState>? _states;
        private int _capWarningLogged;

        internal SpanEnrichmentStore(bool isEnabled)
        {
            _isEnabled = isEnabled;
        }

        /// <summary>
        /// Gets a value indicating whether span enrichment is enabled for this tracer manager.
        /// </summary>
        internal bool IsEnabled => _isEnabled;

        /// <summary>
        /// Gets the number of distinct root spans currently tracked.
        /// </summary>
        internal int TrackedRootCount => _states?.Count ?? 0;

        /// <summary>
        /// Gets or sets a fault-injection hook invoked at the start of <see cref="GetAndClear"/>.
        /// </summary>
        internal Action? OnGetAndClearForTesting { get; set; }

        /// <summary>
        /// Accumulates a single flag evaluation into the per-root-span state. Never throws.
        /// </summary>
        /// <param name="rootSpanId">The id of the root span the active eval belongs to.</param>
        /// <param name="serialId">The split serial id, or null when absent.</param>
        /// <param name="doLog">Whether the allocation authorizes subject logging.</param>
        /// <param name="targetingKey">The evaluation context targeting key, or null.</param>
        /// <param name="hasVariant">Whether the evaluation produced a (non-empty) variant.</param>
        /// <param name="flagKey">The flag key (used for runtime defaults).</param>
        /// <param name="value">The evaluated value (used for runtime defaults).</param>
        public void Accumulate(ulong rootSpanId, long? serialId, bool doLog, string? targetingKey, bool hasVariant, string flagKey, object? value)
        {
            if (!_isEnabled || (serialId is null && hasVariant))
            {
                return;
            }

            try
            {
                var states = LazyInitializer.EnsureInitialized(ref _states, static () => new ConcurrentDictionary<ulong, SpanEnrichmentState>())!;

                if (!states.TryGetValue(rootSpanId, out var state))
                {
                    if (states.Count >= MaxTrackedRootSpans)
                    {
                        if (Interlocked.Exchange(ref _capWarningLogged, 1) == 0)
                        {
                            Log.Warning<int>(
                                "SpanEnrichmentStore: tracked-root cap ({Max}) reached; dropping span enrichment for new root spans until existing roots finish. A root span may not be finishing.",
                                MaxTrackedRootSpans);
                        }

                        return;
                    }

                    state = states.GetOrAdd(rootSpanId, static _ => new SpanEnrichmentState());
                }

                if (serialId.HasValue)
                {
                    state.AddSerialId(serialId.Value);
                    if (doLog && !StringUtil.IsNullOrEmpty(targetingKey))
                    {
                        state.AddSubject(targetingKey!, serialId.Value);
                    }
                }
                else if (!hasVariant)
                {
                    state.AddDefault(flagKey, value);
                }
            }
            catch (Exception ex)
            {
                // Enrichment must never break flag evaluation.
                Log.Warning(ex, "SpanEnrichmentStore.Accumulate failed");
            }
        }

        /// <summary>
        /// Accumulates a native FeatureFlags SDK evaluation into the root span.
        /// </summary>
        /// <param name="rootSpanId">The id of the root span the active eval belongs to.</param>
        /// <param name="evaluation">The completed evaluation returned by the evaluator.</param>
        /// <param name="targetingKey">The caller's targeting key, or null.</param>
        public void AccumulateForRoot(ulong rootSpanId, IEvaluation? evaluation, string? targetingKey)
        {
            if (evaluation is null)
            {
                return;
            }

            long? serialId = null;
            var metadata = evaluation.FlagMetadata;
            if (metadata is not null &&
                metadata.TryGetValue(FeatureFlagMetadataKeys.SplitSerialId, out var serialIdStr) &&
                !StringUtil.IsNullOrEmpty(serialIdStr) &&
                long.TryParse(serialIdStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                serialId = parsed;
            }

            var doLog =
                metadata is not null &&
                metadata.TryGetValue(FeatureFlagMetadataKeys.DoLog, out var doLogStr) &&
                string.Equals(doLogStr, "true", StringComparison.OrdinalIgnoreCase);

            Accumulate(
                rootSpanId,
                serialId,
                doLog,
                targetingKey,
                hasVariant: !StringUtil.IsNullOrEmpty(evaluation.Variant),
                evaluation.FlagKey,
                evaluation.Value);
        }

        /// <summary>
        /// Removes and returns the accumulated state for a root span, if any. Called from
        /// <c>Span.Finish()</c> after the cheap gate check. Returns null when nothing was
        /// accumulated (a plain dictionary lookup — no allocation).
        /// </summary>
        /// <param name="rootSpanId">The root-span id.</param>
        /// <returns>The state, or null.</returns>
        public SpanEnrichmentState? GetAndClear(ulong rootSpanId)
        {
            OnGetAndClearForTesting?.Invoke();

            var states = _states;
            if (states is null)
            {
                return null;
            }

            states.TryRemove(rootSpanId, out var state);
            return state;
        }

        /// <summary>
        /// Releases all accumulated state.
        /// </summary>
        public void Clear()
        {
            Interlocked.Exchange(ref _states, null);
            Interlocked.Exchange(ref _capWarningLogged, 0);
        }
    }
}
