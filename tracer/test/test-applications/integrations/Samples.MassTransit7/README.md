# Samples.MassTransit7

Test driver app for the MassTransit 7.x auto-instrumentation integration tests.

## What it exercises

Per transport (publish + send round-trip with a single consumer):

- **In-memory** — `loopback://` transport, always runs.
- **RabbitMQ** — requires a broker reachable at `RABBITMQ_HOST` (default `localhost`).
- **Amazon SQS** — requires LocalStack (or AWS) reachable at `LOCALSTACK_ENDPOINT` (default `http://localhost:4566`).

In-memory–only scenarios (always run):

- Saga state machine (`Sagas/OrderStateMachine.cs`) — `OrderSubmitted` → `OrderAccepted` → `OrderCompleted`.
- Consumer exception (`FailingConsumer`) — produces a `Fault<>`.
- Handler exception — inline `e.Handler<>` that throws.
- Saga exception — `OrderFailed` event triggers a saga throw.

## Running

```bash
# default: every available transport, then in-memory-only scenarios
dotnet run

# pick a transport explicitly
MASSTRANSIT_TRANSPORT=inmemory   dotnet run
MASSTRANSIT_TRANSPORT=rabbitmq   dotnet run   # also: RABBITMQ_HOST
MASSTRANSIT_TRANSPORT=amazonsqs  dotnet run   # also: LOCALSTACK_ENDPOINT, AWS_REGION
```

`MASSTRANSIT_INMEMORY_ONLY=true` is accepted as a back-compat alias for `MASSTRANSIT_TRANSPORT=inmemory`.

When run under the Datadog tracer, each scenario calls `SampleHelpers.ForceTracerFlushAsync()` so spans are written before the bus stops.

## Layout

- `Program.cs` — top-level scenario dispatcher.
- `Sagas/` — `OrderStateMachine` + `OrderState` (used by the saga and saga-exception scenarios).
- `ConsumeSignalObserver.cs` — `IConsumeObserver` that signals `TestSignal` on `Fault<TMessage>` so the driver can wait for failure events.
- Shared message types, consumers, and `TestSignal` come from `../Samples.MassTransit.Shared/` (linked via the csproj).

## Where the instrumentation lives

`tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/MassTransit/` and the `MassTransitDiagnosticObserver` under `tracer/src/Datadog.Trace/DiagnosticListeners/`. Integration tests are in `tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/MassTransit7Tests.cs`.
