# MassTransit 7 Comprehensive Sample

This sample application demonstrates all major features of **MassTransit 7.x**, a distributed application framework for .NET.

> **Note**: This is the MassTransit 7 version. For MassTransit 8, see `Samples.MassTransit`.

## Key Differences from MassTransit 8

MassTransit 7 has several API differences from version 8:

1. **No Built-in Sagas**: Saga state machines require the separate Automatonymous library (not included in this sample due to version conflicts)
2. **Bus Interface**: Uses `IBus` instead of `IBusControl`
3. **No Built-in OpenTelemetry**: MassTransit 7 doesn't have native OpenTelemetry support (added in v8)
4. **Configuration API**: Some differences in retry configuration and other APIs
5. **Simpler Feature Set**: This sample focuses on core messaging patterns without sagas

## Features Demonstrated

### 1. **Message Patterns**
- **Publish/Subscribe**: Broadcasting events to multiple consumers
- **Send (Command)**: Sending commands to specific endpoints
- **Request/Response**: Synchronous-style communication over messaging

### 2. **Consumers**
- `SubmitOrderConsumer` - Processes order submissions
- `ProcessPaymentConsumer` - Handles payment processing with simulated success/failure
- `ShipOrderConsumer` - Manages order shipping
- `InventoryConsumer` - Request/response pattern for inventory checks

### 3. **Event-Driven Workflow**
- Order workflow coordinated through publish/subscribe events
- Payment and shipping triggered by order submission
- Demonstrates event-driven architecture without sagas

### 4. **Advanced Features**
- **Retry Policies**: Automatic retry with configurable intervals
- **Error Handling**: Fault handling and error propagation
- **In-Memory Transport**: No external dependencies required
- **Parallel Processing**: Multiple messages processed concurrently
- **Dependency Injection**: Full integration with Microsoft.Extensions.DependencyInjection

## Project Structure

```
Samples.MassTransit7/
├── Messages/
│   ├── Commands.cs         # Command messages (SubmitOrder, ProcessPayment, etc.)
│   ├── Events.cs           # Event messages (OrderSubmitted, PaymentProcessed, etc.)
│   └── RequestResponse.cs  # Request/Response messages
├── Consumers/
│   ├── SubmitOrderConsumer.cs
│   ├── ProcessPaymentConsumer.cs
│   ├── ShipOrderConsumer.cs
│   └── InventoryConsumer.cs
└── Program.cs              # Main application with demos
```

## Message Flow

### Complete Order Saga Flow:
```
1. OrderSubmitted (Event)
   ↓
2. ProcessPayment (Command) → Saga: AwaitingPayment
   ↓
3a. PaymentProcessed (Event) → Saga: AwaitingShipment
    ↓
    ShipOrder (Command)
    ↓
    OrderShipped (Event)
    ↓
    OrderCompleted (Event) → Saga: Finalized
    
3b. PaymentFailed (Event) → Saga: Cancelled
    ↓
    OrderCancelled (Event) → Saga: Finalized
```

## Running the Sample

```bash
cd Samples.MassTransit7
dotnet run
```

## What You'll See

The application runs through four demonstrations:

1. **Request/Response**: Makes a synchronous-style inventory check
2. **Publish/Subscribe**: Order workflow coordinated through events
3. **Parallel Processing**: Three orders processed simultaneously
4. **Payment Failures & Error Handling**: Orders with payment failures

## Key MassTransit Concepts

### Messages
- **Commands**: Tell a service to do something (e.g., `SubmitOrder`)
- **Events**: Notify that something happened (e.g., `OrderSubmitted`)
- **Queries**: Request information (e.g., `CheckInventory`)

### Communication Patterns
- **Publish**: One-to-many (events)
- **Send**: One-to-one (commands)
- **Request/Response**: Synchronous-style queries

### Event-Driven Architecture
- Coordinate workflows through publish/subscribe
- Loosely coupled components
- Scalable message processing

### Retry & Error Handling
- Automatic retries with configurable intervals
- Fault consumers for handling failures
- Compensation logic for saga failures

## Dependencies

- MassTransit 7.3.1
- MassTransit.Extensions.DependencyInjection 7.3.1
- Microsoft.Extensions.Hosting 8.0.0
- Microsoft.Extensions.Logging.Console 8.0.0

## Notes

- Uses **in-memory transport** for simplicity (no RabbitMQ/Azure Service Bus required)
- **No OpenTelemetry support**: MassTransit 7 doesn't have built-in OpenTelemetry instrumentation (use MassTransit 8 for OTEL)
- **Deterministic behavior** (except for GUIDs):
  - Payment processing: Orders with amount < $200 succeed, >= $200 fail (deterministic)
  - Transaction IDs and tracking numbers are generated deterministically from order IDs
  - Order amounts are fixed/predictable (e.g., $99.99, $149.99, $200, $250)
  - Inventory checks always return available with fixed quantities
  - Order IDs are randomly generated (GUIDs) on each run
- All state is maintained in-memory and will be lost on restart
- Saga state is stored in an in-memory repository

## Migrating to MassTransit 8

If you want to upgrade to MassTransit 8, see the `Samples.MassTransit` project which includes:
- Built-in OpenTelemetry instrumentation
- Simplified configuration API
- Built-in saga state machines (no separate Automatonymous package needed)
- Updated APIs and improved developer experience
