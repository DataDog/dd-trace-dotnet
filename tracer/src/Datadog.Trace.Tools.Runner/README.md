# Datadog.Trace.Tools.Runner

The `dd-trace` CLI tool for running .NET applications with Datadog instrumentation and CI Visibility features.

## Purpose

Primary developer-facing CLI tool for:
- Running applications with automatic instrumentation (`dd-trace`)
- CI Visibility test execution (`ci run`, `configure-ci`)
- Code coverage merging
- GAC operations (install/uninstall/get)
- AOT and pre-instrumented assembly generation
- Instrumentation error analysis

## Key Functionality

- **Run commands**: Execute .NET applications or tests with tracer attached
- **CI Visibility**: Configure and run tests with CI metadata collection
- **Coverage tools**: Merge coverage reports for CI Visibility
- **GAC management**: Install/uninstall Datadog assemblies in the Global Assembly Cache
- **AOT support**: Generate pre-instrumented assemblies for Native AOT scenarios
- **Diagnostics**: Analyze instrumentation errors and assembly issues

## Dependencies

Project references:
- `Datadog.Trace.Tools.Shared` - Process/environment utilities
- `Datadog.Trace` - Core tracer library
- `Datadog.InstrumentedAssemblyGenerator` - AOT pre-instrumentation
- `Datadog.InstrumentedAssemblyVerification` - Verify pre-instrumented assemblies
- `Datadog.Trace.Coverage.collector` - Coverage collection

## Dependents

- `Datadog.Trace.Tools.Runner.Tests` - Unit tests
- `Datadog.Trace.Tools.Runner.IntegrationTests` - Integration tests

## Artifacts

### NuGet Tool Package
- **Package**: `dd-trace` (published to NuGet.org)
- **Command**: `dd-trace`
- **Install**: `dotnet tool install -g dd-trace`

### Standalone Executables (when built with `BuildStandalone=true`)
- **Name**: `dd-trace` or `dd-trace.exe`
- **Platforms**: win-x64, win-x86, linux-x64, linux-musl-x64, linux-arm64, linux-musl-arm64, osx-x64
- **Features**: Self-contained, single-file, trimmed
- **Includes**: Embedded monitoring home directory with tracer assets
