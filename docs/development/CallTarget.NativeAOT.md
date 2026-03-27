# CallTarget NativeAOT Guide

## Purpose

This document describes the supported NativeAOT workflow for `CallTarget` auto-instrumentation in this repository.

The implementation goal is simple:

1. Keep the current runtime IL-emission path for normal JIT runtimes.
2. Replace runtime IL emission with generated static adapters for NativeAOT.
3. Integrate generation and rewrite into `dotnet publish /p:PublishAot=true`.

## Scope

Current v1 scope is:

1. `net8.0+` NativeAOT publish only.
2. Publish-integrated workflow only.
3. AOT-generated adapters for:
   1. begin handlers
   2. slow begin handlers
   3. end handlers
   4. `Task` / `Task<T>` async end continuations
   5. `ValueTask` / `ValueTask<T>` async end continuations
4. `CallTargetKind.Default`, `CallTargetKind.Derived`, and `CallTargetKind.Interface`.
5. DuckType-constrained instance, argument, return-value, and async-result bindings through the companion DuckType AOT pipeline.

This guide does not cover the legacy `apply-aot` folder-patching workflow. `CallTarget` NativeAOT support is build-integrated and project-driven.

## Why NativeAOT Needs a Separate Path

`CallTarget` normally adapts profiler callbacks to integration methods by generating small methods at runtime. NativeAOT does not support that model.

The NativeAOT path solves that by moving adapter generation to build time:

1. discover integration definitions from `InstrumentMethodAttribute`
2. match target methods in the application and its rewritten references
3. emit a generated registry assembly with static adapter methods
4. rewrite the compiled assemblies so they root that registry during publish/runtime

At runtime, `Datadog.Trace` resolves only pre-registered generated handlers. It does not emit IL, compile expression trees, or construct new generic helper types for the supported AOT path.

## Supported CLI Workflow

The supported public command is:

```bash
dd-trace calltarget-aot generate \
  --tracer-assembly /path/to/Datadog.Trace.dll \
  --target-folder /path/to/bin/Release/net8.0 \
  --target-filter MyApp.dll \
  --output /tmp/Datadog.Trace.CallTarget.AotRegistry.MyApp.dll \
  --assembly-name Datadog.Trace.CallTarget.AotRegistry.MyApp \
  --emit-trimmer-descriptor /tmp/Datadog.Trace.CallTarget.AotRegistry.MyApp.linker.xml \
  --emit-props /tmp/Datadog.Trace.CallTarget.AotRegistry.MyApp.props \
  --emit-targets /tmp/Datadog.Trace.CallTarget.AotRegistry.MyApp.targets \
  --emit-manifest /tmp/Datadog.Trace.CallTarget.AotRegistry.MyApp.manifest.json
```

The internal rewrite command is `dd-trace calltarget-aot rewrite`.

`rewrite` is emitted into generated MSBuild targets and is not the supported manual entry point.

## Generated Artifacts

`calltarget-aot generate` emits:

1. a generated registry assembly
2. a manifest that describes matched definitions and the rewrite plan
3. a trimmer descriptor
4. generated `.props`
5. generated `.targets`
6. compatibility report artifacts
7. when needed, a dependent DuckType AOT registry and its companion artifacts

The generated registry assembly contains:

1. static adapter methods for matched begin/end/async-end bindings
2. a bootstrap type: `Datadog.Trace.ClrProfiler.CallTarget.Generated.CallTargetAotRegistryBootstrap`
3. contract metadata rooted into the bootstrap

## Build Integration

The generated `.props` file is imported by the application project:

```xml
<Import Project="$(CallTargetAotPropsPath)"
        Condition="'$(CallTargetAotPropsPath)' != '' and Exists('$(CallTargetAotPropsPath)')" />
```

The generated `.props` file:

1. adds references to the generated registry assembly
2. adds the generated trimmer descriptor
3. imports the generated `.targets`
4. adds the dependent DuckType registry when the matched bindings require it

The generated `.targets` file:

1. runs after `CoreCompile`
2. runs before `ComputeFilesToPublish`
3. invokes `dd-trace calltarget-aot rewrite`
4. replaces `$(IntermediateAssembly)` and the selected reference assemblies with rewritten copies

That is the supported NativeAOT workflow. The published native app consumes the rewritten assemblies, not the original compiled IL.

## Runtime Contract and Registry Rules

The generated bootstrap calls into `Datadog.Trace.ClrProfiler.CallTarget.CallTargetAot` and does four things:

1. validates the generated registry contract
2. initializes the dependent DuckType registry, when present
3. enables CallTarget AOT mode
4. registers the generated handlers

The runtime enforces these rules:

1. runtime mode is process-wide and immutable once initialized
2. a single generated CallTarget registry assembly identity is allowed per process
3. mixed generated registries are rejected
4. missing registrations fail explicitly instead of falling back to runtime IL emission

The contract validation checks:

1. schema version
2. `Datadog.Trace` assembly version
3. `Datadog.Trace` module MVID
4. generated registry assembly identity

## DuckType Dependency

Some `CallTarget` integrations use DuckType constraints on:

1. the target instance
2. arguments
3. return values
4. async results

When a matched `CallTarget` binding needs that behavior, the generator emits and wires a companion DuckType AOT registry. The `CallTarget` bootstrap initializes that DuckType registry before it registers its own handlers.

This means DuckType AOT is a dependency of `CallTarget` NativeAOT for constrained bindings, but the workflow remains a single `calltarget-aot generate` step for the application.

## Current Limits

The supported NativeAOT path is intentionally narrower than the dynamic runtime path.

Known limits for v1:

1. no fallback to runtime IL emission once AOT mode is active
2. no interpreted expression-tree fallback
3. no support for static target methods
4. no support for generic target methods
5. no support for target methods with by-ref parameters
6. no support for target methods with by-ref return values
7. no attempt to preserve unsupported dynamic shapes that require runtime generic construction or runtime code generation

## Verification in This Repository

The main end-to-end gate is:

```bash
DOTNET_ROLL_FORWARD=Major DOTNET_CLI_TELEMETRY_OPTOUT=1 dotnet test \
  tracer/test/Datadog.Trace.Tools.Runner.Tests/Datadog.Trace.Tools.Runner.Tests.csproj \
  -c Release \
  --framework net8.0 \
  --no-build \
  --filter FullyQualifiedName~CallTargetAotNativeAotPublishIntegrationTests \
  /m:1
```

That test verifies a published NativeAOT sample covering:

1. begin/end
2. value-return end
3. `Task`
4. `Task<T>`
5. `ValueTask`
6. `ValueTask<T>`
7. derived matching
8. interface matching
9. DuckType-constrained bindings
10. slow begin for methods with more than 8 arguments

## Troubleshooting

If NativeAOT publish or runtime bootstrap fails, check these areas first:

1. generated `.props` path is imported by the app project
2. generated `.targets` ran and rewrote the intermediate assembly
3. the generated registry assembly is referenced and copied into publish inputs
4. the bootstrap module initializer call is present in the rewritten assembly
5. contract validation did not reject schema, version, MVID, or registry identity
6. DuckType companion artifacts were generated when constrained bindings are present

If a registration is missing in AOT mode, treat that as a generation or wiring bug. The runtime is designed to fail explicitly instead of silently falling back to dynamic IL emission.
