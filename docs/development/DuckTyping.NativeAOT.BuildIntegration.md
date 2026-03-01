# DuckTyping NativeAOT Build Integration

## Purpose

This guide describes how to integrate the DuckTyping NativeAOT pipeline into local builds, CI pipelines, and release packaging.

It complements:

1. [DuckTyping.NativeAOT.md](./DuckTyping.NativeAOT.md) for end-to-end usage.
2. [DuckTyping.NativeAOT.Spec.md](./DuckTyping.NativeAOT.Spec.md) for artifact and schema contracts.
3. [DuckTyping.NativeAOT.Testing.md](./DuckTyping.NativeAOT.Testing.md) for validation gates.

## Integration Model

The build integration has three explicit stages:

1. Resolve mapping inputs.
2. Generate registry and companion artifacts.
3. Consume generated props/descriptors during app build or publish.

The generated registry assembly is a build artifact and should be versioned alongside the app build output, not checked into source control.

## Prerequisites

1. Build `Datadog.Trace.Tools.Runner` before invoking `ducktype-aot generate`.
2. Ensure proxy and target assemblies are built and accessible.
3. Use deterministic paths in CI workspaces so emitted manifests and logs are easy to trace.
4. If signing is required, provide `--strong-name-key-file` or `DD_TRACE_DUCKTYPE_AOT_STRONG_NAME_KEY_FILE`.

## Recommended Build Order

1. Build proxy definition assemblies.
2. Build target assemblies.
3. Run `ducktype-aot generate`.
4. Run `ducktype-aot verify-compat`.
5. Build/publish app with generated `.props` and linker descriptor.
6. Execute AOT validation tests.

## Local Developer Workflow

### 1. Build dependencies

```bash
dotnet build tracer/src/Datadog.Trace.Tools.Runner/Datadog.Trace.Tools.Runner.csproj -c Release -f net8.0
dotnet build /abs/path/My.Proxy.Contracts.csproj -c Release
dotnet build /abs/path/My.Targets.csproj -c Release
```

### 2. Generate registry

```bash
dotnet tracer/src/Datadog.Trace.Tools.Runner/bin/Release/Tool/net8.0/Datadog.Trace.Tools.Runner.dll \
  ducktype-aot generate \
  --proxy-assembly /abs/path/My.Proxy.Contracts.dll \
  --target-assembly /abs/path/My.Targets.dll \
  --map-file /abs/path/ducktype-aot-map.json \
  --output /abs/path/Datadog.Trace.DuckType.AotRegistry.MyApp.dll
```

### 3. Validate compatibility

```bash
dotnet tracer/src/Datadog.Trace.Tools.Runner/bin/Release/Tool/net8.0/Datadog.Trace.Tools.Runner.dll \
  ducktype-aot verify-compat \
  --compat-report /abs/path/Datadog.Trace.DuckType.AotRegistry.MyApp.dll.compat.md \
  --compat-matrix /abs/path/Datadog.Trace.DuckType.AotRegistry.MyApp.dll.compat.json \
  --failure-mode strict
```

### 4. Build app with generated props

```bash
dotnet publish /abs/path/MyApp.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  /p:PublishAot=true \
  /p:DuckTypeAotPropsPath=/abs/path/Datadog.Trace.DuckType.AotRegistry.MyApp.props
```

## CI Pipeline Pattern

Use this stage layout:

1. `build-contracts`: builds proxy and target assemblies.
2. `generate-ducktype-aot`: runs `ducktype-aot generate` and stores all outputs as artifacts.
3. `verify-ducktype-aot`: runs `verify-compat --failure-mode strict`.
4. `publish-nativeaot`: publishes app using `DuckTypeAotPropsPath`.
5. `aot-smoke`: runs generated binary and validates key output.

## Artifact Publishing Rules

Publish these as a single immutable artifact set:

1. `*.dll` registry assembly.
2. `*.dll.manifest.json`.
3. `*.dll.compat.json`.
4. `*.dll.compat.md`.
5. `*.linker.xml`.
6. `*.props`.

Do not publish only the registry DLL without the companion files.

## MSBuild Consumption

In the application project:

```xml
<Import Project="$(DuckTypeAotPropsPath)"
        Condition="'$(DuckTypeAotPropsPath)' != '' and Exists('$(DuckTypeAotPropsPath)')" />
```

Set in CI command line:

```bash
/p:DuckTypeAotPropsPath=/abs/path/Datadog.Trace.DuckType.AotRegistry.MyApp.props
```

## Incremental Build Strategy

Regenerate the registry when any of the following changes:

1. Proxy assembly binary hash.
2. Target assembly binary hash.
3. Map file contents.
4. Mapping catalog contents.
5. Generic instantiations file contents.
6. Datadog.Trace runtime identity (assembly name/version/MVID/schema).

The manifest can be used as a cache key material source for this invalidation.

## Multi-Service Monorepo Strategy

For monorepos with many services:

1. Generate one registry per deployable service/runtime process.
2. Keep one registry identity per process to satisfy runtime isolation rules.
3. Version artifact names with service and build id to avoid accidental reuse.
4. Avoid shared mutable output folders between services.

## Failure Handling in CI

Treat these as hard failures:

1. `generate` exits non-zero.
2. `verify-compat --failure-mode strict` exits non-zero.
3. Missing required generated artifacts.
4. NativeAOT publish fails with linker/root errors.
5. Runtime smoke indicates missing mappings or mode conflicts.

## Recommended CI Log Attachments

Attach the following on failure:

1. Generator command line (with secrets redacted).
2. `*.compat.md` report.
3. `*.manifest.json`.
4. NativeAOT publish log.
5. Runtime smoke output.

## Determinism and Reproducibility

1. Use pinned SDK via `global.json`.
2. Keep map/catalog files in repo.
3. Use canonical absolute paths in scripts and logs.
4. Avoid non-deterministic wildcard target selection unless filtered and audited.
5. Keep `ducktype-aot` tool version tied to the same repository revision as runtime consumption.

## Security and Signing

If strong-name signing is required:

1. Use secured key storage in CI.
2. Inject key path through secure variables.
3. Prefer environment variable only in secure agent context.
4. Avoid printing key paths in verbose logs when policies require masking.

## Suggested Release Gate

A practical release gate is:

1. Dynamic DuckTyping baseline tests pass.
2. AOT registry generation succeeds.
3. `verify-compat --failure-mode strict` passes.
4. NativeAOT publish succeeds.
5. Smoke app confirms no dynamic code path dependency at runtime.

## Related Documents

1. [DuckTyping.NativeAOT.md](./DuckTyping.NativeAOT.md)
2. [DuckTyping.NativeAOT.Spec.md](./DuckTyping.NativeAOT.Spec.md)
3. [DuckTyping.NativeAOT.CompatibilityMatrix.md](./DuckTyping.NativeAOT.CompatibilityMatrix.md)
4. [DuckTyping.NativeAOT.Troubleshooting.md](./DuckTyping.NativeAOT.Troubleshooting.md)
5. [DuckTyping.NativeAOT.Testing.md](./DuckTyping.NativeAOT.Testing.md)
