# DuckTyping Dynamic vs AOT Parity Closure (Final State)

## Goal
Enforce strict behavioral parity between Dynamic DuckTyping and AOT DuckTyping for mapped scenarios.

## Locked Contract
1. Dynamic behavior is the semantic reference.
2. AOT must not add special-case compatibility overrides.
3. Strict gate outcome is binary:
   - pass only when all mapped entries are `compatible`,
   - fail otherwise.

## Implemented Closure Points
1. Strict map-driven verification:
   - `verify-compat` validates only the canonical `--map-file` mapping set against the generated matrix.
2. Deterministic parity gate:
   - build/test gates run strict compatibility checks and fail on any mismatch.
3. Exception-channel removal from compatibility contract:
   - compatibility verification no longer relies on separate scenario override files.
4. Emitter/runtime parity improvements:
   - default interface proxies emit as value types (unless `[DuckAsClass]`),
   - typed activator registration via method handles,
   - avoidable boxing reduced where parity allows.

## Gate Sequence (Current)
1. `ducktype-aot discover-mappings` (drift input generation).
2. `ducktype-aot generate --map-file ...`.
3. `ducktype-aot verify-compat --map-file ... --failure-mode strict`.

## Acceptance Criteria
1. Canonical map and compat matrix identity sets are identical.
2. Every mapped entry status is `compatible`.
3. Full DuckTyping dynamic + AOT suites stay green (except explicitly excluded test(s) where applicable by policy).

## Notes
1. This document reflects the final strict parity contract.
2. Historical intermediate designs are intentionally not authoritative.
