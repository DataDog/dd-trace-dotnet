# Datadog.Trace.Coverage.collector

Visual Studio Test Platform data collector for code coverage with CI Visibility.

## Purpose

VSTest data collector that instruments assemblies to track code coverage and report to Datadog CI Visibility:
- Instrument test assemblies using Mono.Cecil
- Track line/branch coverage during test execution
- Integrate coverage with CI Visibility test spans
- Support both in-process and out-of-process collection

## Key Functionality

- **Coverage instrumentation**: Use Mono.Cecil to inject coverage tracking
- **VSTest integration**: Implement DataCollector for Test Platform
- **CI Visibility**: Report coverage to Datadog with test results
- **Execution modes**: In-process or out-of-process collection

## Dependencies

Project references:
- `Datadog.Trace` - Core tracer library for CI Visibility integration

Package references:
- `Microsoft.TestPlatform.ObjectModel` - VSTest integration
- `Mono.Cecil` - IL instrumentation

## Dependents

- `Datadog.Trace.Tools.Runner` (dd-trace) - Used by `ci run` command

## Artifacts

### Data Collector Library
- **Name**: `Datadog.Trace.Coverage.collector.dll`
- **Target Framework**: netstandard2.0
- **Type**: Visual Studio Test Platform data collector
- **Usage**: Configured in `.runsettings` or via command line
