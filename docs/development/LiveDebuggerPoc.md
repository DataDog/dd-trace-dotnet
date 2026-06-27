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

### Native Exploration Findings

The next implementation slice should be non-async only. The existing debugger method-probe path already provides method discovery, method metadata index assignment, local signature mutation, return rewriting, and EH protection.

Relevant flow:

1. `CorProfiler::JITCompilationStarted` calls `DebuggerProbesInstrumentationRequester::PerformInstrumentAllIfNeeded`.
2. `PerformInstrumentAllIfNeeded` creates synthetic `MethodProbeDefinition` instances when `DD_INTERNAL_DEBUGGER_INSTRUMENT_ALL` is enabled.
3. `DebuggerRejitPreprocessor` queues methods and `ProbesMetadataTracker::GetInstrumentedMethodIndex` assigns the `methodMetadataIndex` used by managed metadata.
4. `DebuggerMethodRewriter::Rewrite` creates additional locals with `DebuggerTokens::ModifyLocalSigAndInitialize`.
5. Non-async method probes are injected in `DebuggerMethodRewriter::ApplyMethodProbe`.

Smallest native POC slice:

1. Add native environment helper support for `DD_INTERNAL_DEBUGGER_FLOW_RECORDER_ENABLED`.
2. Add `DebuggerTokens` support for:
   - `Datadog.Trace.Debugger.LiveDebuggerPoc.FlowRecorder`
   - `Datadog.Trace.Debugger.LiveDebuggerPoc.FlowRecorderState`
   - `FlowRecorder.Enter(int) : FlowRecorderState`
   - `FlowRecorder.Exit(ref FlowRecorderState, Exception) : void`
3. Increase `DebuggerTokens::GetAdditionalLocalsCount` from `3` to `4`.
4. Append a `FlowRecorderState` local in `DebuggerTokens::AddAdditionalLocals`.
   - Existing local layout:
     - `debuggerLocals[0]`: line probe state
     - `debuggerLocals[1]`: span method state
     - `debuggerLocals[2]`: multi-probe state array
   - Proposed POC local:
     - `debuggerLocals[3]`: flow recorder state
5. In `DebuggerMethodRewriter::Rewrite`, name `debuggerLocals[3]` as `flowRecorderStateIndex`.
6. In `DebuggerMethodRewriter::ApplyMethodProbe`, behind the native flow-recorder env flag and only for non-async method probes:
   - after `UpdateProbeInfo`, load `instrumentedMethodIndex`, call `FlowRecorder.Enter`, and store the result in `flowRecorderStateIndex`;
   - in the existing end-method protected region, load `flowRecorderStateIndex` by address, load `exceptionIndex`, and call `FlowRecorder.Exit`;
   - keep the call inside existing debugger EH-protected instrumentation regions so recorder failures do not break customer methods.
7. Extend the viewer output with method metadata/name mapping once native metadata is available.
8. Re-run concurrency review after native callbacks are wired because rewritten IL changes the recorder lifecycle surface.

Native risks to watch:

- Managed/native signature mismatch will produce invalid IL or runtime method resolution failures.
- `Exit(ref FlowRecorderState, Exception)` must load `ldloca flowRecorderStateIndex` before `ldloc exceptionIndex`.
- The added local must not disturb the final `callTargetState` local used for duplicate-rewrite detection.
- `DD_INTERNAL_DEBUGGER_INSTRUMENT_ALL` currently uses broad matching (`is_exact_signature_match = false`), so keep the POC allowlist strict before enabling automatic recording broadly.
- This does not cover async `MoveNext` rewriting yet.
