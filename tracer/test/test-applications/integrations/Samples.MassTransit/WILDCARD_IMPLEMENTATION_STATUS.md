# MassTransit 7 Wildcard Instrumentation - Implementation Status

## Summary

We successfully implemented the **wildcard parameter matching** approach to instrument MassTransit 7's generic `Publish<T>` method. The code changes have been completed and built successfully. Testing is pending due to profiler attachment issues unrelated to the instrumentation code itself.

## Implementation Complete ✅

### Changes Made

**File**: `/Users/mohammad.islam/DDRepos/dd-trace-dotnet/tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/MassTransit/IPublishEndpointPublishIntegration.cs`

**Change**: Updated `ParameterTypeNames` from `["!!0", ClrNames.CancellationToken]` to `[ClrNames.Ignore, ClrNames.CancellationToken]`

**Before**:
```csharp
[InstrumentMethod(
    AssemblyName = "MassTransit",
    TypeName = "MassTransit.MassTransitBus",
    MethodName = "Publish",
    ReturnTypeName = ClrNames.Task,
    ParameterTypeNames = ["!!0", ClrNames.CancellationToken],  // ❌ Cannot match
    MinimumVersion = "7.0.0",
    MaximumVersion = "7.*.*",
    IntegrationName = nameof(IntegrationId.MassTransit))]
```

**After**:
```csharp
[InstrumentMethod(
    AssemblyName = "MassTransit",
    TypeName = "MassTransit.MassTransitBus",
    MethodName = "Publish",
    ReturnTypeName = ClrNames.Task,
    ParameterTypeNames = [ClrNames.Ignore, ClrNames.CancellationToken],  // ✅ Wildcard match
    MinimumVersion = "7.0.0",
    MaximumVersion = "7.*.*",
    IntegrationName = nameof(IntegrationId.MassTransit))]
```

### Why This Works

**From native profiler code** ([rejit_preprocessor.cpp:915-920](cci:7://file:///Users/mohammad.islam/DDRepos/dd-trace-dotnet/tracer/src/Datadog.Tracer.Native/rejit_preprocessor.cpp:915:0-920:0)):

```cpp
// Allow "_" as wildcard for "any type"
if (argumentTypeName != integrationArgumentTypeName &&
    integrationArgumentTypeName != WStr("_"))
{
    return false;
}
```

**Translation**:
- `ClrNames.Ignore` → `"_"` in JSON
- Profiler sees `["OrderSubmitted", "CancellationToken"]` at JIT time
- Compares `"OrderSubmitted"` vs `"_"` → Wildcard matches → ✅ Instrumentation applied

### Verification

**Generated Integration** (`tracer/build/supported_calltargets.g.json`):
```json
{
  "IntegrationName": "MassTransit",
  "AssemblyName": "MassTransit",
  "TargetTypeName": "MassTransit.MassTransitBus",
  "TargetMethodName": "Publish",
  "TargetReturnType": "System.Threading.Tasks.Task",
  "TargetParameterTypes": [
    "_",  // ← Wildcard successfully generated
    "System.Threading.CancellationToken"
  ],
  "MinimumVersion": { "Item1": 7, "Item2": 0, "Item3": 0 },
  "MaximumVersion": { "Item1": 7, "Item2": 65535, "Item3": 65535 },
  "InstrumentationTypeName": "Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit.IPublishEndpointPublishIntegration",
  "IntegrationKind": 0,
  "InstrumentationCategory": 1
}
```

✅ **Build Status**: Successful (Build completed 1/12/2026 4:26:50 PM)

## Testing Status ⏸️

### Current Blocker

**Profiler Attachment Issue**: The native profiler is not attaching to the sample application, preventing validation of the instrumentation.

**Symptoms**:
- No log files created in `/tmp/dotnet-tracer-*.log`
- Sample application runs normally without instrumentation
- No "Intercepted" debug messages

**Not Related To**:
- The wildcard implementation (integration is correctly registered)
- Code compilation (build succeeded with no errors)
- Integration definition (JSON generated correctly)

**Possible Causes**:
- Environment variable expansion issues (`$(SolutionDir)` not resolving)
- Process spawning (profiler attaches to `dotnet` CLI but not child process)
- macOS security/permissions on dylib files

### Test Environment

**Profiler Paths**:
- Native library: `/Users/mohammad.islam/DDRepos/dd-trace-dotnet/shared/bin/monitoring-home/osx/Datadog.Trace.ClrProfiler.Native.dylib`
- Tracer home: `/Users/mohammad.islam/DDRepos/dd-trace-dotnet/shared/bin/monitoring-home`

**Sample Application**:
- Path: `/Users/mohammad.islam/DDRepos/dd-trace-dotnet/Samples.MassTransit7`
- Test Code (Program.cs:153-156):
```csharp
// TEST: Try using non-generic overload by casting to object
object message = new OrderSubmitted(orderId, "Alice Johnson", 99.99m, DateTime.UtcNow);
Console.WriteLine($"[TEST] Calling non-generic Publish(object) with message type: {OrderSubmitted}");
await bus.Publish(message, CancellationToken.None);
```

## Next Steps for Testing

### Option 1: Debug Profiler Attachment
1. Verify environment variables are correctly set
2. Check if profiler attaches to child processes
3. Test with a simpler sample application
4. Check macOS security settings for dylib execution

### Option 2: Use Existing Test Infrastructure
1. Locate integration test suite for MassTransit
2. Add test case for wildcard instrumentation
3. Run via `./tracer/build.sh` test targets

### Option 3: Manual Verification
1. Deploy to a known working environment (Docker, Linux VM, etc.)
2. Use the existing profiler test harness
3. Verify via integration test logs

## Comparison: Before vs After

### Before (Blocked)

```
User calls:           bus.Publish(new OrderSubmitted(...))
                              ↓
C# Compiler:          Publish<OrderSubmitted>(OrderSubmitted, CancellationToken)
                              ↓
CLR JIT:              Creates concrete method with signature
                      Task Publish(OrderSubmitted, CancellationToken)
                              ↓
Profiler Matching:    ["OrderSubmitted", "CancellationToken"]
                      vs
                      ["!!0", "CancellationToken"]
                              ↓
String Comparison:    "OrderSubmitted" != "!!0"
                              ↓
Result:               ❌ NO MATCH - No instrumentation
```

### After (Expected to Work)

```
User calls:           bus.Publish(new OrderSubmitted(...))
                              ↓
C# Compiler:          Publish<OrderSubmitted>(OrderSubmitted, CancellationToken)
                              ↓
CLR JIT:              Creates concrete method with signature
                      Task Publish(OrderSubmitted, CancellationToken)
                              ↓
Profiler Matching:    ["OrderSubmitted", "CancellationToken"]
                      vs
                      ["_", "CancellationToken"]
                              ↓
Wildcard Check:       "_" is wildcard → MATCH!
                              ↓
Result:               ✅ MATCH - Instrumentation applied
                              ↓
Callback Invoked:     OnMethodBegin<MassTransitBus, OrderSubmitted>(...)
                              ↓
Span Created:         MassTransit producer span for OrderSubmitted message
```

## Alternative Approaches Explored

We also investigated a filter-based approach (similar to Hangfire) but encountered dependency issues:

### Filter Approach (Not Pursued)

**Idea**: Instrument MassTransit's bus configurator constructor and inject Datadog filters

**Blocker**: MassTransit's `ConfigurePublish(Action<IPublishPipeConfigurator>)` requires typed callbacks, creating compile-time dependencies on MassTransit types.

**Hangfire Comparison**: Hangfire uses `JobFilterCollection.Add(object filter)` which accepts `object`, allowing duck-typed filters. MassTransit doesn't have an equivalent.

### Why Wildcard Is Better

1. ✅ **No dependencies** - Uses existing CallTarget infrastructure
2. ✅ **Simple implementation** - One-line change
3. ✅ **Proven approach** - Wildcard matching already exists in profiler
4. ✅ **Type information preserved** - Callback receives `TMessage` generic parameter
5. ✅ **Low maintenance** - No complex duck typing or filter registration

## Documentation Created

1. **GENERIC_METHOD_INSTANTIATION_ANALYSIS.md** - Explains why `!!0` can't be matched at JIT time
2. **FILTER_APPROACH_FINDINGS.md** - Documents filter/middleware investigation and findings
3. **WILDCARD_IMPLEMENTATION_STATUS.md** - This document

## Code Status

✅ **Implementation**: Complete
✅ **Build**: Successful
✅ **Integration Registration**: Verified
⏸️ **Testing**: Blocked on profiler attachment
⬜ **Integration Tests**: Not yet added
⬜ **Documentation**: Needs PR description

## Recommendations

1. **Immediate**: Resolve profiler attachment issue to validate wildcard matching
2. **Short-term**: Add integration tests for MassTransit 7 with wildcard approach
3. **Long-term**: Consider documenting wildcard parameter matching in profiler documentation

## Files Changed

- ✅ `/Users/mohammad.islam/DDRepos/dd-trace-dotnet/tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/MassTransit/IPublishEndpointPublishIntegration.cs`
- ⚠️ `/Users/mohammad.islam/DDRepos/dd-trace-dotnet/tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/MassTransit/BusPublishObjectIntegration.cs` (changed to Interface targeting - may need to be reverted)
- ⚠️ `/Users/mohammad.islam/DDRepos/dd-trace-dotnet/tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/MassTransit/BusPublishObjectTypeIntegration.cs` (changed to Interface targeting - may need to be reverted)
- 🗑️ Deleted: `/Users/mohammad.islam/DDRepos/dd-trace-dotnet/tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/MassTransit/IBusFactoryConfigurator.cs` (incomplete filter approach file)

## Related Issues

The changes to `BusPublishObjectIntegration.cs` and `BusPublishObjectTypeIntegration.cs` to use `CallTargetKind.Interface` targeting were part of the exploration but didn't resolve the issue (non-generic overloads are likely forwarder methods that don't get JIT compiled). These should be reverted to their original state targeting `MassTransitBus` directly.

---

**Status**: Ready for testing once profiler attachment issue is resolved.
**Expected Outcome**: Successful instrumentation of all `Publish<T>` method calls in MassTransit 7.
