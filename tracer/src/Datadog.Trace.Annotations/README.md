# Datadog.Trace.Annotations

Lightweight annotations library for manual instrumentation hints.

## Purpose

Provides attributes for marking code for custom instrumentation without taking a dependency on the full tracer:
- `[Trace]` - Mark methods for automatic tracing
- Custom operation/resource name hints
- Safe to reference in production code (no-op if tracer not present)

Allows customers to annotate their code for instrumentation without requiring the full `Datadog.Trace` assembly.

## Key Functionality

- **Attributes**: Tracing annotations recognized by auto-instrumentation
- **Lightweight**: Minimal dependency for customer applications
- **No-op safe**: Attributes have no effect if tracer isn't loaded

## Dependencies

None - standalone library with no dependencies.

## Dependents

- `Datadog.AzureFunctions` - Azure Functions package

## Artifacts

### NuGet Package
- **Package**: `Datadog.Trace.Annotations` (version 1.0.0)
- **Target Frameworks**: net461, netstandard2.0
- **Type**: Public customer-facing library
