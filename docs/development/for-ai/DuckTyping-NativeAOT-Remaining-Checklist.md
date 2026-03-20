# DuckTyping NativeAOT Remaining Checklist (Current)

This checklist tracks the current end-state for the simplified NativeAOT mapping model.

## Status
All core items are complete for the current contract.

## Contract Items
1. [x] Canonical single mapping file is the only mapping contract.
2. [x] `ducktype-aot discover-mappings` discovers mappings from type-level attributes and writes canonical map output.
3. [x] `ducktype-aot generate` consumes `--map-file`.
4. [x] `ducktype-aot verify-compat` consumes `--map-file` and enforces strict compatibility rules.
5. [x] Strict verification fails on set mismatch (missing/extra mappings) and non-compatible statuses.
6. [x] Canonical-map drift check is wired into gate workflow.
7. [x] Reverse type-level mapping support is available via `[DuckReverse]`.
8. [x] One proxy definition to multiple targets is supported via repeated type-level attributes.
9. [x] DuckTyping NativeAOT docs and onboarding are aligned with the single-map workflow.

## Validation Baseline
1. DuckType AOT tooling tests pass.
2. DuckTyping dynamic and AOT test suites pass under current branch policy.
3. Strict compatibility gate passes with the canonical map.
