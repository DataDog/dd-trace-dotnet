# Datadog.Trace.Tools.dd_dotnet.SourceGenerators

Source generators specifically for the `dd-dotnet` CLI tool.

## Purpose

Roslyn source generators that generate code at compile-time for `Datadog.Trace.Tools.dd_dotnet`:
- Configuration key constants
- CLI command scaffolding
- Other boilerplate code specific to dd-dotnet

## Key Functionality

- **Code generation**: Generate dd-dotnet specific code at compile time
- **Boilerplate reduction**: Reduce manual code in dd-dotnet tool

## Dependencies

None (links `Constants.cs` from `Datadog.Trace.SourceGenerators`)

Package references:
- Microsoft.CodeAnalysis.CSharp (Roslyn)

## Dependents

- `Datadog.Trace.Tools.dd_dotnet` (as analyzer reference, compile-time only)

## Artifacts

### Source Generator
- **Name**: `Datadog.Trace.Tools.dd_dotnet.SourceGenerators.dll`
- **Target Framework**: netstandard2.0
- **Type**: Roslyn source generator (IsRoslynComponent: true)
- **Usage**: Consumed during `dd-dotnet` compilation
- **Output**: Generated C# source files in `dd-dotnet` project
