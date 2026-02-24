# Datadog.Trace.OpenTracing

OpenTracing bridge for Datadog .NET tracer.

## Purpose

Provides OpenTracing API compatibility for Datadog tracer:
- Implement OpenTracing interfaces on top of Datadog.Trace
- Allow OpenTracing instrumentation to work with Datadog backend
- Enable migration from OpenTracing to Datadog
- Support existing OpenTracing integrations

## Key Functionality

- **OpenTracing implementation**: ITracer, ISpan, IScope interfaces
- **Datadog backend**: Route traces to Datadog agent
- **API bridge**: Translate OpenTracing calls to Datadog operations
- **Compatibility**: Support OpenTracing instrumented libraries

## Dependencies

Project references:
- `Datadog.Trace.Manual` - Manual instrumentation API

Package references:
- `OpenTracing` - OpenTracing API

## Dependents

None - consumed as NuGet package by applications using OpenTracing.

## Artifacts

### NuGet Package
- **Package**: `Datadog.Trace.OpenTracing`
- **Target Frameworks**: net461, netstandard2.0, netcoreapp3.1, net6.0
- **Type**: Public customer-facing library
