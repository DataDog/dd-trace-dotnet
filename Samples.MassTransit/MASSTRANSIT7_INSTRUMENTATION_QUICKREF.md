# MassTransit 7 Instrumentation - Quick Reference

## File Checklist

Location: `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/MassTransit/`

- [ ] `MassTransitConstants.cs` - Constants and integration ID
- [ ] `MassTransitTags.cs` - Tag definitions
- [ ] `MassTransitIntegration.cs` - Helper methods for scope creation
- [ ] `ContextPropagation.cs` - Headers adapter for trace propagation
- [ ] Duck-typed interfaces (IBus, IPublishContext, IConsumeContext, etc.)
- [ ] Integration classes for each method (BusPublishIntegration, ConsumeIntegration, etc.)

## Key Methods to Instrument

| Method | Integration Class | Activity Kind |
|--------|------------------|---------------|
| `IBus.Publish<T>` | `BusPublishIntegration` | Producer |
| `IPublishEndpoint.Publish<T>` | `PublishEndpointPublishIntegration` | Producer |
| `ISendEndpoint.Send<T>` | `SendEndpointSendIntegration` | Producer |
| `IConsumer<T>.Consume` | `ConsumeIntegration` | Consumer |
| `IRequestClient<T>.GetResponse<T>` | `RequestClientGetResponseIntegration` | Client |
| `ConsumeContext.RespondAsync<T>` | `RespondAsyncIntegration` | Server |

## InstrumentMethod Attribute Template

```csharp
[InstrumentMethod(
    AssemblyName = "MassTransit",
    TypeName = "MassTransit.IBus",
    MethodName = "Publish",
    ReturnTypeName = ClrNames.Task,
    ParameterTypeNames = new[] { ClrNames.GenericParameterAttribute, ClrNames.CancellationToken },
    MinimumVersion = "7.0.0",
    MaximumVersion = "7.*.*",
    IntegrationName = "MassTransit")]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class BusPublishIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TMessage>(
        TTarget instance, 
        TMessage message, 
        CancellationToken cancellationToken)
    {
        var scope = MassTransitIntegration.CreateProducerScope(...);
        return new CallTargetState(scope);
    }

    internal static TReturn OnAsyncMethodEnd<TTarget, TReturn>(
        TTarget instance, 
        TReturn returnValue, 
        Exception exception, 
        in CallTargetState state)
    {
        state.Scope.DisposeWithException(exception);
        return returnValue;
    }
}
```

## Duck Typing Interface Template

```csharp
internal interface IPublishContext
{
    Guid? MessageId { get; }
    Guid? ConversationId { get; }
    Uri? SourceAddress { get; }
    Uri? DestinationAddress { get; }
    IHeaders? Headers { get; }
}
```

## Context Propagation Pattern

### Inject (Producer)

```csharp
// In OnMethodBegin for Publish/Send
if (scope != null && headers != null)
{
    var context = new PropagationContext(scope.Span.Context, Baggage.Current);
    var adapter = new ContextPropagation(headers);
    Tracer.Instance.TracerManager.SpanContextPropagator.Inject(context, adapter);
}
```

### Extract (Consumer)

```csharp
// In OnMethodBegin for Consume
PropagationContext propagationContext = default;
if (consumeContext.Headers != null)
{
    var adapter = new ContextPropagation(consumeContext.Headers);
    propagationContext = Tracer.Instance.TracerManager.SpanContextPropagator.Extract(adapter);
}

var scope = MassTransitIntegration.CreateConsumerScope(
    Tracer.Instance,
    operation,
    messageType,
    context: propagationContext);
```

## Tags to Set

### Producer Tags
```csharp
tags.SpanKind = SpanKinds.Producer;
tags.MessagingOperation = "send" or "publish";
tags.MessagingSystem = "in-memory" or transport name;
tags.DestinationName = $"urn:message:{messageType}";
tags.MessageId = context.MessageId?.ToString();
tags.ConversationId = context.ConversationId?.ToString();
tags.SourceAddress = context.SourceAddress?.ToString();
tags.DestinationAddress = context.DestinationAddress?.ToString();
```

### Consumer Tags
```csharp
tags.SpanKind = SpanKinds.Consumer;
tags.MessagingOperation = "process";
tags.MessagingSystem = "in-memory" or transport name;
tags.MessageId = context.MessageId?.ToString();
tags.ConversationId = context.ConversationId?.ToString();
tags.SourceAddress = context.SourceAddress?.ToString();
tags.DestinationAddress = context.DestinationAddress?.ToString();
tags.InitiatorId = context.InitiatorId?.ToString();
tags.RequestId = context.RequestId?.ToString();
```

## Resource Name Pattern

- **Producer**: `{operation} {MessageType}` (e.g., `publish OrderSubmitted`)
- **Consumer**: `process {MessageType}` (e.g., `process OrderSubmitted`)
- **Request/Response**: `request {MessageType}` or `respond {MessageType}`

## Span Type

Always use: `SpanTypes.Queue`

## Operation Name Pattern

`masstransit.{operation}`
- `masstransit.publish`
- `masstransit.send`
- `masstransit.process`
- `masstransit.request`
- `masstransit.respond`

## Integration Testing

Create test in: `tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/MassTransitTests.cs`

```csharp
[SkippableFact]
[Trait("Category", "EndToEnd")]
[Trait("Integration", "MassTransit")]
public async Task SubmitsTraces()
{
    // Use test agent to verify spans
    using var telemetry = this.ConfigureTelemetry();
    using var agent = EnvironmentHelper.GetMockAgent();
    
    // Run sample app
    using var process = await RunSampleAndWaitForExit(agent);
    
    // Verify spans
    var spans = agent.WaitForSpans(expectedCount);
    Assert.NotEmpty(spans);
}
```

## Sample Application

Create in: `tracer/test/test-applications/integrations/Samples.MassTransit7/`

Use the existing `Samples.MassTransit7` project we created as the sample application.

## Build Commands

```bash
# Build tracer
cd tracer
./build.sh

# Run tests
./build.sh BuildAndRunManagedUnitTests

# Run integration tests
./build.sh BuildAndRunLinuxIntegrationTests --filter "MassTransit"
```

## Common Patterns

### Async Method
```csharp
OnMethodBegin<TTarget, ...>() → returns CallTargetState
OnAsyncMethodEnd<TTarget, TReturn>() → returns TReturn
```

### Sync Method
```csharp
OnMethodBegin<TTarget, ...>() → returns CallTargetState
OnMethodEnd<TTarget>() → returns CallTargetReturn
```

### Generic Method
Use `ClrNames.GenericParameterAttribute` in ParameterTypeNames

### Method with No Return (void)
Use `ReturnTypeName = ClrNames.Void`

## Version Ranges

- **MinimumVersion**: `"7.0.0"`
- **MaximumVersion**: `"7.*.*"` (all 7.x versions)

Use `"8.*.*"` for MassTransit 8 separately.

## Integration ID

Add to `Configuration/IntegrationId.cs`:
```csharp
MassTransit = XX, // Pick next available number
```

## Quick Start Steps

1. Copy RabbitMQ or Kafka integration as template
2. Replace type/method names with MassTransit equivalents
3. Create duck-typed interfaces for MassTransit types
4. Update constants and tags
5. Build and test
6. Add integration tests
7. Submit PR

## Reference Integrations

Study these for patterns:
- `RabbitMQ/` - Similar messaging patterns
- `Kafka/` - Producer/Consumer patterns
- `Azure/ServiceBus/` - Cloud messaging
- `Grpc/` - Request/Response patterns
