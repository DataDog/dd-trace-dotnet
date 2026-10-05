# DuckTyping NativeAOT Testing Playbook

## Purpose

This document defines how to test DuckTyping NativeAOT functionality, parity, and release readiness.

Use this as the operational checklist for local validation and CI gates.

## Test Layers

The testing strategy has five layers:

1. Generator correctness.
2. Compatibility verification correctness.
3. Dynamic vs AOT differential parity.
4. Runtime isolation/concurrency behavior.
5. NativeAOT publish/runtime behavior.

## Required Inputs

Before running tests:

1. Build `Datadog.Trace` and `Datadog.Trace.Tools.Runner`.
2. Ensure canonical map test assets are present (`ducktype-aot-bible-mappings.json`).
3. Ensure discovered mappings and canonical map remain in sync.
4. Ensure environment variables for mode selection are set per scenario.

## Core Commands

### Dynamic baseline test suite

```bash
DD_DUCKTYPE_TEST_MODE=dynamic \
  dotnet test tracer/test/Datadog.Trace.DuckTyping.Tests/Datadog.Trace.DuckTyping.Tests.csproj \
  -c Release --framework net8.0
```

### AOT suite execution with full-suite registry (optional diagnostics)

Use this only when the registry was generated from full-suite discovery/parity inputs.
Do not use the Bible compatibility-gate registry for this command.

```bash
DD_DUCKTYPE_TEST_MODE=aot \
DD_DUCKTYPE_AOT_REGISTRY_PATH=/abs/path/Datadog.Trace.DuckType.AotRegistry.dll \
  dotnet test tracer/test/Datadog.Trace.DuckTyping.Tests/Datadog.Trace.DuckTyping.Tests.csproj \
  -c Release --framework net8.0 --no-build
```

### Full differential parity orchestration

```bash
DD_RUN_DUCKTYPE_AOT_FULL_SUITE_PARITY=1 \
DD_DUCKTYPE_AOT_FULL_SUITE_PARITY_SEED=20260301 \
  dotnet test tracer/test/Datadog.Trace.Tools.Runner.Tests/Datadog.Trace.Tools.Runner.Tests.csproj \
  -c Release --framework net8.0 \
  --filter FullyQualifiedName~DuckTypeAotFullSuiteParityIntegrationTests
```

The DuckTyping test framework flushes discovered mappings before reporting assembly completion. Testhost termination can interrupt a process-exit flush, so the parity harness must receive a complete map before the host begins shutting down.

### Managed DuckTyping AOT gate bundle

Use the build gate bundle for protected-branch validation. It runs strict compatibility verification, full-suite dynamic-vs-AOT parity, and NativeAOT publish validation.

In CI, `RunManagedUnitTests` also runs this bundle, but only in the Linux x64 glibc net9.0 shard, which has the NativeAOT publish toolchain. Locally, `RunManagedUnitTests` skips it; invoke the bundle (or a single gate target) explicitly to run it. Set `DD_DUCKTYPE_AOT_GATES=true` (or `false`) to force (or skip) the gates in any job.

```bash
./tracer/build.sh RunDuckTypeAotGates
```

### Runner AOT-focused test suite

```bash
dotnet test tracer/test/Datadog.Trace.Tools.Runner.Tests/Datadog.Trace.Tools.Runner.Tests.csproj \
  -c Release --framework net8.0 \
  --filter FullyQualifiedName~DuckTypeAot
```

## Compatibility Verification Command

Run strict verification for contract gating:

```bash
dotnet artifacts/bin/Datadog.Trace.Tools.Runner.Tool/release_net8.0/Datadog.Trace.Tools.Runner.dll \
  ducktype-aot verify-compat \
  --compat-report /abs/path/Datadog.Trace.DuckType.AotRegistry.dll.compat.md \
  --compat-matrix /abs/path/Datadog.Trace.DuckType.AotRegistry.dll.compat.json \
  --map-file tracer/test/Datadog.Trace.DuckTyping.Tests/AotCompatibility/ducktype-aot-bible-mappings.json \
  --mapping-catalog tracer/test/Datadog.Trace.DuckTyping.Tests/AotCompatibility/ducktype-aot-bible-mapping-catalog.json \
  --scenario-inventory tracer/test/Datadog.Trace.DuckTyping.Tests/AotCompatibility/ducktype-aot-bible-scenario-inventory.json \
  --manifest /abs/path/Datadog.Trace.DuckType.AotRegistry.dll.manifest.json \
  --failure-mode strict
```

## Current Strict Bible-Gate Baseline

Strict verification expects required Bible mappings to be `compatible` (no per-scenario `expectedStatus` overrides in current baseline).

Any non-compatible status should be treated as a regression until explicitly reviewed and approved.

## Scenario Family Coverage Expectations

The parity harness should cover:

1. Bible families `A-01..E-41` in the strict generated-artifact contract.
2. IL atlas IDs `FG-*`, `FS-*`, `FF-*`, `FM-*`, `RT-*`.
3. Bible examples `EX-01..EX-20`.
4. Test-adapted excerpts `TX-A..TX-T`.

`E-42` is a non-creatable reverse type-constraint guard. It is intentionally covered by `DuckTypeAotDifferentialParityTests`, not by the strict generated-artifact catalog.

Any newly added scenario IDs should fail CI until included in mapping catalog and scenario inventory contracts.

## NativeAOT Publish Validation

NativeAOT validation should assert:

1. App publishes with `/p:PublishAot=true`.
2. Generated registry props/descriptors are consumed.
3. Runtime reports no dependency on runtime dynamic code generation.
4. Forward, reverse, and DuckCopy paths execute correctly.

Use:

```bash
./tracer/build.sh RunDuckTypeAotNativeAotPublishGate
```

Or run the focused test directly. The publish test is opt-in, so it doesn't run in every managed unit test shard:

```bash
DD_RUN_DUCKTYPE_AOT_NATIVEAOT_PUBLISH=1 \
dotnet test tracer/test/Datadog.Trace.Tools.Runner.Tests/Datadog.Trace.Tools.Runner.Tests.csproj \
  -c Release --framework net8.0 \
  --filter FullyQualifiedName~DuckTypeAotNativeAotPublishIntegrationTests
```

## CI Gate Recommendations

Minimum protected-branch gate:

1. Dynamic baseline tests pass.
2. `RunDuckTypeAotGates` passes.
3. Runner AOT suite passes.

## Failure Triage Order

When a gate fails, triage in this order:

1. Dynamic baseline failure.
2. Generation/compatibility status failures.
3. Expected outcomes mismatch.
4. Runtime isolation/mode conflict failures.
5. NativeAOT publish/runtime failures.

## Test Isolation Rules

1. Do not mix dynamic and AOT mode in the same process unless tests explicitly verify conflict behavior.
2. Ensure registry path environment variable points to the registry generated for the same runtime build.
3. Reset or isolate process state for mode-sensitive tests.

Managed parity tests also cover separate `AssemblyLoadContext` instances. Load and initialize the same generated registry in each target context so registrations use that context's target type identities. Shared contracts and `Datadog.Trace` remain in the default context. The tests check private field access, returned values, proxy caching and context isolation in both modes; dynamic proxies use emitted assemblies and AOT proxies use the loaded registry assembly. The NativeAOT executable exercises compiled registrations without assembly loading or dynamic emission. It also verifies cached failure identity and class proxies that skip a base constructor requiring arguments. Explicit interface property naming and writes are compared directly with the dynamic engine.

The full-suite matrix defaults to .NET 11 through .NET 6. It includes the current dynamic regression tests for first-chance exceptions, cached concurrent failures, generic signature validation and ref/out storage preservation. The generator checks successful metadata plans against dynamic dry-run validation when the referenced runtime types and duck attribute identities are compatible. Standalone contracts that declare metadata equivalents of the tracer's internal attributes use metadata validation, because dynamic reflection cannot bind those attribute types.

## Performance and Flakiness Guardrails

1. Use deterministic output paths per test run.
2. Avoid shared mutable artifact directories across parallel test jobs.
3. Clean stale generated artifacts before re-running publish integration tests.
4. Keep full-suite parity orchestration in a dedicated CI stage to isolate runtime mode state.

## Release Readiness Checklist

Release readiness requires all of the following:

1. Dynamic suite green.
2. AOT parity suite green.
3. Strict compatibility verification green.
4. NativeAOT publish integration green.
5. No unreviewed canonical map deltas.

## Related Documents

1. [DuckTyping.NativeAOT.md](./DuckTyping.NativeAOT.md)
2. [DuckTyping.NativeAOT.BuildIntegration.md](./DuckTyping.NativeAOT.BuildIntegration.md)
3. [DuckTyping.NativeAOT.Spec.md](./DuckTyping.NativeAOT.Spec.md)
4. [DuckTyping.NativeAOT.CompatibilityMatrix.md](./DuckTyping.NativeAOT.CompatibilityMatrix.md)
5. [DuckTyping.NativeAOT.Troubleshooting.md](./DuckTyping.NativeAOT.Troubleshooting.md)
