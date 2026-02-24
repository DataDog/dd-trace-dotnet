# Datadog.Trace.Bundle

NuGet package containing complete auto-instrumentation assets (monitoring-home).

## Purpose

Packaging project that bundles all auto-instrumentation components:
- Native profiler libraries (all platforms)
- Managed tracer assemblies (all target frameworks)
- Loader assemblies
- Configuration files
- Complete monitoring-home directory structure

Used for deployment scenarios where customers need all tracer assets.

## Key Functionality

- **Asset bundling**: Packages complete monitoring-home directory
- **Multi-platform**: Includes native libraries for all supported platforms
- **No compilation**: Pure packaging project, no build output

## Dependencies

Project references:
- `Datadog.Trace.Manual` - Manual instrumentation API (for packaging)

## Dependents

None - consumed as NuGet package by customers.

## Artifacts

### NuGet Package
- **Package**: `Datadog.Trace.Bundle`
- **Content**: Complete monitoring-home directory structure
  - Native profilers: win-x64, win-x86, linux-x64, linux-arm64, osx-x64, osx-arm64
  - Managed assemblies: net461, netcoreapp3.1, net6.0
- **Type**: Content-only package (no compiled assembly)
