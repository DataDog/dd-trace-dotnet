# FFE Span Enrichment — dotnet implementation hand-off (Level 1)

**For:** a fresh implementation session rooted in `dd-trace-dotnet`.
**Branch:** the stacked branch off `leo.romanovsky/ffe-apm-span-enrichment` (already checked out).
**Companion doc:** `FFE_SPAN_ENRICHMENT_REDESIGN.md` (the "why"). This doc is the "how".

## Goal in one line

Replace the central `SpanEnrichmentStore` with **per-trace state stored on `TraceContext`**, keep writing the `ffe_*` tags at `Span.Finish()` (reading the trace's own state instead of the store), delete the store + its cap/leak/cleanup machinery, and apply the small review fixes. **Do NOT touch the serializer** (that's "Level 2", left to the dotnet maintainers).

## Scope

**In scope (Level 1):**
- Move enrichment state onto `TraceContext`.
- Delete `SpanEnrichmentStore` + the 10k cap / eviction / warn.
- Rewire the accumulate integration + `Span.Finish()` to use the new state.
- Remove the global-static store access + the clear/cleanup plumbing.
- All the small functional fixes (see §7).

**Out of scope (do NOT do):**
- `SpanMessagePackFormatter` changes / deferring encoding to the writer thread ("Level 2"). Leave encoding in `Span.Finish()`.

## Working assumptions

Most of the original open questions are settled by **cross-SDK precedent** — the FFE feature already shipped/landed in JS (#8343), Python (#18640), Ruby (#5910), and PHP (#3996). Where the fleet is unanimous, treat it as decided:

- **No serializer deferral ("Level 2") — DECIDED (precedent).** None of JS/Python/Ruby/PHP defer encoding to a background serializer; they all encode at/around span finish. Keep encoding in `Span.Finish()`. Do NOT touch `SpanMessagePackFormatter`.
- **No cap — DECIDED (precedent).** JS/Python/Ruby/PHP all run with no cap (weak-ref GC / request-scoped). State dies with the trace here, so no cap/eviction. Per-eval limits (200/10/20/5/64) stay in the accumulator.
- **State home = `TraceContext` — LOW-RISK assumption.** JS/Python key by the span; Ruby keys by the trace op; `TraceContext` is dotnet's analog of Ruby's trace op, so this is fleet-consistent. If @andrewlock prefers a field on `Span`, only the field location changes — everything else is identical.
- **Cleanup ABI — the one genuinely open call for @andrewlock.** Plan is to delete `ClearSpanEnrichment` + its integration. If he wants a no-op stub kept for manual-instrumentation version compat, keep the method as an empty no-op instead of deleting. Proceed with deletion unless he says otherwise; it's a one-line change to reverse.

## Current code map (read these first)

- `tracer/src/Datadog.Trace/FeatureFlags/SpanEnrichmentStore.cs` — the central store to DELETE.
- `tracer/src/Datadog.Trace/FeatureFlags/SpanEnrichmentState.cs` — per-root accumulator; KEEP (with fixes). Encoding lives in `ToSpanTags()`.
- `tracer/src/Datadog.Trace/FeatureFlags/ULeb128Encoder.cs`, `Util/Sha256Helper.cs`, `FeatureFlags/FeatureFlagMetadataKeys.cs` — frozen codec/keys; KEEP unchanged.
- `tracer/src/Datadog.Trace/Span.cs` — `Finish()` currently drains the store (~line 487); `IsRootSpan`, `RootSpanId`, `Context.TraceContext` available here.
- `tracer/src/Datadog.Trace/TraceContext.cs` — has `RootSpan`; this is where the new state field goes.
- `tracer/src/Datadog.Trace/TracerManager.cs` — constructs `SpanEnrichment = new SpanEnrichmentStore(...)` (~line 112); REMOVE.
- `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/ManualInstrumentation/OpenFeature/OpenFeatureSdkAccumulateSpanEnrichmentIntegration.cs` — resolves `Tracer.Instance.InternalActiveScope?.Span?.Context.TraceContext?.RootSpan` and calls `store.Accumulate(rootSpan.SpanId, …)`; REWIRE to the trace-context state.
- `tracer/src/Datadog.Trace/ClrProfiler/.../OpenFeatureSdkClearSpanEnrichmentIntegration.cs` + `FeatureFlagsSdk.ClearSpanEnrichment` — cleanup seam; DELETE (or no-op per ABI decision).
- `tracer/src/Datadog.FeatureFlags.OpenFeature/SpanEnrichmentHook.cs` — the OpenFeature `finally` hook; the `IDisposable`/store-bridge comment can be trimmed.
- `tracer/src/Datadog.FeatureFlags.OpenFeature/DatadogProvider.cs` — drops the clear/close bridging.

## Target design

### 1. State on `TraceContext`

Add to `TraceContext`:
```csharp
private SpanEnrichmentState? _featureFlagEnrichment;

// Lazily created; null until the first flag eval on this trace, so traces that
// never evaluate a flag pay nothing.
internal SpanEnrichmentState? FeatureFlagEnrichment => _featureFlagEnrichment;

internal SpanEnrichmentState GetOrCreateFeatureFlagEnrichment()
{
    return LazyInitializer.EnsureInitialized(ref _featureFlagEnrichment, static () => new SpanEnrichmentState())!;
}
```
- Confirm the exact lazy/thread-safe idiom against how `TraceContext` initializes its other lazy fields.
- No cap, no dictionary, no keying — one optional state per trace context.

### 2. Accumulate path (integration)

In `OpenFeatureSdkAccumulateSpanEnrichmentIntegration.OnMethodBegin`, replace the store call:
```csharp
var traceContext = Tracer.Instance.InternalActiveScope?.Span?.Context.TraceContext;
if (traceContext is not null && Tracer.Instance.Settings.IsSpanEnrichmentEnabled)
{
    traceContext.GetOrCreateFeatureFlagEnrichment()
                .Accumulate(serialId, doLog, targetingKey, hasVariant, flagKey, value);
}
```
- Move the branch logic (serialId → AddSerialId/AddSubject; missing variant → AddDefault) that currently lives in `SpanEnrichmentStore.Accumulate` **into `SpanEnrichmentState`** as an `Accumulate(...)` method (or keep the individual Add* calls in the integration — pick one, keep it in the state class for testability).
- The gate is a cheap settings bool read here; nothing constructed when off.

### 3. Write path (`Span.Finish`)

Keep writing at finish, but read the trace's own state:
```csharp
if (IsRootSpan)
{
    var enrichment = Context.TraceContext?.FeatureFlagEnrichment;
    if (enrichment is not null && enrichment.HasData())
    {
        foreach (var tag in enrichment.ToSpanTags())
        {
            if (!StringUtil.IsNullOrEmpty(tag.Value)) { Tags.SetTag(tag.Key, tag.Value); }
        }
    }
}
```
- No `GetAndClear`, no store lookup. No explicit removal — the state is GC'd with the `TraceContext`.
- Keep the try/catch so enrichment never breaks span finish. Use `Debug`-level logging, never a per-span `Warning`.
- Drop the gate/`SpanEnrichment.IsEnabled` store check here; presence of non-null state already implies the gate was on.

### 4. Delete

- `SpanEnrichmentStore.cs` (whole file).
- `TracerManager.SpanEnrichment` property + its construction.
- `OpenFeatureSdkClearSpanEnrichmentIntegration.cs` + `FeatureFlagsSdk.ClearSpanEnrichment` (or no-op per ABI decision).
- The store-bridge cleanup in `DatadogProvider.Dispose` / `SpanEnrichmentHook.Dispose` comments.

## 5. Concurrency

`SpanEnrichmentState` keeps its per-instance `lock` — a single trace can accumulate from concurrent async evals (`Task.WhenAll`). That's the only lock left; there is no shared-map contention. `ToSpanTags()` snapshots under the lock then encodes after releasing it (already the case). At `Finish()` the trace is done, so the encode sees no concurrent writers.

## 6. Frozen contract — DO NOT CHANGE

Tag names, ULEB128 encoding, SHA256 hashing, limits (200/10/20/5/64), golden vector `ZAgUAg==`. Output must stay byte-identical. There are existing golden-vector/round-trip tests — they must stay green.

## 7. Functional fixes (from the review)

- **Truncation** (`SpanEnrichmentState.TruncateValue`): keep the plain `Substring`; delete any "UTF-8 safe" comment.
- **`AsDictionary()`** (`SpanEnrichmentHook.ToPlainObject`, structure branch): use `value.AsStructure!.AsDictionary()`, pre-size the dict with `.Count`, `foreach` it (per andrewlock's corrected suggestion).
- **Boxing:** the `context.DefaultValue is Value` pattern-match is already applied — verify it's there.
- **Hand-rolled JSON:** ensure `JsonHelper.SerializeObject` is used throughout; remove any bespoke serialization.
- **Duplicated metadata-key constants:** dedupe `__dd_split_serial_id` / `__dd_do_log` into the shared `FeatureFlagMetadataKeys` and reference it from both the hook and the native path.
- **AI/Node comments:** strip verbose AI-style docs and "Node" references across the FFE files (`FeatureFlagsEvaluator.cs`, `FeatureFlagsSdk.cs`, etc.).
- **`public` → `internal`:** tighten visibility on anything that needn't be public (keep the CallTarget ABI seams as required).
- **`Split` field:** confirm the new field is optional and tolerant of tracer/package version skew; document it; drop if it turns out unused. (This is the value encoded into `ffe_flags_enc`, so it's needed — be ready to say so.)
- **Nits:** collection expressions, `HashSet` vs `SortedSet` where flagged, stray blank lines, the lock-comment wording andrewlock suggested.

## 8. Tests

- Existing `SpanEnrichmentTests.cs` / `SpanEnrichmentIntegrationTests.cs` should stay green (contract unchanged) — retarget any that referenced `SpanEnrichmentStore` to the new `TraceContext` state.
- Add: lazy creation (no eval → no state, no tags); **two concurrent traces don't cross-contaminate** (turns andrewlock's concern into a passing test); tags land on the local root only, not child spans; gate-off produces nothing.
- No serializer test (that's Level 2).

## 9. Suggested commit sequence

1. Add `TraceContext.FeatureFlagEnrichment` + move the branch logic into `SpanEnrichmentState.Accumulate`; rewire the integration; **keep** the store temporarily so tests pass.
2. Point `Span.Finish()` at the trace-context state; remove the store read.
3. Delete `SpanEnrichmentStore` + `TracerManager` wiring + clear integration/bridge.
4. Functional fixes (§7) + comment cleanup.
5. Tests (§8).

## 10. Build & verify (implementer runs these)

```bash
# from dd-trace-dotnet root
dotnet build tracer/src/Datadog.Trace/Datadog.Trace.csproj
# FFE unit tests (adjust the project/filter to the repo's layout)
dotnet test tracer/test/Datadog.Trace.Tests/Datadog.Trace.Tests.csproj --filter "FullyQualifiedName~FeatureFlags"
```
(Confirm exact project paths/targets via the repo's build docs / `tracer/build`. The tracer uses a Nuke build; there may be a preferred `./tracer/build.sh` target for unit tests.)

## 11. Guardrails

- Never let enrichment throw into flag evaluation or span finish (try/catch, Debug logging).
- Don't add per-SDK knobs for the limits.
- Don't touch `SpanMessagePackFormatter`.
- If the `TraceContext` lazy-field or `Span.Finish` change feels like it's touching more core than expected, stop and confirm with @andrewlock rather than pushing through.
