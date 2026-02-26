# AOT DuckTyping (NativeAOT) - Build-Time Proxy Generation + Runtime Pre-Registration

## Summary
1. Add a new `dd-trace` command that generates a concrete DuckTyping proxy assembly at build time from proxy definitions + target assemblies.
2. Introduce a separate AOT DuckTyping runtime engine for NativeAOT apps; keep the existing dynamic DuckTyping engine behavior unchanged.
3. Use static-link bootstrap for NativeAOT builds; do not introduce runtime dynamic assembly loading in NativeAOT mode.
4. Keep first delivery scoped to `Datadog.Trace.DuckTyping` only; do not AOT-enable `CallTarget IntegrationMapper` in this effort.
5. Add explicit registry/runtime compatibility validation, trimming-root contracts, deterministic conflict handling, explicit engine isolation guarantees, and a hard 100% parity gate against `docs/development/DuckTyping.Bible.md`.

## Tool Choice
1. Use `dnlib`.
2. Rationale: better low-level IL control for calli/generics/metadata, already present in repo toolchain, no new dependency introduction.

## Scope and Non-Goals
1. In scope: forward/reverse DuckTyping parity, attributes, cache behavior, conversion rules, visibility handling, static/instance members, struct duck copy, nullable duck chaining, `ValueWithType<T>`, byref/out behavior.
2. In scope: command-line generation, mapping discovery from attributes + map-file overrides, NativeAOT bootstrap, strict validation mode.
3. In scope: NativeAOT target profile (`net8.0+`) with no runtime dynamic assembly load assumptions.
4. In scope: strict runtime isolation between Dynamic engine and AOT engine with minimal shared code (attributes/contracts/facade only).
5. Out of scope: behavior changes to existing dynamic DuckTyping emit/cache pipeline.
6. Out of scope: replacing dynamic emit in `IntegrationMapper` and other non-DuckTyping dynamic code paths.
7. Out of scope: source generator implementation (explicitly deferred).
8. Out of scope for Phase 1: call-site auto-discovery from `DuckCast` / `GetOrCreateProxyType` usage.
9. Out of scope for Phase 1: multi-registry merge/ordering semantics (single generated registry assembly per application publish).

## Bible Compatibility Contract
1. Source of truth:
   `docs/development/DuckTyping.Bible.md` is normative for DuckTyping behavior, with `tracer/test/Datadog.Trace.DuckTyping.Tests/` as executable oracle.
2. Compatibility target:
   AOT engine must be behaviorally equivalent to dynamic engine for all supported features in the Bible, including forward/reverse/duck-copy modes.
3. Compatibility scope definition (Phase 1):
   100% compatibility means semantic parity for all Bible-tracked scenarios within the generated mapping scope; undeclared/unmapped pairs are explicit configuration errors, not silent runtime fallbacks.
4. Feature coverage contract:
   every Bible feature catalog item (A.1-E.42) must have an explicit AOT parity test mapping and pass status.
5. IL scenario coverage contract:
   every Bible IL atlas scenario group (`FG`, `FS`, `FF`, `FM`, `RT`, reverse diffs, visibility/combination tables) must be represented by parity tests or snapshot assertions.
6. Example coverage contract:
   Bible detailed examples (1-20) and test-adapted excerpts (A-T) must be runnable against AOT mode and produce equivalent outcomes.
7. Exception compatibility contract:
   exception type/category behavior must match Bible exception taxonomy; message text may differ, but semantic category and trigger conditions must be equivalent.
8. Null and chaining compatibility contract:
   null semantics, duck chaining, reverse chaining, nullable duck chaining, and `ValueWithType<T>` behavior must match Bible-documented rules.
9. Known limitations parity contract:
   Bible-documented hard limitations and known detection gaps must remain behaviorally equivalent unless intentionally changed with explicit approval.
10. Differential oracle contract:
   parity assertions are differential against the current dynamic engine behavior in the same commit/test run, not only hand-maintained expected outputs.
11. Release gate:
   no GA/enable-by-default until compatibility matrix shows 100% pass for all Bible-tracked scenarios.
12. Analyzer/code-fix non-regression:
   DuckTyping analyzer and code-fix behavior remains equivalent unless explicitly changed in a separate scoped proposal.

## Isolation Strategy
1. Runtime engines:
   `DuckTypeDynamicEngine` (existing behavior, unchanged) and `DuckTypeAotEngine` (new NativeAOT path) are separate implementations.
2. Shared surface:
   share only attributes, mapping contracts, and minimal facade contracts required to invoke either engine.
3. Dispatch model:
   single explicit mode selection (`Dynamic` or `AOT`) from build profile/config, then calls route to one engine only for process lifetime.
4. Cross-engine fallback:
   prohibited in Phase 1 (AOT never falls back to dynamic emit; Dynamic never consults AOT registry).
5. Code ownership boundary:
   no edits inside existing dynamic emit IL generation logic except minimal non-behavioral wiring where strictly unavoidable.

## Public API / Type Changes
1. Add AOT registration API in a dedicated AOT runtime component:
   `DuckTypeAotEngine.RegisterProxy(Type proxyDefinitionType, Type targetType, Type generatedProxyType, Func<object?, object?> activator)`.
2. Keep existing dynamic `DuckType` emit/cache code path intact; use a thin facade/dispatcher layer to select engine.
3. Add internal mode flags and guard rails:
   `Dynamic` and `AOT` (process-wide immutable after initialization).
4. Add internal bootstrap helper class for static-link NativeAOT registry initialization.
5. Add runtime metadata contract types for compatibility checks:
   `DuckTypeAotContract` (schema version) and `DuckTypeAotAssemblyMetadata` (generated assembly metadata).
6. Add AOT cache implementation isolated from dynamic cache internals:
   AOT registration finalization occurs before first AOT lookup; late AOT registration invalidates only AOT negative-cache entries.

## CLI Design (`dd-trace`)
1. Add command registration in `Program.cs`: `ducktype-aot`.
2. Command shape:
   `dd-trace ducktype-aot generate` and `dd-trace ducktype-aot verify-compat`.
3. `generate` required options:
   `--proxy-assembly <path>` (repeatable), `--output <path>`.
4. `verify-compat` required options:
   `--compat-report <path>` and `--compat-matrix <path>` (or equivalent resolved defaults from `generate` outputs).
5. Target options:
   `--target-assembly <path>` (repeatable), `--target-folder <path>` (repeatable), `--target-filter <glob>` (repeatable, default `*.dll`).
6. Mapping options:
   `--map-file <path>` optional JSON overrides/additions, `--mapping-catalog <path>` declared inventory contract file for CI/release enforcement, `--generic-instantiations <path>` optional closed-generic roots file.
7. Output options:
   `--assembly-name <name>` optional, deterministic default, `--emit-trimmer-descriptor <path>` (default `<output>.linker.xml`), `--emit-props <path>` (default `<output>.props`) for build integration.
8. Behavior:
   attributes discovery enabled by default; map file augments/overrides; reverse mappings primarily map-driven; command emits NativeAOT-only artifacts and forbids hybrid/dynamic fallback settings.
9. Compatibility verification command:
   `verify-compat` validates produced artifacts against the Bible compatibility matrix and fails with deterministic diagnostics for any uncovered or failing Bible scenario.

## Mapping Model and Resolution
1. Attribute discovery inputs:
   `[DuckType(targetType, targetAssembly)]` and `[DuckCopy(targetType, targetAssembly)]` from both `Datadog.Trace` and `Datadog.Trace.Manual` variants.
2. Reverse mappings:
   map-file required for complete coverage, because reverse pair discovery is not reliably inferable from attributes alone.
3. Matching identity:
   `(mode, proxy-or-base type, target-or-delegate type, target assembly identity + target MVID + member signature fingerprint)`.
4. Resolution precedence:
   map-file explicit entries override discovered entries; excludes are applied last.
5. Validation output:
   structured diagnostics with stable codes, member path, and actionable fix hints.
6. Phase 1 mapping source of truth:
   declared attributes + explicit map file only (no call-site scanning).
7. Completeness contract for declared-only mode:
   `mapping-catalog` defines required mappings for a product scope; CI/release generation must provide it and fail if any required mapping is missing or invalid.
8. Generic closure contract:
   NativeAOT profile requires closed generic instantiations for proxy/target/member generic usage; unresolved open generic paths fail generation with explicit diagnostics.
9. Bible scenario identity mapping:
   compatibility reports and tests use stable Bible-derived IDs (for example `A-01..A-15`, `B-16..B-27`, `C-28..C-33`, `D-34..D-37`, `E-38..E-42`, `FG-*`, `FS-*`, `FF-*`, `FM-*`, `RT-*`, `EX-01..EX-20`, `TX-A..TX-T`).

## Emission Architecture
1. New generator module in Tools Runner (new `DuckTypeAot` namespace) that loads assemblies, resolves mappings, validates parity, emits registry assembly with dnlib.
2. Emitted assembly contents:
   one generated proxy type per resolved pair, one static factory per pair, one bootstrap class with `Initialize()`, and optional module initializer for static-link bootstrap.
3. Emitted dependencies:
   explicit references to Datadog.Trace, proxy assemblies, and target assemblies actually used.
4. Visibility support:
   emit `System.Runtime.CompilerServices.IgnoresAccessChecksToAttribute` usage for required assemblies.
5. IL policy (matches runtime intent):
   public members use direct `call/callvirt/ldfld/stfld`; non-public method/property accessor calls use calli-compatible emission strategy; static/instance opcodes selected per member kind.
6. Generated type shape parity:
   preserves IDuckType contract, instance storage, `Type`/`Instance` behavior, forward and reverse proxy inheritance/interface behavior, struct copy pathways.
7. Trimming-root emission:
   emit linker descriptor entries and/or `DynamicDependency`-based roots for generated proxies and all required target members so NativeAOT trimming does not remove required metadata/code.

## Generated Artifact Contract
1. Emit deterministic artifacts:
   stable mapping sort order, deterministic assembly name, deterministic metadata order, stable diagnostics IDs.
2. Emit assembly-level metadata:
   schema version, tool version, Datadog.Trace minimum/maximum compatible version, source hash fingerprint, and target assembly MVID/fingerprint set.
3. Strong-name behavior:
   generated assembly is signed when signing key is available in build context; otherwise generation warns and marks artifact unsigned in metadata.
4. Optional sidecar manifest:
   `<output>.manifest.json` with mapping list, fingerprints of input assemblies, generation timestamp (UTC), tool version, and generic-instantiation roots.
5. Runtime compatibility checks:
   registry load fails fast with explicit error code if schema or Datadog.Trace compatibility does not match.
6. Manifest/metadata schema rules:
   sidecar and assembly metadata include explicit schema versions and mapping identity checksums so runtime validation is deterministic across tool versions.
7. Trimmer descriptor contract:
   generated linker descriptor schema is versioned and validated together with registry metadata to prevent drift between mapping and preservation roots.
8. Compatibility policy:
   member-signature fingerprint mismatch is a hard failure; target assembly MVID mismatch is warning by default and strict-mode failure.
9. Compatibility matrix artifacts:
   generation/verification produce machine-readable and human-readable parity reports (for example `ducktyping-aot-compat.json` and `ducktyping-aot-compat.md`) keyed by Bible scenario IDs.

## Runtime Integration and Bootstrap
1. NativeAOT bootstrap path:
   no dynamic assembly loading; generated registry assembly is statically referenced at build/publish time and invokes `RegistryBootstrap.Initialize()` via generated module initializer (or explicit generated bootstrap call when initializer is unavailable).
2. Dynamic runtime path:
   existing dynamic DuckTyping startup/bootstrap behavior remains unchanged.
3. Engine dispatch:
   mode is selected once (`Dynamic` or `AOT`) and frozen for process lifetime; engine selection is explicit from build/profile settings and not inferred per call.
4. Cross-engine interaction policy:
   no runtime fallback between engines; AOT uses only `DuckTypeAotEngine`, Dynamic uses only existing dynamic engine.
5. NativeAOT behavior:
   no runtime emit and no dynamic assembly load; missing pair results in deterministic AOT-missing error path.
6. Fail-fast policy implementation:
   initialization reports deterministic failure codes when configured registry is invalid/unloadable, incompatible, or contains failed mappings; this must not surface as an unhandled process-level exception.
7. Registration conflict and idempotency rules:
   registration key is `(mode, proxyDefinitionType, targetType)`; same registration is no-op; conflicting registration fails fast with dedicated diagnostic code.
8. Cache consistency rules:
   AOT cache is separate from dynamic cache; registration is finalized before first AOT lookup in steady state, and late AOT registration invalidates only AOT negative-cache entries.
9. Concurrency and once-only initialization:
   `RegistryBootstrap.Initialize()` and AOT engine bootstrap use thread-safe one-time guards; concurrent initialization attempts cannot produce partial registration state.
10. Single-registry rule (Phase 1):
    exactly one generated AOT registry assembly is supported per application publish; multiple configured registries are a configuration error.
11. Failure mode policy:
    default AOT mode logs and disables only the AOT DuckTyping component for the affected mappings; strict AOT mode disables the Instrumentation component with explicit diagnostics, without crashing the host application.

## Key File Touchpoints
1. `tracer/src/Datadog.Trace.Tools.Runner/Program.cs` (new command wiring).
2. `tracer/src/Datadog.Trace/DuckTyping/DuckType.cs` (minimal engine dispatcher only; no dynamic emit logic changes).
3. `tracer/src/Datadog.Trace/DuckTyping/DuckTypeAotEngine.cs` (new isolated AOT runtime engine).
4. `tracer/src/Datadog.Trace/DuckTyping/DuckTypeAotCache.cs` (new isolated AOT cache).
5. `tracer/src/Datadog.Trace/ClrProfiler/Instrumentation.cs` (AOT bootstrap entry wiring only, without dynamic behavior change).
6. New files under `tracer/src/Datadog.Trace.Tools.Runner/DuckTypeAot/*` for resolver, validator, emitter, map parser, diagnostics, trimmer-descriptor writer, and generated props integration.
7. New compatibility inventory under `tracer/test/Datadog.Trace.DuckTyping.Tests/AotCompatibility/*` with Bible scenario ID mappings and parity assertions.

## Test Plan and Acceptance Criteria
1. Command tests:
   parse/options/validation/error messaging for missing assemblies, unresolved types, malformed map file, and missing required NativeAOT integration artifacts.
2. Engine isolation tests:
   when running in AOT mode, dynamic engine methods are never invoked; when running in Dynamic mode, AOT engine lookup/registry code is never invoked.
3. Engine selection tests:
   build/profile-selected mode is immutable for process lifetime; no runtime auto-switching based on dynamic-code support checks.
4. Dynamic non-regression tests:
   existing dynamic behavior (cache keys, emit paths, visibility handling, exceptions) remains byte-for-byte or behaviorally equivalent to baseline for non-AOT runs.
5. Analyzer/code-fix non-regression tests:
   analyzer diagnostics and code-fix behavior for DuckTyping null-check rules remain unchanged.
6. Bible feature catalog parity tests:
   explicit pass coverage for all feature catalog IDs (`A-01..A-15`, `B-16..B-27`, `C-28..C-33`, `D-34..D-37`, `E-38..E-42`).
7. Bible IL atlas parity tests:
   representative parity/snapshot coverage for IL scenario groups (`FG-*`, `FS-*`, `FF-*`, `FM-*`, `RT-*`, reverse deltas, visibility/combination tables).
8. Bible detailed examples parity tests:
   examples `EX-01..EX-20` execute in AOT mode with equivalent behavior to dynamic mode.
9. Bible test-adapted excerpts parity tests:
   excerpts `TX-A..TX-T` execute in AOT mode with equivalent behavior to dynamic mode.
10. Null/chaining/value-wrapper parity tests:
   null semantics, duck chaining, reverse chaining, nullable duck chaining, and `ValueWithType<T>` behavior match Bible-documented semantics.
11. Exception taxonomy parity tests:
   Bible exception categories and trigger conditions match dynamic behavior for AOT mode.
12. Known limitations parity tests:
   Bible “hard limitations” and “known detection gaps” are explicitly tested for equivalent behavior against dynamic mode.
13. Differential parity harness tests:
   same scenario is executed through Dynamic and AOT engines in the same run; outcomes are compared structurally (value, type, exception category, side effects).
14. Generation parity tests:
   run existing DuckTyping feature matrix against generated proxies in AOT engine mode (no runtime emit path).
15. Reverse parity tests:
   validate `DuckImplement`/`CreateReverse` scenarios including abstract/virtual/property enforcement.
16. Visibility matrix tests:
   public/internal/private, static/instance, fields/properties/methods, byref/out, generics, explicit interface members.
17. Cache tests:
   AOT pre-registration hits isolated AOT cache fast paths; existing dynamic `DuckTypeCache`/`CreateCache<T>` semantics remain unchanged in Dynamic mode.
18. Late-registration cache invalidation tests:
   if AOT registration happens after an AOT miss, AOT negative-cache entries are invalidated and subsequent AOT lookups resolve the new mapping.
19. NativeAOT integration sample test:
   generate registry, publish NativeAOT sample, verify duck casts/reverse proxies execute without runtime emit or dynamic assembly loading.
20. Backward compatibility:
   existing runtime DuckTyping tests still pass with no AOT assemblies configured.
21. Declared-only mapping behavior tests:
   ensure unannotated/unmapped duck pairs are reported with clear diagnostics and are not silently generated.
22. Mapping-catalog enforcement tests:
   CI/release profile fails when `mapping-catalog` is missing and passes only when all required mappings are present.
23. Compatibility contract tests:
    reject generated registries with schema mismatch, Datadog.Trace version mismatch, member-fingerprint mismatch, and invalid metadata; validate default-warning vs strict-failure behavior for target MVID mismatch.
24. Trimming preservation tests:
    NativeAOT/trim publish keeps all members required by generated proxies (private/internal/public) when descriptor is applied.
25. Generic closure tests:
    unresolved open generic mappings fail generation in NativeAOT profile; provided closed instantiations pass and execute.
26. Idempotency/conflict tests:
    repeated `Initialize()` calls are no-op, conflicting registrations fail deterministically.
27. Non-public visibility spike gate:
    before feature-complete signoff, pass explicit NativeAOT matrix for private/internal/public + static/instance + field/property/method including calli paths on supported runtimes.
28. Concurrency tests:
    parallel startup/first-use registration calls produce exactly-once initialization and stable cache state.
29. Failure semantics tests:
    invalid/incompatible registry paths produce deterministic diagnostics and component disablement behavior, with no unhandled exceptions.
30. Single-registry constraint tests:
    NativeAOT phase-1 profile fails generation or publish integration when multiple AOT registries are configured for the same app.
31. Compatibility acceptance gate:
    no GA/enable-by-default until Bible compatibility matrix reports 100% pass with zero unreviewed or `N/A` Bible scenario entries.

## Rollout Plan
1. Phase 1:
   hidden command + isolated AOT runtime engine + NativeAOT static bootstrap path + forward mappings from attributes + map-file reverse entries + compatibility metadata + deterministic registration semantics.
2. Phase 2:
   add trimmer descriptor/generic closure enforcement, engine-isolation guardrails, and Bible-traceable compatibility matrix generation (`json` + `md`) in CI.
3. Phase 3:
   close all Bible scenario gaps (`A-01..E-42`, IL atlas groups, `EX-01..EX-20`, `TX-A..TX-T`) and complete non-public NativeAOT spike matrix.
4. Phase 4:
   GA gate: enable by default only after 100% Bible compatibility pass; after GA, evaluate separate `IntegrationMapper` AOT project and optional call-site auto-discovery.

## Assumptions and Defaults (Locked)
1. Scope boundary: DuckTyping subsystem only.
2. Bootstrap discovery: static-link bootstrap for NativeAOT only in Phase 1.
3. Missing mapping policy: fail fast for configured mappings; runtime misses in NativeAOT raise deterministic missing-proxy errors.
4. Strategy: isolated engines with no cross-engine fallback.
5. Tooling: dnlib.
6. Mapping generation in Phase 1 uses declared attributes/map only.
7. Deterministic conflict handling and compatibility validation are mandatory for Phase 1.
8. Dynamic-code unavailability does not modify dynamic engine behavior; NativeAOT uses dedicated AOT engine.
9. NativeAOT profile assumes no runtime dynamic assembly loading APIs.
10. CI/release generation requires `mapping-catalog` and compatibility/trimming artifacts.
11. NativeAOT profile integrates generated assembly + props + linker descriptor into publish inputs by default.
12. Shared code between engines is minimized to attributes/contracts/facade wiring.
13. Engine mode is chosen explicitly from build/profile configuration; no runtime heuristics switch engines.
14. Phase 1 supports one generated AOT registry assembly per application publish.
15. `docs/development/DuckTyping.Bible.md` is the normative compatibility reference for AOT parity planning and validation.
16. Release quality target is 100% compatibility against Bible-tracked scenarios before GA/enable-by-default.
17. DuckTyping analyzer/code-fix behavior is preserved unless changed in a separately approved scope.
18. 100% compatibility is measured by Bible scenario IDs within generated mapping scope; undeclared/unmapped pairs are tracked as configuration misses, not parity failures.
19. CI compatibility reports compare AOT outcomes against dynamic baseline outcomes from the same commit/test run.
