# Tracer Debugging Guide

This guide covers how to configure environment variables for debugging the Datadog .NET tracer locally.

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
<DD_DOTNET_TRACER_HOME>$(SolutionDir)shared\bin\monitoring-home</DD_DOTNET_TRACER_HOME>

<!-- ✅ Reliable - works in all scenarios (for local use only) -->
<DD_DOTNET_TRACER_HOME>C:\Users\your.name\DDRepos\dd-trace-dotnet\shared\bin\monitoring-home</DD_DOTNET_TRACER_HOME>
```

⚠️ **Important:** Never commit absolute paths containing your personal username or machine-specific paths (e.g., `C:\Users\john.doe\...`), as they won't work for other developers.

## Platform-Specific Paths

**Windows:**
```json
"DD_DOTNET_TRACER_HOME": "C:\\Users\\your.name\\DDRepos\\dd-trace-dotnet\\shared\\bin\\monitoring-home",
"CORECLR_PROFILER_PATH": "C:\\Users\\your.name\\DDRepos\\dd-trace-dotnet\\shared\\bin\\monitoring-home\\win-x64\\Datadog.Trace.ClrProfiler.Native.dll"
```

**Linux:**
```json
"DD_DOTNET_TRACER_HOME": "/home/your.name/DDRepos/dd-trace-dotnet/shared/bin/monitoring-home",
"CORECLR_PROFILER_PATH": "/home/your.name/DDRepos/dd-trace-dotnet/shared/bin/monitoring-home/linux-x64/Datadog.Trace.ClrProfiler.Native.so"
```

**macOS:**
```json
"DD_DOTNET_TRACER_HOME": "/Users/your.name/DDRepos/dd-trace-dotnet/shared/bin/monitoring-home",
"CORECLR_PROFILER_PATH": "/Users/your.name/DDRepos/dd-trace-dotnet/shared/bin/monitoring-home/osx-x64/Datadog.Trace.ClrProfiler.Native.dylib"
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
- [../CONTRIBUTING.md](../CONTRIBUTING.md) - General contribution guidelines
- [../../tracer/README.MD](../../tracer/README.MD) - Build and development setup
