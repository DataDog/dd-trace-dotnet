# Findings & Decisions

## Requirements
- Analyze DuckTyping deeply in code and docs.
- Treat docs as potentially outdated but still important.
- Prepare a high-confidence technical baseline for a follow-up implementation discussion.
- Deliver a very large, very detailed markdown "bible" documenting DuckTyping internals and all supported features with examples.

## Research Findings
- `docs/development/DuckTyping.md` is the only dedicated DuckTyping document under `docs/`.
- Core implementation lives in `tracer/src/Datadog.Trace/DuckTyping/` and is split by concern (`Methods`, `Properties`, `Fields`, `Statics`, `Utilities`).
- There is a large dedicated test project: `tracer/test/Datadog.Trace.DuckTyping.Tests/`.
- Duck typing is heavily used across integrations (e.g., testing, MongoDB, logging, gRPC, Activity/OTEL compatibility layers).
- Core runtime flow is: API entry (`DuckType` / `DuckTypeExtensions`) -> cache lookup (`DuckTypeCache`, `CreateCache<T>`) -> dry-run validation -> dynamic type emit -> activator delegate invoke.
- Reverse duck typing uses `CreateReverse`/`DuckImplement` with `[DuckReverseMethod]` and enforces strict constraints (cannot derive from struct; implementor cannot be interface/abstract).
- Visibility/access strategy uses dynamic assemblies + `IgnoresAccessChecksToAttribute` via `EnsureTypeVisibility`, not only direct `DynamicMethod` tricks.
- Dedicated tests encode many behavioral guarantees and also known gaps (some skipped mismatch-detection tests).
- Usage across `tracer/src/Datadog.Trace` appears in roughly 500 files, concentrated in `ClrProfiler/AutoInstrumentation`, `DiagnosticListeners`, `Activity`, `AppSec`, `Iast`.
- Common production conventions: prefer `TryDuckCast` for version variance, use `[DuckField(Name=\"_...\")]` for private fields, use comma-separated name fallback aliases, and use string type names for runtime-only dependency signatures.
- Docs drift highlights:
  - reverse docs reference `[DuckImplement]` attribute but code uses `[DuckReverseMethod]`;
  - docs omit `DuckPropertyOrField` and explicit-interface mapping support;
  - docs mention `HasValue`/`IsNull` APIs that are not implemented;
  - docs miss behavior around `DuckInclude`/`DuckIgnore`, reverse custom-attribute copy rules, and known detection gaps.
- Additional implementation detail captured for final doc authoring:
  - `CreateTypeResult` stores either activator delegate or captured exception and rethrows lazily.
  - Dynamic module strategy has 3 branches: visible shared-by-assembly, non-visible dedicated module, and generic cross-assembly dedicated module.
  - `EnsureTypeVisibility` recursively processes nested/generic types and applies `IgnoresAccessChecksToAttribute` per module/assembly.
  - Method resolution supports explicit interface binding and wildcard explicit interface matching (`ExplicitInterfaceTypeName = "*"`) plus fallback parameter matching heuristics.
  - Conversion logic is centralized in `WriteTypeConversion` / `CheckTypeConversion` with strict value-type handling and runtime-cast paths for object/interface boundaries.
- Deliverable produced:
  - `docs/development/DuckTyping.Bible.md` created as exhaustive reference (feature catalog, internals, caches, visibility, helpers, exceptions, analyzer guidance, and test-inspired examples).
- Follow-up expansion completed:
  - Added `Feature-by-feature test-adapted excerpts` section with concrete snippets mapped to feature IDs and linked to representative test files.
- Additional follow-up expansion completed:
  - Added a large `IL emission atlas (opcode-level)` section including forward/reverse IL paths, static/instance behavior, public/internal/private combination tables, chaining/ref-out patterns, and dynamic-method fallback mechanics.
- Additional IL companion expansion completed:
  - Added `IL companion for Detailed examples (1-20)` section that pairs each C# detailed sample with a representative emitted IL form and branch notes.

## Technical Decisions
| Decision | Rationale |
|----------|-----------|
| Analyze internals first, then usages | Prevents misinterpretation of usage patterns |
| Use dedicated tests as behavior oracle | Tests often encode edge-case and compatibility intent better than docs |
| Explicitly reconcile doc-vs-code differences | User already flagged docs as outdated |

## Issues Encountered
| Issue | Resolution |
|-------|------------|
| Broad grep produced very large output | Switched to targeted file-level reads and scoped searches |

## Resources
- `docs/development/DuckTyping.md`
- `tracer/src/Datadog.Trace/DuckTyping/`
- `tracer/test/Datadog.Trace.DuckTyping.Tests/`
- `tracer/src/Datadog.Trace.Tools.Analyzers/DuckTypeAnalyzer/`
- `tracer/src/Datadog.Trace/ClrProfiler/CallTarget/Handlers/IntegrationMapper.cs`
- `tracer/src/Datadog.Trace/DiagnosticListeners/AspNetCoreDiagnosticObserver.cs`
- `tracer/src/Datadog.Trace/Activity/ActivityListenerDelegatesBuilder.cs`

## Visual/Browser Findings
- No visual/browser sources used yet.

---
*Update this file after every 2 view/browser/search operations*
