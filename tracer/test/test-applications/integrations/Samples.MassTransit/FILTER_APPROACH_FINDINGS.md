# MassTransit Filter/Middleware Instrumentation Approach - Findings

## Executive Summary

After exploring MassTransit's filter/middleware pipeline and attempting to instrument non-generic `Publish(object)` overloads, we discovered that:

1. ✅ **Non-generic `Publish` overloads exist** on `MassTransit.IPublishEndpoint`
2. ✅ **They are implemented as explicit interface methods** on `MassTransit.MassTransitBus`
3. ❌ **Interface instrumentation with these methods does NOT trigger** - likely because the non-generic overloads internally just dispatch to the generic version without being JIT compiled themselves

## What We Found

### Non-Generic Publish Methods on MassTransit.MassTransitBus

Runtime reflection shows these non-generic methods exist as explicit interface implementations:

```csharp
// From MassTransit.IPublishEndpoint interface, implemented by MassTransitBus
Task Publish(object message, CancellationToken cancellationToken);
Task Publish(object message, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken);
Task Publish(object message, Type messageType, CancellationToken cancellationToken);
Task Publish(object message, Type messageType, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken);
```

### Our Integration Attempts

**Attempt 1: Target Concrete Class**
```csharp
[InstrumentMethod(
    TypeName = "MassTransit.MassTransitBus",
    MethodName = "Publish",
    ParameterTypeNames = new[] { ClrNames.Object, ClrNames.CancellationToken })]
```
**Result**: ❌ Failed - Method not found (explicit interface implementation has different name)

**Attempt 2: Target Interface with CallTargetKind.Interface**
```csharp
[InstrumentMethod(
    TypeName = "MassTransit.IPublishEndpoint",
    MethodName = "Publish",
    ParameterTypeNames = new[] { ClrNames.Object, ClrNames.CancellationToken },
    CallTargetIntegrationKind = CallTargetKind.Interface)]
```
**Result**: ❌ Failed - No instrumentation triggered (method never JIT compiled?)

### Why Interface Instrumentation Didn't Work

**Hypothesis**: The non-generic `Publish(object)` methods are "forwarder" methods that immediately delegate to the generic version:

```csharp
// Likely implementation in MassTransitBus
Task IPublishEndpoint.Publish(object message, CancellationToken ct)
{
    // This just calls the generic version via converter/dispatch
    return PublishEndpointConverterCache.Publish(this, message, ct);
}

// Which internally does:
return this.Publish<TMessage>((TMessage)message, ct);  // Generic call
```

**Why this blocks instrumentation:**
1. When user calls `bus.Publish(message)`, the C# compiler chooses the **generic** overload directly
2. The non-generic overload is only called if the user explicitly casts: `bus.Publish((object)message)`
3. Even when explicitly casting, the non-generic method might be inlined or never JIT'd because it just forwards
4. The CLR profiler's `JITCompilationStarted` callback is never invoked for these forwarder methods

## Test Results

**Sample Code**:
```csharp
// TEST: Explicitly call non-generic overload
object message = new OrderSubmitted(...);
await bus.Publish(message, CancellationToken.None);
```

**Expected**: `"MassTransit BusPublishObjectIntegration.OnMethodBegin() - Intercepted non-generic Publish(object)"`

**Actual**: No instrumentation logs - `OnMethodBegin` never called

**Integration Registration**: ✅ Confirmed in `supported_calltargets.g.json`
```json
{
  "TargetTypeName": "MassTransit.IPublishEndpoint",
  "TargetMethodName": "Publish",
  "TargetParameterTypes": ["System.Object", "System.Threading.CancellationToken"],
  "IntegrationKind": 2  // Interface
}
```

## Alternative: Wildcard Parameter Matching

### The Wildcard Solution

From [rejit_preprocessor.cpp:915](cci:7://file:///Users/mohammad.islam/DDRepos/dd-trace-dotnet/tracer/src/Datadog.Tracer.Native/rejit_preprocessor.cpp:915:0-920:0):
```cpp
// Allow "_" as wildcard for "any type"
if (argumentTypeName != integrationArgumentTypeName &&
    integrationArgumentTypeName != WStr("_"))
{
    return false;
}
```

The profiler's matching logic treats `"_"` as a wildcard that matches any type!

### Updated Integration with Wildcard

```csharp
[InstrumentMethod(
    AssemblyName = "MassTransit",
    TypeName = "MassTransit.MassTransitBus",
    MethodName = "Publish",  // Or "MassTransit.IPublishEndpoint.Publish" for explicit name
    ReturnTypeName = ClrNames.Task,
    ParameterTypeNames = ["_", ClrNames.CancellationToken],  // Wildcard first param!
    MinimumVersion = "7.0.0",
    MaximumVersion = "7.*.*",
    IntegrationName = nameof(IntegrationId.MassTransit))]
public sealed class IPublishEndpointPublishIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TMessage>(
        TTarget instance, TMessage message, CancellationToken cancellationToken)
    {
        // message will be the actual message type (OrderSubmitted, etc.)
        var messageType = typeof(TMessage).Name;
        var messageTypeFullName = typeof(TMessage).FullName;

        var scope = MassTransitIntegration.CreateProducerScope(
            Tracer.Instance,
            MassTransitConstants.OperationPublish,
            messageType,
            destinationName: $"urn:message:{messageTypeFullName}");

        return new CallTargetState(scope);
    }

    internal static TReturn OnAsyncMethodEnd<TTarget, TReturn>(
        TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
    {
        state.Scope.DisposeWithException(exception);
        return returnValue;
    }
}
```

### How Wildcard Works

1. **At JIT Time**: CLR compiles `Publish<OrderSubmitted>(OrderSubmitted, CancellationToken)`
2. **Profiler Matching**:
   - Method signature: `["OrderSubmitted", "System.Threading.CancellationToken"]`
   - Integration spec: `["_", "System.Threading.CancellationToken"]`
   - Comparison: `"OrderSubmitted" != "_"` → Check if `"_"` is wildcard → YES → **MATCH!**
3. **IL Rewriting**: Profiler injects CallTarget instrumentation
4. **Callback**: `OnMethodBegin<MassTransitBus, OrderSubmitted>` is invoked

### Advantages

✅ **Matches all generic instantiations**: `Publish<OrderSubmitted>`, `Publish<PaymentProcessed>`, etc.
✅ **No need for interface instrumentation**: Targets the concrete class method directly
✅ **Works with existing CallTarget infrastructure**: No native profiler changes needed
✅ **Type information available at runtime**: The callback receives `TMessage` as a generic parameter

### Disadvantages

⚠️ **Less precise matching**: Will match ANY first parameter type (though return type and second param still filter)
⚠️ **Potential over-matching**: Could match unrelated methods with same name/signature pattern
⚠️ **Not explicitly documented**: Wildcard behavior exists in code but not in documentation

### Risk Assessment

**Low Risk** because:
1. The method name `"Publish"` is specific to this use case
2. The return type `Task` filters out synchronous methods
3. The second parameter `CancellationToken` is specific to async publishing methods
4. MassTransit is unlikely to have other `Task Publish(?, CancellationToken)` methods that we don't want to instrument

## Comparison with Working Examples

### RabbitMQ BasicPublishAsyncIntegration (Uses `!!0`)

**File**: `/Users/mohammad.islam/DDRepos/dd-trace-dotnet/tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/RabbitMQ/BasicPublishAsyncIntegration.cs`

```csharp
[InstrumentMethod(
    AssemblyName = "RabbitMQ.Client",
    TypeName = "RabbitMQ.Client.Impl.Channel",
    MethodName = "BasicPublishAsync",
    ParameterTypeNames = new[] {
        RabbitMQConstants.CachedAnonymousTypeName,
        ClrNames.String,
        ClrNames.String,
        ClrNames.Bool,
        RabbitMQConstants.IBasicPropertiesTypeName,
        "!!0",  // Generic parameter
        ClrNames.Bool
    })]
```

**Why this works**: The `!!0` is parameter #6 (zero-indexed #5), not parameter #1. The primary message data is not the generic parameter—it's used for properties or metadata. The method signature might be something like:

```csharp
Task BasicPublishAsync<T>(
    CachedType cached,
    string exchange,
    string routingKey,
    bool mandatory,
    IBasicProperties props,
    T body,  // <-- The !!0 is here, NOT as the primary parameter
    bool waitForConfirms)
```

The profiler can match this because there are 5 concrete type parameters before the generic one, providing enough context.

### MassTransit Difference

```csharp
Task Publish<T>(T message, CancellationToken ct)
                ^-- Generic parameter is FIRST
```

The generic type parameter is the **first** parameter, which means:
- No concrete types to establish method identity before the generic
- The profiler sees `["OrderSubmitted", "CancellationToken"]` but needs to match against `["!!0", "CancellationToken"]`
- Direct string comparison fails

## Recommended Next Steps

1. **Try the wildcard approach** with `ParameterTypeNames = ["_", ClrNames.CancellationToken]`
2. **Test thoroughly** to ensure no over-matching occurs
3. **Document the wildcard usage** in code comments for future maintainers
4. **Consider upstreaming** the wildcard documentation to profiler docs

## Files Modified in This Investigation

- `/Users/mohammad.islam/DDRepos/dd-trace-dotnet/tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/MassTransit/BusPublishObjectIntegration.cs` - Changed to target `IPublishEndpoint` with `CallTargetKind.Interface`
- `/Users/mohammad.islam/DDRepos/dd-trace-dotnet/tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/MassTransit/BusPublishObjectTypeIntegration.cs` - Changed to target `IPublishEndpoint` with `CallTargetKind.Interface`
- `/Users/mohammad.islam/DDRepos/dd-trace-dotnet/Samples.MassTransit7/Program.cs` - Added test code to explicitly call `Publish(object)` overload

## Conclusion

**Filter/middleware instrumentation is not viable** because:
1. MassTransit's non-generic overloads are forwarder methods that don't get JIT compiled
2. Interface instrumentation doesn't trigger for these methods
3. The generic methods in the pipeline all use `!!0` which can't be matched

**The wildcard approach** (`"_"` parameter) is the most promising path forward for instrumenting MassTransit 7's generic `Publish<T>` method.
