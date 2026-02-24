# Datadog.Trace.Manual

Public manual instrumentation API package (facade over auto-instrumentation).

## Purpose

Provides the public `Datadog.Trace` NuGet package API for manual instrumentation:
- Create custom spans
- Add tags and metrics
- Inject/extract trace context
- Control sampling
- Access active span/scope

This is a **facade** - it links many files from `Datadog.Trace` project to provide a lightweight public API without exposing internal auto-instrumentation details.

## Key Functionality

- **Manual spans**: `Tracer.StartActive()`, `Tracer.Instance`
- **Tagging**: Add custom tags, metrics, errors to spans
- **Context propagation**: Inject/extract trace context for distributed tracing
- **Sampling**: Control sampling decisions
- **Public API**: Customer-facing instrumentation API

## Dependencies

None (links files from `Datadog.Trace` project via `<Compile Include>`)

## Dependents

- `Datadog.AzureFunctions` - Azure Functions package
- `Datadog.Trace.Bundle` - Bundled assets package
- `Datadog.Trace.OpenTracing` - OpenTracing bridge

## Artifacts

### NuGet Package
- **Package**: `Datadog.Trace` (same package ID used for auto-instrumentation)
- **Target Frameworks**: net461, netstandard2.0, netcoreapp3.1
- **Type**: Public customer-facing library
- **Includes**: MSBuild props/targets for integration

**Important**: This project produces the public `Datadog.Trace` NuGet package. The `Datadog.Trace` project (auto-instrumentation) is NOT published as a NuGet package - it's only distributed via monitoring-home.
