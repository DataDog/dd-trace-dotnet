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
10. [ ] Implement and test strict/default AOT failure-mode policy wiring.
11. [ ] Close artifact-contract gaps (assembly metadata, signing behavior, descriptor contract coupling checks).
12. [ ] Wire end-to-end CI/release gate for 100% compatibility and zero unreviewed scenario IDs.
13. [ ] Add NativeAOT publish integration test (no runtime emit, no dynamic assembly loading) after unit parity + compatibility gate closure.
14. [ ] Replace known-limitations allowlist with a dynamic-parity expected-outcomes contract so there are zero AOT-only limitations.

## Current Checkpoint

- Scenario inventory contract/enforcement is implemented.
- `--require-mapping-catalog` enforcement is implemented.
- Explicit feature IDs `A-01..E-42` are now implemented and passing in differential parity.
- IL atlas parity IDs are implemented and passing: `FG-1..FG-9`, `FS-1..FS-6`, `FF-1..FF-5`, `FM-1..FM-8`, `RT-1..RT-5` (including parity for unsupported invocation paths such as FS-4).
- Bible example parity IDs are implemented and passing: `EX-01..EX-20`.
- Bible test-adapted excerpt parity IDs are implemented and passing: `TX-A..TX-T`.
- `verify-compat` now supports an explicit known-limitations allowlist (`--known-limitations`) with stale/mismatch detection, and catalog validation now allows only approved non-compatible required mappings.
- Checked-in approved known limitations are tracked in `tracer/test/Datadog.Trace.DuckTyping.Tests/AotCompatibility/ducktype-aot-bible-known-limitations.json`.
- Runtime mode isolation/immutability tests are implemented in `DuckTypeAotEngineTests` and use `DuckType.ResetRuntimeModeForTests()` for deterministic cross-test isolation.
- Concurrency coverage is implemented in `DuckTypeAotEngineTests` for concurrent `EnableAotMode`, mixed dynamic/AOT initialization races, and concurrent duplicate AOT registrations.
- Next active focus: item 10 (unit parity and compatibility policy closure before publish integration).
