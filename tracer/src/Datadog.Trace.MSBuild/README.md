# Datadog.Trace.MSBuild

MSBuild logger for CI Visibility build telemetry.

## Purpose

MSBuild distributed logger that integrates builds with Datadog CI Visibility:
- Capture build events (start, end, errors, warnings)
- Create spans for build sessions and projects
- Track build performance and dependencies
- Report build telemetry to Datadog

## Key Functionality

- **INodeLogger**: Implement MSBuild's distributed logging interface
- **Build spans**: Create CI Visibility spans for builds
- **Build telemetry**: Track build performance, errors, warnings
- **Distributed logging**: Support multi-node MSBuild scenarios

## Dependencies

Project references:
- `Datadog.Trace` - Core tracer library for CI Visibility

Package references:
- `Microsoft.Build.Framework` - MSBuild API
- `Microsoft.Build.Utilities.Core` - Logger utilities

## Dependents

None - consumed as MSBuild logger via command line or environment variables.

## Artifacts

### MSBuild Logger Library
- **Name**: `Datadog.Trace.MSBuild.dll`
- **Target Frameworks**: net461, netstandard2.0, netcoreapp3.1, net6.0
- **Type**: MSBuild distributed logger
- **Usage**: `msbuild /distributedLogger:Datadog.Trace.MSBuild.dll`
