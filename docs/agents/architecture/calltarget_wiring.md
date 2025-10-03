# CallTarget Wiring

CallTarget is the mechanism by which the tracer automatically instruments third-party libraries and frameworks.

## Overview

- Define an integration class decorated with `InstrumentMethod` describing the target assembly/type/method, version range, and integration name. Example: `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/Couchbase/ClusterNodeIntegration.cs` and attribute API in `tracer/src/Datadog.Trace/ClrProfiler/InstrumentMethodAttribute.cs`.
- Build collects attributes and generates native definitions used by the CLR profiler (see generator `tracer/build/_build/CodeGenerators/CallTargetsGenerator.cs`). This emits a C++ list (registered at startup) and a JSON snapshot.
- Native CLR profiler (C++) registers those definitions and rewrites IL for matched methods during JIT/ReJIT to call the managed invoker.
- The managed entry point is `CallTargetInvoker` (`tracer/src/Datadog.Trace/ClrProfiler/CallTarget/CallTargetInvoker.cs`). It invokes `OnMethodBegin` and `OnMethodEnd`/`OnAsyncMethodEnd` on your integration type, handling generics, ref/out, and async continuations.
- `OnMethodBegin` returns `CallTargetState`, which can contain a tracing `Scope` to represent the span. That state flows to the end handler; async returns are awaited and then `OnAsyncMethodEnd` runs.
- Integrations typically create a scope in `OnMethodBegin` and tag/finish it in the end handler. See the Couchbase example's `OnMethodBegin`/`OnAsyncMethodEnd` methods.
- Enable/disable per-integration via config (IntegrationName), and by framework/versions declared in the attribute.
- For a full walkthrough and patterns, read `docs/development/AutomaticInstrumentation.md`.

## Integration Flow

```
[InstrumentMethod] attribute on integration class
         ↓
CallTargetsGenerator (build time)
         ↓
Native C++ definitions registered
         ↓
IL rewriting during JIT/ReJIT
         ↓
CallTargetInvoker.Invoke
         ↓
Integration.OnMethodBegin → CallTargetState
         ↓
Original method execution
         ↓
Integration.OnMethodEnd / OnAsyncMethodEnd
```

## Key Components

- **InstrumentMethodAttribute**: Declares what to instrument (assembly, type, method, versions)
- **CallTargetsGenerator**: Build-time code generator that emits native definitions
- **CallTargetInvoker**: Runtime entry point that dispatches to integration handlers
- **CallTargetState**: State object that flows from Begin to End handlers, typically containing the Scope
- **Integration handlers**: `OnMethodBegin`, `OnMethodEnd`, `OnAsyncMethodEnd` implemented by integration classes

## Example Integration

See `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/Couchbase/ClusterNodeIntegration.cs` for a complete example of:
- `[InstrumentMethod]` attribute usage
- `OnMethodBegin` creating a scope
- `OnAsyncMethodEnd` tagging and closing the scope
- Duck typing with generic constraints
