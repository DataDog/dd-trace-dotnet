---
name: Debugger snapshot correlation
overview: "Match Java PR #12452 and system-tests feature 564 (#7425/#7572): one EMIT/DROP per local TraceContext using the first capturing probe's AdaptiveSampler, at most one snapshot per probe, log-only independent. Correlate via existing flat dd.trace_id. Skip generation tokens, a dedicated session sampler, and inventing root runtime_id just to pass a follow-on test."
todos:
  - id: trace-decision-state
    content: "Add a single CoordinatedSamplingState reference on TraceContext (status + probe-id set), published with lock (RootSpan) + Interlocked.CompareExchange like CreateAppSecRequestContext. NOT a bare CAS on an int plus a separate set."
    status: pending
  - id: session-sampler
    content: Reuse first capturing probe's existing AdaptiveSampler + global limiter once per TraceContext (no new session sampler)
    status: pending
  - id: probe-processor
    content: "Port Java trySample: full snapshots coordinate; template logs independent; claim cap at sample time (safe in .NET, see call-site inventory). No ER opt-out code needed. Decide explicitly what happens to condition-error snapshots."
    status: pending
  - id: scope-helper
    content: "Stamp TraceContext at TryBeginProcess (dotnet-specific; Java re-reads active span). Lookup is InternalActiveScope, then .NET Framework WCF CWT. Must NOT null dd.trace_id/dd.span_id for Exception Replay snapshots, which never call TryBeginProcess."
    status: pending
  - id: runtime-id-envelope
    content: Optional same-PR Java parity for debugger.runtimeId; does not satisfy system-tests test_runtime_id_in_envelope as written (looks at runtime_id / dd.runtime_id)
    status: pending
  - id: unit-tests
    content: "Port Java CoordinatedSamplingTest matrix (chain, drop, conditions, loop, siblings, two local roots, log templates, concurrent tryEmit), plus two .NET cases: sampler consulted exactly once per trace under concurrent first hits, and ER snapshots unaffected."
    status: pending
  - id: integration-and-approvals
    content: "Sample with StartActive nested/sibling/loop probes. Approval updates are required regardless of runtimeId: stamping moves dd.trace_id/dd.span_id for async probes. Include an Exception Replay snapshot approval."
    status: pending
  - id: system-tests-gate
    content: "After tracer ships, add .NET weblog Correlation/Loop endpoints (each /debugger/correlation request must take several hundred ms so the run spans many 1s sampler windows), dotnet line map, and flip manifests (depends on system-tests #7572)"
    status: pending
isProject: false
---

# RFC v2: Coordinated Live Debugger sampling (.NET)

This file is the authoritative copy of the implementation plan. The Cursor plan `debugger_snapshot_correlation_a83cb40e` predates the review recorded here; where the two disagree, this file wins.

## Scope

**Implement now (parity with Java [PR #12452](https://github.com/DataDog/dd-trace-java/pull/12452) and system-tests feature 564):**

- One sampling decision per in-process local trace when a Datadog span is active
- Per-probe cap of **1 snapshot per capturing probe per local trace**
- Log-only / template probes stay independent
- Exception Replay must **not** share that decision. In .NET this needs **no production code**: `ExceptionReplayProcessor` is a separate `IProbeProcessor` implementation and never reaches `ProbeProcessor.SamplePayload`, and `ExceptionReplayProbe` already registers `NopAdaptiveSampler`. Java needed commit `6cdb1ce3` only because `ExceptionProbe extends LogProbe`. **But ER does share `DebuggerSnapshotCreator`** — see "Stamp TraceContext at begin" for the one place it can regress.
- Keep existing envelope `dd.trace_id` / `dd.span_id` (no payload change for feature 564)

**Acceptance gate (not in this repo):** [system-tests #7425](https://github.com/DataDog/system-tests/pull/7425) `Test_Debugger_Coordinated_Sampling`, stacked with [#7572](https://github.com/DataDog/system-tests/pull/7572) so flat dotted `dd.trace_id` is read. That is the definition of done for coordinated sampling.

**Optional same PR (Java already has it; not feature 564):** emit `debugger.runtimeId` inside the `debugger` object. Do not invent root `runtime_id` / `dd.runtime_id` just to pass the follow-on `not_reported` test.

**Defer (Java #12452 and system-tests feature 564 also exclude these):**

- Tier 2 invented task IDs
- Gap 2 thread-generation token (`logger.generation`; `test_generation_token`)
- Agent skill/cookbook files
- Java `forceCoordinatedEmit` when a Live Debugger session is `ACTIVE` (propagated `session_id` / `_dd.p.debug`). Dotnet has no `DebugSessionStatus`. Revisit if that lands.
- A dedicated “per-trace rate” API (the #7425 probe comment says “swap it for the per-trace rate once one exists”; Java reused the first probe’s AdaptiveSampler instead)

## Current behavior (the bug)

Today every capturing probe samples independently in `[ProbeProcessor.SamplePayload](tracer/src/Datadog.Trace/Debugger/Expressions/ProbeProcessor.cs)`:

1. Global limiter (`[DebuggerGlobalRateLimiter](tracer/src/Datadog.Trace/Debugger/RateLimiting/DebuggerGlobalRateLimiter.cs)`, default 100/sec, driven by `[ServiceConfiguration.Sampling](tracer/src/Datadog.Trace/Debugger/Configurations/Models/ServiceConfiguration.cs)`)
2. Per-probe `[AdaptiveSampler](tracer/src/Datadog.Trace/Debugger/RateLimiting/AdaptiveSampler.cs)` (1/sec for snapshots, 5000/sec for template logs)

Unconditional probes sample in `TryBeginProcess` **before** capture. Conditional probes skip that and sample **after** expression evaluation. Invokers (`[MethodDebuggerInvoker.SingleProbe](tracer/src/Datadog.Trace/Debugger/Instrumentation/MethodDebuggerInvoker.SingleProbe.cs)`, line/async variants, and the multi-probe loop) all enter through `TryBeginProcess`.

Snapshots already carry `dd.trace_id` / `dd.span_id` from `Tracer.Instance.InternalActiveScope`, and `logger.thread_id` as `Thread.ManagedThreadId`. `runtime_id` exists on diagnostics/status/upload APIs but **not** on the snapshot envelope. There is no Live Debugger “session id” in RCM; a session is just the set of installed probes.

Independent per-probe `Sample()` is why parent/child/sibling snapshots in the same request arrive as a partial chain.

### Verified call-site inventory

`TryBeginProcess` has exactly **four** call sites. This is what makes "claim the cap at sample time" safe:

| Call site | Phase | More than once per probe per invocation? |
|---|---|---|
| `MethodDebuggerInvoker.SingleProbe.cs:99` | method entry, before `EntryStart` | No |
| `AsyncMethodDebuggerInvoker.SingleProbe.cs:175` | first `MoveNext` entry, before `EntryAsync` | No — guarded by `IsReEnter` in `AsyncMethodDebuggerInvoker.MultiProbe.cs:56-60` |
| `LineDebuggerInvoker.cs:201` | `BeginLine` | **Yes** — every line hit (this is exactly what the loop cap is for) |
| `AsyncLineDebuggerInvoker.cs:176` | `BeginLineAsync` | **Yes** — every line hit, including after each resumption |

The multi-probe invokers fan out to the single-probe overload once per probe (`MethodDebuggerInvoker.MultiProbe.cs:44-47`, `AsyncMethodDebuggerInvoker.MultiProbe.cs:64-67`), so N probes on one method make N independent decisions today. `AsyncMethodDebuggerInvokerV2.MultiProbe.BeginMethod` is only a shim forwarding to `AsyncMethodDebuggerInvoker.BeginMethod` (`AsyncMethodDebuggerInvokerV2.MultiProbe.cs:75-79`); the native rewriter requires V2 for async probes (`debugger_method_rewriter.cpp:1803`). `SpanDebuggerInvoker` never touches `IProbeProcessor`, so span probes are out of scope automatically.

Java holds the same exactly-once invariant by a different route: the multi-probe overload `DebuggerContext.isReadyToCapture(Class<?>, int... probeIndices)` deliberately does **not** call `probe.isReadyToCapture()` (it only does the re-entrancy check), which is why `LogProbe.evaluate` skips re-sampling when `singleProbe` is true and calls `sample()` otherwise. Do not port the shape without that detail, or a probe claims its cap at entry and then `tryEmit` returns false on the second call.

## Java reference (source of truth for behavior)

Cross-language behavior is defined by **dd-trace-java** [PR #12452](https://github.com/DataDog/dd-trace-java/pull/12452) — *Add coordinated sampling for snapshot probes* — merged to `master` on 2026-09-14 ([merge commit `f4b1632`](https://github.com/DataDog/dd-trace-java/commit/f4b1632bdbc42ed5ca1953b20aaa12ea5045bf12)). Jira: [DEBUG-5829](https://datadoghq.atlassian.net/browse/DEBUG-5829). Author: Jean-Philippe Bempel (`jpbempel`). Branch: `jpbempel/logpoint-sync`.

### Java files to port from

- Algorithm: [`LogProbe.java`](https://github.com/DataDog/dd-trace-java/blob/master/dd-java-agent/agent-debugger/src/main/java/com/datadog/debugger/probe/LogProbe.java)
- ER opt-out — read for context, **nothing to port** (see Scope): [`ExceptionProbe.java`](https://github.com/DataDog/dd-trace-java/blob/master/dd-java-agent/agent-debugger/src/main/java/com/datadog/debugger/probe/ExceptionProbe.java)
- Global then per-probe: [`ProbeRateLimiter.java`](https://github.com/DataDog/dd-trace-java/blob/master/dd-java-agent/agent-debugger/debugger-bootstrap/src/main/java/datadog/trace/bootstrap/debugger/ProbeRateLimiter.java)
- Test matrix: [`CoordinatedSamplingTest.java`](https://github.com/DataDog/dd-trace-java/blob/master/dd-java-agent/agent-debugger/src/test/java/com/datadog/debugger/agent/CoordinatedSamplingTest.java)
- Concurrent tryEmit: [`LogProbeTest.java`](https://github.com/DataDog/dd-trace-java/blob/master/dd-java-agent/agent-debugger/src/test/java/com/datadog/debugger/probe/LogProbeTest.java)
- Nested / sibling / loop sample: [`CapturedSnapshot21.java`](https://github.com/DataDog/dd-trace-java/blob/master/dd-java-agent/agent-debugger/src/test/resources/com/datadog/debugger/CapturedSnapshot21.java)
- Envelope `debugger.runtimeId` (already on `master`, **not** introduced by #12452): [`JsonSnapshotSerializer.java`](https://github.com/DataDog/dd-trace-java/blob/master/dd-java-agent/agent-debugger/src/main/java/com/datadog/debugger/agent/JsonSnapshotSerializer.java)

PR commits worth reading in order:

1. [`101474cd`](https://github.com/DataDog/dd-trace-java/commit/101474cdda05cc1760dea586d3872d8d4a8976ea) — coordinated sampling on local-root `Context`
2. [`94fbfeb0`](https://github.com/DataDog/dd-trace-java/commit/94fbfeb0540c589f4cf885a1188a6ce443abd956) — review follow-ups
3. [`6cdb1ce3`](https://github.com/DataDog/dd-trace-java/commit/6cdb1ce39a7c2fb718f50b7e9b359699d0447865) — **fix exception probe sampling** (ER must not share snapshot-probe state)

Java algorithm (port this):

```text
trySample(sampler):
  if not full snapshot:          // template log
      return globalLogSampler && probeSampler
  if not useCoordinatedSampling: // ExceptionProbe override
      return globalSnapshotSampler && probeSampler
  localRoot = activeSpan.localRootSpan
  if localRoot is null:
      return globalSnapshotSampler && probeSampler   // independent fallback
  state = localRoot.context[debugger_sampling]
  if state is null:
      synchronized (localRoot):
          create state from (globalSnapshot && probeSampler) ? EMIT : DROP
          attach to localRoot
  return state.tryEmit(probeEncodedId)   // EMIT && set.add(id)
```

First capturing probe’s **own** AdaptiveSampler decides the local root. There is **no** extra session sampler. Later capturing probes skip `tryProbe` and only `tryEmit`.

Do **not** add a dedicated 1/sec session `AdaptiveSampler`. Do **not** use OTel `TraceIdRatioBased`.

## System-tests stack (#7425 / #7572)

The cross-language gate lives in **DataDog/system-tests**, not in this repo. Stack (both still open; no further stacked PRs):

- [PR #7425](https://github.com/DataDog/system-tests/pull/7425) `grantseltzer/correlation-tests-go` on `main` — feature 564, Go weblog, manifests, budgets loop test
- [PR #7572](https://github.com/DataDog/system-tests/pull/7572) `pi/accept-dotted-trace-id` on #7425 — one-line lookup so Java/.NET/Go/Ruby dotted keys work

Feature 564 (`debugger_snapshot_correlation` in `utils/_features.py`):

> The sampling decision is made once per trace and applies to every probe in that trace, with snapshots correlated by the `dd.trace_id` already carried in the snapshot envelope (**no snapshot payload change**). Runtime-id-in-envelope and the execution-context generation token are separate follow-on work and are **not** part of this feature.

### Gate: `Test_Debugger_Coordinated_Sampling`

File: `tests/debugger/test_debugger_snapshot_correlation.py`. Scenario: `debugger_probes_snapshot`.

- Install three capturing **method** probes from `probe_snapshot_log_correlation.json`: `Correlation`, `CorrelationMiddle`, `CorrelationLeaf`
- Force `sampling.snapshotsPerSecond = 1` on each probe (pressure, not a new per-trace rate)
- Hit `/debugger/correlation` **30 times** (each request is one local trace)
- Group snapshots by `dd.trace_id`
- Assert **no partial chains** and **at least one full chain** of all three probe ids

Go weblog (the only language #7425 implements) sleeps **400ms** in `correlation` and in `correlationMiddle`, *after* each nested call returns, so one request takes ~800ms. System-tests rewrites `ACTUAL_TYPE_NAME` method probes to `evaluateAt: EXIT` and maps the dotnet typeName to `weblog.DebuggerController` (`tests/debugger/utils.py`, already on `main`).

**The sleeps are the discriminator — but not because they space the probes relative to each other.** What makes the gate discriminating is elapsed time across the 30 requests:

- `AdaptiveSampler.Sample()` is probabilistic (`NextDouble() < _probability`). It starts at `_probability = 1` with `_samplesBudget = samplesPerWindow + BudgetLookback * samplesPerWindow` = `1 + 16*1` = 17, and rolls its window only once per second.
- Each of the three probes is hit once per request, so each sampler sees 30 hits. If those finish inside the **first one-second window**, `_probability` is still 1 (no randomness yet) and each sampler emits on its first 17 hits and drops the remaining 13 — **identically across probes**, because all three see the same hit sequence. Result: 17 full chains, zero partial chains, and the gate **passes with no coordination at all**.
- Go's ~800ms per request stretches the run over ~24 windows, `_probability` collapses below 1, and three independent RNG draws then diverge into partial chains.

Consequence for .NET: **the requirement is that each `/debugger/correlation` request take several hundred milliseconds**, not that the probed sites be spaced relative to one another. .NET samples unconditional probes in `TryBeginProcess` at method **entry** regardless of `evaluateAt`, so the `EXIT` rewrite does not move .NET's sampling point and copying Go's sleep *placement* would space nothing.

#7425 reads only the nested form `snapshot.get("dd", {}).get("trace_id")`. .NET already writes a **flat dotted** key `"dd.trace_id"`. Without #7572 the gate fails the presence assert before any sampling logic. Do **not** nest a `dd` object to satisfy #7425 as landed. Depend on #7572 before flipping the .NET manifest.

### Follow-ons in the same #7425 file (not feature 564)

`Test_Debugger_Snapshot_Correlation_Fields` is `@features.not_reported`:

- `test_runtime_id_in_envelope` looks at root `runtime_id` **or** nested `dd.runtime_id`. It does **not** look at Java’s `debugger.runtimeId`. Emitting `debugger.runtimeId` for Java parity still fails this test as written.
- `test_generation_token` wants `logger.generation` beside `thread_id`. Matches RFC Gap 2. Stay deferred.

### Loop cap: `Test_Debugger_Probe_Budgets::test_per_span_budget`

Also added in #7425 (all-language `missing_feature`, including Java). Hits `/debugger/correlation/loop/3` with two line probes (`CorrelationLoopBody` inside a 1s-per-iteration loop, `CorrelationLoopSibling` after the loop). Expects **exactly 1 snapshot per probe** for that one request. Line map currently has **golang only**; .NET must add `dotnet` line numbers after the weblog methods exist.

### .NET weblog work (system-tests PR after the tracer ships)

#7425 did **not** add .NET endpoints. Follow Go’s shape in a later system-tests PR:

- `GET /debugger/correlation` → `Correlation()` → `CorrelationMiddle()` → `CorrelationLeaf()` (method names stay PascalCase; `utils.py` already maps the dotnet type to `weblog.DebuggerController`). Keep Go's **400ms** delays so each request takes ~800ms — what matters is the **per-request duration**, not where the sleeps sit, because .NET samples at method entry (see the discriminator note above)
- `GET /debugger/correlation/loop/{count}` → loop body line + sibling after the loop, **1s** per iteration
- Add `dotnet` entries for `CorrelationLoopBody` / `CorrelationLoopSibling` in `method_and_language_to_line_number`
- After a released tracer version: flip `manifests/dotnet.yml` for `Test_Debugger_Coordinated_Sampling` and `test_per_span_budget`. Leave the follow-on field tests until they match what the tests read.

## Recommended design

Use **`TraceContext` as the local-root execution unit**. It is already allocated at unit-of-work boundaries and flows through `AsyncLocal`.

### Decision storage (no leak, no extra dictionary)

Store **one `CoordinatedSamplingState` reference** on `[TraceContext](tracer/src/Datadog.Trace/TraceContext.cs)` (status + probe-id set), published atomically. Do **not** use a bare `Interlocked.CompareExchange` on an `int` plus a separately-created set — that has two races:

- **Burned sampler tokens.** `SamplePayload()` has side effects: it consumes a `DebuggerGlobalRateLimiter` token *and* a per-probe `AdaptiveSampler` token. With a bare CAS, two concurrent probes both call it and the CAS loser's tokens are gone. Java avoids this with `synchronized (localRootSpan)` and asserts the contract in `CoordinatedSamplingTest.coordinatedSamplingIsScopedToLocalRootSpan` (`probeSampler.getCallCount() == 2` for two traces × three probes).
- **Torn publication.** Writing the status and then creating the set is two publications; a racing reader can observe EMIT with a null set.

`TraceContext` has two different lazy-init precedents and only one of them applies here. `EnableIastInRequest` and `GetOrCreateFeatureFlagEnrichment` use a bare CAS **because `new()` is side-effect-free**. Sampling is not, so follow `CreateAppSecRequestContext` instead: `lock (rootSpan)` around the decision, then `Interlocked.CompareExchange` to publish. Also handle `RootSpan == null` the way `CreateAppSecRequestContext` does, or use a dedicated lock object — `lock (_rootSpan!)` would throw if a probe ever fires before the first `AddSpan`.

Probe-id set allocated **only on EMIT**; DROP carries no set. The state dies with the `TraceContext`, so there is nothing to clean up.

### Who participates

Coordinate only **capturing** probes (`ProbeType.Snapshot` / log probes with `CaptureExpressions`). Leave template logs, metric/span decoration, and Exception Replay unchanged.

### Sampler vs global limiter (match Java `tryProbe`)

No new session sampler. The **first capturing probe that actually samples** runs today’s `SamplePayload` once and caches EMIT/DROP on `TraceContext`. Later capturing probes only `tryEmit(probeId)`.

### Stamp TraceContext at begin; do not re-read at finalize

Resolve `TraceContext` once in `TryBeginProcess`, store it on `DebuggerSnapshotCreator`, and use that object for EMIT/DROP, the per-probe cap, and envelope `dd.trace_id` / `dd.span_id` at finalize. Lookup: `InternalActiveScope`, then .NET Framework WCF CWT.

**This is a payload behavior change, not a no-op.** Today `FinalizeSnapshot` reads `Tracer.Instance.InternalActiveScope` at finalize time (`DebuggerSnapshotCreator.cs:953-959`). Stamping at begin changes the emitted ids wherever the active scope differs between entry and exit — most visibly async probes, whose finalize runs on the last `MoveNext` continuation. It is arguably the fix that makes correlation trustworthy, but feature 564 promises "no snapshot payload change", so call it out in the PR description and expect to update existing snapshot approvals.

**Exception Replay hazard (must not regress).** `ExceptionReplaySnapshotCreator : DebuggerSnapshotCreator` is constructed directly in `TrackedStackFrameNode.CreateSnapshot()` (`TrackedStackFrameNode.cs:121`, `:136`) and **never goes through `TryBeginProcess`**, yet it calls `FinalizeMethodSnapshot` (`:160`) which funnels into the same `FinalizeSnapshot`. If `FinalizeSnapshot` reads only a stamped field, **every ER snapshot gets null `dd.trace_id` / `dd.span_id`** — a silent correlation regression in a shipping product that no coordinated-sampling test would catch. Two acceptable fixes:

1. Keep a fallback in `FinalizeSnapshot`: use the stamped context when present, else `Tracer.Instance.InternalActiveScope`.
2. Stamp in the `DebuggerSnapshotCreator` base constructor so every entry path gets it.

Prefer (2) — it removes the "did this path stamp?" question entirely. (1) is the smaller diff.

**Java parity note.** Java coordinates on the local root span but keeps reading the *active* span for the envelope. Stamping both `dd.trace_id` and `dd.span_id` from the entry context is therefore a deliberate .NET deviation, not just an implementation detail.

## Known consequences (state these in the PR description)

These follow from the design and from Java parity. None of them block the gate; all of them will come up in review.

1. **The global limiter stops meaning what it says.** Only the first capturing probe per trace consults `DebuggerGlobalRateLimiter`, so its 100/sec ceiling now counts *traces* — strictly, one token per trace in which a capturing probe reaches the sampler. With N capturing probes the snapshot rate can reach N × 100/sec. Java has the identical property. Moving the limiter to emit time would reintroduce partial chains, so the answer is "accepted, and the default may need to drop".
2. **Long-lived traces go silent.** One decision plus one emit per probe for the whole life of a `TraceContext` means a long-running root span (background service, batch consumer, SignalR / long-poll request) emits at most one snapshot per probe *ever*, where today's time-based limiter keeps producing. This is the RFC's stated design and Java has no window-based reset, but record it as an accepted consequence rather than discovering it from a support ticket.
3. **Conditional probes still pay full evaluation cost on a dropped trace.** The RFC promises that dropped probes "cost essentially nothing", but coordination for `HasCondition` probes can only happen *after* expression evaluation, which is where .NET samples. Java has the same gap. An early return in `TryBeginProcess` on an already-DROP trace would close it — but it would also suppress condition-error diagnostic snapshots on dropped traces, which is a product call. Decide explicitly.
4. **Condition-error snapshots bypass coordination.** In .NET the error path never reaches `SamplePayload`; it uses a hard one-per-5-minutes cap (`ShouldSampleEvaluationErrorSnapshot`, `ProbeProcessor.cs:430-445`). So error snapshots also bypass the per-probe cap unless changed. Recommended: leave them uncoordinated — they are diagnostics, and Java routes them to a separate `errorSampler` — and say so in the PR.
5. **`dd.trace_id` on the wire is a 64-bit projection.** `FinalizeSnapshot` truncates to `TraceId128.Lower`, with an open TODO in the code. Harmless for the gate, since grouping happens within a single service, but worth knowing given that the whole premise is correlating on `dd.trace_id`.

## Envelope: Gap 1 (optional; not feature 564)

Feature 564 requires **no snapshot payload change**. If we add `debugger.runtimeId` for Java parity, do not invent root `runtime_id` / `dd.runtime_id` to chase `test_runtime_id_in_envelope`.

## Implementation sketch (files)

- `TraceContext.cs` — single `CoordinatedSamplingState` reference (status + emit set); `TryGetOrCreateDecision` + `TryEmit(probeId)`, published under `lock (RootSpan)` + CAS
- `ProbeProcessor.cs` — port `trySample`
- Helper for the `InternalActiveScope` + WCF CWT lookup
- `DebuggerSnapshotCreator.cs` — stamped context for `dd.trace_id` / `dd.span_id`, **with a fallback (or base-constructor stamping) so Exception Replay keeps its ids**; optionally `debugger.runtimeId`
- No native/IL changes. No new session sampler type. No invoker signature changes (confirmed against the call-site inventory above).

## Tests

Port the Java `CoordinatedSamplingTest` matrix, plus two .NET-specific cases:

- the sampler is consulted **exactly once per trace** under concurrent first hits (the Java `getCallCount()` assertion — this is what catches a bare CAS)
- **ER snapshots still carry `dd.trace_id` / `dd.span_id`**

Local integration must reproduce the real discriminator: **repeat the invocation across many 1-second sampler windows**, each probe at 1 snapshot/sec. A tight loop of the same three probes inside one window is **not** enough — `_probability` starts at 1 with a budget of 17, so everything emits and the test passes vacuously. Spacing the probes *within* one invocation does not help either, because .NET samples all of them at method entry.

## Review gates (after implementation)

- `async-concurrency-review` on the `TraceContext` lock + CAS publication and the emit-set
- `datadog-dd-trace-dotnet-pr-preflight` on `TryBeginProcess`
- `datadog-debugger-di-playbook`
- System-tests: wait for [#7572](https://github.com/DataDog/system-tests/pull/7572); add weblog endpoints + line map; flip `manifests/dotnet.yml`
