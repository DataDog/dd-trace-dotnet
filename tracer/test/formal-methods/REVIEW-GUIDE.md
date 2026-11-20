# Formal-methods diagnostic review guide

This change is intentionally diagnostic. It adds executable models, focused
tests, and environmental fault injection; it does not change production code.
The purpose of the draft PR is to let .NET tracer owners validate whether each
reported schedule represents a real contract violation, an explicitly accepted
best-effort behavior, or a test assumption that should be revised.

## Method used

The search focused on boundaries where work changes owners and another thread
later claims that the work was flushed, retried, committed, or stopped.

For each component, the experiment recorded:

1. The owner before and after each handoff.
2. What happens when a bounded queue or transport rejects the handoff.
3. Which accepted work a flush operation includes.
4. What must be stopped or terminal when close returns.
5. Whether failure leaves work delivered, explicitly dropped, cancelled, or
   retryable.

The common contracts are:

- **Ownership:** every accepted item is in exactly one meaningful state:
  queued, in flight, retry-buffered, delivered, or explicitly dropped.
- **Flush barrier:** successful flush means all work preceding that caller's
  barrier is terminal.
- **Close barrier:** successful close means workers have stopped and preceding
  accepted work is terminal.
- **Fan-out:** every applicable healthy subscriber receives a phase, or its
  incomplete delivery remains retryable.
- **Per-caller acknowledgement:** a caller observes completion of its own
  command, not merely another command of the same type.

### Evidence chain

Each finding has two forms of evidence:

1. A small TLA+ state machine derived from the C# transition order. The runner
   expects the documented invariant violation and rejects an unexpected pass,
   parse failure, or wrong failure type.
2. A real-implementation xUnit replay with controlled queues, callbacks,
   barriers, transports, or loopback sockets. These tests assert the observed
   behavior rather than private state.

Finding 16 additionally uses the real `AgentWriter`, MessagePack serializer,
`Api`, `HttpClientRequestFactory`, and TCP stack. Its raw intake reads the full
HTTP request body and then resets the connection before acknowledgement.

The models validate the stated abstract protocols, not the whole C# program.
The implementation tests close that gap for the recorded schedules, but do not
establish production frequency or prove that longer schedules have no other
behavior.

## Priority-ranked findings

Priority reflects potential data loss, false completion, shutdown safety, and
how ordinary the precondition appears. It is a review recommendation, not a
claim about measured customer frequency. No security vulnerability or P0 issue
is claimed.

### P1: core loss and false completion

| Rank | Finding | Minimal schedule | Implementation replay |
|---:|---|---|---|
| 1 | **#16 Trace transport exceptions bypass retry** | `UPLOAD_COMPLETE -> TCP_RESET -> CALLBACK_THROWS` | `Diagnostic_TraceTransportExceptionBypassesRetryLoop` |
| 2 | **#9 Failed DSM send is not retried** | `ADD -> SEND_FAILURE -> FLUSH` | `Diagnostic_FailedSendIsNotRetried` |
| 3 | **#6 Failed direct-log batch is not retried** | `ENQUEUE -> SEND_FAILURE -> SEND_SUCCESS` | `Diagnostic_FailedBatchIsNotRetried` |
| 4 | **#7 Direct-log flush acknowledges a discarded batch** | `ENQUEUE -> FLUSH -> SEND_FAILURE` | `Diagnostic_FlushAcknowledgesFailedAndDiscardedBatch` |
| 5 | **#10 DSM flush returns with preceding work pending** | `SEND(blocked) -> ADD -> FLUSH -> timeouts` | `Diagnostic_FlushCanReturnWhilePrecedingPointIsStillPending` |
| 6 | **#11 DSM disposal returns with an active send** | `ADD -> SEND(blocked) -> DISPOSE` | `Diagnostic_DisposeReturnsWhileAnAcceptedSendIsStillRunning` |

### P2: configuration consistency, telemetry, and shutdown

| Rank | Finding | Minimal schedule | Implementation replay |
|---:|---|---|---|
| 7 | **#1 RCM apply failure is not retryable** | `POLL(v1) -> APPLY throws -> POLL(v1)` | `Diagnostic_ApplyFailureIsNotRetriedForIdenticalRemoteState` |
| 8 | **#2 RCM removal failure is not retryable** | `APPLY -> REMOVE throws -> POLL(empty)` | `Diagnostic_RemoveFailureIsNotRetriedForIdenticalRemoteState` |
| 9 | **#13 Failed final telemetry push is stranded** | `START -> ADD -> FINAL_FAILURE` | `Diagnostic_FailedFinalTelemetryPushIsNotRetried` |
| 10 | **#14 Failed redacted-log telemetry is not retried** | `ADD_LOG -> SEND_FAILURE -> next interval` | `Diagnostic_FailedRedactedLogPayloadIsNotRetried` |
| 11 | **#12 Second concurrent AgentWriter close returns early** | `PROCESS(blocked) -> CLOSE_A -> CLOSE_B` | `Diagnostic_SecondConcurrentCloseReturnsWhileSerializationIsStillRunning` |
| 12 | **#8 Direct-log flush after disposal never completes** | `DISPOSE -> FLUSH` | `Diagnostic_FlushRequestedAfterDisposeNeverCompletes` |
| 13 | **#5 Direct-log saturation is silent** | `ENQUEUE(first) -> ENQUEUE(second)` | `Diagnostic_QueueSaturationSilentlyRejectsTheNewestLog` |

### P3: narrower RCM schedules

| Rank | Finding | Minimal schedule | Implementation replay |
|---:|---|---|---|
| 14 | **#4 Late RCM subscriber misses cached state** | `APPLY -> SUBSCRIBE -> POLL(same hash)` | `Diagnostic_LateSubscriberDoesNotReceiveAlreadyCachedState` |
| 15 | **#15 Concurrent capability enables lose a bit** | `READ_A -> READ_B -> WRITE_A -> WRITE_B` | `Diagnostic_ConcurrentCapabilityUpdatesLoseAnEnabledBit` |
| 16 | **#3 RCM removal crosses product boundaries** | `APPLY(ASM) -> REMOVE(ASM)` | `Diagnostic_RemovalIsDeliveredToSubscriberForUnrelatedProduct` |

The detailed preconditions, actual results, suspected consequences, and causal
mechanisms are in [`FINDINGS.md`](FINDINGS.md).

## Replaying the evidence

The implementation replays are opt-in:

```bash
dotnet test tracer/test/Datadog.Trace.Tests/Datadog.Trace.Tests.csproj \
  -f net10.0 -m:1 -nodeReuse:false \
  --filter 'Category=FormalMethods|Category=FormalMethodsPhase2'
```

The production-shaped HTTP/TCP subset is:

```bash
dotnet test tracer/test/Datadog.Trace.Tests/Datadog.Trace.Tests.csproj \
  -f net10.0 -m:1 -nodeReuse:false \
  --filter 'Category=EnvironmentalFaultInjection'
```

Run the models with a local `tla2tools.jar`:

```bash
tracer/test/formal-methods/run-models.sh /path/to/tla2tools.jar
```

The last local run on .NET 10/macOS produced:

- 16/16 implementation diagnostic replays passed.
- 4/4 environmental fault-injection scenarios passed, where the known-bug
  scenario passes by reproducing finding 16.
- 16/16 model configurations produced their expected invariant violation.

Cross-runtime and cross-platform replay is still required.

## Questions for component owners

For each finding, reviewers should decide:

1. Is the stated contract the intended product contract?
2. Is the precondition reachable through supported production paths?
3. Is the observed drop or timeout already explicitly accounted for elsewhere?
4. Does a successful `Flush` or `Close` promise delivery, attempted delivery,
   or only local queue drainage?
5. Should the diagnostic remain as a known counterexample, be converted into a
   failing regression test, or be rejected with a documented reason?

No production fix is proposed in this PR so these decisions can be reviewed
independently from remediation design.
