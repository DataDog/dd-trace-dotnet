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
2. Ensure mapping/catalog/inventory/expected-outcomes test assets are present.
3. Ensure environment variables for mode selection are set per scenario.

## Core Commands

### Dynamic baseline test suite

```bash
DD_DUCKTYPE_TEST_MODE=dynamic \
  dotnet test tracer/test/Datadog.Trace.DuckTyping.Tests/Datadog.Trace.DuckTyping.Tests.csproj \
  -c Release --framework net8.0
```

### AOT suite execution with pre-generated registry

```bash
DD_DUCKTYPE_TEST_MODE=aot \
DD_DUCKTYPE_AOT_REGISTRY_PATH=/abs/path/Datadog.Trace.DuckType.AotRegistry.dll \
  dotnet test tracer/test/Datadog.Trace.DuckTyping.Tests/Datadog.Trace.DuckTyping.Tests.csproj \
  -c Release --framework net8.0 --no-build
```

### Full differential parity orchestration

```bash
DD_RUN_DUCKTYPE_AOT_FULL_SUITE_PARITY=1 \
  dotnet test tracer/test/Datadog.Trace.Tools.Runner.Tests/Datadog.Trace.Tools.Runner.Tests.csproj \
  -c Release --framework net8.0 \
  --filter FullyQualifiedName~DuckTypeAotFullSuiteParityIntegrationTests
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
dotnet tracer/src/Datadog.Trace.Tools.Runner/bin/Release/Tool/net8.0/Datadog.Trace.Tools.Runner.dll \
  ducktype-aot verify-compat \
  --compat-report /abs/path/Datadog.Trace.DuckType.AotRegistry.dll.compat.md \
  --compat-matrix /abs/path/Datadog.Trace.DuckType.AotRegistry.dll.compat.json \
  --mapping-catalog tracer/test/Datadog.Trace.DuckTyping.Tests/AotCompatibility/ducktype-aot-bible-mapping-catalog.json \
  --scenario-inventory tracer/test/Datadog.Trace.DuckTyping.Tests/AotCompatibility/ducktype-aot-bible-scenario-inventory.json \
  --expected-outcomes tracer/test/Datadog.Trace.DuckTyping.Tests/AotCompatibility/ducktype-aot-bible-expected-outcomes.json \
  --manifest /abs/path/Datadog.Trace.DuckType.AotRegistry.dll.manifest.json \
  --failure-mode strict
```

## Scenario Family Coverage Expectations

The parity harness should cover:

1. Bible families `A-01..E-42`.
2. IL atlas IDs `FG-*`, `FS-*`, `FF-*`, `FM-*`, `RT-*`.
3. Bible examples `EX-01..EX-20`.
4. Test-adapted excerpts `TX-A..TX-T`.

Any newly added scenario IDs should fail CI until included in expected outcomes/policy inputs.

## NativeAOT Publish Validation

NativeAOT validation should assert:

1. App publishes with `/p:PublishAot=true`.
2. Generated registry props/descriptors are consumed.
3. Runtime reports no dependency on runtime dynamic code generation.
4. Forward, reverse, and DuckCopy paths execute correctly.

Use:

```bash
dotnet test tracer/test/Datadog.Trace.Tools.Runner.Tests/Datadog.Trace.Tools.Runner.Tests.csproj \
  -c Release --framework net8.0 \
  --filter FullyQualifiedName~DuckTypeAotNativeAotPublishIntegrationTests
```

## CI Gate Recommendations

Minimum protected-branch gate:

1. Dynamic baseline tests pass.
2. Full parity orchestration passes.
3. Runner AOT suite passes.
4. Strict verify-compat passes for produced artifacts.
5. NativeAOT publish integration test passes.

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
5. No unreviewed scenario IDs in inventory/catalog/expected outcomes.

## Related Documents

1. [DuckTyping.NativeAOT.md](./DuckTyping.NativeAOT.md)
2. [DuckTyping.NativeAOT.BuildIntegration.md](./DuckTyping.NativeAOT.BuildIntegration.md)
3. [DuckTyping.NativeAOT.Spec.md](./DuckTyping.NativeAOT.Spec.md)
4. [DuckTyping.NativeAOT.CompatibilityMatrix.md](./DuckTyping.NativeAOT.CompatibilityMatrix.md)
5. [DuckTyping.NativeAOT.Troubleshooting.md](./DuckTyping.NativeAOT.Troubleshooting.md)
