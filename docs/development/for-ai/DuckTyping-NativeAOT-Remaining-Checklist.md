# DuckTyping NativeAOT Remaining Checklist

This file is the persistent execution checklist for the remaining work from [DuckTyping-NativeAOT-Plan.md](/Users/tony.redondo/repos/github/Datadog/dd-trace-dotnet/docs/development/for-ai/DuckTyping-NativeAOT-Plan.md).

## Ordered Sequence

1. [x] Expand Bible mapping catalog to full scoped coverage (seed exists; full catalog still pending).
2. [x] Expand differential parity harness so each scenario runs in both Dynamic and AOT and compares outcomes structurally.
3. [x] Complete explicit Bible feature ID coverage (`A-01..E-42`).
4. [x] Add IL atlas parity coverage (`FG-*`, `FS-*`, `FF-*`, `FM-*`, `RT-*`).
5. [x] Add Bible example parity coverage (`EX-01..EX-20`).
6. [ ] Add test-adapted excerpt parity coverage (`TX-A..TX-T`).
7. [ ] Close remaining non-compatible statuses for in-scope features (or explicitly mark approved known limitations).
8. [ ] Add explicit engine-isolation and mode-immutability tests.
9. [ ] Add concurrency and once-only initialization race tests.
10. [ ] Add NativeAOT publish integration test (no runtime emit, no dynamic assembly loading).
11. [ ] Implement and test strict/default AOT failure-mode policy wiring.
12. [ ] Close artifact-contract gaps (assembly metadata, signing behavior, descriptor contract coupling checks).
13. [ ] Wire end-to-end CI/release gate for 100% compatibility and zero unreviewed scenario IDs.

## Current Checkpoint

- Scenario inventory contract/enforcement is implemented.
- `--require-mapping-catalog` enforcement is implemented.
- Explicit feature IDs `A-01..E-42` are now implemented and passing in differential parity.
- IL atlas parity IDs are implemented and passing: `FG-1..FG-9`, `FS-1..FS-6`, `FF-1..FF-5`, `FM-1..FM-8`, `RT-1..RT-5` (including parity for unsupported invocation paths such as FS-4).
- Bible example parity IDs are implemented and passing: `EX-01..EX-20`.
- Next active focus: item 6 + item 7.
