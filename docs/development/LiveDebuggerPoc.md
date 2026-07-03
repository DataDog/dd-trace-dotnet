# Live Debugger POC Work Log

> Start with [`FlowRecorderHandoff.md`](FlowRecorderHandoff.md) before using this work log. The handoff page explains the current source-of-truth hierarchy, key files, validation commands, product direction, and how older plans/transcripts should be treated.

## Current Branch

- Branch: `innovation/live-flow-recorder-poc`
- Goal: prove a local-only Flow Recorder product direction using operation-scoped call-flow capture. Broad instrument-all remains a stress benchmark; the product path is an armed recorder operation with bounded capture, optional trace correlation, method filters, budgets, and viewer-visible truncation/suppression.

## Current Status

Implemented a managed recorder slice plus native recorder-only rewrite slices for non-async methods and async `MoveNext` methods:

- `FlowRecorder` exposes lightweight `Enter(int methodMetadataIndex)`, `EnterAsyncStep(int methodMetadataIndex, ref long operationId, ref long generation)`, `RecordAsyncEdge(int methodMetadataIndex, ref long childOperationId, ref long childGeneration)`, `EnterDetached(int methodMetadataIndex)`, and `Exit(ref FlowRecorderState, Exception?)` callbacks for native IL injection.
- Events are fixed numeric records: kind, timestamp, method metadata index, flow id, frame id, parent frame id, depth, managed thread id, exception type id, and recorder operation id. Trace/span correlation is no longer stored per event; it is captured once into the operation metadata section.
- Recorder operation context is independent from tracing and is the primary capture gate. Trace/span context is optional correlation captured when present.
- Coarse recorder operation context can flow across awaits, but frame context is stored in thread-static primitive state for synchronous nesting; async logical identity is stored on state-machine instances.
- Events are stored in a preallocated bounded in-memory buffer with lock-free append and dropped-event counting.
- Flush writes a compact local `.dflp` binary file. No backend upload.
- A `net10.0` console sample manually calls the recorder callbacks.
- A `net10.0` viewer reads `.dflp` files and renders a simple call tree/timeline with method names when recorder metadata is present.
- The viewer also renders a POC-only async logical summary by grouping generated `<Method>d__*.MoveNext` frames by original async method name and stable async operation flow id.
- Async parent/child causality is recorded as lightweight `AsyncEdge` events when a child async state-machine operation id is first assigned while another async operation is active on the same thread.
- Native instrument-all selection can now create recorder-only probes when `DD_INTERNAL_DEBUGGER_FLOW_RECORDER_ENABLED` is enabled.
- Instrument-all recording keeps hard excludes for framework/Datadog/generated/helper methods and has a high method budget guard (`DD_INTERNAL_DEBUGGER_FLOW_RECORDER_MAX_METHODS`, default `10000`) to stop accidental runaway ReJIT requests.
- Recorder-only rewriting uses cold method metadata from the native `.methods` sidecar plus hot-path `FlowRecorder.Enter(instrumentedMethodIndex)` / `FlowRecorder.Exit(ref state, exception)` callbacks. It no longer emits a per-invocation `FlowRecorder.RegisterMethod(...)` callback.
- Benchmark-only native rewrite modes can also be selected with `DD_INTERNAL_DEBUGGER_FLOW_RECORDER_REWRITE_MODE`:
  - default/empty: current exception-aware enter/exit rewrite;
  - `minimal-finally`: removes original-method catch/rethrow and calls `Exit(ref state, null)` from a minimal finally;
  - `entry-only`: lower-bound mode that emits detached enter only, with no exit/finally wrapping and no thread-static frame publication.
- Regular debugger method probes are no longer used as the recorder transport and keep their existing `MethodDebuggerInvoker` behavior/cost.
- Native async `MoveNext` rewriting records each state-machine step with a state-machine-instance operation id, so resumptions of the same async invocation share a flow id without flowing recorder frame context through `ExecutionContext`.
- Optional value capture is shallow, bounded, and opt-in per armed method. Primitive value types use `typeof(T)` fast paths to avoid boxing in common cases; complex reference types currently degrade to summaries.

## Native Slice

The native POC slice adds:

- `DD_INTERNAL_DEBUGGER_FLOW_RECORDER_ENABLED` support in the debugger environment helper.
- `DebuggerTokens` references for `Datadog.Trace.Debugger.LiveDebuggerPoc.FlowRecorder`, `FlowRecorderState`, `FlowRecorder.Enter(int)`, `FlowRecorder.EnterFast(int)`, `FlowRecorder.EnterAsyncStep(...)`, `FlowRecorder.EnterAsyncStepFast(...)`, `FlowRecorder.EnterDetached(int)`, `FlowRecorder.Exit(ref FlowRecorderState, Exception)`, `FlowRecorder.ExitFast(ref FlowRecorderState)`, and the shallow value-capture callbacks.
- `FlowRecorderProbeDefinition`, deriving from `MethodProbeDefinition`, so existing method matching/ReJIT can be reused while the rewriter classifies recorder probes before normal method probes.
- Instrument-all POC creation in `DebuggerProbesInstrumentationRequester::PerformInstrumentAllIfNeeded` creates `FlowRecorderProbeDefinition` when the recorder flag is enabled.
- Recorder-only injection in `DebuggerMethodRewriter::ApplyFlowRecorderProbe`:
  - appends only the locals recorder-only rewriting needs: return value for non-void methods, `Exception`, and `FlowRecorderState`;
  - uses trailing `FlowRecorderState` as the recorder duplicate-rewrite sentinel instead of relying on the final CallTarget state local;
  - initializes `Exception` to `null` and `FlowRecorderState` to default before injected calls run;
  - uses method metadata indices assigned during instrumentation; display names are written cold to `<capture>.methods` and merged during managed flush;
  - isolates `FlowRecorder.Enter`/`FlowRecorder.EnterAsyncStep` in its own catch-swallowing protected block;
  - wraps original method execution so exceptions are stored and rethrown;
  - runs `FlowRecorder.Exit(ref state, exception)` from the finally path for normal and thrown exits;
  - isolates `FlowRecorder.Exit` failures so they do not affect customer code.
- `DebuggerMethodRewriter::ApplyMethodProbe` no longer emits recorder calls, so regular method probes keep the existing `MethodDebuggerInvoker`, capture, return wrapper, and dispose behavior unchanged.
- Recorder probes are rejected if mixed with regular debugger probes on the same rewrite in this POC.
- Instrument-all POC recording skips known framework/Datadog assemblies, generated/helper methods, low-value special-name methods, compiler helpers, async kickoff stubs, task-like kickoff stubs, and async `Main` orchestration methods. It also stops creating recorder probes after `DD_INTERNAL_DEBUGGER_FLOW_RECORDER_MAX_METHODS` methods; unset, invalid, or non-positive values use the default budget of `10000`.
- Runtime capture is gated by an active recorder operation by default. Set `DD_INTERNAL_DEBUGGER_FLOW_RECORDER_ALLOW_RECORDING_WITHOUT_OPERATION=true` only for broad stress investigations, not for product benchmarks or normal POC runs.
- Operation budgets enforce event, depth, duration, and unique-method limits. Budget hits emit explicit `Truncated` or `Suppressed` marker events so the viewer can explain missing flow.

## Concurrency Fixes Already Applied

Reviews found recorder lifecycle risks, all fixed in the current branch:

- Broad synchronous frame context uses thread-static primitive state, so normal `Enter`/`Exit` no longer allocates per-frame objects or mutates `AsyncLocal`/`ExecutionContext` state.
- Recorder operation context is coarse-grained and flows across awaits only while the operation is armed. Disposing the operation clears the active gate before flush/drain, so later callbacks return before enqueue work.
- Recorder state carries a generation id, so exits from a stale reset/reconfigure session do not write into the new sink.
- Recorder runtime state is published as an immutable `{generation, settings, sink}` session snapshot, so `Enter`/`Exit` enqueue to a coherent sink/session boundary.
- `Reset` and test reconfiguration reset id counters before publishing the new session, so racing callbacks cannot reuse ids inside the new generation.
- `Flush`, `Reset`, test drain, and test reconfiguration share the recorder lifecycle lock to avoid racing sink swaps.
- `FlowRecorder.Enter` publishes the new thread-static frame context only after state creation and enter-event enqueue succeed. If native swallows an `Enter` exception, the previous frame context is preserved instead of leaking an unbalanced frame.
- Async operation parent context is thread-static and generation-scoped. `Reset` or test reconfiguration on one thread cannot leave stale async parent ids behind on worker threads for a later recorder generation.

## Verification

Focused unit tests:

```powershell
dotnet test .\tracer\test\Datadog.Trace.Tests\Datadog.Trace.Tests.csproj -c Release -f net6.0 --filter "FullyQualifiedName~LiveDebuggerPoc"
```

Latest local result (`2026-07-02`):

- 34 passed, 0 failed, 0 skipped.
- Includes `DetachedEnter_DoesNotFlowAsAsyncParent`, direct `EnterAsyncStep` operation-id/generation coverage, recorder operation context, operation context across awaits, stale captured operation rejection, child-context dispose repair, thread-safe operation dispose, operation stop behavior, inactive gate behavior, no per-frame `AsyncLocal` continuation behavior, `.dflp` method metadata/value/operation-section round-trip coverage, exception details, budget truncation/suppression markers, bounded-buffer drops, and primitive value capture.

Native build check:

```powershell
.\tracer\build.cmd CompileManagedLoader --BuildConfiguration Release
.\tracer\build.cmd CompileTracerNativeSrc --TargetPlatform x64 --BuildConfiguration Release
.\tracer\build.cmd BuildTracerHome --TargetPlatform x64 --BuildConfiguration Release
```

Latest result:

- `CompileManagedLoader` passed.
- `CompileTracerNativeSrc --TargetPlatform x64 --BuildConfiguration Release` passed.
- `BuildTracerHome --TargetPlatform x64 --BuildConfiguration Release` passed.

Sample builds:

```powershell
dotnet build .\tracer\test\test-applications\debugger\Samples.LiveDebuggerPoc.Console\Samples.LiveDebuggerPoc.Console.csproj -c Release -f net10.0
dotnet build .\tracer\test\test-applications\debugger\Samples.LiveDebuggerPoc.Viewer\Samples.LiveDebuggerPoc.Viewer.csproj -c Release -f net10.0
```

Latest result:

- both builds passed with 0 warnings.

Manual sample loop:

```powershell
$capture = ".\artifacts\tmp\live-debugger-poc\flow-events-checkout.dflp"
$env:DD_INTERNAL_DEBUGGER_FLOW_RECORDER_ENABLED = "true"
$env:DD_INTERNAL_DEBUGGER_FLOW_RECORDER_OUTPUT_PATH = $capture
dotnet run --project .\tracer\test\test-applications\debugger\Samples.LiveDebuggerPoc.Console\Samples.LiveDebuggerPoc.Console.csproj -c Release -f net10.0 -- --scenario checkout --recording manual --output $capture
dotnet run --project .\tracer\test\test-applications\debugger\Samples.LiveDebuggerPoc.Viewer\Samples.LiveDebuggerPoc.Viewer.csproj -c Release -f net10.0 -- $capture
```

For native recorder-only validation, run the same sample with `--recording native` under a built monitoring home and profiler environment (`DD_INTERNAL_DEBUGGER_INSTRUMENT_ALL=true`, `DD_INTERNAL_DEBUGGER_FLOW_RECORDER_ENABLED=true`). Native mode disables the sample's manual `FlowRecorder.Enter`/`Exit` calls so `.dflp` events prove the native rewrite path.

Demo scenarios:

- `presentation`: full happy-path checkout narrative with eligibility, quote, inventory, payment, and receipt async branches. This is the primary walkthrough because it shows operation-scoped capture, async edges, timings, and trace/span correlation without an exception.
- `exception`: compact payment-decline failure path. The sample catches the demo exception so the process exits successfully, but marks the active scenario span with `SetException(ex)` so a running Datadog Agent can send an errored trace for APM/Error Tracking correlation.
- `async`: minimal async-shape proof. This is the clearest fallback slide when explaining await continuations and logical async edges without the checkout domain noise.

For a presentation validation run, prefer `.\tracer\test\test-applications\debugger\Samples.LiveDebuggerPoc.Console\run-poc.ps1 -Recording native -RootMode traced -Scenario <scenario>`. The script enables tracing and defaults `DD_TRACE_AGENT_URL` to `http://127.0.0.1:8126`, `DD_SERVICE` to `live-debugger-flow-recorder-poc`, `DD_ENV` to `local-poc`, and `DD_VERSION` to `flow-recorder-poc`. A Datadog Agent must actually be listening on that intake to verify backend APM/Error Tracking ingestion; without it, the local capture and viewer still prove trace/span ids are stamped, but Datadog UI links cannot resolve to uploaded traces.

Local Agent handoff: this machine has a Docker Desktop Agent harness under `C:\dev\my-dd`. Do not copy secrets into this repo; the helper reads `DD_API_KEY`, `DD_SITE`, and ports from `C:\dev\my-dd\.env`. Start and verify the Agent with:

```powershell
cd C:\dev\my-dd
.\stop-agents.ps1
.\start-agents.ps1
.\test-dd.ps1
```

`C:\dev\my-dd\docker-compose.yml` maps the latest Agent intake to `127.0.0.1:${DD_APM_PORT}:8126`; check `.env` or the `test-dd.ps1` output for the actual host port before running the POC. If it is not `8126`, set `DD_TRACE_AGENT_URL` in the same shell before invoking `run-poc.ps1`, for example `http://127.0.0.1:<DD_APM_PORT>`. `C:\dev\agentless-debugger-smoke` is useful for agentless debugger/Exception Replay smoke work, but the native flow-recorder presentation path needs the Agent harness when validating APM trace and Error Tracking correlation.

Backend validation: after generating the viewer report, copy the trace id shown in the operation header and use the Datadog MCP `user-datadog` tools to verify ingestion. Load `datadog/traces` first, then call `get_datadog_trace` with `trace_id` and `only_service_entry_spans=true`. A successful response returns `trace_deep_link_url`; for the `exception` scenario it should also show `status: error`, `error.*` metadata, and usually Error Tracking `issue` metadata. This avoids requiring `DD_APPLICATION_KEY` in the local shell.

Continuous Profiler correlation is currently indirect through normal tracer/profiler context: with `DD_PROFILING_ENABLED=true` and code hotspots enabled (`DD_PROFILING_CODEHOTSPOTS_ENABLED` defaults to true), the tracer writes local-root-span/span ids to the profiler context tracker. Flow Recorder operation metadata stamps trace id, root span id, and active span id, so the same traced scenario can be compared with APM Code Hotspots/profile views for that span/resource. The POC does not currently write profiler sample ids, profile ids, or direct profiler deep-links into the `.dflp` capture.

Latest native validation:

- Manual profiler-driven runs write native recorder events with 0 dropped events in the focused sample/integration-test paths.
- Viewer output showed balanced enter/exit frames and no manual sample method ids (`100`-`104`), proving the events came from native instrumentation rather than manual callbacks.
- Native logs for the focused integration test showed `Applying 0 method probes, 0 line probes and 0 span probes and 1 flow recorder probes` followed by `Applying Non-Async Flow Recorder instrumentation with 1 probes`; the same logs had no `MethodDebuggerInvoker` matches.

Focused native integration test:

```powershell
dotnet test .\tracer\test\Datadog.Trace.ClrProfiler.IntegrationTests\Datadog.Trace.ClrProfiler.IntegrationTests.csproj -c Release -f net10.0 --logger "console;verbosity=minimal" --filter "FullyQualifiedName~LiveDebuggerPocNativeTests"
```

Latest local result (`2026-07-02`):

- 6 passed, 0 failed, 0 skipped.
- Includes non-async native recorder-only frames, async `MoveNext` flow grouping, cold native method-name assertions, fast rewrite balanced events, viewer async logical-summary assertions, fault-injection swallowing for `Enter`/`Exit`, exception details, and armed value capture.

Example viewer output:

```text
Flow 1 (0.077 ms, 4 events)
  - Samples.LiveDebuggerPoc.Console.Program+<AsyncValueAsync>d__7.MoveNext (#17) frame=1 duration=0.044ms
  - Samples.LiveDebuggerPoc.Console.Program+<AsyncValueAsync>d__7.MoveNext (#17) frame=2 duration=0.033ms

Async logical operations
  - Samples.LiveDebuggerPoc.Console.Program.AsyncValueAsync flow=1 2 steps duration=0.077ms
```

## Method Metadata And Operation Context Slice

Native recorder-only instrumentation no longer emits the hot `FlowRecorder.RegisterMethod(int, RuntimeMethodHandle, RuntimeTypeHandle)` callback. Instead, instrument-all selection assigns the same `methodMetadataIndex` cold and writes a sidecar beside the capture file (`<capture>.methods`). Managed flush merges that sidecar into the `.dflp` method table, so fast mode keeps method display names without per-invocation method registration.

The recorder operation context is the primary product gate. `FlowRecorder.StartOperation(triggerReason, root, trace ids...)` creates an operation id/generation, records trigger/root metadata, optionally attaches active Datadog trace/span ids, and restores any previous operation on dispose. The operation context is intentionally coarse and may flow across awaits; per-frame nesting still stays thread-static/state-machine-local. Callbacks first check a cheap active-operation counter, then read the current operation only when at least one operation is armed. This keeps inactive callbacks as early no-ops and lets async continuations inside the selected operation keep the same recorder operation id.

Operation lifecycle is now explicit: arm/start, accept events, dispose to stop accepting new enters, allow already-admitted frame states to emit their balancing exit/exception/value callbacks, then drain/flush. Disposing an operation decrements the active-operation gate and clears/restores the coarse operation context before `Flush` drains the buffers. `FlowRecorderState` carries the operation id captured at enter time, so exit/value/exception callbacks do not need to rediscover operation identity. `DD_INTERNAL_DEBUGGER_FLOW_RECORDER_ALLOW_RECORDING_WITHOUT_OPERATION=true` remains only for broad stress investigations.

The `.dflp` binary format is now version 6. Version 2 added the optional `{methodMetadataIndex, displayName}` section after fixed flow events. Version 3 added `AsyncEdge`, where `FlowId` is the parent async operation id and `FrameId` is the child async operation id. Version 4 added string/type tables, exception details, and captured values. Version 5 added per-event recorder operation id and an operation metadata section with trigger reason, root, start timestamp, and optional trace/span ids. Version 6 removed the per-event trace/span id fields (correlation now lives only in the operation metadata section) and switched string/type table entries to a length-prefixed UTF-8 encoding. The managed reader and sample viewer still accept older captures (v1-v5) and fall back to `method#<id>` when metadata is absent.

## Async MoveNext Slice

The native recorder-only path now accepts async state-machine `MoveNext` methods. It records each `MoveNext` invocation as a physical step, but it also stores a recorder-only operation identity directly on the compiler-generated state-machine instance. That keeps the POC out of `AsyncMethodDebuggerInvokerV2`, task-result discovery, probe metadata arrays, and normal debugger async capture cost.

During module metadata preparation, native instrumentation adds two private `long` fields to non-generic async state-machine types:

- `<>dd_flowRecorder_operationId`
- `<>dd_flowRecorder_generation`

Async `MoveNext` loads those field addresses and calls `FlowRecorder.EnterAsyncStep(int, ref long operationId, ref long generation)`. The managed callback lazily assigns a flow id on the first step and reuses it on later resumptions while the recorder generation matches. If another async operation is already active on the same physical thread when a new child operation id is assigned, the callback also emits an `AsyncEdge` event from the parent operation id to the new child operation id. If the generation changes after `Reset` or test reconfiguration, the next step gets a fresh operation id. The callback does not publish normal frame context through `AsyncLocal`/`ExecutionContext`, so await continuations cannot capture a stale in-progress state-machine step as their parent frame.

If the async operation fields are missing, for example on currently skipped generic state machines, the native rewrite falls back to `FlowRecorder.EnterDetached(int)`. That preserves balanced physical-step recording without precise per-invocation grouping for unsupported state-machine shapes.

The instrument-all POC skips original async kickoff methods and records their generated `MoveNext` methods instead. It also skips async `Main` orchestration methods and generated `<Main>.MoveNext` methods. Recording kickoff stubs with the normal flowing enter callback can leak a stale parent into the first awaited continuation, and the console sample flushes before its async entry-point state machine has fully returned.

The focused native integration test runs the console sample's `--scenario async --recording native` path and verifies balanced native events with no manual sample ids (`100`-`106`) and no temporally invalid parent/child nesting. It also checks that at least one logical async method has multiple `MoveNext` enter events in the same `FlowId`, proving the state-machine operation field is used across resumptions, and that async edges reference recorded parent and child operation ids.

## Async Reconstruction Slice

The viewer now adds an `Async logical operations` section. It scans method metadata for compiler-generated state-machine names like `Samples.LiveDebuggerPoc.Console.Program+<AsyncValueAsync>d__7.MoveNext`, groups the matching `MoveNext` frames by original method name and `FlowId`, and prints a logical summary such as `Samples.LiveDebuggerPoc.Console.Program.AsyncValueAsync flow=1 2 steps duration=...ms`. It also prints `AsyncEdge` parent-to-child operation links before the operation summaries.

This reconstruction is intentionally viewer-side. The native recorder still records physical per-`MoveNext` steps; the state-machine fields provide precise async invocation identity, and same-thread nested first-step execution adds parent/child causality edges for common direct-await shapes.

### Async Causality Coverage

Currently covered:

- Direct async calls where the child async method's first `MoveNext` executes synchronously while the parent async operation is active on the same physical thread.
- Later resumptions of the same child operation, because the child state-machine instance stores its stable operation id and generation.
- Generation resets, because thread-static parent operation state is ignored unless it belongs to the current recorder generation.

Not yet covered:

- Queued or scheduled handoffs where the child starts later on another thread, such as `Task.Run`, `TaskFactory.StartNew`, timer callbacks, thread-pool queueing, or custom schedulers.
- Fire-and-forget async work whose lifetime extends past the recorder's operation stop/flush boundary.
- Cross-operation causality if the only link is a task object/result rather than synchronous child first-step execution.

To support queued handoffs, a future slice should capture the current `{generation, operationId}` at selected scheduling APIs and restore it only for the child operation's first `EnterAsyncStep`. That propagation must stay explicitly scoped and generation-checked; the current coarse operation context is a capture gate, not a frame-parent propagation mechanism.

## Value Capture Slice

Native recorder-only instrumentation can optionally capture argument, local, and return values in addition to flow events.

- Managed surface: `FlowRecorder.ShouldCaptureValues(ref state, phase)`, `LogArg<T>`, `LogLocal<T>`, `LogReturn<T>`, and `RecordExceptionDetails(ref state, exception)`.
- Native injection (`DebuggerMethodRewriter::WriteFlowRecorderValues` + `ApplyFlowRecorderProbe`):
  - arguments captured after `Enter`; locals + return captured in the exit/finally path, each guarded by a `ShouldCaptureValues` gate so a disabled session pays almost nothing;
  - by-ref-like and pinned values are skipped;
  - value callbacks live in the same catch-swallowing protected blocks as the recorder enter/exit.
- Value capture is **opt-in per method** via `DD_INTERNAL_DEBUGGER_FLOW_RECORDER_CAPTURE_VALUE_METHODS` (comma/semicolon substring filter on `Type.Method`) and gated by `DD_INTERNAL_DEBUGGER_FLOW_RECORDER_CAPTURE_VALUES` mode (`off|exceptions|entry|exit|all`).
- Captured values are compact numeric records (`FlowCapturedValue`) with interned string/type tables. Common non-nullable primitive value types use `typeof(T) == typeof(...)` fast paths with `Unsafe.As` to avoid boxing; strings and collection counts use bounded summary paths; complex objects currently degrade to a type-name summary. Strict caps: `MAX_STRING_LENGTH` (256), `MAX_COLLECTION_ITEMS` (3), `MAX_STACK_LENGTH` (2048), and a separate bounded `VALUE_BUFFER_SIZE` queue.
- Exception details (`FlowExceptionDetails`) capture the interned type name, message, stack trace, and HResult on the throwing frame.
- Binary format is now version 6. Version 4 added the string table, type table, exception details, and captured values after the method metadata section; version 5 added per-event recorder operation id plus operation metadata; version 6 removed the per-event trace/span id fields (correlation now lives only in operation metadata) and uses length-prefixed UTF-8 string/type table entries. Readers still accept v1-v5.

## Viewer / HTML Slice

The viewer (`Samples.LiveDebuggerPoc.Viewer`) HTML report now includes:

- A dedicated **Exception panel** (type/message/HResult, reconstructed call stack with per-frame captured values, collapsible raw stack trace, span deep-link).
- **Structured captured values** (kind badge + name + value + short type + not-captured/truncation flag) in both the call-flow tree and the exception panel, replacing the previous raw string.
- A **sticky section nav** with scroll-spy, an exception counter, a **global method search** (dims timeline rows / hides non-matching flow cards), and a **light/dark toggle**.
- Existing sections retained: metrics, legend, APM span correlation, wall-clock timeline, thread swim-lanes, call flow, hot spots.

Known viewer limitation: the exception "call stack" is currently shallow because native records non-async methods as their own flow (`ParentFrameId=0`) and links async work via `AsyncEdge`, not parent-frame chains. Cross-flow / async-edge stitching is pending (see Next Work).

## Relationship to Exception Replay — CONSTRAINTS (read before adding value/exception features)

The tracer already ships **Exception Replay** (`Datadog.Trace/Debugger/ExceptionAutoInstrumentation`). The flow recorder must **complement, not duplicate** it.

- Exception Replay is **reactive** (captures on a later occurrence, misses the first), **exception-stack only** (default 4 frames, `ExceptionReplaySettings`), **rate-limited** (1/hour per case), captures **deep object-graph snapshots** (`SnapshotSerializer`/`SnapshotSlicer`/`SnapshotPruner`), and **uploads to Error Tracking**.
- The flow recorder is intended to be **always-on**, **every method**, **first-occurrence**, and low-overhead. That is the part Exception Replay cannot do; benchmark evidence is still required before calling it proven.

Hard rules for the next agent:

- **DO NOT** call or reuse `SnapshotSerializer` / `SnapshotSlicer` / `SnapshotPruner` / the JSON snapshot model on the recorder capture path. They are reflection- and allocation-heavy and are only acceptable on Exception Replay's rare, rate-limited path.
- **DO NOT** build a second deep-snapshot upload pipeline in the POC. For deep per-frame values on exceptions, **correlate and hand off to Exception Replay** via trace/span/error ids and deep-link from the viewer.
- Reuse from Exception Replay is allowed **only** for cold/cheap pieces: `ExceptionNormalizer` (stack fingerprint for grouping, runs once per reported exception) and the **redaction allow/deny policy** (cheap lookups) - never the serializer engine.

## Product Direction

Flow Recorder should be productized as operation-scoped call-flow capture, not process-wide method tracing.

- **APM answers** which request/span/resource was slow or failed.
- **Exception Replay answers** what deep object state existed on the throwing exception stack.
- **Flow Recorder answers** how a selected operation got there: calls that returned normally, async resumptions, timing, selected values, and optional span correlation.

The first product shape should support multiple roots:

- traced roots such as selected service/resource/span, erroring request, slow request, or “record next matching trace”;
- user-directed logical roots such as assembly, namespace, type, method, or root-method pattern;
- untraced roots such as job handlers, framework entrypoints, tests, functions, or sample-configured roots;
- debugger roots where a DI probe hit arms the recorder for that invocation;
- profiler roots where AlwaysOn Profiler identifies a slow/hot method or stack shape and Flow Recorder captures the next matching logical operation.

Capture policy belongs to the recorder operation context. The operation owns trigger reason, root, optional trace/span ids, budgets, and suppression/truncation state. The viewer must always show whether a capture is complete, filtered, suppressed, or truncated. This is the product contract that makes bounded capture trustworthy.

## Efficiency Constraints (always-on hot path)

- The always-on path stays **numeric + interned + allocation-light**: fixed `FlowEvent` records, preallocated bounded buffers, `AggressiveInlining`, and no per-event string formatting.
- Value capture must stay **bounded and opt-in**: prefer **trace-scoped or operation-scoped** capture windows over process-wide, keep the strict length/collection/depth caps, and keep the separate value buffer.
- The value dispatch in `FlowRecorder.CreateCapturedValue<T>` now uses `typeof(T) == typeof(...)` fast paths for common non-nullable primitive value types so those branches are JIT-constant-folded and avoid boxing. Preserve this constraint for any value-path change.
- Ref-type capture, when added, is limited to **1-2 levels** with a hard field cap: emit a type plus a few interned primitive fields rather than walking an object graph.

## Overhead Benchmark Plan

The recorder is designed to be low overhead, but current benchmark evidence shows the broad instrument-all POC is **not** low overhead yet. Before presenting any production-readiness claim, keep using focused overhead benchmarks that separate instrumentation cost, per-call recorder cost, value-capture cost, and file flush cost.

Recommended scenario:

- Use the console sample's `--scenario benchmark` mode and `run-benchmark-poc.ps1`. The benchmark mode does **not** flush inside the measured loop; it drains warmup events before measuring and reports flush duration after the loop.
- Workload shape: a small checkout request loop with synchronous leaf methods, direct async awaits, nested async calls, one `Task.Yield`, one `Task.Delay(0)`/completed task path, primitive math, string arguments, a small collection argument, and an optional exception every N requests.
- Scale: warm up first, then run at least `100_000` requests per variant in a single process; repeat each variant at least 5 times from a fresh process to include JIT/ReJIT startup separately from steady state.
- Keep the agent path constant: either disable trace export for all variants or use the same local mock/blackhole agent for all variants. Do not compare recorder-on with tracing-on against recorder-off with tracing-off.

Current runner:

```powershell
.\tracer\test\test-applications\debugger\Samples.LiveDebuggerPoc.Console\run-benchmark-poc.ps1 -Iterations 20000 -Warmup 2000 -SkipBuild -OutputDirectory .\artifacts\tmp\live-debugger-poc\benchmark-20k
```

Benchmark variants:

1. Baseline app, no profiler.
2. Profiler/tracer loaded, Dynamic Instrumentation disabled.
3. Profiler/tracer loaded, `DD_DYNAMIC_INSTRUMENTATION_ENABLED=true`, no recorder.
4. Recorder enabled with instrument-all, event-only capture.
5. Recorder enabled with instrument-all and value capture armed for one hot method, `DD_INTERNAL_DEBUGGER_FLOW_RECORDER_CAPTURE_VALUES=entry`.
6. Recorder enabled with value capture armed for one hot method, `DD_INTERNAL_DEBUGGER_FLOW_RECORDER_CAPTURE_VALUES=all`.
7. Regular DI method probe on the same hot method, if easy to configure, to compare against the existing `MethodDebuggerInvoker` snapshot path.

Metrics to collect:

- Throughput: requests/second and percent overhead vs variants 2 and 3.
- Latency: p50/p95/p99 per request, using `Stopwatch.GetTimestamp()` around the request body.
- Allocation: `GC.GetAllocatedBytesForCurrentThread()` per request for single-threaded runs; process allocation/GC counts for async/multithreaded runs.
- GC: gen0/gen1/gen2 collections and total pause-sensitive outliers.
- Recorder health: event count, value count, dropped events, final `.dflp` size, and flush duration outside the request loop.
- Startup/ReJIT: time from process start to first request and time from first request to steady state, reported separately.

Acceptance bar for a convincing POC:

- Event-only recorder overhead should be low single-digit percent over the profiler/tracer-loaded baseline on the steady-state checkout loop.
- Event-only recorder should add near-zero per-request allocation beyond unavoidable context/frame objects currently present in the POC; any allocation hotspots must be identified before productization.
- Armed value capture may cost more, but cost must scale only with explicitly armed methods and remain bounded by value buffer/string/collection caps.
- No customer-visible failures: callback fault injection must stay swallowed, event drops must be counted, and capture flush must stay outside the hot request path.

Initial benchmark evidence (`2026-07-01`, local Windows, single run, 20k iterations/2k warmup, broad instrument-all):


| Variant                             | Throughput/s | Overhead vs DI-enabled recorder-off | P50     | P95     | P99     | Alloc/request | Events  | Dropped | Flush    |
| ----------------------------------- | ------------ | ----------------------------------- | ------- | ------- | ------- | ------------- | ------- | ------- | -------- |
| profiler + DI enabled, recorder off | 218,362      | 0%                                  | 2.7 us  | 7.6 us  | 16.6 us | 1,231 B       | 0       | 0       | 0.793 ms |
| recorder event-only                 | 47,315       | 361.5%                              | 15.4 us | 33.9 us | 70.0 us | 11,222 B      | 967,862 | 0       | 570 ms   |
| recorder value entry                | 42,445       | 414.5%                              | 17.1 us | 49.8 us | 80.2 us | 11,744 B      | 967,838 | 0       | 880 ms   |
| recorder value all                  | 43,200       | 405.5%                              | 17.9 us | 47.2 us | 81.6 us | 12,257 B      | 967,906 | 0       | 685 ms   |


Follow-up validation after draining warmup events (`5k` iterations/`500` warmup) showed the same shape: event-only recorder overhead was ~424% vs DI-enabled recorder-off, with ~44 measured events/request and 0 dropped events.

Conclusion: this broad instrument-all POC is **not** ready to claim low overhead. The presentation can honestly show that the recorder transport/viewer works, but the next engineering target must be reducing event volume and per-event allocation before positioning it as always-on.

Post-optimization managed sanity check (`2026-07-01`, manual recorder mode, 5k iterations/500 warmup, compiled sample without native instrument-all) validated the managed allocation work in isolation:


| Variant                          | Throughput/s | P50    | P95     | P99     | Alloc/request | Events | Dropped | Flush |
| -------------------------------- | ------------ | ------ | ------- | ------- | ------------- | ------ | ------- | ----- |
| manual recorder fast-path sanity | 126,876      | 6.3 us | 12.3 us | 20.6 us | 1,153 B       | 85,000 | 0       | 43 ms |


This run is not comparable to the native instrument-all table because it uses manual sample callbacks and fewer events/request. Its purpose is narrower: after replacing the event queues with preallocated buffers and removing per-frame `AsyncLocal`, event recording itself no longer shows the earlier multi-KB/request allocation signature in this manual path. Rebuild the monitoring home before using `run-benchmark-poc.ps1` to compare native instrument-all numbers for these changes.

Rebuilt historical native instrument-all benchmark (`2026-07-02`, fresh monitoring home after fast-callback slice, 100k iterations/5k warmup, broad instrument-all). This table is retained as ablation evidence, not as the current product benchmark matrix:


| Variant                             | Throughput/s | Overhead vs DI-enabled recorder-off | P50    | P95     | P99     | Alloc/request | Events    | Dropped | Flush    |
| ----------------------------------- | ------------ | ----------------------------------- | ------ | ------- | ------- | ------------- | --------- | ------- | -------- |
| profiler + DI enabled, recorder off | 381,661      | 0%                                  | 2.3 us | 3.4 us  | 8.2 us  | 1,190 B       | 0         | 0       | 0.002 ms |
| recorder event-only                 | 109,669      | 248%                                | 7.1 us | 17.3 us | 26.9 us | 3,154 B       | 4,399,325 | 0       | 2,282 ms |
| recorder no method registration     | 97,529       | 291%                                | 8.1 us | 17.8 us | 29.0 us | 3,158 B       | 4,398,897 | 0       | 2,336 ms |
| recorder no enqueue                 | 110,885      | 244%                                | 7.7 us | 16.1 us | 27.9 us | 3,158 B       | 0         | 0       | 0.9 ms   |
| recorder no trace correlation       | 107,375      | 255%                                | 8.0 us | 15.7 us | 27.2 us | 3,158 B       | 4,399,173 | 0       | 2,346 ms |
| recorder no flow context            | 104,411      | 266%                                | 7.4 us | 19.0 us | 28.9 us | 3,154 B       | 4,399,319 | 0       | 2,247 ms |
| recorder minimal ablation           | 134,970      | 183%                                | 6.0 us | 11.5 us | 23.3 us | 3,158 B       | 0         | 0       | 0.5 ms   |
| recorder fast                       | 110,132      | 247%                                | 7.0 us | 16.9 us | 28.9 us | 1,238 B       | 4,398,581 | 0       | 2,489 ms |
| recorder rewrite minimal-finally    | 102,866      | 271%                                | 7.3 us | 18.6 us | 29.7 us | 3,158 B       | 4,398,871 | 0       | 2,165 ms |
| recorder rewrite entry-only         | 122,546      | 211%                                | 7.0 us | 13.4 us | 23.4 us | 3,110 B       | 1,999,926 | 0       | 1,142 ms |
| recorder value entry                | 86,105       | 343%                                | 8.9 us | 21.5 us | 41.5 us | 3,286 B       | 4,399,167 | 0       | 2,275 ms |
| recorder value all                  | 84,746       | 350%                                | 9.4 us | 23.0 us | 46.0 us | 3,406 B       | 4,399,301 | 0       | 2,455 ms |


Interpretation from the historical broad benchmark:

- Allocation improved substantially: native event-only dropped from the earlier ~`14.9 KB/request `ablation result to ~`3.1 KB/request`, and the benchmark-only` fast `rewrite drops to ~`1.2 KB/request`, close to recorder-off.
- Event queueing is no longer a visible allocation source in this benchmark: `no enqueue` and event-only both report ~`3.1 KB/request`.
- CPU is now the blocker. The `fast` rewrite uses specialized event-only callbacks and removes exception-aware exit work, but throughput remains close to event-only while allocation falls sharply; the remaining wall-clock cost is dominated by event volume, callback count, enqueue work, and the balanced finally/rewrite shape.
- The benchmark-only `minimal-finally` rewrite did **not** improve throughput versus the current exception-aware rewrite. Removing the original-method catch/rethrow while keeping balanced exit/finally shape is not enough to justify making this the next productized default.
- The `entry-only` lower-bound mode is faster (`122,546` req/s, ~`211%`overhead) but emits only detached entry events, so it proves callback/event volume and exit/finally work matter; it does not satisfy balanced timing semantics. It intentionally uses`EnterDetached` to avoid leaking thread-static frame context without a matching exit.
- `no flow context` is no longer useful as an ablation because broad mode already removed per-frame `AsyncLocal`; the remaining difference is noise/branch shape.
- Value capture overhead is now bounded and incremental: entry/all modes add hundreds of bytes/request, not the previous multi-KB queue/context allocation.

Current product benchmark matrix:

- `baseline-no-profiler`
- `profiler-di-disabled`
- `profiler-di-enabled-recorder-off`
- `recorder-event-only`
- `recorder-value-entry`
- `recorder-value-all`

The current `run-benchmark-poc.ps1` intentionally removed the old ablation and rewrite-mode variants from the default matrix. Benchmark recorder runs now use an armed operation for warmup and an armed operation for the measured loop, with `DD_INTERNAL_DEBUGGER_FLOW_RECORDER_ALLOW_RECORDING_WITHOUT_OPERATION` left unset. This measures the product shape: baseline, operation-scoped event-only capture, and operation-scoped value capture.

### CPU Benchmark Evidence

The current CPU evidence is based on throughput/latency ablation, not a sampled CPU flamegraph. Profiling tools were not available locally during the first investigation, so the benchmark isolates hot-path work by running controlled variants with specific recorder operations disabled.

Important measurement boundaries:

- The measured loop does **not** flush recorder data. The benchmark stops the measured recorder operation before flush, and flush duration is reported after the loop, so request latency/throughput mostly reflects instrumentation, managed callbacks, event creation, queueing, context handling, trace lookup, and native IL shape.
- The workload is single-process and intentionally small. It is useful for finding hot-path recorder costs, but it should not be treated as a full application performance claim.
- Allocation and CPU are coupled in these results. Lower allocation can reduce CPU indirectly through less GC pressure, but the ablation shows several CPU costs remain even when the largest allocation paths are disabled.

CPU-oriented reading of the original benchmark:


| Signal                                                                      | Evidence                                                                                                                                   | Interpretation                                                                                                          |
| --------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------- |
| Broad recorder path is much slower before value capture                     | Event-only recorder dropped from `218,362` req/s to `47,315` req/s in the 20k run                                                          | The core flow recorder path is already expensive before argument/return capture                                         |
| Tail latency moves with throughput                                          | P50 moved from `2.7 us` to `15.4 us`; P99 moved from `16.6 us` to `70.0 us`                                                                | The overhead is paid inside requests, not only during flush/export                                                      |
| Runtime method registration is visible CPU cost                             | Disabling registration improved ablation throughput from `53,793` req/s to `64,081` req/s with no allocation change                        | Move metadata registration out of the invocation hot path                                                               |
| Queue allocation is the clearest allocation cost, but not the only CPU cost | Disabling enqueue cut allocation from `14,948 B/request` to `6,423 B/request`, but throughput stayed around `53k/s` in that run            | Replace the queue to remove allocation, then remeasure CPU because other callback/IL costs may dominate                 |
| Minimal managed path still costs too much                                   | Disabling enqueue, trace correlation, flow context, and method registration improved throughput to `106,752` req/s, still ~`172%` overhead | Native callback count, EH/finally/catch shape, state locals, and `Enter`/`Exit` call overhead need direct investigation |


Current CPU conclusion after the rebuilt benchmark:

- Allocation is no longer the main blocker in the measured native path; event-only is ~`3.1 KB/request`.
- The strongest remaining avoidable CPU costs are event volume, enqueue/callback overhead, callback count, and managed hot-path indirection. Cold sidecar metadata removes invocation-time registration from the native recorder path, and the `fast` rewrite removes most extra allocation, but neither is enough to fix wall-clock overhead.
- Benchmark-only rewrite variants now compare current exception-aware EH, minimal finally, and detached entry-only lower-bound mode. The result argues against spending the next slice only on EH catch/rethrow removal: minimal-finally was not faster than current event-only, while entry-only was faster because it removes exit/finally work and more than halves event volume.
- Next best performance slice: reduce event/callback volume and hot callback indirection further. The cold/native method metadata sidecar now preserves method names without a per-invocation registration callback, including in fast rewrite mode.

### Hot-Path Ablation Findings

The benchmark runner has internal investigation variants controlled by POC-only env vars:

- `DD_INTERNAL_DEBUGGER_FLOW_RECORDER_SKIP_EVENT_ENQUEUE=true`
- `DD_INTERNAL_DEBUGGER_FLOW_RECORDER_SKIP_TRACE_CORRELATION=true`
- `DD_INTERNAL_DEBUGGER_FLOW_RECORDER_DISABLE_FLOW_CONTEXT=true`
- `DD_INTERNAL_DEBUGGER_FLOW_RECORDER_SKIP_METHOD_REGISTRATION=true`

Local ablation run (`2026-07-01`, 3k iterations/300 warmup) suggests there is no single magic expensive call; cost is distributed across event volume, queueing/allocation, flowing frame context, metadata registration, and the remaining callback/IL shape.


| Variant                                                                      | Throughput/s | Overhead vs DI-enabled recorder-off | Alloc/request | Notes                                                                               |
| ---------------------------------------------------------------------------- | ------------ | ----------------------------------- | ------------- | ----------------------------------------------------------------------------------- |
| DI enabled, recorder off                                                     | 290,116      | 0%                                  | 1,224 B       | Baseline for ablation                                                               |
| recorder event-only                                                          | 53,793       | 439%                                | 14,948 B      | Full broad instrument-all recorder path                                             |
| no method registration                                                       | 64,081       | 353%                                | 14,948 B      | Runtime `RegisterMethod` is visible CPU cost, not allocation cost                   |
| no event enqueue                                                             | 53,467       | 443%                                | 6,423 B       | Queue enqueue is a major allocation source, but not the only CPU source in this run |
| no trace correlation                                                         | 57,588       | 404%                                | 14,951 B      | Trace/span lookup is a smaller cost than queue/context                              |
| no flow context                                                              | 57,694       | 403%                                | 12,192 B      | `AsyncLocal`/`FlowContext` allocation is meaningful                                 |
| no enqueue + no trace correlation + no flow context + no method registration | 106,752      | 172%                                | 3,671 B       | Even the minimal callback/IL path still costs materially                            |


Interpretation:

- **Allocation:** biggest confirmed allocation sources are event queueing (`ConcurrentQueue<T>` node churn) and flowing frame context (`FlowContext` objects / `AsyncLocal` updates). Removing enqueue drops allocation by more than half; disabling flow context also cuts several KB/request.
- **CPU:** runtime method registration on every call is a clear avoidable CPU cost. Trace correlation is measurable but not dominant. Even with queue, trace, flow context, and registration disabled, native callback/finally/catch/state plumbing still leaves high overhead, so the injected IL shape and callback count need attention too.
- **Value capture:** value capture adds incremental cost, but event-only is already the primary problem.

### Performance Design Notes

These notes are intentionally aggressive. We control native IL rewriting and module metadata, so fixes should use that control when there is a strong reason. Do not keep expensive managed hot-path work just because it is easier to express in C#; the async state-machine operation fields already prove that metadata/IL changes can remove whole classes of runtime work.

#### 1. Queue / Event Storage Allocation

Original behavior:

- Every recorded event (`Enter`, `Exit`, `AsyncEdge`, `Exception`) is a `FlowEvent` value enqueued through `BoundedConcurrentQueue<FlowEvent>`.
- `BoundedConcurrentQueue<T>` wraps `ConcurrentQueue<T>`.
- `ConcurrentQueue<T>` is safe and convenient, but it is not a zero-allocation event log. It allocates internal segments/nodes as the queue grows.
- Broad instrument-all currently records roughly `44-48` events/request in the benchmark. That turns small per-event costs into large per-request allocation.

Current managed status:

- `FlowRecorderSink` now uses preallocated `BoundedRecorderBuffer<T>` arrays for events, exception details, and captured values.
- Enqueue reserves a slot with `Interlocked.Increment`, writes the struct directly into the array, then publishes the slot with a `Volatile.Write` flag. There is no lock on successful enqueue.
- Drain snapshots the reserved range and waits for reserved slots to publish before copying. In the product path this happens after the operation has been stopped, so customer callbacks are no longer accepting new events for that operation.
- When the buffer is full, the event is dropped and counted; customer threads are never blocked.
- The buffer does not wrap. First `N` events win, later events drop until the next drain/reset.

Why it matters:

- The `no event enqueue` ablation cut allocation from ~`14.9 KB/request `to ~`6.4 KB/request`.
- That means queue/event storage is the largest confirmed allocation source.
- CPU did not improve as much in one run, so enqueue allocation is not the only cost, but it is the clearest allocation target.

Implemented direction:

1. Replace `BoundedConcurrentQueue<FlowEvent>` with a preallocated bounded event buffer. **Implemented for managed recorder buffers.**
2. Allocate `FlowEvent[]` once per recorder session, sized from `DD_INTERNAL_DEBUGGER_FLOW_RECORDER_BUFFER_SIZE`.
3. Reserve a slot using `Interlocked.Increment(ref writeIndex) - 1`.
4. If the slot is within capacity, write the `FlowEvent` directly into the array.
5. If the slot exceeds capacity, increment dropped-event count and return.
6. On flush, snapshot `Math.Min(writeIndex, capacity)` and copy/serialize that range.

Design details:

- Use separate preallocated buffers for:
  - `FlowEvent`
  - `FlowCapturedValue`
  - `FlowExceptionDetails`
- Do not use `ConcurrentQueue<T>` on the hot path.
- Avoid per-event object creation and avoid queue node allocation entirely.
- Consider a per-thread chunked buffer or per-slot sequence design only if the single global write index becomes a CPU bottleneck.
- Preserve bounded behavior: dropping events is acceptable; blocking customer threads is not.

Risks / questions:

- Drain/flush assumes the product operation has been stopped before draining. If future scenarios flush while accepting events, the drain protocol must be revisited or use a generation/chunk swap.
- If the buffer wraps, reconstruction gets harder. For the POC, prefer no wrap: first `N` events win, later events drop. That keeps ordering simple.
- For production, a chunked segmented buffer may give better flush behavior and less contention than one very large array.

#### 2. Flow Context / `AsyncLocal`

Original behavior:

- Non-async nesting uses `AsyncLocal<FlowContext?> CurrentFlow`.
- `Enter` reads the current context, allocates a new `FlowContext`, writes it to `AsyncLocal`, and records parent frame/depth.
- `Exit` may allocate another `FlowContext` to restore the parent frame.
- Async `MoveNext` does **not** use this path for operation identity; it uses native-added state-machine fields.

Current managed status:

- Broad synchronous nesting now uses thread-static primitive frame state: generation, flow id, frame id, and depth.
- `Enter`/`Exit` no longer allocates `FlowContext` objects or updates `AsyncLocal`.
- Normal frame context no longer flows across awaits or `Task.Run`; async logical identity remains state-machine-field based.

Why it matters:

- The `no flow context` ablation cut allocation from ~`14.9 KB/request `to ~`12.2 KB/request`.
- `AsyncLocal` updates are not just object assignment; they interact with `ExecutionContext`.
- Updating `AsyncLocal` for every method frame is too expensive for broad always-on recording.

Preferred improvement:

Use `AsyncLocal` only for coarse operation/request identity, not for every physical frame.

Implemented direction:

1. Keep async operation identity on compiler-generated state-machine instances, as the POC already does.
2. For synchronous nesting on one thread, use `[ThreadStatic]` frame state:
  - current flow id
  - current frame id
  - current depth
  - previous frame id stack or fixed small stack
3. On `Enter`, update the thread-static stack without allocating.
4. On `Exit`, restore the previous frame from the stack without allocating.
5. Only bridge async boundaries with explicit metadata/IL help where needed, not with blind `AsyncLocal` frame propagation.

More aggressive IL/metadata option:

- For async state machines, add more recorder fields if needed:
  - operation id
  - generation
  - parent operation id
  - maybe last active frame id for reconstruction
- For regular methods, consider an injected local `FlowRecorderState` that contains enough previous-frame state to restore thread-static state on exit. That avoids allocating a managed context object.

Tradeoff:

- Removing per-frame `AsyncLocal` can reduce exact parent/child nesting across async continuations.
- That is acceptable if async method operation identity is preserved through state-machine fields and viewer reconstruction owns logical stitching.
- Parent-frame precision should be a mode, not something every hot-path event pays for.

#### 3. Runtime Method Metadata

Original behavior:

- Native IL injects `FlowRecorder.RegisterMethod(int, RuntimeMethodHandle, RuntimeTypeHandle)` before recorder `Enter`.
- Managed code resolves handles into method/type names and stores them in a metadata dictionary.
- Even after the method is registered, the rewritten method still calls `RegisterMethod` on every invocation.

Current managed/native status:

- Native recorder-only rewriting no longer injects the hot `RegisterMethod` callback before `Enter`.
- During instrument-all selection, native assigns the same `methodMetadataIndex` used by events and appends `{methodMetadataIndex, displayName}` rows to `<capture>.methods`.
- Managed flush reads that sidecar via `FlowRecorderSink.GetMethodMetadata(...)` and merges used method names into the `.dflp` method table. Native tests assert that fast rewrite captures include method names.
- The managed `RegisterMethod(...)` surface remains as a fallback/testable API, but the current native recorder path does not call it.

Why it matters:

- The `no method registration` ablation improved throughput from ~`53.8k/s `to ~`64.1k/s` in the 3k run.
- Allocation did not change, so this is mostly CPU: managed call overhead, session checks, dictionary checks, locks, and runtime handle resolution path.

Preferred improvement:

Do not perform method metadata registration on every method invocation.

Implemented direction:

1. **Done:** Move method display-name registration out of the invocation hot path using native metadata/probe data.
2. **Done:** Emit cold sidecar metadata from native instrument-all selection.
3. **Done:** Have managed flush merge sidecar names into the final `.dflp` method table.
4. **Remaining productization work:** replace the POC file sidecar with a production metadata handoff that does not depend on `DD_INTERNAL_DEBUGGER_FLOW_RECORDER_OUTPUT_PATH`, works for backend/upload scenarios, and has explicit lifecycle/error semantics.

#### 4. Trace / Span Correlation

Original behavior:

- Every `CreateEvent` called `Tracer.Instance.InternalActiveScope?.Span`.
- If a span existed, every event stored trace id, root span id, and active span id.

Current status:

- Trace/span correlation is no longer stored per event. It is captured once when the recorder operation is armed and stored in the operation metadata section (`FlowOperationMetadata`), so binary format v6 dropped the four per-event correlation fields.
- The per-event hot path no longer performs a trace/span lookup for correlation storage.

Why it matters:

- The ablation says trace lookup is measurable but not dominant.
- It previously happened per event, so it scaled with event count; operation-scoped capture removes that per-event cost.

Preferred improvement (now implemented):

- Cache trace/span correlation at operation/frame scope when possible.
- For async operations, store correlation on the state-machine recorder fields or operation table when the operation id is assigned.
- For synchronous call stacks, load correlation once at root frame enter and inherit it for child frames unless the active span changes.
- If span changes matter, record a separate lightweight `SpanChanged` or span-context event rather than repeating full trace/span ids on every event.

Tradeoff:

- APM correlation is presentation-critical, but we do not need to repeat the same ids on every event if a flow-level or operation-level table can represent it.

#### 5. Exception Recording and EH Shape

Current behavior:

- Recorder-only rewriting wraps original method execution, stores exceptions, rethrows, and runs `FlowRecorder.Exit(ref state, exception)` in a finally path.
- Recorder callback failures are catch-swallowed.
- This gives balanced exit events and exception events, but it means every instrumented method pays for extra EH/control-flow shape.

Important product question:

- If the only reason for the full `try/catch/finally` shape is exception capture/reporting, we should not pay it in the regular always-on scenario by default.
- The tracer already has Exception Replay for deep exception state.
- Flow recorder can complement Exception Replay without always doing exception-aware method rewriting.

Preferred improvement: tiered rewrite modes.

1. **Mode 1: Ultra-light flow/timing**
  - No exception capture.
  - Avoid catch/rethrow wrapping.
  - Possibly no finally if the mode only records entry/counts, or use the smallest possible finally shape if duration is required.
  - No value capture.
  - No method registration in hot path.
  - Goal: prove always-on viability.
2. **Mode 2: Balanced enter/exit timing**
  - Records enter/exit and durations.
  - Uses minimal finally only where required for balance.
  - Still no exception detail/value capture by default.
3. **Mode 3: Exception-aware flow**
  - Enabled by flag or targeted window.
  - Adds exception capture and catch/rethrow/finally shape.
  - Correlates with Exception Replay/Error Tracking instead of deep snapshotting.
4. **Mode 4: Value capture**
  - Narrowly armed by method/trace/window.
  - Accepts higher cost because it is not broad always-on.

Design principle:

- Broad instrument-all must use Mode 1 or Mode 2.
- Exception-aware and value-capable rewrites should be opt-in layers, not the default IL shape.

Risks / questions:

- Without finally, exits can be missed on exceptions. That may be acceptable for Mode 1 if Exception Replay/trace errors cover exception scenarios.
- If balanced call trees are required even when exceptions happen, minimal finally may still be needed.
- Benchmark-only variants for current exception-aware shape, minimal finally, and entry-only lower-bound now exist. The current data says minimal finally alone does not move throughput enough; entry-only helps because it removes exit/finally work and event volume, but it gives up balanced timing.

#### 6. Remaining Native Callback / IL Shape

Current behavior:

- Every instrumented method pays for:
  - one metadata registration call
  - one `Enter` or `EnterAsyncStep` call
  - original method body wrapped with additional EH
  - one `Exit` call
  - extra locals for exception/state/return
  - branch/leave/rethrow plumbing

Why it matters:

- The latest minimal ablation still showed ~`114%` overhead with enqueue, trace correlation, flow context, and method registration disabled.
- That means the remaining managed callbacks and rewritten IL shape are still materially expensive.

Possible improvements beyond "instrument fewer methods":

- **Reduce callback count.**
  - Combine callback responsibilities.
  - Avoid separate registration callback.
  - Consider a single `EnterExit`-style fast path only where duration is not required.
- **Use precomputed per-method metadata.**
  - Native can encode method index, flags, and capture mode into the injected IL.
  - Managed callback should not rediscover settings repeatedly.
- **Split session state into hot primitive fields.**
  - Avoid object/session/sink indirection in the hottest callback.
  - Example: static volatile enabled flag + static buffer reference + primitive generation.
  - Keep rich `RecorderSession` for cold paths.
- **Use specialized callbacks by mode.**
  - `EnterFast(int methodId)` for event-only.
  - `EnterAsyncStepFast(int methodId, ref long opId, ref long generation)` for async.
  - `ExitFast(ref state)` without exception parameter for non-exception-aware mode.
  - `ExitExceptionAware(ref state, Exception?)` only when enabled.
- **Use IL/metadata to avoid generic managed decisions.**
  - If a method does not need return/local capture, do not inject value gates.
  - If a method is known non-throw-sensitive in broad mode, do not inject exception-aware wrapping.
  - If a method is tiny/trivial, skip or use count-only mode.
- **Consider native-side or unmanaged buffering only if managed ring buffer is insufficient.**
  - Managed preallocated arrays are simpler and likely enough for the next slice.
  - Native buffering adds complexity around GC safety, shutdown, and cross-runtime support.

#### 7. Suggested Optimization Order

1. **Done:** Replace `ConcurrentQueue<T>` event storage with preallocated bounded buffers.
2. **Done:** Remove per-frame `AsyncLocal` updates from broad mode; use thread-static frame state plus async state-machine fields.
3. **Done:** Remove hot-path method registration from native recorder-only rewriting. Cold native `.methods` sidecar metadata is merged during managed flush, and fast-mode captures keep method names.
4. **Done:** Add benchmark-only rewrite variants for current EH-heavy mode, minimal finally mode, and entry-only lower-bound mode. Result: minimal finally alone is not enough; entry-only is faster but loses balanced exits.
5. **Partially done:** Split fast callbacks by mode. `DD_INTERNAL_DEBUGGER_FLOW_RECORDER_REWRITE_MODE=fast` uses `EnterFast`/`EnterAsyncStepFast`/`ExitFast` and removes most extra allocation, but throughput remains near the current event-only mode.
6. Next: reduce event/callback volume or move more of the hot event path out of managed callbacks; fast callbacks alone are not enough.

## Known Limitations

- Native recorder injection is wired only for recorder-only probes created by the instrument-all POC path.
- Async support records `MoveNext` invocations. The viewer groups generated state-machine steps by original async method name and operation `FlowId`, and prints parent/child operation edges when a child async operation starts synchronously under an active parent operation. Queued work such as `Task.Run`, timers, and thread-pool callbacks still need explicit parent-id propagation if those edges are required.
- The console sample defaults to manual `FlowRecorder.Enter`/`Exit`; pass `--recording native` when running it under profiler-driven recorder instrumentation.
- Native method metadata names are currently handed off through the POC `<capture>.methods` sidecar and merged during managed flush. This removes hot-path registration, but a productized backend/upload path still needs a real metadata transport that is not tied to local files.
- The recorder is Windows-gated when initialized from environment. Tests use `ConfigureForTesting`.
- Value capture is implemented (see Value Capture Slice) but is **opt-in per method** and **shallow** for complex reference types (type-name summary only). It is good enough for the POC/demo, but it is not yet a safe production capture story for arbitrary object graphs.
- Operation-scoped stop/drain exists for the current POC path, but production still needs a fuller lifecycle story for concurrent independent operations, long-running background work, backend upload, and process shutdown.

## Resilience Fault Injection

The recorder POC has internal fault-injection switches for validating native-swallowed callback failures:

- `DD_INTERNAL_DEBUGGER_FLOW_RECORDER_THROW_ON_ENTER=true`
- `DD_INTERNAL_DEBUGGER_FLOW_RECORDER_THROW_ON_EXIT=true`
- `DD_INTERNAL_DEBUGGER_FLOW_RECORDER_MAX_METHODS=10000`

The focused native integration test proves:

- forced `Enter` failures are swallowed by the native-injected catch block, the sample exits successfully, and the capture contains no leaked recorder events;
- forced `Exit` failures are swallowed by the native-injected catch block, the sample exits successfully, and the capture remains balanced because cleanup/event enqueue happens before the forced throw.

## Native Exploration Findings

The current native slice supports non-async methods and async state-machine `MoveNext` methods. It reuses debugger method discovery, method metadata index assignment, and ReJIT, but it uses a recorder-only rewrite body instead of the normal debugger method-probe body.

Relevant flow:

1. `CorProfiler::JITCompilationStarted` calls `DebuggerProbesInstrumentationRequester::PerformInstrumentAllIfNeeded`.
2. `PerformInstrumentAllIfNeeded` creates synthetic `FlowRecorderProbeDefinition` instances when both `DD_INTERNAL_DEBUGGER_INSTRUMENT_ALL` and `DD_INTERNAL_DEBUGGER_FLOW_RECORDER_ENABLED` are enabled.
3. `DebuggerRejitPreprocessor` queues methods and `ProbesMetadataTracker::GetInstrumentedMethodIndex` assigns the `methodMetadataIndex` used by managed metadata.
4. `DebuggerMethodRewriter::Rewrite` classifies `FlowRecorderProbeDefinition` before `MethodProbeDefinition`.
5. Recorder-only rewrites use `ModifyLocalSigForFlowRecorder` and `ApplyFlowRecorderProbe`.
6. Normal method probes still use `DebuggerTokens::ModifyLocalSigAndInitialize` and `ApplyMethodProbe`.

Recorder-only native local layout:

- non-void return local, when needed;
- `Exception` local;
- `FlowRecorderState` local.

Regular debugger local layout remains:

- `debuggerLocals[0]`: line probe state.
- `debuggerLocals[1]`: span method state.
- `debuggerLocals[2]`: multi-probe state array.

## Presentation Readiness Workstreams

These are the only must-do items before presenting the POC. They are written so separate agents can take them independently. Do not expand scope into productization unless explicitly asked.

### Workstream A: Overhead Benchmark Evidence

Owner fit: best for an implementation/performance agent. This is the highest-priority blocker before claiming the recorder is low overhead.

Task:

1. Implement the benchmark scenario described in `Overhead Benchmark Plan`.
2. Run at least the required presentation variants:
  - profiler/tracer loaded, Dynamic Instrumentation disabled;
  - profiler/tracer loaded, Dynamic Instrumentation enabled, recorder disabled;
  - recorder enabled with instrument-all, event-only capture;
  - recorder enabled with instrument-all and value capture armed for one hot method.
3. Keep trace export/agent behavior identical across variants.
4. Keep `FlowRecorder.Flush` outside the measured request loop and report flush duration separately.

Required output:

- A short markdown result table with throughput, percent overhead, p50/p95/p99, allocation/request where available, GC counts, event count, dropped events, `.dflp` size, and flush duration.
- The exact commands/env vars used for every variant.
- A clear conclusion: whether the event-only recorder is low-single-digit overhead on the benchmark or which hotspot blocks that claim.

Done condition:

- We can put one honest benchmark slide in the presentation. If the numbers are bad, the slide must say what the bottleneck is instead of hiding it.

### Workstream B: Clean Native Demo Capture

Owner fit: best for a demo/verification agent familiar with local profiler environment.

Task:

1. Build the monitoring home if needed:

```powershell
.\tracer\build.cmd CompileManagedLoader --BuildConfiguration Release
.\tracer\build.cmd CompileTracerNativeSrc --TargetPlatform x64 --BuildConfiguration Release
.\tracer\build.cmd BuildTracerHome --TargetPlatform x64 --BuildConfiguration Release
```

1. Run a native recorder demo using either `presentation` or `multi-span`:

```powershell
$capture = ".\artifacts\tmp\live-debugger-poc\flow-events-presentation-native.dflp"
$env:DD_DYNAMIC_INSTRUMENTATION_ENABLED = "true"
$env:DD_INTERNAL_DEBUGGER_INSTRUMENT_ALL = "true"
$env:DD_INTERNAL_DEBUGGER_FLOW_RECORDER_ENABLED = "true"
$env:DD_INTERNAL_DEBUGGER_FLOW_RECORDER_OUTPUT_PATH = $capture
dotnet run --project .\tracer\test\test-applications\debugger\Samples.LiveDebuggerPoc.Console\Samples.LiveDebuggerPoc.Console.csproj -c Release -f net10.0 -- --scenario presentation --recording native --output $capture
dotnet run --project .\tracer\test\test-applications\debugger\Samples.LiveDebuggerPoc.Viewer\Samples.LiveDebuggerPoc.Viewer.csproj -c Release -f net10.0 -- $capture --html
```

1. If profiler env vars are not already set by the local test harness, configure them from the built monitoring home before running the sample.

Required output:

- Path to the `.dflp` capture.
- Path to the generated `.html` report.
- Console output showing `Dropped events: 0`.
- Two or three screenshots or copied snippets showing: timeline/call flow, async logical operations, and APM/span correlation if present.

Done condition:

- The presenter has a deterministic local artifact to open during the presentation and a backup screenshot/snippet if live demo fails.

### Workstream C: Final Focused Verification

Owner fit: best for a test/CI agent after Workstream A/B changes land.

Task:

1. Run focused managed tests:

```powershell
dotnet test .\tracer\test\Datadog.Trace.Tests\Datadog.Trace.Tests.csproj -c Release -f net6.0 --filter "FullyQualifiedName~LiveDebuggerPoc"
```

1. Run focused native integration tests:

```powershell
dotnet test .\tracer\test\Datadog.Trace.ClrProfiler.IntegrationTests\Datadog.Trace.ClrProfiler.IntegrationTests.csproj -c Release -f net10.0 --logger "console;verbosity=minimal" --filter "FullyQualifiedName~LiveDebuggerPocNativeTests"
```

Required output:

- Pass/fail counts for both commands.
- Any failure details and whether they affect presentation readiness.
- Current git diff summary for POC files.

Done condition:

- The presenter can say the focused managed and native POC tests passed after the final demo/benchmark changes.

### Workstream D: Presentation Positioning

Owner fit: best for a product/architecture-summary agent. It should not change code.

Task:

1. Draft a short presentation outline with 4 messages:
  - Regular Dynamic Instrumentation probes answer: "What happened at this configured point?"
  - Exception Replay answers: "Can we capture deep exception state on later occurrences?"
  - Flow Recorder answers: "What happened across the first occurrence of the request, including call flow, async resumptions, timing, and span context?"
  - This POC proves the transport/viewer shape; benchmark evidence decides how strongly we can claim low overhead.
2. Include non-goals:
  - no backend upload/UI integration;
  - shallow opt-in values only;
  - queued async handoffs like `Task.Run` are future work;
  - production capture policy/redaction/RCM are not solved here.

Required output:

- A 5-7 slide outline or speaker notes.
- One crisp comparison table: regular DI probes vs Exception Replay vs Flow Recorder.
- One final "ask" slide: approve next investment only if benchmark results are acceptable.

Done condition:

- The presenter has a clear story that does not overclaim production readiness.

## Next Work

Current priority order (owner direction): reduce hot-path overhead first, then complement Exception Replay rather than duplicate it.

1. **Reduce event/callback volume or move more hot event work out of managed callbacks.** The fast-callback rewrite removes most allocation but leaves throughput near event-only, so callback/event count is the next gating issue.
2. **Productize method metadata handoff.** The POC local `.methods` sidecar proves cold metadata can preserve method names without hot registration, including in fast mode. A production path still needs a metadata transport that is not tied to local capture files and works with backend upload/UI ingestion.
3. **Correlate + hand off to Exception Replay / Error Tracking (do not reimplement deep capture).** Emit the same error-tracking/exception-replay correlation ids and deep-link the viewer's Exception panel out to the Exception Replay snapshot / Error Tracking issue.
4. **Cross-flow / async-edge stack stitching in the viewer.** Reconstruct the full exception ancestor chain across flows (and async edges), so the Exception panel shows every caller frame with its captured values instead of just the throwing frame.
5. Scope value capture to a **trace or root operation** window instead of the per-method substring filter, with strict per-operation budgets.
6. Extend async causality beyond same-thread nested first-step execution by propagating parent ids through scheduling/kickoff sites such as `Task.Run` if those edges are required. (Deprioritized by owner.)
7. Define explicit recorder shutdown/quiesce semantics before supporting long-running, background, or fire-and-forget recording scenarios.

Done since earlier revisions: value capture (managed + native), primitive value-type de-boxing for common fast paths, exception details with human-readable type/message/stack, binary format v6 with operation-scoped trace/span correlation, cold native method metadata sidecar, the viewer Exception panel + structured values + nav, preallocated recorder buffers, thread-static broad frame context, benchmark-only EH rewrite variants, and benchmark-only fast callbacks.

Native risks to watch:

- Managed/native signature mismatch, especially for `EnterAsyncStep(int, ref long, ref long)`, will produce invalid IL or runtime method resolution failures.
- Native sidecar metadata failures must not suppress balanced recorder events; hot recorder enter/exit callbacks must remain independent from cold metadata handoff.
- `Exit(ref FlowRecorderState, Exception)` must load `ldloca flowRecorderStateIndex` before `ldloc exceptionIndex`.
- Recorder-only duplicate detection depends on trailing `FlowRecorderState`.
- Regular debugger method probes must not regain recorder calls or recorder locals.
- `DD_INTERNAL_DEBUGGER_INSTRUMENT_ALL` currently uses broad matching (`is_exact_signature_match = false`), so keep the POC allowlist strict before enabling automatic recording broadly.
- Async `MoveNext` support is still per-state-machine-step at capture time; current logical reconstruction is viewer-side and uses method metadata plus state-machine operation `FlowId`.

