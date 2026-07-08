# Flow Recorder Handoff

Use this page as the entry point for continuing the Live Debugger Flow Recorder POC. It explains which documents matter, what is source-of-truth, and where to look for implementation, tests, benchmarks, and product reasoning.

## Source Of Truth

1. `docs/development/FlowRecorderHandoff.md`  
   Start here. This is the map and handoff for future agents.

2. `docs/development/LiveDebuggerPoc.md`  
   Detailed work log and engineering source of truth. It contains current implementation notes, constraints, verification commands, benchmark guidance, and product direction. If another document disagrees with this file, verify against code and update this file.

3. Code and tests  
   The implementation and behavior tests are the final authority for what currently works.

Do not treat prior chat transcripts or local Cursor plans as canonical. They are useful for historical reasoning, but they can be stale after implementation.

## How The Planning Artifacts Fit

- Local Cursor plan (not committed to the repo): product direction and execution checklist. If available in your local session, it is useful for goals, milestones, open product questions, and why the POC moved toward operation-scoped capture.
- Previous Flow Recorder chat transcript: historical reasoning and tradeoffs. Use it only when you need discussion archaeology, not as a current implementation spec.
- `LiveDebuggerPoc.md`: current repo-facing state. Keep it synchronized when behavior, commands, or conclusions change.

Recommended rule: distill useful decisions from plans/transcripts into repo docs, then continue from repo docs.

## Current Product Direction

Flow Recorder should not be productized as broad process-wide method tracing. Broad instrument-all is only a stress benchmark.

The product direction is operation-scoped call-flow capture:

- A recorder operation context is the primary capture gate.
- Trace/span context is optional correlation.
- Callbacks are cheap no-ops when no recorder operation is active.
- Operation lifetime is explicit: arm/start the operation, stop accepting new events on dispose, then drain/flush.
- Capture is bounded by event, depth, duration, and unique-method budgets.
- Static filters and runtime suppression avoid low-value/noisy methods.
- The viewer must explain incomplete capture with suppression/truncation markers.
- Exception Replay remains complementary: Flow Recorder explains the operation path; Exception Replay explains deep exception state at the failure point.

## Key Implementation Files

Managed recorder:

- `tracer/src/Datadog.Trace/Debugger/LiveDebuggerPoc/FlowRecorder.cs`
- `tracer/src/Datadog.Trace/Debugger/LiveDebuggerPoc/FlowRecorderSettings.cs`
- `tracer/src/Datadog.Trace/Debugger/LiveDebuggerPoc/FlowEvent.cs`
- `tracer/src/Datadog.Trace/Debugger/LiveDebuggerPoc/FlowEventBinaryFormat.cs`
- `tracer/src/Datadog.Trace/Debugger/LiveDebuggerPoc/FlowOperationMetadata.cs`

Native recorder:

- `tracer/src/Datadog.Tracer.Native/debugger_method_rewriter.cpp`
- `tracer/src/Datadog.Tracer.Native/debugger_probes_instrumentation_requester.cpp`
- `tracer/src/Datadog.Tracer.Native/debugger_tokens.cpp`
- `tracer/src/Datadog.Tracer.Native/debugger_environment_variables_util.cpp`

Viewer and sample:

- `tracer/test/test-applications/debugger/Samples.LiveDebuggerPoc.Console/Program.cs`
- `tracer/test/test-applications/debugger/Samples.LiveDebuggerPoc.Console/run-poc.ps1`
- `tracer/test/test-applications/debugger/Samples.LiveDebuggerPoc.Console/run-benchmark-poc.ps1`
- `tracer/test/test-applications/debugger/Samples.LiveDebuggerPoc.Viewer/Program.cs`

Behavior tests:

- `tracer/test/Datadog.Trace.Tests/Debugger/LiveDebuggerPoc/FlowRecorderTests.cs`
- `tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/LiveDebuggerPocNativeTests.cs`

Benchmark output:

- `artifacts/tmp/live-debugger-poc/benchmark/summary.csv`

## What Is Implemented

- Cold method metadata sidecar for native recorder method names, avoiding per-invocation method registration.
- Recorder operation context with trigger/root metadata and optional trace/span correlation. The coarse operation context may flow across awaits, but per-frame context stays thread-static/state-machine-local.
- Inactive gate: callbacks first check whether any operation is active and return before enqueue/trace work when no operation is active, unless broad stress mode is explicitly enabled.
- Operation stop/drain semantics: disposing the operation clears the active gate before capture is flushed.
- Preallocated lock-free append buffers for events, values, and exception details; flush waits for reserved slots to publish before draining.
- Optional shallow value preview (`DD_INTERNAL_DEBUGGER_FLOW_RECORDER_VALUE_PREVIEW=shallow`) that emits bounded flat child records for object fields and safe collection items while keeping the `.dflp` value record fixed-size.
- Conservative native method filters for generated/helper/framework/Datadog/low-value methods.
- Operation budgets for max events, max depth, max duration, and max unique methods.
- Explicit `Truncated` and `Suppressed` marker events.
- Viewer sections for operation context, capture limits, async logical operations, spans, values, exceptions, timeline, and call flow. Expanded shallow-preview values are reconstructed into a visual hierarchy by the viewer after reading flat `.dflp` value records.
- Sample logical/traced root modes.
- Focused managed and native tests.

## Current Validation Commands

Managed tests:

```powershell
dotnet test .\tracer\test\Datadog.Trace.Tests\Datadog.Trace.Tests.csproj -c Release -f net6.0 --filter "FullyQualifiedName~LiveDebuggerPoc"
```

Native build:

```powershell
.\tracer\build.cmd BuildTracerHome
```

Native integration tests:

```powershell
dotnet test .\tracer\test\Datadog.Trace.ClrProfiler.IntegrationTests\Datadog.Trace.ClrProfiler.IntegrationTests.csproj -c Release -f net10.0 --filter "FullyQualifiedName~LiveDebuggerPocNativeTests"
```

Small benchmark matrix:

```powershell
.\tracer\test\test-applications\debugger\Samples.LiveDebuggerPoc.Console\run-benchmark-poc.ps1 -Iterations 2000 -Warmup 200 -SkipBuild
```

Demo with real Datadog intake:

```powershell
cd C:\dev\my-dd
.\stop-agents.ps1
.\start-agents.ps1
.\test-dd.ps1
```

The local Agent helper reads `DD_API_KEY`, `DD_SITE`, and port settings from `C:\dev\my-dd\.env`. Its compose file maps the latest Agent APM intake to `127.0.0.1:${DD_APM_PORT}:8126`; if the validated host port is not `8126`, set `DD_TRACE_AGENT_URL=http://127.0.0.1:<DD_APM_PORT>` before running `run-poc.ps1 -Recording native -RootMode traced -Scenario presentation|exception|async`. Do not copy secrets into this repo.

Validate backend ingestion with Datadog MCP after the viewer prints trace ids: load `datadog/traces`, then call `get_datadog_trace` for each trace id with `only_service_entry_spans=true`. The response should include a `trace_deep_link_url`; the `exception` trace should show `status: error`, `error.*` metadata, and usually Error Tracking issue metadata. This is preferred over local API scripts when `DD_APPLICATION_KEY` is not available.

Latest known validation from the shallow value preview implementation pass:

- Managed FlowRecorder tests: 41 passed.
- Viewer sample build: passed.
- Console benchmark sample build: passed.
- Small preview benchmark matrix: latest 2,000 iterations / 200 warmup run at `artifacts\tmp\live-debugger-poc\benchmark-values-preview-idcache\summary.csv`. Bounded name/id caching removes some repeated string-table work, but shallow preview still adds substantial allocation/latency from extra child records and reflection/boxing. `MAX_COLLECTION_ITEMS` now defaults to `20`, but shallow preview stays opt-in; do not enable preview by default without a deeper hot-path redesign and larger repeated benchmark runs.
- Native LiveDebuggerPoc tests: not rerun in this pass.
- `BuildTracerHome`: not rerun in this pass.

## Important Assumptions And Constraints

- Do not use `AsyncLocal` for per-frame state. The operation gate may use a coarse operation context; keep per-frame context thread-static or state-machine-local.
- Do not reuse Exception Replay deep snapshot serialization on the recorder hot path.
- Keep value capture shallow, bounded, and opt-in.
- Prefer hot-path capture/write efficiency over viewer convenience. Keep hierarchy reconstruction in the viewer unless binary metadata is effectively free.
- Keep broad instrument-all as a benchmark/stress path, not the product promise.
- Operation-scoped inactive overhead matters more than broad active-capture throughput.
- Viewer explanations are part of the product contract. If capture is filtered, suppressed, or truncated, the UI must say so.

## Suggested New Chat Prompt

```text
We are working in dd-trace-dotnet on the Live Debugger Flow Recorder POC.
Start with docs/development/FlowRecorderHandoff.md, then use docs/development/LiveDebuggerPoc.md as the detailed source of truth.
Do not treat old chat transcripts or Cursor plans as canonical unless you need historical reasoning.
Current direction: operation-scoped Flow Recorder capture with cold metadata, operation context, inactive gate, method filters, budgets, viewer markers, and optional trace correlation.
Before changing code, check the implementation and tests listed in the handoff.
```
