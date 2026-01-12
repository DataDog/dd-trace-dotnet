# MassTransit 8 OpenTelemetry Instrumentation Reference

This document lists all the operations and tags that MassTransit 8 automatically instruments with OpenTelemetry. Use this as a reference for creating manual instrumentation in MassTransit 7.

## ActivitySource

- **Name**: `"MassTransit"`
- **Version**: Matches MassTransit assembly version (e.g., `"8.3.4.0"`)

## Instrumented Operations

MassTransit 8 creates Activities (spans) for the following operations:

### 1. **Message Send Operations**
- **DisplayName**: `urn:message:{MessageType} send`
- **Kind**: `ActivityKind.Producer`
- **Operation**: Sending a message to a specific endpoint using `Send()` or `SendEndpoint.Send()`

### 2. **Message Publish Operations**
- **DisplayName**: `urn:message:{MessageType} send` (same as Send)
- **Kind**: `ActivityKind.Producer`  
- **Operation**: Publishing an event using `Publish()`

### 3. **Message Consume Operations**
- **DisplayName**: `{MessageType} process`
- **Kind**: `ActivityKind.Consumer`
- **Operation**: Processing a message in a consumer's `Consume()` method

### 4. **Saga State Machine Operations**
- **DisplayName**: `{SagaName} process`
- **Kind**: `ActivityKind.Consumer`
- **Operation**: Processing events in a saga state machine

### 5. **Request/Response Operations**
- **Client Side** (Request):
  - **DisplayName**: `urn:message:{RequestType} send`
  - **Kind**: `ActivityKind.Client`
- **Server Side** (Response Handler):
  - **DisplayName**: `{RequestType} process`
  - **Kind**: `ActivityKind.Server`

### 6. **Routing Slip Execution** (if using routing slips)
- Tracks execution flow through routing slip activities

## Standard Semantic Convention Tags

MassTransit follows OpenTelemetry semantic conventions for messaging:

### Core Messaging Tags (Always Present)

| Tag | Example Value | Description |
|-----|---------------|-------------|
| `messaging.operation` | `"send"`, `"receive"`, `"process"` | Type of messaging operation |
| `messaging.system` | `"in-memory"`, `"rabbitmq"`, `"azureservicebus"` | Messaging system being used |
| `messaging.destination.name` | `"urn:message:Samples.MassTransit.Messages:OrderSubmitted"` | Destination queue/topic name |

### MassTransit-Specific Tags

| Tag | Example Value | Description |
|-----|---------------|-------------|
| `messaging.masstransit.message_id` | `"005b0000-b2b3-b20f-2270-08de51f2943f"` | Unique message identifier |
| `messaging.message.conversation_id` | `"005b0000-b2b3-b20f-c03e-08de51f29441"` | Conversation/correlation ID |
| `messaging.masstransit.source_address` | `"loopback://localhost/OrderState"` | Source endpoint address |
| `messaging.masstransit.destination_address` | `"loopback://localhost/urn:message:..."` | Destination endpoint address |
| `messaging.masstransit.message_types` | `"urn:message:Samples.MassTransit.Messages:OrderSubmitted"` | Message type URN |
| `messaging.message.body.size` | `1199` | Message body size in bytes |
| `messaging.masstransit.initiator_id` | `"ea96b0ea-b293-4aa3-929f-fb6b8d06415f"` | Saga/initiator correlation ID |

### Conditional Tags

| Tag | When Present | Example |
|-----|--------------|---------|
| `messaging.masstransit.request_id` | Request/Response | Request correlation ID |
| `messaging.masstransit.response_address` | Request operations | Where to send response |
| `messaging.masstransit.fault_address` | When fault handling configured | Where to send faults |

## Trace Context Propagation

MassTransit automatically propagates trace context using:
- **W3C TraceContext** (default)
- **Baggage** (if configured)

The trace context is embedded in message headers:
- `traceparent` header for W3C TraceContext
- `tracestate` header for additional state
- Activity baggage propagated via message headers

## Activity Relationships

### Parent-Child Relationships

```
RootActivity (Your code)
  └─ Send Activity (Producer, MassTransit)
       └─ Consume Activity (Consumer, MassTransit)
            └─ Send Activity (if consumer publishes)
                 └─ Consume Activity (downstream consumer)
```

### Saga Example

```
OrderSubmitted Send (Producer)
  └─ OrderState process (Saga Consumer)
       └─ ProcessPayment Send (Producer)
            └─ ProcessPayment process (Consumer)
                 └─ PaymentProcessed Send (Producer)
                      └─ OrderState process (Saga Consumer)
```

## Example Activity Output

From our MassTransit 8 sample run:

```
Activity.TraceId:            e1777db22ffe0372e93a406f391cd04b
Activity.SpanId:             74bf3e76b4af187c
Activity.TraceFlags:         Recorded
Activity.DisplayName:        urn:message:Samples.MassTransit.Messages:OrderSubmitted send
Activity.Kind:               Producer
Activity.StartTime:          2026-01-12T15:52:24.4479500Z
Activity.Duration:           00:00:00.0266780
Activity.Tags:
    messaging.operation: send
    messaging.system: in-memory
    messaging.destination.name: urn:message:Samples.MassTransit.Messages:OrderSubmitted
    messaging.masstransit.message_id: 005b0000-b2b3-b20f-2270-08de51f2943f
    messaging.message.conversation_id: 005b0000-b2b3-b20f-c03e-08de51f29441
    messaging.masstransit.source_address: loopback://localhost/...
    messaging.masstransit.destination_address: loopback://localhost/...
    messaging.masstransit.message_types: urn:message:...
    messaging.message.body.size: 1199
StatusCode: Ok
Instrumentation scope (ActivitySource):
    Name: MassTransit
    Version: 8.3.4.0
```

## For MassTransit 7 Manual Instrumentation

To replicate this in MassTransit 7, you would need to:

1. **Create an ActivitySource**: `new ActivitySource("MassTransit")`

2. **Wrap Send/Publish operations**:
   ```csharp
   using var activity = activitySource.StartActivity(
       $"urn:message:{messageType} send",
       ActivityKind.Producer);
   
   activity?.SetTag("messaging.operation", "send");
   activity?.SetTag("messaging.system", "in-memory");
   // ... add other tags
   
   await bus.Publish(message);
   ```

3. **Wrap Consumer operations**:
   ```csharp
   using var activity = activitySource.StartActivity(
       $"{messageType} process",
       ActivityKind.Consumer,
       parentContext); // Extract from message headers
   
   activity?.SetTag("messaging.operation", "process");
   // ... add other tags
   
   // Execute consumer logic
   ```

4. **Propagate trace context**:
   - Inject: Add `traceparent` header when sending
   - Extract: Read `traceparent` header when consuming

## References

- OpenTelemetry Semantic Conventions for Messaging: https://opentelemetry.io/docs/specs/semconv/messaging/
- MassTransit Observability: https://masstransit.io/documentation/configuration/observability
- W3C TraceContext: https://www.w3.org/TR/trace-context/
