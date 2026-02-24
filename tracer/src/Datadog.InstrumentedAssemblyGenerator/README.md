# Datadog.InstrumentedAssemblyGenerator

Library for generating pre-instrumented assemblies with embedded instrumentation.

## Purpose

Generates ahead-of-time instrumented assemblies for scenarios where runtime instrumentation isn't available:
- Native AOT applications
- IL rewriting validation
- Testing instrumentation logic
- Performance benchmarking

Uses dnlib and ILSpy to:
- Read profiler metadata
- Rewrite method bodies with instrumentation calls
- Validate and emit modified assemblies

## Key Functionality

- **IL rewriting**: Inject instrumentation calls into method bodies
- **Metadata mapping**: Map profiler integration definitions to target methods
- **Assembly generation**: Produce pre-instrumented assemblies
- **AOT support**: Enable instrumentation in Native AOT scenarios

## Dependencies

None - standalone library using dnlib and ILSpy packages.

## Dependents

- `Datadog.Trace.Tools.Runner` (dd-trace) - Used by AOT command

## Artifacts

### Library
- **Name**: `Datadog.InstrumentedAssemblyGenerator.dll`
- **Target Framework**: netstandard2.0
- **Type**: Internal library (not published as NuGet package)
