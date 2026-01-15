# MassTransit Integration - Current Status & TODO

## Last Updated: January 2026

## Current Status
The MassTransit 7 integration is working. Context propagation for headers uses a reflection-based approach since the MassTransit `Get<T>` method has a `where T : struct` constraint that prevents duck-typing with string values.

### What's Working
- **ISendContext duck-casting** - Working (removed `SupportedMessageTypes` property)
- **IReceiveContext duck-casting** - Working
- **Header extraction** - Using reflection-based `GetAll()` approach in `ContextPropagation.cs`
- **Header injection** - Using `ISendHeaders` duck-typing with `Set(string, object, bool)` method
- **Telemetry mapping** - Added `MassTransit` to `IntegrationIdExtensions.cs` and `MetricTags.cs`
- **Send span destination** - Fixed by getting destination from SendEndpoint's backing field (not context)
- **All tags populating** - Send, receive, and process spans have correct tags

### Key Implementation Notes
1. **DestinationAddress timing issue**: The `DestinationAddress` on `SendContext` is null at `OnMethodBegin` because MassTransit sets it INSIDE the `Send` method (`context.DestinationAddress = _endpoint.DestinationAddress`). We get the destination from the outer `SendEndpoint` class via the `_endpoint` field's `<DestinationAddress>k__BackingField`.

### Known Issues (Non-blocking)
1. **IConsumerConsumeContext duck-cast failing** - `MessageConsumeContext<T>` only has `Message` property directly, other properties are inherited/from interfaces. This doesn't block core functionality since process spans still work.

## Files Modified
- `ISendContext.cs` - Removed `SupportedMessageTypes`
- `IReceiveContext.cs` - Simplified to essential properties
- `IHeaders.cs` - Changed to use `GetAll()` approach
- `ContextPropagation.cs` - Rewritten to use reflection-based header reading
- `SendContextPropagation.cs` - Uses duck-typed `ISendHeaders` for injection
- `SendEndpointPipeSendIntegration.cs` - Gets destination from SendEndpoint backing field
- `ReceivePipeDispatcherIntegration.cs` - Updated to use new ContextPropagation
- `MethodConsumerMessageFilterIntegration.cs` - Updated to use new ContextPropagation
- `IntegrationIdExtensions.cs` - Added MassTransit telemetry mapping
- `MetricTags.cs` - Added MassTransit enum value

## Remaining Tasks
- Update `MassTransitTests.cs` to use snapshot verification for multiple spans
- Test sample app under `tracer/test/test-applications/integrations/Samples.MassTransit`
- Optionally fix IConsumerConsumeContext duck-casting if needed for additional tags

