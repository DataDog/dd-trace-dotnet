# Datadog.AutoInstrumentation.Generator

GUI tool for generating instrumentation integration boilerplate code.

## Purpose

Avalonia-based desktop application that helps developers create instrumentation integrations by:
- Analyzing target assemblies using dnlib
- Inspecting methods to be instrumented
- Generating C# integration code with `[InstrumentMethod]` attributes
- Creating integration implementations with proper duck typing constraints

## Key Functionality

- **Assembly inspection**: Browse and analyze assemblies to find methods to instrument
- **Code generation**: Generate boilerplate integration code from selected methods
- **Integration scaffolding**: Create properly structured integration classes with OnMethodBegin/OnMethodEnd handlers

## Dependencies

None - standalone tool.

## Dependents

None - developer utility tool.

## Artifacts

### NuGet Tool Package
- **Package**: `dd-instrumentation-generator`
- **Command**: `dd-instrumentation-generator`
- **Install**: `dotnet tool install -g dd-instrumentation-generator`
- **Target Framework**: net7.0
