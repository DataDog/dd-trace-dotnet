# MassTransit 7 Methods to Instrument

This document lists all the key methods in MassTransit 7 that should be instrumented to achieve similar observability to MassTransit 8's built-in OpenTelemetry support.

## IBus Interface Methods

### Publishing Messages (Producer Operations)

| Interface | Method Signature | Activity Kind | Notes |
|-----------|------------------|---------------|-------|
| `IBus` | `Task Publish<T>(T message, CancellationToken cancellationToken = default)` | Producer | Publish event to all subscribers |
| `IBus` | `Task Publish<T>(T message, IPipe<PublishContext<T>> publishPipe, CancellationToken cancellationToken = default)` | Producer | Publish with pipeline configuration |
| `IBus` | `Task Publish<T>(T message, Action<PublishContext<T>> publishPipe, CancellationToken cancellationToken = default)` | Producer | Publish with context configuration |
| `IBus` | `Task Publish(object message, CancellationToken cancellationToken = default)` | Producer | Publish object (non-generic) |
| `IBus` | `Task Publish(object message, Type messageType, CancellationToken cancellationToken = default)` | Producer | Publish with explicit type |
| `IPublishEndpoint` | `Task Publish<T>(T message, CancellationToken cancellationToken = default)` | Producer | Same as IBus.Publish |

### Sending Messages (Producer Operations)

| Interface | Method Signature | Activity Kind | Notes |
|-----------|------------------|---------------|-------|
| `ISendEndpoint` | `Task Send<T>(T message, CancellationToken cancellationToken = default)` | Producer | Send to specific endpoint |
| `ISendEndpoint` | `Task Send<T>(T message, IPipe<SendContext<T>> pipe, CancellationToken cancellationToken = default)` | Producer | Send with pipeline |
| `ISendEndpoint` | `Task Send(object message, CancellationToken cancellationToken = default)` | Producer | Send object (non-generic) |
| `ISendEndpoint` | `Task Send(object message, Type messageType, CancellationToken cancellationToken = default)` | Producer | Send with explicit type |
| `ISendEndpointProvider` | `Task<ISendEndpoint> GetSendEndpoint(Uri address)` | N/A | Get endpoint for sending |
| `IBus` | `Task Send<T>(Uri destinationAddress, T message, CancellationToken cancellationToken = default)` | Producer | Send directly via bus |

### Request/Response (Client/Server Operations)

| Interface | Method Signature | Activity Kind | Notes |
|-----------|------------------|---------------|-------|
| `IRequestClient<TRequest>` | `Task<Response<TResponse>> GetResponse<TResponse>(TRequest message, CancellationToken cancellationToken = default, RequestTimeout timeout = default)` | Client | Request/Response pattern |
| `IRequestClient<TRequest>` | `RequestHandle<TRequest> Create(TRequest message, CancellationToken cancellationToken = default, RequestTimeout timeout = default)` | Client | Create request handle |
| `ConsumeContext` | `Task RespondAsync<T>(T message)` | Server | Send response in consumer |
| `ConsumeContext` | `Task RespondAsync<T>(object values)` | Server | Send response with object initializer |

### Consumer Methods (Consumer Operations)

| Interface | Method Signature | Activity Kind | Notes |
|-----------|------------------|---------------|-------|
| `IConsumer<T>` | `Task Consume(ConsumeContext<T> context)` | Consumer | Process consumed message |
| `ConsumeContext<T>` | `T Message { get; }` | N/A | Access message being consumed |
| `ConsumeContext` | `Task Publish<T>(T message, CancellationToken cancellationToken = default)` | Producer | Publish from within consumer |
| `ConsumeContext` | `Task Send<T>(Uri destinationAddress, T message, CancellationToken cancellationToken = default)` | Producer | Send from within consumer |

### Saga State Machine Methods

| Interface | Method Signature | Activity Kind | Notes |
|-----------|------------------|---------------|-------|
| `MassTransitStateMachine<TInstance>` | `Event<TData>` property declarations | N/A | Define events |
| `State` | State property declarations | N/A | Define states |
| `Initially(...)` | Configure initial state behavior | Consumer | Entry point to saga |
| `During(state, When(event)...)` | Configure state transitions | Consumer | Process events in states |
| `.Then(context => ...)` | Execute actions | N/A | Action within saga |
| `.TransitionTo(state)` | Change state | N/A | State transition |
| `.Publish(context => message)` | Publish from saga | Producer | Saga publishing |
| `.Finalize()` | Complete saga | N/A | Mark saga complete |

## Context Properties to Propagate

### ConsumeContext Properties (for trace context)

| Property | Type | Purpose |
|----------|------|---------|
| `MessageId` | `Guid?` | Unique message identifier |
| `CorrelationId` | `Guid?` | Conversation/correlation ID |
| `ConversationId` | `Guid?` | Conversation tracking |
| `InitiatorId` | `Guid?` | Saga/initiator ID |
| `RequestId` | `Guid?` | Request correlation (req/resp) |
| `SourceAddress` | `Uri` | Where message came from |
| `DestinationAddress` | `Uri` | Where message is going |
| `ResponseAddress` | `Uri` | Where to send response |
| `FaultAddress` | `Uri` | Where to send faults |
| `Headers` | `Headers` | Message headers (for traceparent) |

### SendContext Properties

| Property | Type | Purpose |
|----------|------|---------|
| `MessageId` | `Guid` | Message ID |
| `CorrelationId` | `Guid?` | Correlation |
| `ConversationId` | `Guid?` | Conversation |
| `DestinationAddress` | `Uri` | Destination |
| `SourceAddress` | `Uri` | Source |
| `Headers` | `SendHeaders` | Headers for propagation |

### PublishContext Properties

| Property | Type | Purpose |
|----------|------|---------|
| `MessageId` | `Guid` | Message ID |
| `CorrelationId` | `Guid?` | Correlation |
| `ConversationId` | `Guid?` | Conversation |
| `Headers` | `PublishHeaders` | Headers for propagation |

## Instrumentation Points Summary

### For Producer Operations (Send/Publish)
```csharp
// Before: await bus.Publish(message);
using var activity = activitySource.StartActivity(
    $"urn:message:{typeof(T).Name} send",
    ActivityKind.Producer);

activity?.SetTag("messaging.operation", "send");
activity?.SetTag("messaging.system", transport); // e.g., "in-memory"
activity?.SetTag("messaging.destination.name", $"urn:message:{typeof(T).FullName}");
activity?.SetTag("messaging.masstransit.message_id", messageId.ToString());
// ... set other tags

// Inject trace context into message headers
InjectTraceContext(activity, messageHeaders);

await bus.Publish(message);
```

### For Consumer Operations (Consume)
```csharp
// In IConsumer<T>.Consume(ConsumeContext<T> context)
// Extract trace context from message headers
var parentContext = ExtractTraceContext(context.Headers);

using var activity = activitySource.StartActivity(
    $"{typeof(T).Name} process",
    ActivityKind.Consumer,
    parentContext);

activity?.SetTag("messaging.operation", "process");
activity?.SetTag("messaging.system", transport);
activity?.SetTag("messaging.masstransit.message_id", context.MessageId?.ToString());
// ... set other tags

// Execute consumer logic
await ProcessMessage(context);
```

### For Request/Response Operations
```csharp
// Client side (Request)
using var activity = activitySource.StartActivity(
    $"urn:message:{typeof(TRequest).Name} send",
    ActivityKind.Client);

// Server side (Response Handler in Consumer)
using var activity = activitySource.StartActivity(
    $"{typeof(TRequest).Name} process",
    ActivityKind.Server,
    parentContext);
```

## Key Interfaces for Instrumentation

| Interface | Purpose | When to Instrument |
|-----------|---------|-------------------|
| `IBus` | Main bus interface | Publish/Send operations |
| `IPublishEndpoint` | Publishing interface | Publish operations |
| `ISendEndpoint` | Sending interface | Send operations |
| `IRequestClient<T>` | Request/Response client | Request operations |
| `IConsumer<T>` | Message consumer | Consume operations |
| `ConsumeContext<T>` | Consumer context | Access message & publish/send from consumer |
| `MassTransitStateMachine<T>` | Saga state machine | Saga event processing |

## Header Names for Trace Context

| Header Key | Value Format | Purpose |
|------------|-------------|---------|
| `traceparent` | `00-{trace-id}-{span-id}-{flags}` | W3C TraceContext |
| `tracestate` | Vendor-specific | W3C TraceState |
| Custom baggage headers | Key-value pairs | Propagate baggage |

## Example: Wrapping Methods

```csharp
// Decorator pattern for IBus
public class InstrumentedBus : IBus
{
    private readonly IBus _innerBus;
    private readonly ActivitySource _activitySource;
    
    public async Task Publish<T>(T message, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity(
            $"urn:message:{typeof(T).Name} send",
            ActivityKind.Producer);
        
        // Set tags
        activity?.SetTag("messaging.operation", "send");
        // ... more tags
        
        // Call inner bus
        await _innerBus.Publish(message, cancellationToken);
    }
    
    // Implement other methods...
}
```

## Recommended Approach for MassTransit 7

Instead of manually instrumenting all these methods, use the **OpenTelemetry.Instrumentation.MassTransit** package:

```bash
dotnet add package OpenTelemetry.Instrumentation.MassTransit
```

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .AddSource("MassTransit")
            .AddMassTransitInstrumentation() // Automatically instruments all operations
            .AddConsoleExporter();
    });
```

This package uses DiagnosticSource to automatically instrument MassTransit 7 operations without manual intervention.
