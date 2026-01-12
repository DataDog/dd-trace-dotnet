# MassTransit Comprehensive Sample

This sample application demonstrates all major features of MassTransit, a distributed application framework for .NET.

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

### 3. **Saga State Machine**
- `OrderStateMachine` - Orchestrates the complete order workflow
- Manages state transitions: Submitted → Payment → Shipment → Completed
- Handles payment failures and order cancellations
- Demonstrates long-running process coordination

### 4. **Advanced Features**
- **Retry Policies**: Automatic retry with configurable intervals
- **Error Handling**: Fault handling and error propagation
- **In-Memory Transport**: No external dependencies required
- **Parallel Processing**: Multiple messages processed concurrently
- **Dependency Injection**: Full integration with Microsoft.Extensions.DependencyInjection
- **OpenTelemetry Instrumentation**: Built-in distributed tracing with MassTransit's ActivitySource
  - Automatic trace context propagation across messages
  - Detailed messaging spans with operation type, destination, message IDs
  - Console and OTLP exporters configured

## Project Structure

```
Samples.MassTransit/
├── Messages/
│   ├── Commands.cs         # Command messages (SubmitOrder, ProcessPayment, etc.)
│   ├── Events.cs           # Event messages (OrderSubmitted, PaymentProcessed, etc.)
│   └── RequestResponse.cs  # Request/Response messages
├── Consumers/
│   ├── SubmitOrderConsumer.cs
│   ├── ProcessPaymentConsumer.cs
│   ├── ShipOrderConsumer.cs
│   └── InventoryConsumer.cs
├── Sagas/
│   ├── OrderState.cs       # Saga state class
│   └── OrderStateMachine.cs # State machine definition
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
cd Samples.MassTransit
dotnet run
```

## What You'll See

The application runs through four demonstrations:

1. **Request/Response**: Makes a synchronous-style inventory check
2. **Saga State Machine**: Single order with full workflow (Order → Payment → Shipping → Completion)
3. **Parallel Processing**: Three orders processed simultaneously
4. **Payment Failures & Error Handling**: Orders with payment failures and saga cancellations

## Key MassTransit Concepts

### Messages
- **Commands**: Tell a service to do something (e.g., `SubmitOrder`)
- **Events**: Notify that something happened (e.g., `OrderSubmitted`)
- **Queries**: Request information (e.g., `CheckInventory`)

### Communication Patterns
- **Publish**: One-to-many (events)
- **Send**: One-to-one (commands)
- **Request/Response**: Synchronous-style queries

### Sagas
- Coordinate long-running processes
- Maintain state across multiple messages
- Handle compensation and error scenarios

### Retry & Error Handling
- Automatic retries with configurable intervals
- Fault consumers for handling failures
- Outbox pattern for reliable message delivery

## Dependencies

- MassTransit 8.3.4 (with built-in OpenTelemetry instrumentation)
- Microsoft.Extensions.Hosting 9.0.0
- Microsoft.Extensions.Logging.Console 9.0.0
- OpenTelemetry.Extensions.Hosting 1.11.0

## Notes

- Uses **in-memory transport** for simplicity (no RabbitMQ/Azure Service Bus required)
- **OpenTelemetry instrumentation enabled**:
  - MassTransit automatically creates Activities (spans) for all message operations
  - Trace context is propagated across publish/send/consume operations
  - Traces are picked up automatically by Datadog's tracer when DD_TRACE_OTEL_ENABLED=true
- **Deterministic behavior** (except for GUIDs):
  - Payment processing: Orders with amount < $200 succeed, >= $200 fail (deterministic)
  - Transaction IDs and tracking numbers are generated deterministically from order IDs
  - Order amounts are fixed/predictable (e.g., $99.99, $149.99, $200, $250)
  - Inventory checks always return available with fixed quantities
  - Order IDs are randomly generated (GUIDs) on each run
- All state is maintained in-memory and will be lost on restart
- Saga state is stored in an in-memory repository

## Next Steps

To use with real transports:
- Replace `UsingInMemory` with `UsingRabbitMq`, `UsingAzureServiceBus`, etc.
- Configure appropriate connection strings
- Consider persistent saga repositories (Entity Framework, MongoDB, etc.)
