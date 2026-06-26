# DuckTyping NativeAOT Parity Stabilization Plan

## Goal

Reach and keep **100% behavioral parity** between Dynamic DuckTyping and NativeAOT DuckTyping for:

1. `Datadog.Trace.DuckTyping.Tests` full suite.
2. DuckTyping Bible scenarios and parity excerpts.
3. Assertion outcomes and assertion messages (not only pass/fail counts).
4. Full execution and pass of the DuckTyping suite in AOT mode.

This plan is focused on parity correctness and test reliability, without changing Dynamic runtime behavior in production paths.

## Non-Negotiable Quality Bar

1. AOT is not accepted if it only "matches failures" from Dynamic.
2. For the target validation TFM (`net8.0`), the full `Datadog.Trace.DuckTyping.Tests` suite must:
   1. execute end-to-end in Dynamic mode,
   2. execute end-to-end in AOT mode,
   3. have all test outcomes `Passed` in both modes.
3. Parity requires both:
   1. same executed test set,
   2. same behavior for each test,
   3. and zero failures as release gate.
4. Any skipped tests must be explicitly tracked and approved; "silent skip drift" is not allowed.

## Current Problems To Fix

1. Running the full DuckTyping suite in a single mixed runtime process can produce invalid failures due to runtime mode contamination.
2. Parity evidence exists in isolated runs, but it must be enforced as the canonical validation path.
3. Generation input from dynamic discovery can include non-portable entries (runtime-generated assemblies), so sanitization rules must be explicit and validated.
4. Existing parity logic must be hard-gated on all tests passing, not only equivalent outcomes.

## Current Baseline Status (as of February 28, 2026, branch state)

1. Raw full-suite run of `Datadog.Trace.DuckTyping.Tests` on `net8.0` is green in Dynamic mode (`0 failed / 5448 passed / 5 skipped / 5453 total`).
2. Isolated full-suite parity harness is green and now enforces hard criteria:
   1. Dynamic run exit code must be success.
   2. AOT run exit code must be success.
   3. Dynamic run must have zero `Failed/Error` outcomes.
   4. AOT run must have zero `Failed/Error` outcomes.
3. AOT processor test suite and NativeAOT publish integration test suite are green.
4. Remaining work is CI gate wiring and workflow/documentation hardening.

## Scope Rules

1. Keep Dynamic and AOT runtime implementations isolated.
2. Share only intended cross-cutting contracts/helpers.
3. Never weaken existing Dynamic behavior to make AOT pass.
4. Every parity gate compares Dynamic vs AOT under identical test inputs.
5. The canonical gate is "equal and green", not just "equal."

## Phase 0: Green Baseline Establishment

1. Establish a deterministic Dynamic-only baseline run for `Datadog.Trace.DuckTyping.Tests` on `net8.0`.
2. Fail the stabilization effort if Dynamic baseline is red until baseline regressions are fixed or explicitly triaged.
3. Produce a machine-readable baseline manifest:
   1. total tests discovered,
   2. total executed,
   3. per-test outcome,
   4. per-test assertion message when failed.
4. Lock this baseline as the reference for AOT parity gating.

## Phase 1: Test Runtime Isolation Hardening

1. Enforce explicit runtime mode bootstrap for test processes using environment variables.
2. Add fail-fast guardrails in test bootstrap/helpers for invalid mode transitions inside the same process.
3. Add deterministic reset boundaries for runtime-mode-sensitive tests to avoid cross-test contamination.
4. Ensure default local command guidance does not rely on mixed-mode single-process execution for parity claims.

## Phase 2: Full-Suite Parity Gate Hardening

1. Keep the full-suite orchestrator test that runs:
   1. Dynamic run with discovery map emission.
   2. AOT registry generation.
   3. AOT run with generated registry.
2. Enforce hard pass criteria before parity diff:
   1. Dynamic run exit code is success.
   2. AOT run exit code is success.
   3. Dynamic has zero failed/error tests.
   4. AOT has zero failed/error tests.
3. Compare parity on:
   1. Executed test identity set.
   2. Outcome per test (`Passed`, `Failed`, etc.).
   3. Assertion messages for failed/error tests (for debug and drift detection).
4. Keep deterministic map sanitization:
   1. Exclude only known non-portable runtime-generated assembly mappings.
   2. Reject unexpected exclusions.
5. Resolve full proxy/target assembly closure for generation, including attribute-discovered dependencies.

## Phase 3: Compatibility Parser and Generation Reliability

1. Keep and expand tests for closed/open generic name detection in assembly-qualified nested generic signatures.
2. Prevent false-open-generic detection regressions.
3. Ensure generated mapping resolution remains stable across:
   1. Core framework assemblies.
   2. Vendored assemblies.
   3. Test-only external dependencies.

## Phase 4: CI Enforcement

1. Add/keep a required PR gate for:
   1. `DuckTypeAotProcessorsTests`.
   2. `DuckTypeAotNativeAotPublishIntegrationTests`.
   3. `DuckTypeAotFullSuiteParityIntegrationTests` (with parity env var enabled).
2. Add/keep a required PR gate for DuckTyping Bible parity subset.
3. Add/keep a required PR gate for Dynamic baseline full-suite green run.
4. Publish artifacts for debugging failures:
   1. Dynamic TRX.
   2. AOT TRX.
   3. Discovered map.
   4. Sanitized map.
   5. Generated registry compatibility artifacts.

## Phase 5: Developer Workflow and Documentation

1. Document canonical parity commands and expected environment variables.
2. Document that mixed-mode single-process results are non-authoritative for parity.
3. Add a short troubleshooting section for common parity failures:
   1. Missing assembly resolution.
   2. Unexpected exclusions in sanitized mapping.
   3. Assertion message drift.

### Canonical Commands (Current)

1. Dynamic full-suite baseline (`net8.0`):
   1. `DD_DUCKTYPE_TEST_MODE=dynamic dotnet test tracer/test/Datadog.Trace.DuckTyping.Tests/Datadog.Trace.DuckTyping.Tests.csproj -c Release --framework net8.0`
2. Full isolated parity harness (Dynamic discovery -> AOT generation -> AOT run):
   1. `DD_RUN_DUCKTYPE_AOT_FULL_SUITE_PARITY=1 dotnet test tracer/test/Datadog.Trace.Tools.Runner.Tests/Datadog.Trace.Tools.Runner.Tests.csproj -c Release --framework net8.0 --filter FullyQualifiedName~DuckTypeAotFullSuiteParityIntegrationTests`
3. AOT processor and NativeAOT integration gates:
   1. `dotnet test tracer/test/Datadog.Trace.Tools.Runner.Tests/Datadog.Trace.Tools.Runner.Tests.csproj -c Release --framework net8.0 --filter FullyQualifiedName~DuckTypeAot`

### Runtime Mode Rules

1. Always run Dynamic and AOT validations in isolated processes.
2. Never claim parity from a mixed runtime-mode single-process run.
3. Set `DD_DUCKTYPE_TEST_MODE` explicitly (`dynamic` or `aot`) for direct suite runs.

### Troubleshooting Cheatsheet

1. Missing assembly resolution:
   1. Verify proxy and target assemblies are present in `bin/Release/net8.0` or explicitly passed to generator.
   2. Check parity harness logs for `proxy-assembly-unresolved` or `target-assembly-unresolved`.
2. Unexpected sanitized-map exclusions:
   1. Inspect discovered map and sanitized map from parity artifacts.
   2. Only runtime-generated assembly exclusions are expected by default.
3. Assertion drift:
   1. Inspect Dynamic TRX vs AOT TRX error messages for the same test case.
   2. Treat message differences as parity failures, not informational noise.

## Acceptance Criteria

1. Dynamic full-suite baseline is green (`0 failed`) on `net8.0`.
2. AOT full-suite run is green (`0 failed`) on `net8.0`.
3. Full-suite isolated parity gate passes with:
   1. identical test sets,
   2. identical outcomes,
   3. identical assertion details for any non-passed case (should be none in release gate).
4. DuckTyping Bible parity tests pass.
5. AOT processor and NativeAOT publish integration tests pass.
6. No unexpected mapping exclusions remain in parity generation.
7. CI runs all required parity gates on every PR touching DuckTyping/AOT runner code.

## Execution Order

1. Establish Dynamic green baseline (Phase 0).
2. Finalize runtime isolation guardrails.
3. Lock full-suite parity gate semantics with hard green criteria.
4. Close any remaining generic parsing or mapping-resolution corner cases.
5. Make parity and baseline gates required in CI.
6. Update docs and contributor workflow.

## Out of Scope For This Stabilization Pass

1. Auto-discovery of mappings from arbitrary call sites outside declared/discovered contract.
2. New feature design beyond parity stabilization.
3. Runtime dynamic emit redesign.
