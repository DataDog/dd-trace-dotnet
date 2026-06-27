# Live Debugger POC Work Log

## Current Branch

- Branch: `innovation/live-flow-recorder-poc`
- Goal: prove a low-overhead, local-only flow recorder for an always-on/contextual debugger POC before wiring native ReJIT/IL injection.

## Current Status

Implemented a managed-only recorder slice:

- `FlowRecorder` exposes lightweight `Enter(int methodMetadataIndex)` and `Exit(ref FlowRecorderState, Exception?)` callbacks for future IL injection.
- Events are fixed numeric records: kind, timestamp, method metadata index, flow id, frame id, parent frame id, depth, managed thread id, trace id, root span id, active span id, and exception type id.
- Flow context is independent from tracing and stored in `AsyncLocal`.
- Trace/span correlation enriches events when a Datadog scope is active.
- Events are stored in a bounded in-memory queue with dropped-event counting.
- Flush writes a compact local `.dflp` binary file. No backend upload.
- A `net10.0` console sample manually calls the recorder callbacks.
- A `net10.0` viewer reads `.dflp` files and renders a simple call tree/timeline.

## Concurrency Fixes Already Applied

The first review found three recorder lifecycle risks, all fixed in the current branch:

- `AsyncLocal` state is immutable per enter/exit, so parallel async fan-out does not mutate a shared `FlowContext`.
- Recorder state carries a generation id, so exits from a stale reset/reconfigure session do not write into the new sink.
- Recorder runtime state is published as an immutable `{generation, settings, sink}` session snapshot, so `Enter`/`Exit` enqueue to a coherent sink/session boundary.
- `Flush`, `Reset`, test drain, and test reconfiguration share the recorder lifecycle lock to avoid racing sink swaps.

## Verification

Focused unit tests:

```powershell
dotnet test .\tracer\test\Datadog.Trace.Tests\Datadog.Trace.Tests.csproj -c Release -f net6.0 --filter "FullyQualifiedName~LiveDebuggerPoc"
```

Latest result:

- 9 passed, 0 failed, 0 skipped.

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
dotnet run --project .\tracer\test\test-applications\debugger\Samples.LiveDebuggerPoc.Console\Samples.LiveDebuggerPoc.Console.csproj -c Release -f net10.0 -- --scenario checkout --output $capture
dotnet run --project .\tracer\test\test-applications\debugger\Samples.LiveDebuggerPoc.Viewer\Samples.LiveDebuggerPoc.Viewer.csproj -c Release -f net10.0 -- $capture
```

Example viewer output:

```text
Flow 1 (7.436 ms, 10 events)
  Trace: 6a3fb1fe00000000dbd56e8d2f6de31a, root span: 9779571572332692067, active span: 9779571572332692067
  - method#100 frame=1 duration=7.436ms
    - method#101 frame=2 duration=0.272ms
    - method#102 frame=3 duration=5.718ms
      - method#103 frame=4 duration=0.002ms
    - method#104 frame=5 duration=0.002ms
```

## Known Limitations

- Native ReJIT/IL injection is not wired yet.
- The console sample manually calls `FlowRecorder.Enter`/`Exit`.
- Viewer output currently shows method metadata ids, not resolved method names.
- The recorder is Windows-gated when initialized from environment. Tests use `ConfigureForTesting`.
- No argument/local capture is implemented. This is intentional for the first hot-path slice.
- `Reset`/`Flush` do not quiesce active recording. They are acceptable for the local POC loop, but a production path would need an explicit lifecycle/stop-the-world boundary or drain protocol.

## Next Work

1. Explore native debugger method rewriting extension points:
   - `tracer/src/Datadog.Tracer.Native/debugger_probes_instrumentation_requester.cpp`
   - `tracer/src/Datadog.Tracer.Native/debugger_rejit_preprocessor.cpp`
   - `tracer/src/Datadog.Tracer.Native/debugger_method_rewriter.cpp`
   - `tracer/src/Datadog.Tracer.Native/debugger_members.*`
2. Add native env/config gates:
   - `DD_INTERNAL_DEBUGGER_FLOW_RECORDER_ENABLED`
   - assembly/type allowlist for the POC
3. Add token/member references for `Datadog.Trace.Debugger.LiveDebuggerPoc.FlowRecorder`.
4. Inject enter/exit calls into a tightly scoped sample method set.
5. Extend viewer output with method metadata/name mapping when native metadata is available.
6. Re-run concurrency review after native callbacks are wired because rewritten IL changes the lifecycle surface.
