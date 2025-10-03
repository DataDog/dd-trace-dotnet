# Creating Integrations

## Location

`tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/<Area>/<Integration>.cs`

Place shape interfaces and shared helpers in the same `<Area>` folder (e.g., `AWS/Kinesis/*`, `Couchbase/*`, `GraphQL/*`).

## Steps to Create an Integration

### 1. Add Shape Interfaces

Add shape interfaces for third‑party types you consume (no direct package refs).

### 2. Add Integration Class

Add an integration class with one or more `InstrumentMethod` attributes specifying:
- `AssemblyName`
- `TypeName`
- `MethodName`
- `ReturnTypeName`
- `ParameterTypeNames`
- `MinimumVersion`
- `MaximumVersion`
- `IntegrationName`
- `CallTargetIntegrationKind` (if needed)

### 3. Implement Static Handlers

- `OnMethodBegin` returns `CallTargetState`
- End handlers are `OnMethodEnd` for sync or `OnAsyncMethodEnd` for async
- Use `Tracer.Instance` to create a `Scope`, tag it, and dispose in the end handler

### 4. Use Duck Typing

Use duck typing in method generics:
- `where TReq : IMyShape, IDuckType`
- `DuckCast<TShape>()` for nested members

**Examples:** `AWS/Kinesis/PutRecordsIntegration.cs`, `Couchbase/ClusterNodeIntegration.cs`

## Build and Registration

Definitions are discovered and generated during build; no manual native changes required.

## Tests

Add tests under `tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests` and corresponding samples under `tracer/test/test-applications/integrations`. Run with OS‑specific Nuke targets; filter with `--filter`/`--framework`.

## Related Documentation

- [CallTarget wiring documentation](../architecture/calltarget_wiring.md) for CallTarget mechanism details
- [Duck typing documentation](../architecture/duck_typing.md) for duck typing implementation guide
- See `docs/development/AutomaticInstrumentation.md` for comprehensive walkthrough
