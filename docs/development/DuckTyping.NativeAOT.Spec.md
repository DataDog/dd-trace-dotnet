# DuckTyping NativeAOT Artifact and Mapping Specification

## Purpose

This document defines stable contracts for DuckTyping NativeAOT generation inputs and outputs.

It is intended for:

1. Build tooling maintainers.
2. CI/release gate maintainers.
3. Engineers integrating generated registries into NativeAOT applications.

## Scope

This specification covers:

1. Mapping model.
2. Input file formats.
3. Output artifact set.
4. Runtime contract assumptions.

It does not redefine dynamic DuckTyping behavior. Dynamic behavior is covered by [DuckTyping.Bible.md](./DuckTyping.Bible.md).

## Canonical Mapping Key

A mapping entry is uniquely identified by the tuple:

1. `mode`
2. `proxyType`
3. `proxyAssembly`
4. `targetType`
5. `targetAssembly`

Modes:

1. `forward`
2. `reverse`

If equivalent keys are discovered from multiple sources, later resolution can overwrite earlier entries by resolver precedence.

## Input Sources and Precedence

Effective mappings are composed from these sources:

1. Attribute discovery from proxy assemblies.
2. Map file (`--map-file`) additions, overrides, and exclusions.
3. Optional mapping catalog (`--mapping-catalog`) used as contract coverage validation.

Recommended precedence model in automation:

1. Start with attribute discovery.
2. Apply map-file overlays.
3. Apply excludes.
4. Validate against catalog requirements.

## Map File Schema (`--map-file`)

Top-level object:

1. `mappings`: array of mapping entries.
2. `excludes`: optional array of mapping keys to remove.

Mapping entry fields:

1. `mode`: `forward` or `reverse`; defaults to `forward` when omitted.
2. `scenarioId`: optional identifier used for scenario tracking.
3. `proxyType`: required proxy type full name or assembly-qualified name.
4. `proxyAssembly`: optional when inferable from `proxyType` assembly-qualified value.
5. `targetType`: required target type full name or assembly-qualified name.
6. `targetAssembly`: optional when inferable from `targetType` assembly-qualified value.
7. `exclude`: optional boolean, when `true` removes the mapping key.

Example:

```json
{
  "mappings": [
    {
      "scenarioId": "A-01",
      "mode": "forward",
      "proxyType": "My.Contracts.IRequestProxy",
      "proxyAssembly": "My.Contracts",
      "targetType": "ThirdParty.HttpRequest",
      "targetAssembly": "ThirdParty.Http"
    }
  ],
  "excludes": [
    {
      "mode": "forward",
      "proxyType": "My.Contracts.ILegacyProxy",
      "proxyAssembly": "My.Contracts",
      "targetType": "ThirdParty.LegacyType",
      "targetAssembly": "ThirdParty.Legacy"
    }
  ]
}
```

## Mapping Catalog Schema (`--mapping-catalog`)

The mapping catalog is a policy input used to assert required scenario coverage.

Expected behavior:

1. Declares expected scenarios and identity tuples.
2. Can be used with `--require-mapping-catalog` to fail when required entries are missing.
3. Works with scenario inventory and expected outcomes in compatibility verification.

The exact catalog document in this repository is maintained under:

1. `tracer/test/Datadog.Trace.DuckTyping.Tests/AotCompatibility/ducktype-aot-bible-mapping-catalog.json`

## Generic Instantiations Schema (`--generic-instantiations`)

Accepted forms:

1. String entry containing a closed generic type identity.
2. Object with `type` and optional `assembly`.

Rules:

1. Entry must resolve to a closed generic type.
2. Open generics are rejected.
3. Invalid entries fail generation.

Example:

```json
[
  "System.Collections.Generic.Dictionary`2[[System.String, System.Private.CoreLib],[System.Int32, System.Private.CoreLib]], System.Private.CoreLib",
  {
    "type": "My.Contracts.Box`1[[My.Contracts.Payload, My.Contracts]]",
    "assembly": "My.Contracts"
  }
]
```

## Output Artifact Set

Given output assembly `X.dll`, generation emits:

1. `X.dll`
2. `X.dll.manifest.json`
3. `X.dll.compat.json`
4. `X.dll.compat.md`
5. `X.dll.linker.xml` unless overridden with `--emit-trimmer-descriptor`
6. `X.dll.props` unless overridden with `--emit-props`

## Registry Assembly Contract

The generated assembly must contain:

1. Generated proxy implementations for compatible mappings.
2. `Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap` type.
3. Bootstrap logic that enables AOT mode, validates runtime contract, and registers forward/reverse mappings.
4. Module initializer that triggers bootstrap initialization.

Consumers may still call `DuckTypeAotRegistryBootstrap.Initialize()` explicitly for deterministic startup.

## Manifest Contract (`*.manifest.json`)

Manifest is a provenance and contract-check artifact.

It is expected to encode at least:

1. Registry assembly identity.
2. Runtime assembly identity and fingerprint material.
3. Mapping snapshot summary.
4. Input artifact fingerprints.
5. Signing metadata when signing is configured.

Compatibility verification may compare manifest contract fields against runtime/descriptor expectations.

## Compatibility Matrix Contract (`*.compat.json` and `*.compat.md`)

The compatibility matrix describes status per mapping/scenario.

Status values include:

1. `compatible`
2. `pending_proxy_emission`
3. `unsupported_proxy_kind`
4. `missing_proxy_type`
5. `missing_target_type`
6. `missing_target_method`
7. `non_public_target_method`
8. `incompatible_method_signature`
9. `unsupported_proxy_constructor`
10. `unsupported_closed_generic_mapping`

The markdown report is human-oriented. The JSON matrix is machine-oriented for CI policy checks.

## Verify-Compat Inputs

`ducktype-aot verify-compat` requires:

1. `--compat-report`
2. `--compat-matrix`

Optional contract inputs:

1. `--mapping-catalog`
2. `--scenario-inventory`
3. `--expected-outcomes`
4. `--manifest`

Failure mode:

1. `default`: warns on selected drift cases.
2. `strict`: fails on drift cases treated as hard contract violations.

## Runtime Contract Requirements

At runtime:

1. DuckType mode is process-immutable.
2. AOT runtime path requires pre-registered mappings.
3. One generated registry assembly identity is allowed per process.
4. Missing mappings result in explicit missing-registration failures rather than dynamic emit fallback.

## Versioning and Compatibility Guidance

1. Treat generated registry artifacts as tied to a specific Datadog.Trace runtime identity.
2. Regenerate registry artifacts when runtime version or MVID changes.
3. Do not reuse generated artifacts across unrelated runtime binaries.

## Validation Checklist

For an artifact set to be considered valid:

1. Generator exits successfully.
2. All expected artifacts are present.
3. `verify-compat --failure-mode strict` passes.
4. NativeAOT publish consumes generated props and linker descriptor successfully.
5. Runtime smoke confirms mappings resolve without runtime emission.

## Related Documents

1. [DuckTyping.NativeAOT.md](./DuckTyping.NativeAOT.md)
2. [DuckTyping.NativeAOT.BuildIntegration.md](./DuckTyping.NativeAOT.BuildIntegration.md)
3. [DuckTyping.NativeAOT.CompatibilityMatrix.md](./DuckTyping.NativeAOT.CompatibilityMatrix.md)
4. [DuckTyping.NativeAOT.Troubleshooting.md](./DuckTyping.NativeAOT.Troubleshooting.md)
5. [DuckTyping.NativeAOT.Testing.md](./DuckTyping.NativeAOT.Testing.md)
