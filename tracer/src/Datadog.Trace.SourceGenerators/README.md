# Datadog.Trace.SourceGenerators

C# source generators for compile-time code generation in the tracer.

## Purpose

Roslyn source generators that generate code at compile-time for `Datadog.Trace`:
- Enum extension methods
- Instrumentation integration definitions
- Tag list classes
- Telemetry metrics code
- MessagePack serializer generation

Improves performance and reduces boilerplate by generating code at compile time.

## Key Functionality

- **Enum generators**: Generate efficient extension methods for enums
- **Integration definitions**: Generate CallTarget integration metadata
- **Tag generators**: Generate strongly-typed tag list classes
- **Telemetry generators**: Generate telemetry metric emission code
- **MessagePack**: Generate serialization code

## Dependencies

None (includes MessagePack and other files via `<Compile Include>` links)

Package references:
- Microsoft.CodeAnalysis.CSharp (Roslyn)

## Dependents

- `Datadog.Trace` (as analyzer reference, compile-time only)

## Artifacts

### Source Generator
- **Name**: `Datadog.Trace.SourceGenerators.dll`
- **Target Framework**: netstandard2.0
- **Type**: Roslyn source generator (IsRoslynComponent: true)
- **Usage**: Consumed during `Datadog.Trace` compilation
- **Output**: Generated C# source files in `Datadog.Trace` project
