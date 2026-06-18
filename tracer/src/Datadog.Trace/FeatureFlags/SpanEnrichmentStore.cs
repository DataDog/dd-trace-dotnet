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
    /// <summary>
    /// Process-wide store of per-root-span <see cref="SpanEnrichmentState"/> for FFE APM span
    /// enrichment, keyed by root-span id. This is the seam that decouples the OpenFeature
    /// <c>SpanEnrichmentHook</c> (which lives in the separate <c>Datadog.FeatureFlags.OpenFeature</c>
    /// shim assembly and is bridged here by CallTarget auto-instrumentation) from the core span
    /// pipeline: the hook accumulates via <see cref="Accumulate"/>, and <c>Span.Finish()</c> drains
    /// the state via <see cref="GetAndClear"/> in its <c>IsRootSpan</c> block, before <c>CloseSpan</c>.
    ///
    /// <para>When the <c>DD_EXPERIMENTAL_FLAGGING_PROVIDER_SPAN_ENRICHMENT_ENABLED</c> gate is off,
    /// <c>Span.Finish()</c> reads only the cheap <see cref="IsEnabled"/> latch and never touches this
    /// store, so no per-span state is ever allocated. The backing dictionary is lazily created and
    /// entries are GC-bounded: each is removed on root-span finish (here) and the whole store is
    /// released on provider <c>Dispose</c> via <see cref="Clear"/>.</para>
    /// </summary>
    internal static class SpanEnrichmentStore
    {
        // Gate-on growth bound. Entries are normally reclaimed on root-span finish (GetAndClear)
        // or provider Dispose (Clear), but a root span that never finishes — abandoned scopes,
        // sampled-out traces that skip Finish, or long-lived ambient roots in background
        // workers/message pumps — would otherwise leave its state in the map forever. Once the map
        // holds this many distinct roots we stop creating new entries (existing roots still
        // accumulate so in-flight evals complete and the eventual drain stays correct). The
        // per-root limits already cap each state's memory, so a simple max-roots cap is the
        // lowest-risk mitigation.
        internal const int MaxTrackedRootSpans = 10_000;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(SpanEnrichmentStore));

        // Process-wide one-way latch flipped exactly once when a TracerSettings is constructed with
        // span enrichment enabled. Span.Finish() reads only this volatile bool on the gate-off path,
        // so the disabled path costs a single volatile read with no settings-chain dereference and no
        // allocation. One-way is acceptable: the feature is an experimental opt-in env var
        // (DD_EXPERIMENTAL_FLAGGING_PROVIDER_SPAN_ENRICHMENT_ENABLED), off by default, and the gate-on
        // path is itself internally guarded (an empty store returns null with no allocation).
        private static volatile bool _isEnabled;

        // Keyed by root-span id. Lazily allocated on first accumulate so the gate-off path
        // (which never calls Accumulate) leaves this null and allocation-free.
        private static ConcurrentDictionary<ulong, SpanEnrichmentState>? _states;

        // Set once (per process) after the cap is first hit, so the warning is logged exactly once
        // rather than on every dropped evaluation.
        private static int _capWarningLogged;

        /// <summary>
        /// Gets a value indicating whether span enrichment has been enabled by any TracerSettings in
        /// this process. Cheap gate read for the <c>Span.Finish()</c> hot path: when false, span
        /// finish does no enrichment work and allocates no per-span state.
        /// </summary>
        internal static bool IsEnabled => _isEnabled;

        /// <summary>
        /// Gets the number of distinct root spans currently tracked. Test seam for the growth-bound
        /// regression test; cheap (a <see cref="ConcurrentDictionary{TKey,TValue}.Count"/> read).
        /// </summary>
        internal static int TrackedRootCount => _states?.Count ?? 0;

        /// <summary>
        /// Gets or sets a fault-injection hook invoked at the start of <see cref="GetAndClear"/>.
        /// Test-only seam (mirrors the established <c>*ForTesting</c> pattern) used to deterministically
        /// force a throw on the <c>Span.Finish()</c> drain path and prove the never-throw guard keeps
        /// span finish working. Always null in production.
        /// </summary>
        internal static Action? OnGetAndClearForTesting { get; set; }

        /// <summary>
        /// Latches span enrichment on for the process. Called from the TracerSettings constructor when
        /// the enrichment flag parses true. One-way: never flips back off.
        /// </summary>
        internal static void Enable() => _isEnabled = true;

        /// <summary>
        /// Test-only: resets the process-wide enable latch so a negative-control test can prove the
        /// gate-off path is inert regardless of test ordering. Always on in production once set.
        /// </summary>
        internal static void ResetIsEnabledForTesting() => _isEnabled = false;

        /// <summary>
        /// Accumulates a single flag evaluation into the per-root-span state, applying the frozen
        /// Node branch: a present serial id is added (plus a subject when <paramref name="doLog"/> and
        /// a targeting key are present); otherwise a missing variant signals a runtime default.
        /// Called from the OpenFeature hook via auto-instrumentation. Never throws.
        /// </summary>
        /// <param name="rootSpanId">The id of the root span the active eval belongs to.</param>
        /// <param name="serialId">The split serial id, or null when absent.</param>
        /// <param name="doLog">Whether the allocation authorizes subject logging.</param>
        /// <param name="targetingKey">The evaluation context targeting key, or null.</param>
        /// <param name="hasVariant">Whether the evaluation produced a (non-empty) variant.</param>
        /// <param name="flagKey">The flag key (used for runtime defaults).</param>
        /// <param name="value">The evaluated value (used for runtime defaults).</param>
        public static void Accumulate(ulong rootSpanId, long? serialId, bool doLog, string? targetingKey, bool hasVariant, string flagKey, object? value)
        {
            try
            {
                // Atomic lazy init: concurrent first evaluations must converge on a single backing
                // dictionary. A plain `_states ??= new(...)` is not atomic — two threads can each
                // construct a dictionary and the losing assignment's accumulated state would be
                // silently dropped. EnsureInitialized publishes exactly one instance.
                var states = LazyInitializer.EnsureInitialized(ref _states, static () => new ConcurrentDictionary<ulong, SpanEnrichmentState>())!;

                // Always allow accumulation into a root we are already tracking (so in-flight evals
                // for a live trace are never lost). Only NEW roots are subject to the growth cap.
                if (!states.TryGetValue(rootSpanId, out var state))
                {
                    if (states.Count >= MaxTrackedRootSpans)
                    {
                        // Gate-on growth bound hit: drop this new root rather than grow unboundedly.
                        // Log once to avoid spamming the customer's logs.
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
                    // Runtime-default detection = missing variant (NOT a reason enum).
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
        /// Accumulates a native FeatureFlags SDK evaluation into the active root span. This mirrors
        /// the OpenFeature shim hook, but runs from the Datadog.Trace.Manual CallTarget integration
        /// because native SDK calls do not pass through OpenFeature provider hooks.
        /// </summary>
        /// <param name="evaluation">The completed evaluation returned by the FFE evaluator.</param>
        /// <param name="targetingKey">The caller's targeting key, or null.</param>
        public static void AccumulateForActiveRoot(IEvaluation? evaluation, string? targetingKey)
        {
            if (!_isEnabled || evaluation is null)
            {
                return;
            }

            var rootSpan = Datadog.Trace.Tracer.Instance.InternalActiveScope?.Span?.Context.TraceContext?.RootSpan;
            if (rootSpan is null)
            {
                return;
            }

            long? serialId = null;
            var metadata = evaluation.FlagMetadata;
            if (metadata is not null &&
                metadata.TryGetValue(FeatureFlagsEvaluator.MetadataSplitSerialId, out var serialIdStr) &&
                !StringUtil.IsNullOrEmpty(serialIdStr) &&
                long.TryParse(serialIdStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                serialId = parsed;
            }

            var doLog =
                metadata is not null &&
                metadata.TryGetValue(FeatureFlagsEvaluator.MetadataDoLog, out var doLogStr) &&
                string.Equals(doLogStr, "true", StringComparison.OrdinalIgnoreCase);

            Accumulate(
                rootSpan.SpanId,
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
        public static SpanEnrichmentState? GetAndClear(ulong rootSpanId)
        {
            // Test-only fault injection (null in production): lets a regression test force a throw on
            // the Span.Finish() drain path to prove the never-throw guard.
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
        /// Releases all accumulated state. Called on provider <c>Dispose</c> so a reconfigured
        /// provider does not leak prior state (symmetric with hook teardown). Atomically swaps out the
        /// backing map (rather than clearing it in place) so any concurrent <see cref="Accumulate"/>
        /// either operates on the old map (which is then GC'd) or lazily re-creates a fresh one,
        /// instead of racing an in-place <c>Clear()</c> that would leave a partially-cleared map.
        /// </summary>
        public static void Clear()
        {
            // Atomic release: drop the whole map. A racing Accumulate that already captured the old
            // reference completes against the now-orphaned map (its state is simply discarded, which
            // is the intent of a provider-close clear); a later Accumulate re-initializes a fresh map.
            Interlocked.Exchange(ref _states, null);

            // Re-arm the one-shot cap warning so a reconfigured provider (or a fresh test) can log
            // again if it re-hits the bound.
            Interlocked.Exchange(ref _capWarningLogged, 0);
        }
    }
}
