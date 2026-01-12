# MassTransit 8 OpenTelemetry Instrumentation - Quick Reference Tables

## Instrumented Operations

| Operation | Activity DisplayName | Activity Kind | Trigger |
|-----------|---------------------|---------------|---------|
| Message Send | `urn:message:{MessageType} send` | Producer | `Send()` or `SendEndpoint.Send()` |
| Message Publish | `urn:message:{MessageType} send` | Producer | `Publish()` |
| Message Consume | `{MessageType} process` | Consumer | Consumer's `Consume()` method |
| Saga Process | `{SagaName} process` | Consumer | Saga state machine event handling |
| Request (Client) | `urn:message:{RequestType} send` | Client | Request/Response client call |
| Response Handler | `{RequestType} process` | Server | Processing request in consumer |

## Standard OpenTelemetry Tags

### Core Messaging Tags (Always Present)

| Tag Name | Possible Values | Description |
|----------|-----------------|-------------|
| `messaging.operation` | `send`, `receive`, `process` | Type of messaging operation |
| `messaging.system` | `in-memory`, `rabbitmq`, `azureservicebus`, `amazonsqs`, `kafka` | Messaging system identifier |
| `messaging.destination.name` | `urn:message:Namespace:MessageType` | Destination queue/topic name |

### MassTransit-Specific Tags

| Tag Name | Example Value | Description |
|----------|---------------|-------------|
| `messaging.masstransit.message_id` | `005b0000-b2b3-b20f-2270-08de51f2943f` | Unique message identifier (GUID) |
| `messaging.message.conversation_id` | `005b0000-b2b3-b20f-c03e-08de51f29441` | Conversation/correlation ID for tracking related messages |
| `messaging.masstransit.source_address` | `loopback://localhost/OrderState` | Source endpoint address (where message came from) |
| `messaging.masstransit.destination_address` | `loopback://localhost/urn:message:...` | Destination endpoint address (where message is going) |
| `messaging.masstransit.message_types` | `urn:message:Samples.MassTransit.Messages:OrderSubmitted` | Message type URN (fully qualified type name) |
| `messaging.message.body.size` | `1199` | Message body size in bytes |
| `messaging.masstransit.initiator_id` | `ea96b0ea-b293-4aa3-929f-fb6b8d06415f` | Saga/initiator correlation ID (for saga workflows) |

### Conditional Tags

| Tag Name | When Present | Example Value | Description |
|----------|--------------|---------------|-------------|
| `messaging.masstransit.request_id` | Request/Response pattern | GUID | Request correlation ID for matching responses |
| `messaging.masstransit.response_address` | Request operations | `loopback://localhost/...` | Endpoint address where response should be sent |
| `messaging.masstransit.fault_address` | Fault handling configured | `loopback://localhost/_error` | Endpoint address where faults/errors should be sent |

## ActivitySource Details

| Property | Value |
|----------|-------|
| Name | `MassTransit` |
| Version | Matches MassTransit assembly version (e.g., `8.3.4.0`) |

## Trace Context Propagation

| Mechanism | Header Name | Format |
|-----------|-------------|--------|
| W3C TraceContext | `traceparent` | `00-{trace-id}-{span-id}-{trace-flags}` |
| W3C TraceState | `tracestate` | Vendor-specific state (optional) |
| Baggage | Message headers | Key-value pairs propagated with trace |

## Quick Copy-Paste: All Tags

```
Core Tags:
- messaging.operation
- messaging.system
- messaging.destination.name

MassTransit Tags:
- messaging.masstransit.message_id
- messaging.message.conversation_id
- messaging.masstransit.source_address
- messaging.masstransit.destination_address
- messaging.masstransit.message_types
- messaging.message.body.size
- messaging.masstransit.initiator_id

Conditional Tags:
- messaging.masstransit.request_id
- messaging.masstransit.response_address
- messaging.masstransit.fault_address
```

## For Manual Instrumentation (MassTransit 7)

| Step | Code Pattern |
|------|--------------|
| 1. Create ActivitySource | `var activitySource = new ActivitySource("MassTransit");` |
| 2. Start Send Activity | `using var activity = activitySource.StartActivity("urn:message:{type} send", ActivityKind.Producer);` |
| 3. Add Tags | `activity?.SetTag("messaging.operation", "send");`<br>`activity?.SetTag("messaging.system", "in-memory");` |
| 4. Execute Operation | `await bus.Publish(message);` |
| 5. Activity Auto-Closes | Disposed when `using` block exits |
