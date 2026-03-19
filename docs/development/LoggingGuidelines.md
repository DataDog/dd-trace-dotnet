# Logging Guidelines

Use clear, customer-facing terminology in log messages to avoid confusion. `Profiler` is ambiguous—it can refer to the .NET profiling APIs we use internally or the Continuous Profiler product.

## Customer-facing terminology (high-level logs)

- **Datadog SDK** — When disabling the entire product or referring to the whole monitoring solution
  - Example: `"The Datadog SDK has been disabled"`
- **Instrumentation** or **Instrumentation component** — For the native tracer auto-instrumentation
  - Example: `"Instrumentation has been disabled"` or `"The Instrumentation component failed to initialize"`
- **Continuous Profiler** — Always use full name for the profiling product
  - Example: `"The Continuous Profiler has been disabled"`
- **Datadog.Trace.dll** — For the managed tracer assembly (avoid "managed profiler")
  - Example: `"Unable to initialize: Datadog.Trace.dll was not yet loaded into the App Domain"`

## Internal/technical naming (still valid)

- Native loader, Native tracer, Managed tracer loader, Managed tracer, Libdatadog, Continuous Profiler
- `CorProfiler` / `ICorProfiler` / `COR Profiler` for runtime components

**Reference:** See PR 7467 for examples of consistent terminology in native logs.

## Log Argument Formatting

Never use `ToString()` on numeric types in log calls - use generic log methods instead:
```csharp
// BAD - allocates a string unnecessarily
Log.Debug(ex, "Error (attempt {Attempt})", (attempt + 1).ToString());

// GOOD - uses generic method, no allocation
Log.Debug<int>(ex, "Error (attempt {Attempt})", attempt + 1);
```

## Log Levels for Retry Operations

When implementing retry logic, use appropriate log levels:
- **Debug**: Intermediate retry attempts (transient errors are expected)
- **Error**: Final failure after all retries exhausted
- **Error**: Non-retryable errors (e.g., 400 Bad Request indicates a bug)

## ErrorSkipTelemetry Usage

`Log.ErrorSkipTelemetry` logs locally but does NOT send to Datadog telemetry. Use it for:
- **Expected environmental errors**: Network connectivity issues, endpoint unavailability
- **Transient failures**: Errors that are expected in production and self-resolve

**Do NOT use ErrorSkipTelemetry for:**
- Errors in outer catch blocks that would only catch unexpected exceptions
- HTTP 400 Bad Request (indicates a bug in our payload)
- Errors that indicate bugs in the tracer code

**Understanding code flow is critical**: If inner methods already handle expected errors, outer catch blocks should use `Log.Error` since they would only catch unexpected exceptions (bugs).

## Error Messages for Network Failures

When logging final failures for network operations, include:
1. The endpoint that failed
2. Number of attempts made
3. Link to troubleshooting documentation
