# Performance Guidelines

## Core Principle

Minimize heap allocations: The tracer runs in-process with customer applications and must have minimal performance impact. Avoid unnecessary object allocations, prefer value types where appropriate, use object pooling for frequently allocated objects, and cache reusable instances.

## Performance-Critical Code Paths

Performance is especially critical in two areas:

### 1. Bootstrap/Startup Code

Initialization code runs at application startup in every instrumented process, including:
- The managed loader (`Datadog.Trace.ClrProfiler.Managed.Loader`)
- Tracer initialization in `Datadog.Trace` (static constructors, configuration loading, first tracer instance creation)
- Integration registration and setup

Any allocations or inefficiencies here directly impact application startup time and customer experience. This code must be extremely efficient.

### 2. Hot Paths

Code that executes frequently during application runtime, such as:
- Span creation and tagging (executes on every traced operation)
- Context propagation (executes on every HTTP request/response)
- Sampling decisions (executes on every trace)
- Integration instrumentation callbacks (executes on every instrumented method call)
- Any code in the request/response pipeline

In these areas, even small inefficiencies are multiplied by the frequency of execution and can significantly impact application performance.

## Performance Optimization Patterns

### Zero-Allocation Provider Structs

- Use `readonly struct` implementing interfaces instead of classes for frequently-instantiated abstractions
- Use generic type parameters with interface constraints to avoid boxing: `<TProvider> where TProvider : IProvider`
- Example: `EnvironmentVariableProvider` in managed loader (see tracer/src/Datadog.Trace.ClrProfiler.Managed.Loader)
- Benefits: Zero heap allocations, no boxing, better JIT optimization

### Avoid Allocation in Logging

- Use format strings (`Log("value: {0}", x)`) instead of interpolation (`Log($"value: {x}")`)
- Allows logger to check level before formatting
- Critical in startup and hot paths where logging is frequent

### Avoid params Array Allocations

- Provide overloads for common cases (0, 1, 2 args) that avoid `params object?[]` array allocation
- Keep params overload as fallback for 3+ args
- Example: Logging methods with multiple overloads for different argument counts
