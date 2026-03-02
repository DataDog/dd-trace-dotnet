# DuckTyping NativeAOT Troubleshooting

## Purpose

This guide provides issue diagnosis and remediation steps for DuckTyping NativeAOT generation, verification, and runtime usage.

Use this together with:

1. [DuckTyping.NativeAOT.md](./DuckTyping.NativeAOT.md)
2. [DuckTyping.NativeAOT.Spec.md](./DuckTyping.NativeAOT.Spec.md)
3. [DuckTyping.NativeAOT.Testing.md](./DuckTyping.NativeAOT.Testing.md)

## Fast Triage Checklist

1. Confirm generator command line and input files.
2. Confirm generated artifact set is complete.
3. Confirm `verify-compat` outcome and failure mode.
4. Confirm runtime loaded expected registry assembly.
5. Confirm process mode was not switched after initialization.

## Error Catalog

### AOT mapping not found at runtime

Symptoms:

1. Runtime failure reports missing AOT registration for `(proxy,target,mode)`.

Likely causes:

1. Mapping absent from canonical map file or dropped during discovery compatibility filtering.
2. Wrong registry assembly loaded.
3. Bootstrap initialization did not run before first use.

Actions:

1. Inspect `*.compat.json` for the exact mapping key.
2. Validate the canonical `--map-file` used in generation.
3. Ensure generated registry assembly is referenced and initialized early.
4. Ensure runtime process loads only the intended registry identity.

### Runtime mode conflict

Symptoms:

1. Exception indicates runtime mode immutability conflict.

Likely causes:

1. Process initialized in `dynamic`, then switched to `aot`.
2. Process initialized in `aot`, then attempted dynamic path.

Actions:

1. Keep dynamic and AOT tests/processes isolated.
2. Set explicit mode at process start in tests.
3. Avoid mixed initialization paths in shared test host processes.

### Multiple registry identity conflict

Symptoms:

1. Exception indicates only one generated registry assembly identity is allowed.

Likely causes:

1. Two different generated registries loaded in same process.
2. Stale registry from previous build loaded via probing paths.

Actions:

1. Load one registry artifact set per process.
2. Clear stale outputs and verify assembly probing order.
3. Use unique artifact names per service/build.

### verify-compat strict failure

Symptoms:

1. `ducktype-aot verify-compat --failure-mode strict` exits non-zero.

Likely causes:

1. `compat-matrix` and `--map-file` identity-set mismatch.
2. One or more mapped entries are non-compatible.
3. Manifest contract drift.

Actions:

1. Review `*.compat.md` first for human-readable diagnosis.
2. Compare `*.compat.json` against canonical `--map-file`.
3. Check for missing, extra, or non-compatible mapped entries.
4. Regenerate artifacts with matching runtime build inputs.

### strict verify-compat fails with unexpected non-compatible status

Symptoms:

1. `verify-compat --failure-mode strict` reports non-compatible mappings not accepted by contracts.

Likely causes:

1. Regression introduced new non-compatible status.
2. Map file drift was introduced without synchronized generation.
3. Compatibility matrix was generated from different `--map-file` inputs than verification.

Actions:

1. Check canonical map entries first.
2. Confirm `verify-compat` is using the same `--map-file` as `generate`.
3. Treat any non-compatible mapping as a regression until explicitly reviewed and approved.
4. Re-run generation and verification with identical artifact inputs.

### unsupported_closed_generic_mapping

Symptoms:

1. Compatibility status indicates unsupported closed generic mapping.

Likely causes:

1. Mapping resolves to unsupported adaptation shape.
2. Generic closure roots are incomplete.

Actions:

1. Add explicit generic instantiations where applicable.
2. Refactor proxy/target shape to supported closed mapping pattern.
3. Re-run generator and verify-compat.

### missing_target_type or missing_proxy_type

Symptoms:

1. Generator emits `missing_target_type` or `missing_proxy_type` diagnostics.

Likely causes:

1. Required assembly not included in `--target-assembly`/`--target-folder` or proxy list.
2. Type identity typo in map file.
3. Assembly-qualified name ambiguity.

Actions:

1. Validate type and assembly names exactly.
2. Add missing assemblies explicitly.
3. Use deterministic filters when scanning target folders.

### non_public_target_method or incompatible_method_signature

Symptoms:

1. Compatibility status indicates method visibility or signature mismatch.

Likely causes:

1. Target member is inaccessible in the selected binding context.
2. Proxy contract does not match target call shape.

Actions:

1. Verify proxy attributes/member names and expected signatures.
2. Compare dynamic and AOT scenario details in parity reports.
3. Align proxy definition with target API contract.

### generated interface proxy shape differs from expectation

Symptoms:

1. Generated forward interface proxy appears as `struct` while expecting class, or appears as class while expecting value type.

Likely causes:

1. Default behavior: interface proxies emit as value types for parity/perf.
2. Proxy interface has `[DuckAsClass]`, forcing class proxy emission.

Actions:

1. Confirm whether `[DuckAsClass]` is present on the proxy interface.
2. Remove `[DuckAsClass]` to get default value-type proxy, or add it when class shape is explicitly required.

### ILSpy shows `get_`/`set_` methods instead of a property

Symptoms:

1. Decompiler shows accessor-shaped methods (`get_Name`, `set_Value`) but not a property in some generated proxies.

Likely causes:

1. Source proxy member is a method named like an accessor, but not a special-name property accessor in metadata.
2. Decompiler runs with missing references and falls back to less precise rendering.
3. Generated metadata reflects source contract shape (property metadata is emitted only when accessor metadata is available/inferable).

Actions:

1. Check source proxy contract: if you want property metadata, declare a real C# property instead of manual `get_`/`set_` methods.
2. Load all referenced assemblies in ILSpy before comparing output.
3. Validate via reflection (`Type.GetProperties`) on generated proxy type for definitive property table inspection.

### ILSpy shows `castclass`/`unbox.any`/`box` in activator paths

Symptoms:

1. Generated bootstrap activator methods contain object casts/unboxing or boxing instructions.

Likely causes:

1. Registration bridge activators receive `object` by contract and must cast/unbox to typed target before calling typed activator.
2. Returning value-type proxies through interface/object contracts requires boxing.
3. `IDuckType.Instance` for value-type targets returns `object` and therefore boxes.

Actions:

1. Focus optimization checks on typed activator and generated constructor signatures first.
2. Treat bridge-path casts/boxing as expected unless they appear in places that can stay typed end-to-end.
3. Compare dynamic parity behavior before classifying as regression.

### NativeAOT publish linker errors

Symptoms:

1. `dotnet publish /p:PublishAot=true` fails with missing root/trimmed member errors.

Likely causes:

1. Generated props/linker descriptor not imported.
2. Descriptor path is stale or missing.
3. Registry artifacts from different build revision used.

Actions:

1. Verify `DuckTypeAotPropsPath` points to current generated props.
2. Confirm linker descriptor exists and is referenced by props.
3. Regenerate registry and republish from clean outputs.

## Investigation Workflow

When debugging a pipeline failure, inspect in this order:

1. Generator stdout/stderr.
2. `*.dll.manifest.json`.
3. `*.dll.compat.json`.
4. `*.dll.compat.md`.
5. App publish logs.
6. Runtime logs/output from smoke test.

This order usually narrows the issue before deep code debugging is needed.

## Recovery Actions for CI

1. Clean workspace artifact directories.
2. Regenerate registry artifacts from scratch.
3. Re-run verify-compat in strict mode.
4. Re-run NativeAOT publish.
5. Re-run runtime smoke.

If failures persist, archive all generated artifacts with logs for diagnosis.

## Preventive Practices

1. Pin SDK and tool versions per repo revision.
2. Keep map/catalog files in source control.
3. Use strict verify-compat in protected branches.
4. Fail fast on missing generated artifact files.
5. Keep one registry identity per process boundary.

## Related Documents

1. [DuckTyping.NativeAOT.md](./DuckTyping.NativeAOT.md)
2. [DuckTyping.NativeAOT.Spec.md](./DuckTyping.NativeAOT.Spec.md)
3. [DuckTyping.NativeAOT.BuildIntegration.md](./DuckTyping.NativeAOT.BuildIntegration.md)
4. [DuckTyping.NativeAOT.Testing.md](./DuckTyping.NativeAOT.Testing.md)
