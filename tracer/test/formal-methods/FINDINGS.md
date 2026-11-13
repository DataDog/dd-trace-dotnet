# Formal-methods findings

Baseline: `e9c273312d2c` on 2026-11-01. Production code was not changed.

The 16 findings below are confirmed implementation behaviors under deterministic,
controlled preconditions. The production consequences are hypotheses unless stated
otherwise; these experiments do not establish customer frequency.

## 1. RCM apply failure is not retryable

- Component: `RcmSubscriptionManager`
- Contract: Fan-out/ownership; a failed listener delivery remains retryable.
- Preconditions: One subscribed listener throws while applying a new config.
- Minimal schedule: `POLL(config v1) -> listener throws -> POLL(same hash)`
- Expected: The second poll invokes the failed listener again.
- Actual: The config was cached before delivery, and hash deduplication suppresses the replay.
- Failure frequency: Deterministic, 1/1 model and implementation replays.
- Production consequence: Suspected stale product configuration after a transient listener exception.
- Deterministic replay: `Diagnostic_ApplyFailureIsNotRetriedForIdenticalRemoteState`.
- Distinct causal mechanism: `_appliedConfigurations` is populated before listener success; deduplication ignores delivery state.

## 2. RCM remove failure is not retryable

- Component: `RcmSubscriptionManager`
- Contract: Fan-out/ownership; a failed removal remains retryable.
- Preconditions: A previously applied config disappears and its listener throws during removal.
- Minimal schedule: `APPLY(config) -> POLL(empty) -> remove throws -> POLL(empty)`
- Expected: The second empty poll retries removal.
- Actual: The cache entry is deleted before callback delivery, so no later removal is generated.
- Failure frequency: Deterministic, 1/1 model and implementation replays.
- Production consequence: Suspected stale local configuration after a transient removal exception.
- Deterministic replay: `Diagnostic_RemoveFailureIsNotRetriedForIdenticalRemoteState`.
- Distinct causal mechanism: Destructive cache removal precedes listener acknowledgement.

## 3. RCM removal crosses product subscription boundaries

- Component: `RcmSubscriptionManager`
- Contract: Fan-out isolation; listeners receive only phases for subscribed products.
- Preconditions: An ASM config is removed while a Live Debugging listener is also registered.
- Minimal schedule: `APPLY(ASM) -> REMOVE(ASM)`
- Expected: Only ASM subscribers receive the removal.
- Actual: The Live Debugging listener receives a removal dictionary containing `ASM_FEATURES`.
- Failure frequency: Deterministic, 1/1 model and implementation replays.
- Production consequence: Suspected spurious listener work or errors; impact depends on listener validation.
- Deterministic replay: `Diagnostic_RemovalIsDeliveredToSubscriberForUnrelatedProduct`.
- Distinct causal mechanism: Added configs are filtered per subscription; `removedConfigsByProduct` is passed unfiltered.

## 4. Late RCM subscriber misses current cached state

- Component: `RcmSubscriptionManager`
- Contract: Fan-out; each healthy listener receives current state or remains retryable.
- Preconditions: A second listener for an already requested product registers after a config was applied.
- Minimal schedule: `APPLY(config) -> SUBSCRIBE(second) -> POLL(same hash)`
- Expected: The new listener receives the current config.
- Actual: Global hash deduplication suppresses delivery to the new listener.
- Failure frequency: Deterministic, 1/1 model and implementation replays.
- Production consequence: Suspected missing initialization when product listeners are registered or replaced late.
- Deterministic replay: `Diagnostic_LateSubscriberDoesNotReceiveAlreadyCachedState`.
- Distinct causal mechanism: Deduplication state is global per config rather than per subscriber.

## 5. Direct-log queue saturation is silent

- Component: `BatchingSink<T>`
- Contract: Ownership; rejected items are explicitly rejected or counted as dropped.
- Preconditions: Queue limit is one and the consumer has not drained it.
- Minimal schedule: `ENQUEUE(first) -> ENQUEUE(second)`
- Expected: Both items are owned, or the second rejection is observable.
- Actual: `TryEnqueue(second)` returns false internally, but `EnqueueLog` discards the result.
- Failure frequency: Deterministic, 1/1 model and implementation replays.
- Production consequence: Confirmed silent log loss under saturation; customer likelihood was not measured.
- Deterministic replay: `Diagnostic_QueueSaturationSilentlyRejectsTheNewestLog`.
- Distinct causal mechanism: Unchecked bounded-queue offer.

## 6. Failed direct-log batch is not retried

- Component: `BatchingSink<T>`
- Contract: Ownership; a failed send remains retry-buffered or is explicitly dropped.
- Preconditions: `EmitBatch` returns false once, then would succeed.
- Minimal schedule: `ENQUEUE(log) -> SEND_FAILURE -> SEND_SUCCESS`
- Expected: The second iteration retries the log.
- Actual: `_waitingBatch` is cleared in `finally`; the second iteration has nothing to send.
- Failure frequency: Deterministic, 1/1 model and implementation replays.
- Production consequence: Confirmed loss after an intake failure; production frequency depends on transport failures.
- Deterministic replay: `Diagnostic_FailedBatchIsNotRetried`.
- Distinct causal mechanism: Retry ownership is cleared on both success and failure.

## 7. Direct-log flush acknowledges a failed, discarded batch

- Component: `BatchingSink<T>`
- Contract: Flush barrier; success means preceding accepted work is terminal.
- Preconditions: A log precedes a flush request and `EmitBatch` returns false.
- Minimal schedule: `ENQUEUE(log) -> FLUSH -> SEND_FAILURE`
- Expected: Flush fails, remains pending, or the log is explicitly terminated.
- Actual: The flush task completes successfully after the failed batch is discarded.
- Failure frequency: Deterministic, 1/1 model and implementation replays.
- Production consequence: A caller can receive a false durability/flush acknowledgement.
- Deterministic replay: `Diagnostic_FlushAcknowledgesFailedAndDiscardedBatch`.
- Distinct causal mechanism: Flush completion sources are completed unconditionally after `FlushLogs`.

## 8. Direct-log flush after disposal never completes

- Component: `BatchingSink<T>`
- Contract: Per-caller acknowledgement; every flush returns or explicitly rejects.
- Preconditions: The background loop has completed disposal.
- Minimal schedule: `DISPOSE -> FLUSH`
- Expected: Flush completes immediately or reports closed state.
- Actual: A completion source is queued after its only consumer has exited.
- Failure frequency: Deterministic, 1/1 model and implementation replays.
- Production consequence: Suspected shutdown race can hang a late flush caller indefinitely.
- Deterministic replay: `Diagnostic_FlushRequestedAfterDisposeNeverCompletes`.
- Distinct causal mechanism: Post-terminal requests are accepted without a terminal fast path.

## 9. Failed Data Streams send is not retried

- Component: `DataStreamsWriter`
- Contract: Ownership; a failed send remains retry-buffered or is explicitly dropped.
- Preconditions: DSM is supported and the intake returns `false` for a transaction payload.
- Minimal schedule: `ADD(transaction) -> FLUSH/SEND_FAILURE -> FLUSH`
- Expected: The second flush retries the transaction.
- Actual: Aggregator serialization removes the data before the send result; the second flush sends nothing.
- Failure frequency: Deterministic, 1/1 model and implementation replays.
- Production consequence: Confirmed DSM data loss on a failed intake call; production failure rate was not measured.
- Deterministic replay: `Diagnostic_FailedSendIsNotRetried`.
- Distinct causal mechanism: Destructive serialization occurs before transport success.

## 10. Data Streams flush can return before its preceding point is processed

- Component: `DataStreamsWriter`
- Contract: Flush barrier; preceding accepted work is terminal when flush returns.
- Preconditions: One send holds `_flushSemaphore`; a second transaction and flush arrive.
- Minimal schedule: `SEND(blocked) -> ADD(second) -> FLUSH -> two semaphore timeouts`
- Expected: The second flush remains pending or reports failure.
- Actual: It returns normally while the second transaction is still pending and only one payload was attempted.
- Failure frequency: Deterministic, 1/1 model and implementation replays; approximately two seconds by design.
- Production consequence: Suspected false flush acknowledgement during a slow or hung intake call.
- Deterministic replay: `Diagnostic_FlushCanReturnWhilePrecedingPointIsStillPending`.
- Distinct causal mechanism: Both semaphore timeouts are logged and converted to successful task completion.

## 11. Data Streams disposal returns while an accepted send is active

- Component: `DataStreamsWriter`
- Contract: Close barrier; no worker/send is active when close returns.
- Preconditions: The intake send remains blocked beyond the shutdown and semaphore timeouts.
- Minimal schedule: `ADD(transaction) -> FLUSH/SEND(blocked) -> DISPOSE`
- Expected: Dispose waits for termination or reports incomplete shutdown.
- Actual: `DisposeAsync` returns while the accepted send task is still incomplete.
- Failure frequency: Deterministic, 1/1 model and implementation replays; approximately three seconds by design.
- Production consequence: Suspected use-after-dispose/resource-lifetime races during process shutdown; frequency unknown.
- Deterministic replay: `Diagnostic_DisposeReturnsWhileAnAcceptedSendIsStillRunning`.
- Distinct causal mechanism: Shutdown timeout is followed by two more best-effort timeout paths, then normal return.

## Positive evidence: trace writer

The trace-writer flush audit did not reproduce the Java shared-flush-counter bug.
The .NET `AgentWriter` places a per-call watermark in the serialization queue and
uses a serialized full-buffer flush pass. Existing concurrent-flush and two-buffer
tests cover the primary schedules. This is positive bounded evidence, not a proof
that all publication/shutdown races are absent.

## 12. A second AgentWriter close returns before the first close finishes

- Component: `AgentWriter`
- Contract: Close barrier/per-caller acknowledgement; every successful close caller observes a stopped worker and terminal preceding work.
- Preconditions: Trace processing is blocked after accepting a trace, and two callers invoke `FlushAndCloseAsync` concurrently.
- Minimal schedule: `PROCESS_TRACE(blocked) -> CLOSE_A -> CLOSE_B`
- Expected: Both close tasks wait for the shared close operation, or the second explicitly reports that shutdown remains in progress.
- Actual: `CLOSE_B` returns normally while trace processing and `CLOSE_A` remain blocked.
- Failure frequency: Deterministic, 1/1 model and implementation replays.
- Production consequence: Suspected premature resource teardown by a concurrent shutdown caller; customer frequency was not measured.
- Deterministic replay: `Diagnostic_SecondConcurrentCloseReturnsWhileSerializationIsStillRunning`.
- Distinct causal mechanism: The losing `_processExit.TrySetResult(true)` caller returns instead of awaiting the winning caller's completion.

## 13. A failed final telemetry push is stranded after controller shutdown

- Component: `TelemetryController`
- Contract: Close/ownership; accepted telemetry is delivered, explicitly dropped, or retryable when close returns.
- Preconditions: Initial telemetry succeeds, new configuration telemetry is queued, and the final shutdown push returns a transient failure.
- Minimal schedule: `START_SUCCESS -> ADD_CONFIGURATION -> DISPOSE/FINAL_FAILURE`
- Expected: Shutdown retries the final payload, reports incomplete delivery, or explicitly terminates it.
- Actual: The failed payload is retained in the aggregator, but the push loop exits and a subsequent successful transport result is never consumed.
- Failure frequency: Deterministic, 1/1 model and implementation replays.
- Production consequence: Confirmed telemetry loss when the final process-shutdown request fails; production failure frequency is unknown.
- Deterministic replay: `Diagnostic_FailedFinalTelemetryPushIsNotRetried`.
- Distinct causal mechanism: Failure ownership is restored to an aggregator after the last possible loop iteration.

## 14. Failed redacted-log telemetry is destructively drained and never retried

- Component: `TelemetryController` / `RedactedErrorLogCollector`
- Contract: Ownership; a failed telemetry-log request remains retry-buffered or is explicitly dropped.
- Preconditions: A redacted error log is queued and the log-specific telemetry request returns a transient failure while other telemetry requests succeed.
- Minimal schedule: `ADD_LOG -> PERIODIC_SEND_FAILURE -> PERIODIC_SEND_SUCCESS`
- Expected: A later iteration retries the log payload.
- Actual: The collector is empty after the failed request and later iterations contain no log payload.
- Failure frequency: Deterministic, 1/1 model and implementation replays.
- Production consequence: Confirmed diagnostic-log loss after an intake failure; customer frequency depends on telemetry transport failures.
- Deterministic replay: `Diagnostic_FailedRedactedLogPayloadIsNotRetried`.
- Distinct causal mechanism: `GetLogs` destructively drains the collector and the log push result is ignored rather than entering retry ownership.

## 15. Concurrent RCM capability enables lose an enabled bit

- Component: `RcmSubscriptionManager`
- Contract: Concurrent update ownership; every completed capability enable remains represented until explicitly disabled.
- Preconditions: Two threads enable different capabilities concurrently.
- Minimal schedule: `READ_A(empty) -> READ_B(empty) -> WRITE_A(A) -> WRITE_B(B)`
- Expected: The resulting capability set contains both A and B.
- Actual: At least one completed enable is overwritten; the resulting set contains only one of the two bits.
- Failure frequency: Observed within 20,000 synchronized rounds in the implementation replay; deterministic in the read/write model.
- Production consequence: Suspected under-reporting of supported RCM capabilities when products initialize concurrently; real initialization overlap was not measured.
- Deterministic replay: `Diagnostic_ConcurrentCapabilityUpdatesLoseAnEnabledBit` (bounded native stress with barriers) and `RcmConcurrentCapabilities.cfg` (exact schedule).
- Distinct causal mechanism: `SetCapability` performs an unsynchronized `BigInteger` read-modify-write, so concurrent updates overwrite one another.

## 16. Trace transport exceptions bypass the configured retry loop

- Component: `Api.SendWithRetry` / HTTP trace transport
- Contract: Retry ownership; a retryable transport failure consumes the configured retry attempts before the payload is explicitly dropped.
- Preconditions: The intake receives a complete trace request and resets the TCP connection before sending any acknowledgement.
- Minimal schedule: `UPLOAD_COMPLETE -> CONNECTION_RESET -> SEND_CALLBACK_THROWS`
- Expected: The trace API creates another request and retries the identical payload, up to its five-attempt limit.
- Actual: Only one request is created and the writer drops the payload after the first exception.
- Failure frequency: Deterministic, 1/1 raw-TCP implementation and model replays on .NET 10/macOS.
- Production consequence: Confirmed trace loss for a transport exception that the retry loop is structured and documented to retry; customer failure frequency is unknown. If the Agent committed the first upload before the reset, retry would instead risk duplication, so intake idempotency remains a separate contract question.
- Deterministic replay: `Diagnostic_TraceTransportExceptionBypassesRetryLoop` and `TraceTransportException.cfg`.
- Distinct causal mechanism: `SendWithRetry` initializes `success` to `Failed_DontRetry`; when the send callback throws, the value remains terminal and the loop returns instead of retrying.
