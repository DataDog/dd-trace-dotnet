# Datadog.Trace.Tools.dd_dotnet

The `dd-dotnet` CLI tool for troubleshooting and diagnostics of Datadog .NET instrumentation.

## Purpose

Lightweight diagnostic and troubleshooting tool for:
- Validating tracer installation and configuration
- Inspecting running processes for instrumentation issues
- Checking IIS configuration
- Checking agent connectivity
- Creating crash dumps

## Key Functionality

- **Process checks**: Inspect running processes for tracer attachment and configuration (`check process`)
- **IIS diagnostics**: Validate IIS application pool configuration (`check iis`)
- **Agent checks**: Verify connectivity to Datadog Agent (`check agent`)
- **Crash dumps**: Create process dumps for debugging (`createdump`)
- **Run command**: Launch applications with instrumentation (similar to `dd-trace` but more focused)

## Dependencies

Project references:
- `Datadog.Trace.Tools.Shared` - Process/environment utilities
- `Datadog.Trace.Tools.dd_dotnet.SourceGenerators` - Code generation (analyzer only)

Package references:
- `Microsoft.Diagnostics.Runtime` - Process inspection and memory analysis
- `Spectre.Console` - Rich console output
- `System.CommandLine` - CLI parsing

## Dependents

- `Datadog.Trace.Tools.dd_dotnet.Tests` - Unit tests

## Artifacts

### Native AOT Executable
- **Name**: `dd-dotnet` or `dd-dotnet.exe`
- **Build**: Native AOT compilation for small, fast startup
- **Platforms**: Published per-RID (win-x64, linux-x64, etc.)
- **Size**: Optimized with LZMA compression (`PublishLzmaCompressed`)
- **Features**:
  - Self-contained, no .NET runtime required
  - Stripped symbols for minimal size
  - Invariant globalization

**Note**: macOS builds do not use AOT due to platform limitations (`PublishAot` disabled on OSX).

## Differences from dd-trace

`dd-dotnet` is focused on **diagnostics and troubleshooting**, while `dd-trace` is focused on **running applications with instrumentation**.

Key differences:
- `dd-dotnet`: Smaller, AOT-compiled, diagnostic-focused
- `dd-trace`: Full-featured runner with CI Visibility, coverage, GAC management, and AOT generation
