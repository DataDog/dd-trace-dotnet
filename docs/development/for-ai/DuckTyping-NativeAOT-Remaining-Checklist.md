# DuckTyping NativeAOT Remaining Checklist

This file is the persistent execution checklist for the remaining work from [DuckTyping-NativeAOT-Plan.md](/Users/tony.redondo/repos/github/Datadog/dd-trace-dotnet/docs/development/for-ai/DuckTyping-NativeAOT-Plan.md).

## Ordered Sequence

1. [x] Expand Bible mapping catalog to full scoped coverage (seed exists; full catalog still pending).
2. [x] Expand differential parity harness so each scenario runs in both Dynamic and AOT and compares outcomes structurally.
3. [x] Complete explicit Bible feature ID coverage (`A-01..E-42`).
4. [x] Add IL atlas parity coverage (`FG-*`, `FS-*`, `FF-*`, `FM-*`, `RT-*`).
5. [x] Add Bible example parity coverage (`EX-01..EX-20`).
6. [x] Add test-adapted excerpt parity coverage (`TX-A..TX-T`).
7. [x] Close remaining non-compatible statuses for in-scope features (or explicitly mark approved known limitations).
8. [x] Add explicit engine-isolation and mode-immutability tests.
9. [x] Add concurrency and once-only initialization race tests.
10. [x] Implement and test strict/default AOT failure-mode policy wiring.
11. [x] Close artifact-contract gaps (assembly metadata, signing behavior, descriptor contract coupling checks).
12. [x] Wire end-to-end CI/release gate for 100% compatibility and zero unreviewed scenario IDs.
13. [x] Add NativeAOT publish integration test (no runtime emit, no dynamic assembly loading) after unit parity + compatibility gate closure.
14. [x] Replace known-limitations allowlist with a dynamic-parity expected-outcomes contract so there are zero AOT-only limitations.

## Current Checkpoint

- Scenario inventory contract/enforcement is implemented.
- `--require-mapping-catalog` enforcement is implemented.
- Explicit feature IDs `A-01..E-42` are now implemented and passing in differential parity.
- IL atlas parity IDs are implemented and passing: `FG-1..FG-9`, `FS-1..FS-6`, `FF-1..FF-5`, `FM-1..FM-8`, `RT-1..RT-5` (including parity for unsupported invocation paths such as FS-4).
- Bible example parity IDs are implemented and passing: `EX-01..EX-20`.
- Bible test-adapted excerpt parity IDs are implemented and passing: `TX-A..TX-T`.
- `verify-compat` now supports explicit failure-mode policy wiring (`--failure-mode default|strict`) and an expected-outcomes contract (`--expected-outcomes`) to enforce per-scenario status parity.
- Legacy `--known-limitations` support remains as a deprecated input adapter, but validation semantics are driven by expected outcomes (default status + explicit scenario overrides).
- Checked-in parity expectations are tracked in `tracer/test/Datadog.Trace.DuckTyping.Tests/AotCompatibility/ducktype-aot-bible-expected-outcomes.json`.
- `generate` now supports strong-name signing via `--strong-name-key-file` and `DD_TRACE_DUCKTYPE_AOT_STRONG_NAME_KEY_FILE`, and emits signing metadata into manifest fingerprints.
- Manifest contract validation now includes registry/trimmer/props fingerprint verification, signing metadata verification, and trimmer-descriptor root coupling checks for compatible mappings.
- Nuke now includes `RunDuckTypeAotCompatibilityGate`, wired into `BuildAndRunManagedUnitTests`, generating the map from the Bible catalog and running `ducktype-aot generate` + `verify-compat --failure-mode strict` against the checked-in inventory and expected outcomes.
- Runtime mode isolation/immutability tests are implemented in `DuckTypeAotEngineTests` and use `DuckType.ResetRuntimeModeForTests()` for deterministic cross-test isolation.
- Concurrency coverage is implemented in `DuckTypeAotEngineTests` for concurrent `EnableAotMode`, mixed dynamic/AOT initialization races, and concurrent duplicate AOT registrations.
- NativeAOT publish integration coverage is implemented in `DuckTypeAotNativeAotPublishIntegrationTests` and validates generated-registry execution under `/p:PublishAot=true` for forward, reverse, and DuckCopy proxy paths, with `RuntimeFeature.IsDynamicCodeSupported == false` and zero dynamic assembly loads.
- Ordered checklist status: all listed remaining items are complete.
