# Datadog.Trace.ClrProfiler.Managed.Loader

Managed bootstrapper loaded by the native profiler to initialize the tracer.

## Purpose

First managed code executed by the native profiler, responsible for:
- Locating the correct `Datadog.Trace.dll` for the target framework
- Handling AppDomain and AssemblyLoadContext setup
- Initializing the tracer
- Providing startup logging for troubleshooting
- Abstracting runtime differences (.NET Framework vs .NET Core/.NET)

This is the critical bridge between the native profiler and the managed tracer.

## Key Functionality

- **Tracer discovery**: Find and load appropriate Datadog.Trace.dll
- **Runtime abstraction**: Handle AppDomain (.NET FX) vs AssemblyLoadContext (.NET Core/.NET)
- **Startup logging**: Emit diagnostic logs for initialization troubleshooting
- **Failure handling**: Gracefully handle initialization errors

## Dependencies

None - must be standalone to avoid dependency resolution issues during bootstrap.

## Dependents

None (loaded directly by `Datadog.Tracer.Native.dll` via `ICorProfilerCallback`)

## Artifacts

### Library
- **Name**: `Datadog.Trace.ClrProfiler.Managed.Loader.dll`
- **Target Frameworks**: net461, netcoreapp2.0
- **Output Path**: `..\bin\ProfilerResources\`
- **Deployment**: Shipped in monitoring-home directory
- **Type**: Bootstrap assembly loaded by native profiler

**Critical**: This assembly must remain small and have zero dependencies to ensure reliable loading during profiler startup.
