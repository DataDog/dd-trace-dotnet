# MassTransit Integration

## Overview

The dd-trace-dotnet tracer supports MassTransit 7 and MassTransit 8 with different instrumentation approaches:

- **MassTransit 8**: Instrumented via `ActivitySource` / OpenTelemetry. MassTransit 8 natively emits Activities, and our `ActivityListener` captures them. Spans are created by MassTransit's own code, so `Activity.Current` flows naturally through all internal operations.
- **MassTransit 7**: Instrumented via `DiagnosticSource` events. Our `MassTransitDiagnosticObserver` listens for diagnostic events and creates Datadog spans in observer callbacks. Additional CallTarget hooks handle context propagation for in-memory transport and error capture.

## Architecture

### MassTransit 8 (ActivitySource)

MassTransit 8 creates Activities via `System.Diagnostics.ActivitySource`. Our tracer's `ActivityListener` picks these up and converts them to Datadog spans. The `MassTransitActivityHandler` enriches the Activities with additional metadata (component tag, resource name, span kind).

Key file: `tracer/src/Datadog.Trace/Activity/Handlers/MassTransitActivityHandler.cs`

### MassTransit 7 (DiagnosticSource)

MassTransit 7 emits `DiagnosticSource` events but uses the older Activity pattern (not ActivitySource). Our `MassTransitDiagnosticObserver` creates Datadog spans manually based on diagnostic events.

Key files:
- `tracer/src/Datadog.Trace/DiagnosticListeners/MassTransitDiagnosticObserver.cs` — Event handler for Send/Receive/Consume/Handle/Saga events
- `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/MassTransit/MassTransitCommon.cs` — Shared span creation, context propagation, and utility methods
- `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/MassTransit/CallTarget/NotifyFaultedIntegration.cs` — Error capture via CallTarget on `BaseReceiveContext.NotifyFaulted`
- `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/MassTransit/CallTarget/InMemoryTransportMessageIntegration.cs` — Context propagation fix for in-memory transport

### Diagnostic Events (MassTransit 7)

MassTransit 7 emits these diagnostic events:
- `MassTransit.Transport.Send` (Start/Stop) — Producer spans
- `MassTransit.Transport.Receive` (Start/Stop) — Receive spans
- `MassTransit.Consumer.Consume` (Start/Stop) — Consumer process spans
- `MassTransit.Consumer.Handle` (Start/Stop) — Handler process spans
- `MassTransit.Saga.RaiseEvent` (Start/Stop) — Saga process spans
- `MassTransit.Activity.Execute/Compensate` (Start/Stop) — Routing slip spans

## Known Test Flakiness

### SQS Receive Span Error on MT8 <= 8.0.7

**Symptom**: The SQS `GettingStarted receive` span intermittently shows `Error: 1` with `TaskCanceledException` / `STATUS_CODE_ERROR` instead of `STATUS_CODE_OK`.

**Root cause**: The sample app uses `TestSignal` to wait for the consumer to finish processing before stopping the bus. However, the signal fires when the **consumer** completes, not when the **receive pipeline** finishes (ack/cleanup). On older MT8 versions (particularly <= 8.0.7), the SQS receive pipeline may still be running when `StopAsync()` is called, causing a cancellation.

**Impact**: The snapshot for 8.0.7 and below may not match consistently due to this race condition. This is accepted as a known flaky behavior for these older versions.

### Service Name Broken on MT8 < 8.0.4 (Minimum Supported Version: 8.0.4)

**Symptom**: The span's `Service` field is the full consumer type name (e.g., `Samples.MassTransit8.Consumers.GettingStartedConsumer`) instead of the expected service name, even when `DD_SERVICE` is set.

**Root cause**: MT8 versions < 8.0.4 set a `service.name` Activity tag to the consumer type name. `OtlpHelpers.UpdateSpanFromActivity` picks this up and unconditionally overrides the span's service name (line 486 in `OtlpHelpers.cs`), ignoring the `DD_SERVICE` env var.

**Resolution**: MassTransit 8.0.4 is the minimum supported version for MT8 instrumentation. Versions 8.0.2 and 8.0.3 are not supported due to this service name issue. The test explicitly sets `DD_SERVICE=Samples.MassTransit8` for consistent naming across supported versions.

**TODO**: Update the CI package version list (`PackageVersionsLatestMinors.g.cs` generation source) to change the minimum MT8 version from `8.0.2` to `8.0.4`.

## Known Limitations (MassTransit 7)

### 1. SQS `SendMessageBatch` Span Not Parented Under MassTransit Send Span

**Symptom**: The AWS SDK `sqs.request` span (SQS.SendMessageBatch) appears in a separate trace instead of as a child of the `masstransit.send` span. This only affects direct `Send()` on the SQS transport. SNS `Publish()` spans are correctly parented.

**Root cause**: MassTransit 7's SQS transport dispatches `SendMessageBatch` on a background batch channel reader thread. Our scope is created in the `DiagnosticSource.Write("Send.Start")` callback via `AsyncLocal`, but the batch reader thread has a separate `ExecutionContext` that doesn't inherit our scope.

**Evidence** (from debug logging):
```
OnSendStart:  ThreadId=24, SpanId=X, ScopeMatch=true    ← scope created
BeforeSend:   ThreadId=12, ActiveScope=null               ← SQS SDK call, different thread, NO scope
OnStop(Send): ThreadId=4,  SpanId=X                       ← scope still active in send pipeline
```

The send pipeline's async continuation (Thread 24 → Thread 4) carries the scope correctly, but the batch reader (Thread 12) never had it.

**Why MassTransit 8 doesn't have this issue**: MT8 creates Activities via `ActivitySource` before entering the pipeline. The Activity is part of MassTransit's own code flow, so it propagates through internal mechanisms including batch channels. Our scope is derived from the Activity, not injected externally.

**Workaround**: None currently. The MassTransit-level spans (send → receive → process) are correctly linked. Only the AWS SDK span is orphaned.

**Potential fix**: A CallTarget hook on MassTransit 7's SQS batch sender (`ClientContextSupervisor` or similar) that captures the Datadog scope at enqueue time and restores it at flush time. This is fragile and version-specific.

### 2. AsyncLocal Scope Propagation via DiagnosticSource Callbacks

**General issue**: Scopes created in `DiagnosticSource.Write()` observer callbacks use `AsyncLocal`, which flows through the caller's async continuations. However, if the library dispatches work to background threads/channels AFTER the diagnostic event fires, those background contexts won't have the scope.

**What works**:
- In-memory transport: Scope flows correctly (synchronous pipeline)
- RabbitMQ transport: Scope flows correctly (async pipeline stays on same context)
- SQS receive/consume: Scope flows correctly (diagnostic events fire on the receive pipeline's context)
- SNS publish: AWS SDK span is correctly parented (SNS.Publish is immediate, no batching)

**What doesn't work**:
- SQS direct send: AWS SDK `SendMessageBatch` span is orphaned (batched on background thread)

### 3. Context Propagation Across Transports

Context propagation (linking send trace to receive trace) works differently per transport:

| Transport | Injection | Extraction | Status |
|-----------|-----------|------------|--------|
| In-memory | `InMemoryTransportMessageIntegration` copies headers to transport message | `TransportHeaders` on ReceiveContext | Works (CallTarget hook needed for MT7 < 7.3.0) |
| RabbitMQ | `SendContext.Headers` → AMQP message properties | `TransportHeaders` on ReceiveContext | Works |
| Amazon SQS (Send) | `SendContext.Headers` → SQS message body (serialized) | `TransportHeaders` on ReceiveContext | Works (MassTransit copies headers to SQS message attributes) |
| Amazon SQS (Publish via SNS) | `SendContext.Headers` → SNS message body | `TransportHeaders` on ReceiveContext | Works (headers survive SNS→SQS delivery) |

If `TransportHeaders` extraction fails, the observer falls back to `ExtractTraceContext()` which reads headers via reflection on the ReceiveContext.

### 4. Error Capture Timing

MassTransit 7 does NOT expose exception information through DiagnosticSource Stop events. Error capture relies on a CallTarget hook on `BaseReceiveContext.NotifyFaulted`:

- The hook uses `Tracer.Instance.ActiveScope` to find the current span
- For RabbitMQ: `NotifyFaulted` fires while the process scope is still active → errors captured on process span
- For in-memory: `NotifyFaulted` fires while the process scope is still active → errors captured on process span
- The hook targets `MassTransit.Context.BaseReceiveContext.NotifyFaulted` with signature `(ConsumeContext<T>, TimeSpan, string, Exception)`

## Test Structure

### Test Files
- `tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/MassTransit7Tests.cs`
- `tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/MassTransit8Tests.cs`

### Sample Apps
- `tracer/test/test-applications/integrations/Samples.MassTransit7/`
- `tracer/test/test-applications/integrations/Samples.MassTransit8/`

### Snapshots
- `MassTransit7Tests.verified.txt` — Full test (all transports, macOS/Linux)
- `MassTransit7TestsWindows.verified.txt` — Windows in-memory only
- `MassTransit8Tests.verified.txt` — Default MT8 version
- `MassTransit8Tests.8_5_8_plus.verified.txt` — MT8 8.5.8+ (additional network/transport tags)
- `MassTransit8TestsWindows.verified.txt` — Windows in-memory only

### Snapshot Scrubbers

The tests apply several regex scrubbers for environment-specific values:
- **Bus endpoint names**: Dynamic names like `HOSTNAME_SamplesMassTransit8_bus_xxx`
- **Queue names**: Dynamic queue suffixes
- **Saga IDs/GUIDs**: All GUIDs normalized
- **RabbitMQ host**: `rabbitmq://[host]/` normalized
- **Payload sizes**: `messaging.message.payload_size_bytes` normalized
- **Network tags**: `client.address`, `server.address`, etc. (vary by environment)
- **OTEL library version**: Varies with MT package version
- **Error stacks**: Scrubbed to avoid .NET version format differences
- **Events**: OTEL events contain timestamps, scrubbed to `[scrubbed]`
- **messaging.message.body.size**: Optional tag, removed (only in some MT versions)

### Span Ordering

Spans are sorted deterministically for snapshot comparison:
1. `Resource.Split(' ')[0]` — Group by destination name
2. `messaging.operation` — send (0) → receive (1) → process (2)
3. `messaging.masstransit.destination_address` — Tiebreaker for spans with same resource and operation (e.g., SQS direct vs SQS publish receive spans)

### Running Tests Locally

```bash
# MassTransit 7 (requires Docker for RabbitMQ + LocalStack)
./tracer/build.sh BuildAndRunOsxIntegrationTests -SampleName Samples.MassTransit7 -filter MassTransit7 -Framework net8.0

# MassTransit 8
./tracer/build.sh BuildAndRunOsxIntegrationTests -SampleName Samples.MassTransit8 -filter MassTransit8 -Framework net8.0
```

To test a specific package version, change `<ApiVersion>` in the sample's `.csproj`:
```xml
<ApiVersion Condition="'$(ApiVersion)' == ''">7.3.1</ApiVersion>
```

### Span Metadata Rules

Both V0 and V1 span metadata rules define expected tags for MassTransit spans:
- `tracer/test/Datadog.Trace.TestHelpers/SpanMetadataV0Rules.cs` — `IsMassTransitV0`
- `tracer/test/Datadog.Trace.TestHelpers/SpanMetadataV1Rules.cs` — `IsMassTransitV1`

Optional tags include network tags, message metadata, saga state, and OTEL fields.
