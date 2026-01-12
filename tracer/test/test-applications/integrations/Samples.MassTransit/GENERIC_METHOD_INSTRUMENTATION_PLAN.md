# Generic Method Instrumentation Implementation Plan

## Problem Statement

The MassTransit 7 integration cannot instrument `IBus.Publish<T>(T message, CancellationToken)` because it's a generic method with type parameters in its signature. The current CallTarget system only supports type-level generics (`!0`, `!1` for `Class<T>`), not method-level generics (`!!0`, `!!1` for `Method<T>()`).

## Root Cause Analysis

### How Signature Matching Currently Works

1. **InstrumentMethod Attribute** specifies target method:
   ```csharp
   [InstrumentMethod(
       TypeName = "MassTransit.MassTransitBus",
       MethodName = "Publish",
       ParameterTypeNames = new[] { "System.Object", "System.Threading.CancellationToken" }
   )]
   ```

2. **Native Profiler** (`rejit_preprocessor.cpp:896-932`) matches signatures:
   - Converts ECMA-335 metadata types to strings
   - Compares each parameter type string exactly
   - For `Publish<T>(T message, ...)`, metadata contains `ELEMENT_TYPE_MVAR` (0x1E)
   - This gets converted to `"!!0"` (see `clr_helpers.cpp:645-660`)
   - Comparison: `"System.Object"` ≠ `"!!0"` → **No match**

3. **Why Non-Generic Overloads Don't Help**:
   - C# compiler always prefers `Publish<OrderSubmitted>(OrderSubmitted, ...)` over `Publish(object, ...)`
   - The `Publish(object, CancellationToken)` method exists but is never called
   - Even when instrumented, it receives zero invocations

### Code Locations

| File | Lines | Purpose |
|------|-------|---------|
| `tracer/src/Datadog.Tracer.Native/rejit_preprocessor.cpp` | 896-932 | `CheckExactSignatureMatch` - Parameter matching logic |
| `tracer/src/Datadog.Tracer.Native/clr_helpers.cpp` | 645-660 | `GetTypeName` - Converts `ELEMENT_TYPE_MVAR` → `"!!0"` |
| `tracer/src/Datadog.Tracer.Native/tracer_method_rewriter.cpp` | 70-82 | Documents CallTarget limitations |

## Proposed Solution: Wildcard Pattern Matching

### Design

Introduce a **wildcard pattern** in `InstrumentMethod.ParameterTypeNames` to match method generic parameters:

```csharp
[InstrumentMethod(
    TypeName = "MassTransit.MassTransitBus",
    MethodName = "Publish",
    ParameterTypeNames = new[] {
        "!!*",  // NEW: Wildcard matches any method type parameter (!!0, !!1, etc.)
        "System.Threading.CancellationToken"
    }
)]
```

### Implementation Steps

#### Phase 1: Minimal Proof of Concept

**Goal**: Make a single generic method instrumentable

1. **Update `rejit_preprocessor.cpp`** (Lines 896-932):
   ```cpp
   // In CheckExactSignatureMatch function
   for (size_t i = 0; i < argumentsLen; i++) {
       const auto& actualType = actualSignature.params[i];
       const auto& targetType = targetSignature.params[i];

       // NEW: Check for wildcard pattern
       if (targetType == WStr("!!*")) {
           // Match any method generic parameter (!!0, !!1, !!2, ...)
           if (actualType.size() >= 3 &&
               actualType[0] == '!' &&
               actualType[1] == '!' &&
               std::isdigit(actualType[2])) {
               continue;  // Wildcard match succeeded
           }
       }

       // Existing exact match logic
       if (actualType != targetType) {
           return false;
       }
   }
   ```

2. **Test with BusPublishIntegration.cs**:
   ```csharp
   [InstrumentMethod(
       AssemblyName = "MassTransit",
       TypeName = "MassTransit.MassTransitBus",
       MethodName = "Publish",
       ParameterTypeNames = new[] { "!!*", "System.Threading.CancellationToken" },
       MinimumVersion = "7.0.0",
       MaximumVersion = "7.*.*",
       IntegrationName = "MassTransit")]

   internal static CallTargetState OnMethodBegin<TTarget, TMessage>(
       TTarget instance,
       TMessage message,  // Will receive strongly-typed T
       CancellationToken cancellationToken)
   {
       var messageType = typeof(TMessage).Name;
       var messageTypeFullName = typeof(TMessage).FullName;

       var scope = MassTransitIntegration.CreateProducerScope(
           Tracer.Instance,
           MassTransitConstants.OperationPublish,
           messageType,
           destinationName: $"urn:message:{messageTypeFullName}");

       return new CallTargetState(scope);
   }
   ```

3. **Run Samples.MassTransit7**:
   - Build native tracer with new wildcard logic
   - Run sample app with `DD_TRACE_DEBUG=true`
   - Verify: "Intercepted IBus.Publish<OrderSubmitted>" appears in logs
   - Verify: Spans created in APM

#### Phase 2: Full Wildcard Support

**Expand to support multiple wildcard patterns**:

| Pattern | Matches | Example Use Case |
|---------|---------|------------------|
| `!!*` | Any method generic (`!!0`, `!!1`, ...) | `Method<T>(T param)` |
| `!*` | Any type-level generic (`!0`, `!1`, ...) | `Class<T>.Method(!0 param)` |
| `*` | Any type (generic or concrete) | `Method<T>(T a, object b)` → `new[] { "!!*", "*" }` |

**Implementation**:
```cpp
bool MatchesPattern(const WSTRING& actual, const WSTRING& pattern) {
    if (pattern == WStr("*")) {
        return true;  // Match anything
    }

    if (pattern == WStr("!!*")) {
        // Match method generic: !!0, !!1, !!2, ...
        return actual.size() >= 3 &&
               actual[0] == '!' && actual[1] == '!' &&
               std::isdigit(actual[2]);
    }

    if (pattern == WStr("!*")) {
        // Match type generic: !0, !1, !2, ...
        return actual.size() >= 2 &&
               actual[0] == '!' && actual[1] != '!' &&
               std::isdigit(actual[1]);
    }

    return actual == pattern;  // Exact match
}
```

#### Phase 3: Handle Return Types

**Problem**: Generic return types like `Task<TResponse>` also need wildcard support

```csharp
// Example: IRequestClient<T>.GetResponse<TResponse>()
Task<Response<TResponse>> GetResponse<TResponse>(...)
```

**Solution**: Allow wildcards in `ReturnTypeName`:

```csharp
[InstrumentMethod(
    ReturnTypeName = "System.Threading.Tasks.Task`1[!!*]",  // Task<T> where T is method generic
    ...
)]
```

Update return type matching in `rejit_preprocessor.cpp` similarly.

#### Phase 4: Documentation & Testing

1. **Update documentation**:
   - `docs/development/AutomaticInstrumentation.md` - Add wildcard pattern section
   - `tracer/src/Datadog.Tracer.Native/tracer_method_rewriter.cpp` - Update limitations list

2. **Add test coverage**:
   - Unit tests for `MatchesPattern` function
   - Integration test in `Datadog.Trace.ClrProfiler.IntegrationTests`
   - Sample app: `test-applications/integrations/Samples.GenericMethods`

3. **Performance validation**:
   - Benchmark: Wildcard matching vs exact matching
   - Ensure no regression in startup time

## Alternative Approaches (Why They Don't Work)

### ❌ Option 1: Duck Type the Generic Parameter
**Idea**: Use `where TMessage : IMessage` constraint
**Problem**: MassTransit messages don't inherit common interface; arbitrary POCOs

### ❌ Option 2: Instrument PublishContext<T>
**Idea**: Instrument lower-level pipeline types
**Problem**:
- Only works in MassTransit v8+ (major refactor in v8)
- v7 has different internal architecture
- Still would need generic parameter matching

### ❌ Option 3: Source Generator
**Idea**: Generate instrumentation at compile-time
**Problem**:
- Doesn't work for third-party libraries (can't modify their source)
- Defeats purpose of auto-instrumentation (zero code changes)

## Success Criteria

- [ ] `Samples.MassTransit7` produces spans for `bus.Publish(new OrderSubmitted(...))`
- [ ] Native logs show "Intercepted IBus.Publish<OrderSubmitted>"
- [ ] Integration tests pass for MassTransit 7.x and 8.x
- [ ] Generic methods in other integrations can use wildcard patterns
- [ ] Performance: <1% overhead vs exact matching
- [ ] Documentation updated with wildcard syntax

## Testing Strategy

### Unit Tests (C++)
```cpp
TEST(SignatureMatching, WildcardMatchesMethodGeneric) {
    ASSERT_TRUE(MatchesPattern(WStr("!!0"), WStr("!!*")));
    ASSERT_TRUE(MatchesPattern(WStr("!!1"), WStr("!!*")));
    ASSERT_FALSE(MatchesPattern(WStr("!0"), WStr("!!*")));
    ASSERT_FALSE(MatchesPattern(WStr("System.Object"), WStr("!!*")));
}
```

### Integration Tests (C#)
```csharp
[Fact]
public async Task MassTransit7_Publish_Generic_CreatesSpan()
{
    using var agent = MockAgentWriter.Create();
    using var host = CreateMassTransit7Host();
    var bus = host.Services.GetRequiredService<IBus>();

    await bus.Publish(new TestMessage { Id = 123 });

    var spans = agent.WaitForSpans(1, operationName: "masstransit.publish");
    Assert.Single(spans);
    Assert.Equal("TestMessage", spans[0].Tags["messaging.message_type"]);
}
```

## Timeline Estimate

| Phase | Tasks | Estimate |
|-------|-------|----------|
| 1. POC | C++ wildcard matching, test with MassTransit7 | 2-3 days |
| 2. Full Support | Multiple patterns, edge cases | 2-3 days |
| 3. Return Types | Generic return type matching | 1-2 days |
| 4. Testing | Unit tests, integration tests, samples | 2-3 days |
| 5. Documentation | Docs, PR review, polish | 1-2 days |
| **Total** | | **8-13 days** |

## Next Immediate Steps

1. Create feature branch: `mohammad/generic-method-instrumentation`
2. Modify `rejit_preprocessor.cpp` to add `!!*` wildcard support
3. Update `BusPublishIntegration.cs` to use `new[] { "!!*", "System.Threading.CancellationToken" }`
4. Build and test with `Samples.MassTransit7`
5. Validate spans appear in mock agent
