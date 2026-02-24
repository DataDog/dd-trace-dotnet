# Datadog.Trace.Tools.Analyzers

Roslyn analyzers and code fix providers for the tracer codebase.

## Purpose

Enforces coding standards and best practices in the tracer codebase through compile-time analysis:
- Logging patterns (avoid string interpolation, prefer format strings)
- Duck typing correctness (proper constraints, interface usage)
- Public API guidelines
- Primary constructor usage
- Thread abort safety

## Key Functionality

- **Logging analyzers**: Enforce zero-allocation logging patterns
- **DuckType analyzers**: Validate duck typing constraints and interfaces
- **PublicApi analyzers**: Ensure public API follows guidelines
- **PrimaryConstructor analyzers**: Validate primary constructor usage
- **ThreadAbort analyzers**: Detect unsafe thread abort patterns
- **Code fixes**: Provide automatic fixes for common issues

## Dependencies

None - standalone analyzer project.

Package references:
- Microsoft.CodeAnalysis.CSharp (Roslyn)

## Dependents

Referenced by all tracer projects via `Directory.Build.props` (not direct ProjectReference).

## Artifacts

### Roslyn Analyzer
- **Name**: `Datadog.Trace.Tools.Analyzers.dll`
- **Target Framework**: netstandard2.0
- **Type**: Roslyn analyzer and code fix provider
- **Usage**: Consumed during compilation of tracer projects
- **IsPackable**: false (internal use only)
