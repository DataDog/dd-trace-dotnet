# Tracer Debugging Guide

This guide covers local tracer debugging configuration and how to interpret runtime instrumentation evidence.

## Confirming Runtime Instrumentation

To check whether a particular method is instrumented, reproduce a call to it and inspect
the native tracer logs for that method in the same process.

- `Method enqueued for ReJIT` means the tracer selected the method for instrumentation.
- `CallTarget_RewriterCallback() Finished` means the tracer prepared the modified method
  body. The runtime still needs to accept and compile it, so this message alone does not
  confirm that the instrumentation ran. Even together with an absence of `ReJITError`
  messages, it does not prove runtime acceptance.
- To confirm execution, set a breakpoint in the integration's `OnMethodBegin` or
  `OnMethodEnd` callback and trigger the operation. A breakpoint hit for the relevant method
  confirms execution, but does not by itself confirm that a span was created. If the
  integration should create a span, check for a span corresponding to that operation too.

If the expected span is missing, check callback execution to help narrow the investigation.
A missing span alone does not tell you whether instrumentation failed or the callback ran
without producing a span.

Managed tracer startup messages do not confirm instrumentation of a specific method.
If native log entries are missing, check the logging configuration and that you captured
the relevant time period from the correct process before drawing a conclusion.

For implementation details, see [TracerMethodRewriter](../../tracer/src/Datadog.Tracer.Native/tracer_method_rewriter.cpp),
[RejitHandler](../../tracer/src/Datadog.Tracer.Native/rejit_handler.cpp), and
[FunctionControlWrapper](../../tracer/src/Datadog.Tracer.Native/function_control_wrapper.cpp).

## Understanding `$(SolutionDir)` and Path References

When setting up debugging configurations (e.g., in `launchSettings.json` or `.runsettings` files), you may need to reference tracer DLLs and other build artifacts.

**Key Issue:** The `$(SolutionDir)` MSBuild property is **not always defined**, depending on how you're running tests or building:

| Scenario | `$(SolutionDir)` Availability |
|----------|------------------------------|
| Visual Studio (running solution) | ✅ Defined |
| JetBrains Rider | ❌ **Not always defined** |
| Command line: `dotnet test <project>.csproj` | ❌ **Not defined** (no solution context) |
| Command line: `dotnet test <solution>.sln` | ✅ Defined |
| MSBuild with solution | ✅ Defined |

**Recommendation:** For local debugging configurations that you won't commit, use **absolute paths** instead of `$(SolutionDir)` if you find that the tracer isn't loaded into your sample app during testing:

```xml
<!-- ❌ Unreliable - only works in some scenarios -->
<DD_DOTNET_TRACER_HOME>$(SolutionDir)artifacts\monitoring-home</DD_DOTNET_TRACER_HOME>

<!-- ✅ Reliable - works in all scenarios (for local use only) -->
<DD_DOTNET_TRACER_HOME>C:\Users\your.name\DDRepos\dd-trace-dotnet\artifacts\monitoring-home</DD_DOTNET_TRACER_HOME>
```

⚠️ **Important:** Never commit absolute paths containing your personal username or machine-specific paths (e.g., `C:\Users\john.doe\...`), as they won't work for other developers.

## Platform-Specific Paths

**Windows:**
```json
"DD_DOTNET_TRACER_HOME": "C:\\Users\\your.name\\DDRepos\\dd-trace-dotnet\\artifacts\\monitoring-home",
"CORECLR_PROFILER_PATH": "C:\\Users\\your.name\\DDRepos\\dd-trace-dotnet\\artifacts\\monitoring-home\\win-x64\\Datadog.Trace.ClrProfiler.Native.dll"
```

**Linux:**
```json
"DD_DOTNET_TRACER_HOME": "/home/your.name/DDRepos/dd-trace-dotnet/artifacts/monitoring-home",
"CORECLR_PROFILER_PATH": "/home/your.name/DDRepos/dd-trace-dotnet/artifacts/monitoring-home/linux-x64/Datadog.Trace.ClrProfiler.Native.so"
```

**macOS:**
```json
"DD_DOTNET_TRACER_HOME": "/Users/your.name/DDRepos/dd-trace-dotnet/artifacts/monitoring-home",
"CORECLR_PROFILER_PATH": "/Users/your.name/DDRepos/dd-trace-dotnet/artifacts/monitoring-home/osx-x64/Datadog.Trace.ClrProfiler.Native.dylib"
```

## Required Environment Variables

When debugging with the tracer locally, set these environment variables:

### Core Variables (Required)
- `DD_DOTNET_TRACER_HOME` - Path to the monitoring-home directory
- `CORECLR_ENABLE_PROFILING=1` - Enable CLR profiling (use `COR_ENABLE_PROFILING` for .NET Framework)
- `CORECLR_PROFILER={846F5F1C-F9AE-4B07-969E-05C26BC060D8}` - Tracer profiler GUID (use `COR_PROFILER` for .NET Framework)
- `CORECLR_PROFILER_PATH` - Path to native profiler DLL/SO (use `COR_PROFILER_PATH` for .NET Framework)

### Optional Variables
- `DD_TRACE_DEBUG=1` - Enable verbose debug logging
- `DD_TRACE_LOG_DIRECTORY=/path/to/logs` - Log output directory

## Related Documentation

- [AutomaticInstrumentation.md](AutomaticInstrumentation.md) - Creating and testing integrations
- [DuckTyping.md](DuckTyping.md) - Duck typing patterns for instrumentation
- [../../tracer/README.MD](../../tracer/README.MD) - Build and development setup
