# FFE Span Enrichment — dotnet redesign proposal

Addresses the `CHANGES_REQUESTED` review on [dd-trace-dotnet#8795](https://github.com/DataDog/dd-trace-dotnet/pull/8795) (reviewer: **andrewlock**). This is a design for review; once approved we implement on a **stacked branch** on top of `leo.romanovsky/ffe-apm-span-enrichment`.

---

## 1. Current design (what's on the PR now)

- **`SpanEnrichmentStore`** — a process/tracer-wide `ConcurrentDictionary<ulong rootSpanId, SpanEnrichmentState>` hung off `TracerManager.SpanEnrichment`, hard-capped at 10,000 roots with a warn-once + drop-on-overflow.
- **`SpanEnrichmentState`** — per-root state (`HashSet<long>` serial ids, subjects, defaults) guarded by a `lock`; `ToSpanTags()` snapshots under the lock then does ULEB128 + JSON encoding.
- **Accumulate path** — the OpenFeature hook and native evaluator call a **static** `FeatureFlagsSdk.AccumulateSpanEnrichment(...)`; the CallTarget integration resolves `Tracer.Instance.InternalActiveScope?.Span?.Context.TraceContext?.RootSpan` and calls `store.Accumulate(rootSpan.SpanId, …)`.
- **Drain path** — `Span.Finish()` runs, for **every root span of every trace**, a lookup into the store (`GetAndClear(SpanId)`), then `ToSpanTags()` + `SetTag()` **synchronously in the finish hot path**.
- **Gate** — `settings.IsSpanEnrichmentEnabled` read in the `TracerManager` constructor to build the store.

## 2. Problems raised in review (grouped)

| # | Reviewer concern | Where |
|---|---|---|
| A | **Central store forces synchronization + hot-path work.** "storing that state in a central location seems suboptimal — would it not be better to store it on the span itself?" | `Span.cs:487`, `SpanEnrichmentState.cs:162` |
| B | **Hot-path cost for all apps.** Every root-span finish does a store lookup; when enabled, allocations + the expensive `SetTag()` path, all in `Finish`. | `Span.cs:487` |
| C | **Async-context correctness.** "Won't you end up with random spans tagged with feature flags from completely different traces running concurrently?" | review summary |
| D | **Defer serialization.** "If this state was stored on the span, we can defer the serialization to the background trace serialization path instead." | `Span.cs:487` |
| E | **Static/global access is bad for testing.** Central static store + static SDK bridge. | review summary, `FeatureFlagsSdkEvaluateIntegration.cs:48`, `TracerSettings.cs:687` |
| F | **Log flooding.** A warn in the finish/accumulate path can trip on every span. | `Span.cs:487` |
| G | **Cap/leak machinery** (10k cap, warn-once) exists only because the store outlives spans. | `SpanEnrichmentStore.cs` |

## 3. Proposed design — state lives on the span

**Core idea:** attach the per-root enrichment state directly to the **root `Span`** (via `TraceContext`), and emit the `ffe_*` tags lazily during **span serialization** (`SpanMessagePackFormatter`), on the background writer thread — not in `Span.Finish()`.

This is exactly the reviewer's suggested direction and collapses A–G at once.

### 3.1 Where the state lives

Add a single nullable field to the root-span owner. Two viable homes:

- **Option 1 (preferred): on `TraceContext`.** `TraceContext` already owns `RootSpan` and is the natural per-local-trace container. Add:
  ```csharp
  // TraceContext
  internal SpanEnrichmentState? FeatureFlagEnrichment { get; private set; }
  internal SpanEnrichmentState GetOrCreateFeatureFlagEnrichment(); // lazy, null until first eval
  ```
  Lifetime = the trace's; GC'd with the `TraceContext`. **No central map, no SpanId key, no cap, no eviction.** (Deletes `SpanEnrichmentStore` entirely → resolves A, E-store, G.)

- **Option 2: on `Span`.** A nullable field on the root `Span`. Simpler typing but `Span` is higher-churn; `TraceContext` is the cleaner owner. *Recommendation: Option 1.*

### 3.2 Accumulation path (resolves C, E)

The integration already has the resolved root span/trace context — it writes **directly** to that trace's state, so an evaluation can only ever touch its own trace's root. No SpanId keying, no shared map ⇒ **the "different concurrent trace" bug (C) is structurally impossible.**

```csharp
// OpenFeatureSdkAccumulateSpanEnrichmentIntegration.OnMethodBegin
var traceContext = tracer.InternalActiveScope?.Span?.Context.TraceContext;
if (traceContext is not null && tracer.Settings.IsSpanEnrichmentEnabled)
{
    traceContext.GetOrCreateFeatureFlagEnrichment()
                .Accumulate(serialId, doLog, targetingKey, hasVariant, flagKey, value);
}
```

- The gate is a cheap bool on settings, read at accumulate time (not a store construction). Keeps `TracerSettings` free of behavior (resolves E/`TracerSettings.cs:687`).
- The **static `FeatureFlagsSdk.AccumulateSpanEnrichment`** stays — it is the manual-instrumentation ABI (its siblings `IsAvailable`/`Evaluate` are all static no-op seams instrumented via CallTarget). The reviewer's "avoid static global" is about **state**, not this instrumentation seam; state no longer lives in any static/central object, so the concern is satisfied. We note this explicitly in the PR reply.

### 3.3 Serialization — deferred to the writer thread (resolves B, D)

Instead of draining + `SetTag` in `Span.Finish()`, the `SpanMessagePackFormatter` — which already special-cases many computed tags (env, version, git, …) in `WriteTags` — emits the `ffe_*` tags when it serializes a **root** span that has non-empty enrichment state.

- Encoding (ULEB128 + JSON + SHA256) runs on the **background serialization thread**, off the request hot path.
- By serialization time the trace is finished and single-threaded, so the encode needs **no lock** (the lock only guards concurrent *accumulation*; see 3.4).
- `Span.Finish()` loses the enrichment block entirely ⇒ **zero added work in the finish hot path**, and no `SetTag()` allocations (resolves B).

*Implementation note to confirm during build:* the exact `WriteTags` insertion point and the tag-count bookkeeping in `SpanMessagePackFormatter` (the map header length must include the emitted `ffe_*` keys). This is the one genuinely new integration point and the main implementation risk; everything else is deletion/relocation.

### 3.4 Concurrency (resolves A, partially)

A single root can still accumulate from concurrent evals (e.g. `Task.WhenAll` fan-out under one ambient trace), so `SpanEnrichmentState` keeps a per-instance lock for its mutators. But:
- It's **per-trace**, not a shared global map ⇒ no cross-trace contention (the central-store lock contention in A is gone).
- No `ConcurrentDictionary`, no cap check, no eviction on the accumulate path.

### 3.5 Lifecycle / cleanup (resolves G, and provider-close)

- No central store ⇒ nothing to leak; state dies with its `TraceContext`.
- Provider close: `FeatureFlagsSdk.ClearSpanEnrichment()` / the store's `Clear()` are **removed** — there is no global state to clear. (Deletes `OpenFeatureSdkClearSpanEnrichmentIntegration` and the `DatadogProvider.Dispose` bridge, or reduces them to no-ops.)
- The `SpanEnrichmentHook : IDisposable` can drop the disposal comment about the store bridge.

### 3.6 Log flooding (resolves F)

The only warn was the store cap; with the store gone it's deleted. Any remaining error handling in accumulate/serialize uses `Debug`-level (or a rate-limited/once log), never a per-span `Warning`.

## 4. File-by-file change list

**Deleted**
- `FeatureFlags/SpanEnrichmentStore.cs`
- `ClrProfiler/.../OpenFeatureSdkClearSpanEnrichmentIntegration.cs` (+ its `FeatureFlagsSdk.ClearSpanEnrichment` seam) — unless we keep a no-op ABI stub for version compat (decide in review).

**Changed**
- `TraceContext.cs` — add `FeatureFlagEnrichment` field + `GetOrCreateFeatureFlagEnrichment()`.
- `Span.cs` — **remove** the enrichment block from `Finish()`.
- `Agent/MessagePack/SpanMessagePackFormatter.cs` — emit `ffe_*` for root spans with data in `WriteTags`.
- `OpenFeatureSdkAccumulateSpanEnrichmentIntegration.cs` — write to `TraceContext` state instead of the store; gate on settings.
- `SpanEnrichmentState.cs` — unchanged in spirit; keep the per-instance lock (now clearly justified as "concurrent evals under one root"); apply functional fixes (§5).
- `TracerManager.cs` — remove the `SpanEnrichment = new SpanEnrichmentStore(...)` wiring.
- `DatadogProvider.cs` / `SpanEnrichmentHook.cs` — drop store/Clear bridging.

**Unchanged (frozen contract):** `ULeb128Encoder`, tag names, limits (200/10/20/5/64), golden vector `ZAgUAg==`, `Sha256Helper`. Wire output is byte-identical.

## 5. Functional review fixes (folded into the same branch)

Per decision, all done together with the redesign since they touch code that moves:

- **Truncation (`SpanEnrichmentState.cs:236`)** — reviewer questions surrogate-pair handling; current dotnet code already uses a plain `Substring` (fine). Keep plain truncation; drop any "UTF-8 safe" claim in comments.
- **Hand-rolled JSON / helpers (`:118`, `:190`)** — already uses `JsonHelper.SerializeObject`; audit for any remaining bespoke serialization and use built-ins.
- **`AsDictionary()` allocation (`SpanEnrichmentHook.cs:84/116`)** — reviewer's follow-up: it does **not** allocate; initialize the dict with `structure.Count` and `foreach` it (as suggested).
- **Boxing (`:76`)** — pattern-match `context.DefaultValue is Value` (already applied).
- **AI/Node comments (`FeatureFlagsEvaluator.cs:665`, `FeatureFlagsSdk.cs:34/64`)** — strip verbose/AI-authored docs and "Node" references.
- **`internal` vs `public` (`FeatureFlagsSdk.cs:38`)** — keep the ABI seams as required, but ensure nothing is `public` that needn't be.
- **`Split.cs:22/24` backward-compat** — confirm the new field is safe as an RCM interchange type (nullable/optional, tolerant of old/new tracer + package skew); document it.
- **Collection expressions / `HashSet` vs `SortedSet`** — cosmetic C#; apply where the reviewer flagged.

## 6. Testing

- **Reuse the existing L0/L1 suites** (`SpanEnrichmentTests.cs`, `SpanEnrichmentIntegrationTests.cs`) — the wire contract is unchanged, so assertions stand.
- **New/retargeted tests:** state is created lazily on the trace context; two concurrent traces never cross-contaminate (the C scenario, now a positive test); root-span serialization emits `ffe_*`; child-only / partial flush does not tag a child; gate-off emits nothing and allocates nothing.
- **Serialization test** — assert the formatter writes the tags on the root span's serialized payload (not via `SetTag`), exercising the deferred path.

## 7. Stacked-branch / rollout plan

1. Branch `…/ffe-span-enrichment-redesign` off `leo.romanovsky/ffe-apm-span-enrichment`.
2. Commit 1: introduce `TraceContext` state + move accumulate; keep `Finish` drain temporarily (green tests).
3. Commit 2: move emission to `SpanMessagePackFormatter`; remove `Finish` drain.
4. Commit 3: delete `SpanEnrichmentStore` + Clear integration + TracerManager wiring.
5. Commit 4: functional fixes (§5) + comment cleanup.
6. Commit 5: tests.
7. Rebase/retarget the PR onto this once andrewlock signs off on the approach.

## 8. Open questions for the reviewer

1. **State home:** `TraceContext` (preferred) vs `Span`?
2. **Deferred emission:** OK to add the `ffe_*` write into `SpanMessagePackFormatter.WriteTags`, or is there a preferred extension point for computed root-span tags?
3. **ABI stubs:** delete `ClearSpanEnrichment` outright, or keep as a no-op for manual-instrumentation version compatibility?
4. **Cap:** with state-on-span there's no global cap; confirm we don't need a per-trace serial-id ceiling beyond the existing 200/subject limits (we don't think so).

---

### Cross-language note
dd-trace-java (#11658) uses a `TraceInterceptor` on trace completion rather than a central map, and keys by full 128-bit trace hex — so it does not share dotnet's central-store/async-context issues. The Java PR is a separate track; this redesign is dotnet-specific and does not require Java changes.
