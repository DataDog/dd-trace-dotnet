# DuckTyping NativeAOT Compatibility Matrix

## Purpose

This matrix summarizes NativeAOT parity coverage against the dynamic DuckTyping implementation and the [DuckTyping.Bible.md](./DuckTyping.Bible.md) feature model.

It is intended as a quick status index, while detailed behavior and examples remain in the Bible and test suites.

## Status Legend

1. `Compatible`: behavior parity validated by the AOT parity harness.
2. `Conditional`: compatible when specific mapping/build constraints are satisfied.
3. `Not In Scope`: intentionally outside current DuckTyping scope.

## Current Baseline

As of February 28, 2026 in this repository branch state:

1. All checklist items in `DuckTyping-NativeAOT-Remaining-Checklist.md` are marked complete.
2. Differential parity coverage is implemented for Bible scenario families and excerpt suites.
3. Compatibility enforcement is wired through `verify-compat` and expected outcomes inputs.

## Feature Family Matrix

| Family | Scope | Dynamic | AOT | Status | Primary Coverage Signals |
|---|---|---|---|---|---|
| Forward proxies | Interface/class/abstract forwarding | Supported | Supported | Compatible | `A-01..E-42` parity inventory |
| Reverse proxies | Delegation-based reverse implementation | Supported | Supported | Compatible | `A-01..E-42` + reverse scenarios |
| DuckCopy projection | Struct copy projection | Supported | Supported | Compatible | Bible scenario set + excerpts |
| Field/property mapping | Public/non-public mapping permutations | Supported | Supported | Compatible | `FG-*`, `FS-*`, `FF-*` |
| Method mapping | Signatures, overload constraints, conversions | Supported | Supported | Compatible | `FM-*` and differential parity |
| Return/argument conversion | Primitive/reference/value conversion paths | Supported | Supported | Compatible | `RT-*` scenarios |
| Generic mapping closure | Closed generic mappings in scope | Supported | Supported | Conditional | Requires resolvable closed generic mappings |
| Mode isolation | Dynamic vs AOT mode immutability | Supported | Supported | Compatible | `DuckTypeAotEngineTests` isolation suite |
| Concurrency initialization | Parallel registration/init behavior | Supported | Supported | Compatible | AOT engine concurrency tests |
| NativeAOT publish runtime | No runtime emit/no dynamic proxy generation | Not applicable | Supported | Compatible | `DuckTypeAotNativeAotPublishIntegrationTests` |

## Conditional Compatibility Notes

The following are compatibility-sensitive constraints rather than behavior gaps:

1. Mappings must be declared/resolved before runtime use.
2. Closed generic mappings must be resolvable and supported by generator rules.
3. Runtime must load one registry assembly identity per process.
4. Registry/runtime contract fingerprints must match expected validation rules.

These constraints are enforced by build-time generation and runtime contract checks.

## Scenario Inventory Coverage Summary

Coverage groups tracked by parity orchestration include:

1. Bible core families: `A-01..E-42`.
2. IL atlas families: `FG-*`, `FS-*`, `FF-*`, `FM-*`, `RT-*`.
3. Bible examples: `EX-01..EX-20`.
4. Test-adapted excerpts: `TX-A..TX-T`.

## Compatibility Gates

Recommended gates for any parity-sensitive change:

1. Dynamic baseline tests.
2. AOT generation and compatibility verification.
3. Differential parity orchestration.
4. NativeAOT publish integration tests.

See [DuckTyping.NativeAOT.Testing.md](./DuckTyping.NativeAOT.Testing.md) for exact commands.

## How to Update This Matrix

Update this matrix when one of the following changes:

1. New Bible feature family added.
2. New compatibility status introduced in generator/verify tooling.
3. New parity suite or scenario ID family is added.
4. Expected outcomes policy changes for strict/default gating.

When updating, keep this matrix synchronized with:

1. `ducktype-aot-bible-scenario-inventory.json`
2. `ducktype-aot-bible-expected-outcomes.json`
3. `DuckTyping-NativeAOT-Remaining-Checklist.md`

## Related Documents

1. [DuckTyping.Bible.md](./DuckTyping.Bible.md)
2. [DuckTyping.NativeAOT.md](./DuckTyping.NativeAOT.md)
3. [DuckTyping.NativeAOT.Spec.md](./DuckTyping.NativeAOT.Spec.md)
4. [DuckTyping.NativeAOT.Testing.md](./DuckTyping.NativeAOT.Testing.md)
5. [DuckTyping-NativeAOT-Remaining-Checklist.md](./for-ai/DuckTyping-NativeAOT-Remaining-Checklist.md)
