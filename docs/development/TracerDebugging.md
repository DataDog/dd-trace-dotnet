# Tracer Debugging Guide

This guide covers local tracer debugging configuration and how to interpret runtime instrumentation evidence.

## Confirming Runtime Instrumentation

Correlate evidence with the specific method and running process. A source declaration or an intermediate
native-log message does not establish that the replacement IL was accepted, executed, or produced a span.

| Stage | Evidence | What remains unverified |
|-------|----------|-------------------------|
| Declaration | `[InstrumentMethod]` attributes under `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/` describe the target assembly, type, method, and version range. | Whether the running method matches and is selected. |
| Selection/enqueue | The native log's `Method enqueued for ReJIT` records selection for ReJIT processing. | Whether rewriting or CLR submission succeeds. |
| Rewrite preparation | `GetReJITParameters` means the CLR is requesting replacement IL. `*** CallTarget_RewriterCallback() Finished` follows a successful export of the tracer's rewritten body into a local buffer. | Whether the later submission succeeds and the CLR compiles the replacement body. |
| CLR submission | `FunctionControlWrapper.ApplyChanges` passes the buffered body to the CLR through `SetILFunctionBody`. | The earlier `Finished` message does not establish this call's result or subsequent compilation/execution. |
| Execution and span production | A breakpoint in the relevant CallTarget callback confirms callback execution; a correlated integration span confirms span production. | Callback execution alone does not guarantee a span, and a missing span alone does not identify which earlier stage failed. |

The ordering is visible in [TracerMethodRewriter](../../tracer/src/Datadog.Tracer.Native/tracer_method_rewriter.cpp),
[RejitHandler.NotifyReJITParameters](../../tracer/src/Datadog.Tracer.Native/rejit_handler.cpp), and
[FunctionControlWrapper](../../tracer/src/Datadog.Tracer.Native/function_control_wrapper.cpp): the tracer logs
`Finished` during rewriting, then `ApplyChanges` submits the buffered IL after the rejitters run.

Do not treat `Finished` plus absence of `ReJITError` as proof of CLR acceptance. A qualified hypothesis is
reasonable, but identify what is still unverified and obtain method-specific runtime evidence before asserting
success. Managed tracer initialization logs establish initialization, not per-method rewriting. Likewise,
absence of a native-log line is not conclusive without checking log level and capture completeness.

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
