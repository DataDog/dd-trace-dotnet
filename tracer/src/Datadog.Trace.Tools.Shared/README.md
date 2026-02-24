# Datadog.Trace.Tools.Shared

Shared utility library used by CLI tools (`dd-trace` and `dd-dotnet`).

## Purpose

Provides common process management and environment detection utilities to avoid code duplication across tool projects.

## Key Functionality

- **Process inspection**: Read process information (PID, environment variables, command line)
- **Platform-specific helpers**: Windows and Linux process utilities

## Dependencies

None - this is a leaf library with no project dependencies.

## Dependents

- `Datadog.Trace.Tools.Runner` (dd-trace)
- `Datadog.Trace.Tools.dd_dotnet` (dd-dotnet)
- `Datadog.Trace.Tools.dd_dotnet.Tests`

## Artifacts

This project produces a library that is embedded into the CLI tools. It does not produce standalone artifacts.
