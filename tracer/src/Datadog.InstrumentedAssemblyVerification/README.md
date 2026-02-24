# Datadog.InstrumentedAssemblyVerification

Library for verifying pre-instrumented assemblies are valid.

## Purpose

Validates that pre-instrumented assemblies produced by `Datadog.InstrumentedAssemblyGenerator` are correct:
- Decompile instrumented IL using ILSpy
- Verify IL is valid using Microsoft.ILVerification
- Ensure instrumentation was applied correctly
- Catch IL rewriting errors in testing

## Key Functionality

- **IL verification**: Validate instrumented assemblies are well-formed
- **Decompilation**: Use ILSpy to inspect generated code
- **Testing support**: Verify instrumentation correctness in CI pipeline

## Dependencies

None - standalone library using ILSpy and Microsoft.ILVerification packages.

Shares code:
- `ILSpyHelper.cs` (linked from `Datadog.InstrumentedAssemblyGenerator`)

## Dependents

- `Datadog.Trace.Tools.Runner` (dd-trace) - Used by AOT command

## Artifacts

### Library
- **Name**: `Datadog.InstrumentedAssemblyVerification.dll`
- **Target Framework**: netstandard2.0
- **Type**: Internal library (not published as NuGet package)
