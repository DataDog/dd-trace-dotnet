# DuckTyping Dynamic vs AOT Parity Closure Plan

## Summary
1. Primary goal: enforce strict 100% parity where AOT and Dynamic produce the same scenario outcome for all DuckTyping coverage (`A-*`, `B-*`, `C-*`, `D-*`, `E-*`, `FG-*`, `FS-*`, `FF-*`, `FM-*`, `RT-*`, `EX-*`, `TX-*`).
2. Hard policy: no known-limitations list, no non-compatible allowlist, no scenario override channel.
3. Any Dynamic vs AOT mismatch is a release blocker.
4. This plan closes the remaining gaps by hardening deterministic execution, removing exception-style compatibility remapping, and tightening CI gates.

## Parity Contract (Non-Negotiable)
1. Parity is defined as exact outcome equivalence per scenario:
   1. success vs failure,
   2. value/shape behavior for success paths,
   3. exception category/trigger for failure paths.
2. No exception inventory is accepted:
   1. no known limitations contract,
   2. no expected non-compatible outcomes contract,
   3. no per-scenario compatibility override in map/catalog.
3. Compatibility gate must be derived from differential execution and strict artifact validation.
4. No changes to dynamic runtime semantics are allowed to make AOT pass.

## Gap-to-Solution Matrix
1. Gap: Full-suite parity test is opt-in and can silently no-op.
   1. Root cause: env guard returns early in parity integration test.
   2. Solution: dedicated CI gate target that always sets parity env vars and runs the targeted parity test.
   3. Files: [DuckTypeAotFullSuiteParityIntegrationTests.cs](/Users/tony.redondo/repos/github/Datadog/dd-trace-dotnet/tracer/test/Datadog.Trace.Tools.Runner.Tests/DuckTypeAotFullSuiteParityIntegrationTests.cs), [Build.Steps.cs](/Users/tony.redondo/repos/github/Datadog/dd-trace-dotnet/tracer/build/_build/Build.Steps.cs), [Build.cs](/Users/tony.redondo/repos/github/Datadog/dd-trace-dotnet/tracer/build/_build/Build.cs).
2. Gap: Full-suite parity harness is nondeterministic.
   1. Root cause: randomized order without fixed seed in nested child test runs.
   2. Solution: inject deterministic `RANDOM_SEED` into both child runs, record seed in logs, and keep artifacts for failed runs.
   3. Files: [DuckTypeAotFullSuiteParityIntegrationTests.cs](/Users/tony.redondo/repos/github/Datadog/dd-trace-dotnet/tracer/test/Datadog.Trace.Tools.Runner.Tests/DuckTypeAotFullSuiteParityIntegrationTests.cs).
3. Gap: Exception-style compatibility remapping exists (`expectCanCreate`, effective-status transformations).
   1. Root cause: tooling allows compatibility-preserving remap of emitter incompatibilities.
   2. Solution: remove override schema and remapping logic; keep direct strict status semantics.
   3. Files: [DuckTypeAotMapFileParser.cs](/Users/tony.redondo/repos/github/Datadog/dd-trace-dotnet/tracer/src/Datadog.Trace.Tools.Runner/DuckTypeAot/DuckTypeAotMapFileParser.cs), [DuckTypeAotMapping.cs](/Users/tony.redondo/repos/github/Datadog/dd-trace-dotnet/tracer/src/Datadog.Trace.Tools.Runner/DuckTypeAot/DuckTypeAotMapping.cs), [DuckTypeAotMappingResolver.cs](/Users/tony.redondo/repos/github/Datadog/dd-trace-dotnet/tracer/src/Datadog.Trace.Tools.Runner/DuckTypeAot/DuckTypeAotMappingResolver.cs), [DuckTypeAotArtifactsWriter.cs](/Users/tony.redondo/repos/github/Datadog/dd-trace-dotnet/tracer/src/Datadog.Trace.Tools.Runner/DuckTypeAot/DuckTypeAotArtifactsWriter.cs), [DuckTypeAotCompatibilityStatuses.cs](/Users/tony.redondo/repos/github/Datadog/dd-trace-dotnet/tracer/src/Datadog.Trace.Tools.Runner/DuckTypeAot/DuckTypeAotCompatibilityStatuses.cs).
4. Gap: Verify tooling still exposes scenario exception channels.
   1. Root cause: legacy expected-outcomes/known-limitations contract support.
   2. Solution: enforce strict-empty contracts for parity gate and remove scenario override semantics from strict path.
   3. Files: [DuckTypeAotVerifyCompatCommand.cs](/Users/tony.redondo/repos/github/Datadog/dd-trace-dotnet/tracer/src/Datadog.Trace.Tools.Runner/DuckTypeAot/DuckTypeAotVerifyCompatCommand.cs), [DuckTypeAotVerifyCompatProcessor.cs](/Users/tony.redondo/repos/github/Datadog/dd-trace-dotnet/tracer/src/Datadog.Trace.Tools.Runner/DuckTypeAot/DuckTypeAotVerifyCompatProcessor.cs), [ducktype-aot-bible-expected-outcomes.json](/Users/tony.redondo/repos/github/Datadog/dd-trace-dotnet/tracer/test/Datadog.Trace.DuckTyping.Tests/AotCompatibility/ducktype-aot-bible-expected-outcomes.json), [ducktype-aot-bible-known-limitations.json](/Users/tony.redondo/repos/github/Datadog/dd-trace-dotnet/tracer/test/Datadog.Trace.DuckTyping.Tests/AotCompatibility/ducktype-aot-bible-known-limitations.json).

## Implementation Plan (Detailed)

## Phase 1: Deterministic Full-Suite Parity Harness
1. Add deterministic seed plumbing in parity integration test:
   1. New helper `ResolveParitySeed()` reads `DD_DUCKTYPE_AOT_FULL_SUITE_PARITY_SEED`; fallback to fixed default constant.
   2. Inject `RANDOM_SEED=<seed>` in both child `dotnet test` invocations (dynamic and aot).
   3. Print seed in failure message and artifact metadata.
2. Add artifact retention mode:
   1. New helper `ShouldKeepArtifacts()` from `DD_DUCKTYPE_AOT_FULL_SUITE_PARITY_KEEP_ARTIFACTS`.
   2. On failure or keep flag, skip temp-dir cleanup and print retained directory.
3. Stabilize harness diagnostics:
   1. Include paths to `dynamic.trx`, `aot.trx`, discovered map, sanitized map, generated registry.
   2. Include counts for excluded mappings and first N exclusions.
4. Optional stabilization for known flaky assembly-count test:
   1. Keep full run coverage unchanged.
   2. If flaky behavior persists after deterministic seed, run parity gate with fixed seed and single max CPU process setting (via runner invocation options) to avoid ordering jitter.

## Phase 2: Mandatory CI Gate Wiring
1. Add new Nuke target `RunDuckTypeAotFullSuiteParityGate`:
   1. Target command:
      1. `dotnet test tracer/test/Datadog.Trace.Tools.Runner.Tests/Datadog.Trace.Tools.Runner.Tests.csproj`
      2. `--framework net8.0`
      3. `--filter FullyQualifiedName~DuckTypeAotFullSuiteParityIntegrationTests`
   2. Env vars in target:
      1. `DD_RUN_DUCKTYPE_AOT_FULL_SUITE_PARITY=1`
      2. `DD_DUCKTYPE_AOT_FULL_SUITE_PARITY_SEED=<fixed>`
      3. `RANDOM_SEED=<same fixed>`
2. Wire the new target into build graph:
   1. `BuildAndRunManagedUnitTests` depends on `RunDuckTypeAotFullSuiteParityGate`.
   2. Ensure ordering: after runner tool build and after compatibility gate.
3. Keep target isolated:
   1. run only this filtered test,
   2. avoid full `Tools.Runner.Tests` overhead.

## Phase 3: Remove `expectCanCreate` and Remapping Logic
1. Data model cleanup:
   1. Remove parity expectation enum/property from `DuckTypeAotMapping`.
   2. Remove related constructor parameters and fluent helpers.
2. Parser cleanup:
   1. Remove `expectCanCreate` from map and catalog JSON DTOs.
   2. If field is present, return validation error in strict mode to avoid silent behavior.
3. Resolver cleanup:
   1. Remove propagation/merge of parity expectation from catalog to resolved mappings.
4. Artifacts cleanup:
   1. Delete status transformation helpers in `DuckTypeAotArtifactsWriter` that convert incompatible to compatible.
   2. Persist direct emission status only.
5. Status catalog cleanup:
   1. Remove parity-expectation-specific status constants that no longer apply.
6. Test updates:
   1. Delete tests that validate remapping behavior.
   2. Replace with tests asserting direct strict compatibility semantics.

## Phase 4: Strict Verify-Compat Without Scenario Overrides
1. Verify processor strict rules:
   1. If expected-outcomes/known-limitations file is provided and contains entries, fail with deterministic error.
   2. Accept only empty documents for backward compatibility transition.
2. Command behavior:
   1. Keep options temporarily for compatibility with existing scripts.
   2. Mark as strict-empty only, with clear deprecation warning.
3. Parity gate behavior:
   1. Required mappings from catalog + scenario inventory must all be present.
   2. Any non-compatible mapping in required scope fails.
4. Contract files:
   1. Keep checked-in expected outcomes and known limitations empty.
   2. Add tests to enforce they remain empty.

## Phase 5: Scenario Data and Differential Coverage Alignment
1. Remove exception metadata from scenario assets:
   1. Delete `expectCanCreate` entries from mapping catalog.
2. Preserve scenario IDs and coverage:
   1. Keep all current IDs in inventory and catalog.
   2. Ensure each scenario still has differential test coverage in DuckTyping tests.
3. Enhance mismatch reporting:
   1. Include scenario ID, mode, proxy, target, dynamic outcome, AOT outcome.
   2. Include failing assertion message snippet for quick triage.

## Phase 6: Documentation and Developer Workflow
1. Update NativeAOT docs to declare strict parity policy with no exception channels.
2. Add canonical local commands:
   1. deterministic parity integration run,
   2. compatibility gate run,
   3. full gate sequence.
3. Add troubleshooting notes:
   1. how to re-run with retained artifacts,
   2. how to inspect generated compat matrix and manifests,
   3. how to compare dynamic vs aot trx quickly.

## Ordered Work Breakdown (Implementation Sequence)
1. Land deterministic harness changes and tests.
2. Land Nuke gate target and build graph wiring.
3. Remove `expectCanCreate` model/parser/resolver/artifact/status code paths.
4. Enforce strict-empty expected-outcomes/known-limitations behavior.
5. Clean catalog JSON and adjust tests.
6. Update docs and CI command references.
7. Run full validation matrix before merge.

## Validation Matrix (Must Pass)
1. Unit tests:
   1. `DuckTypeAotProcessorsTests`
   2. `DuckTypeAotNativeAotPublishIntegrationTests`
   3. `DuckTypeAotFullSuiteParityIntegrationTests` (env-enabled).
2. Gate commands:
   1. `ducktype-aot generate` succeeds for Bible catalog scope.
   2. `ducktype-aot verify-compat --failure-mode strict` succeeds with no non-compatible statuses.
3. Determinism:
   1. run parity harness 3 times with same seed,
   2. all 3 runs green.
4. Contract enforcement:
   1. expected-outcomes file empty,
   2. known-limitations file empty,
   3. mapping catalog contains no exception metadata fields.
5. Regression checks:
   1. dynamic ducktyping suite remains green,
   2. differential parity suites remain green.

## Rollout and Risk Controls
1. Rollout in two PR-sized batches:
   1. Batch A: deterministic gate hardening and CI wiring.
   2. Batch B: exception-mechanism removal and strict verify semantics.
2. Risk: transient parity flake in `GetAssemblyTests`.
   1. Mitigation: fixed seed + artifact retention + targeted rerun workflow.
3. Risk: script compatibility for verify command options.
   1. Mitigation: keep options but strict-empty enforcement first; remove fully in later cleanup PR.

## Important Public API / Interface / Type Changes
1. No public `Datadog.Trace.DuckTyping` runtime API change.
2. Internal AOT tooling changes:
   1. remove `expectCanCreate` data-path support,
   2. remove parity remap statuses and logic,
   3. keep direct strict compatibility status semantics.
3. `verify-compat` contract behavior change:
   1. scenario override channels are non-authoritative,
   2. strict parity mode rejects non-empty overrides.

## Assumptions and Defaults
1. `net8.0` remains parity reference TFM.
2. Scope is DuckTyping runtime + AOT tooling only.
3. Dynamic runtime behavior is immutable for this effort.
4. Strict parity applies to all checked-in Bible scenario IDs and differential suites.
