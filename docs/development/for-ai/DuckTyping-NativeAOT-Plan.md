# DuckTyping NativeAOT Plan (Current Contract)

## Status
This document is the current implementation plan/contract snapshot for DuckTyping NativeAOT.

It supersedes the earlier draft model that used separate catalog/inventory/override contracts.

## Final Model (Implemented)
1. One canonical mapping contract:
   - `--map-file` is the single mapping input consumed by generation and strict compatibility verification.
2. Attribute discovery workflow:
   - `ducktype-aot discover-mappings` scans type-level attributes and writes a canonical map file.
3. Strict compatibility gate:
   - `ducktype-aot generate` + `ducktype-aot verify-compat --failure-mode strict` enforce that map entries are present and `compatible`.
4. No exception channels:
   - No allowlist for non-compatible mappings in strict parity flow.
   - No scenario override channel in the compatibility contract.
5. Multi-target support:
   - One proxy contract can map to multiple targets using repeated type-level mapping attributes.

## Mapping Inputs
1. Type-level forward mapping:
   - `[DuckType("TargetType", "TargetAssembly")]`
2. Type-level copy mapping:
   - `[DuckCopy("TargetType", "TargetAssembly")]`
3. Type-level reverse mapping:
   - `[DuckReverse("TargetType", "TargetAssembly")]`

## Canonical Map Schema
The canonical map file contains:
1. `schemaVersion`
2. `mappings[]` with:
   1. `mode` (`forward` or `reverse`)
   2. `proxyType`
   3. `proxyAssembly`
   4. `targetType`
   5. `targetAssembly`

Notes:
1. No `scenarioId`.
2. No excludes block.
3. No per-mapping status override fields.

## Commands (Current)
1. Discover canonical map:
```bash
ducktype-aot discover-mappings \
  --proxy-assembly <proxy.dll> \
  --target-assembly <target.dll> \
  --output <ducktype-aot-map.json>
```
2. Generate registry/artifacts:
```bash
ducktype-aot generate \
  --proxy-assembly <proxy.dll> \
  --target-assembly <target.dll> \
  --map-file <ducktype-aot-map.json> \
  --output <Datadog.Trace.DuckType.AotRegistry.dll>
```
3. Verify strict compatibility:
```bash
ducktype-aot verify-compat \
  --compat-report <registry.dll.compat.md> \
  --compat-matrix <registry.dll.compat.json> \
  --map-file <ducktype-aot-map.json> \
  --manifest <registry.dll.manifest.json> \
  --failure-mode strict
```

## Runtime/Emitter Parity Highlights
1. Interface proxy shape parity:
   - Default interface proxies emit as value types.
   - `[DuckAsClass]` interface proxies emit as class proxies.
2. AOT registration path:
   - Runtime method-handle activator overloads are supported and used by emitted bootstrap.
3. Allocation behavior:
   - Avoidable boxing is removed from typed activator paths where dynamic semantics allow.
   - Required boxing at object/interface boundaries remains (dynamic parity).

## CI/Gate Contract
1. Canonical checked-in map file is the authoritative gate input.
2. `discover-mappings` drift check compares discovered-compatible output to canonical map.
3. Strict verification fails when:
   1. map and matrix identity sets differ, or
   2. any mapped entry is not `compatible`.

## References
1. `/Users/tony.redondo/repos/github/Datadog/dd-trace-dotnet/docs/development/DuckTyping.NativeAOT.md`
2. `/Users/tony.redondo/repos/github/Datadog/dd-trace-dotnet/docs/development/DuckTyping.NativeAOT.Spec.md`
3. `/Users/tony.redondo/repos/github/Datadog/dd-trace-dotnet/tracer/test/Datadog.Trace.DuckTyping.Tests/AotCompatibility/ducktype-aot-bible-mappings.json`
