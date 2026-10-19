---
name: add-lint-rule
description: >-
  Add or change lint rules and static analysis in dd-trace-dotnet. Prefer Roslyn
  analyzers for managed C#. Native code uses CppCheck (tracer/loader) and
  Clang-Tidy plus CppCheck (profiler). Use when adding a lint rule, Roslyn
  analyzer, diagnostic, BannedSymbols entry, StyleCop/editorconfig severity,
  CppCheck suppression, or Clang-Tidy check, or when the user asks how to
  enforce a coding convention at compile time.
---

# Add Lint Rules / Static Analysis

This repo has **no** `dotnet format` or `clang-format --check` CI job. Managed
enforcement is compile-time Roslyn (`TreatWarningsAsErrors=true`). Do not add a
new pipeline stage for a C# rule.

**Scope:** *enforcement* of rules and scripts applies only to **"core"**
production code for now (`Datadog.Trace` and other `tracer/src` libraries;
native tracer/loader/profiler product code). Do **not** apply new analyzers,
Banned APIs, CppCheck/Clang-Tidy tightenings, or helper scripts to tests,
helpers, samples, or test-applications unless the user explicitly asks to
expand scope.

Still add unit tests for the analyzer itself under
`Datadog.Trace.Tools.Analyzers.Tests` — that is testing the rule, not
enforcing it on test code.

Read [reference.md](reference.md) for file layout, ID allocation, and copy-paste
templates. Stay in this file until the rule is classified.

## 1. Classify the rule first

Pick **one** primary category. Do not mix them in the diagnostic title, category,
or PR description.

| Category | What it is | Typical severity | Typical mechanism |
|---|---|---|---|
| **Correctness / safety** | Wrong code, security, config-key misuse, infinite-loop hazard | `Error` | Custom Roslyn analyzer |
| **Product convention** | Must use a helper, logging shape, duck-type null check | `Error` or `Warning` | Analyzer or `BannedSymbols.txt` |
| **Style** | Naming, usings, headers, layout, “looks nicer” | Existing StyleCop / `.editorconfig`. New custom style rules: default `Info` or do not add | StyleCop, editorconfig |
| **Performance** | Proven hot-path win | Only `Error`/`Warning` if the win is **clear**. Otherwise `Info` or do not add | Analyzer or Banned API, with an evidence-backed message |

If the same idea is both “style” and “maybe faster”, it is **style**. Do not
rebrand it as performance to justify blocking PRs.

## 2. Do not over-claim performance

Do **not** classify a rule as Performance, mention allocations/CPU in the
diagnostic message, or sell the PR as a perf win unless at least one of these
is true:

- The replacement **mechanistically** avoids a known allocation or extra work
  on a tracer hot path (span create/tag, instrumentation callback, logging,
  config read). Example already in-tree: `Environment.Version` allocates a
  `Version` on every get; `BannedSymbols.txt` says so.
- You have a benchmark or a tight proof (the API’s implementation obviously
  allocates; the helper is documented as the zero-alloc path).

Not enough:

- “Sealing types can improve performance” as a reason to fail CI (`DDSEAL001`
  is `Info` for this reason).
- Micro-optimizations outside bootstrap/hot paths in `AGENTS.md`.
- Style preferences (braces, `var`, usings, `is not null`) dressed up as perf.
- “Might help the JIT / inlining / virtual dispatch” without a measured case.

If unsure, ship as **style** at `Info`, or as a convention without a perf
sentence. Never use a speculative perf claim to choose `Error`.

## 3. Choose the mechanism

### Managed C# (preferred: Roslyn)

Cheapest fit wins:

1. **Ban a specific API** → add a line to repo-root `BannedSymbols.txt`
   (`RS0030` is already error). Message must state the replacement and the
   real reason (correctness, config, or a **clear** perf win).
2. **Ban a pattern** (syntax, types, logging templates, `WithKeys` arguments)
   → custom analyzer under `tracer/src/Datadog.Trace.Tools.Analyzers/`.
3. **Existing StyleCop / IDE diagnostic, new severity** → `.editorconfig`
   `dotnet_diagnostic.<ID>.severity`. Do not invent StyleCop IDs.
4. Do **not** enable `EnableNETAnalyzers` / `EnforceCodeStyleInBuild` globally.
   Built-in .NET analyzers are off on purpose (`shared/CodeAnalysis.props`).

Custom analyzers are referenced only from `tracer/src/Directory.Build.props`
as `OutputItemType="Analyzer"`. New `[DiagnosticAnalyzer]` types are picked
up with no CI YAML changes. `BannedSymbols.txt` is an `AdditionalFiles` item
on `Datadog.Trace.csproj` (core), not on test projects.

StyleCop and `.editorconfig` are broader (they include tests). Do not raise
a StyleCop/IDE diagnostic severity repo-wide as a way to lint test code, and
do not treat that existing breadth as a reason to extend custom rules or
scripts beyond core.

### Native C/C++ (light)

Do not introduce Roslyn, clang-format CI, or a new analyzer framework.

| Area | Primary tools | Where they run |
|---|---|---|
| Native tracer + native loader | **CppCheck** | Azure `static_analysis_checks_tracer` |
| Profiler | **Clang-Tidy** (fails on ` error:`) **and** CppCheck | Azure `static_analysis_tests_profiler` (only if profiler changed) |

Default posture: **fix the code** or add a **narrow suppression**. Do not
tighten check sets or remove `--suppress=` flags unless the user asked for a
new native rule and existing noise is acceptable.

- Tracer/loader suppressions are CLI `--suppress=` lists on the
  `CppCheckNativeSrc` / `CppCheckNativeLoader` Nuke targets.
- Profiler CppCheck: `profiler/cppcheck-suppressions.txt` (`--inline-suppr`
  is enabled).
- Profiler Clang-Tidy checks: `ClangTidyChecks` in
  `tracer/build/_build/Build.Profiler.Steps.cs`. Many style checks are already
  disabled. Adding a check is opt-in and will fail the profiler analysis
  stage on `error:`.
- `.clang-format` is local style only; CI does not verify it.

## 4. Severity (managed)

`TreatWarningsAsErrors` is true for tracer projects, so **`Warning` fails the
build**. Use:

- `Error` — must not merge (correctness, required convention)
- `Warning` — same as error here; prefer `Error` unless you need Warning for
  IDE presentation
- `Info` — does **not** block PRs (`DDSEAL001`)

Match `category` to the classification in §1 (`Usage`, `CodeQuality`,
`Maintainability`, `Performance` only when §2 is satisfied).

## 5. Implementation workflow (managed)

1. Classify (§1) and confirm the perf bar (§2).
2. Grep `DiagnosticId` under `tracer/src/Datadog.Trace.Tools.Analyzers` for the
   next `DDxxxx` or start `DD<AREA>001` for a family (`DDLOG`, `DDDUCK`).
3. Follow [reference.md](reference.md): analyzer, tests, optional code fix.
4. For `Error`/`Warning`, clean up **all** current hits in the same PR, or
   the tree will not compile (`TreatWarningsAsErrors`). Prefer real fixes
   over `#pragma` / `GlobalSuppressions`. `Info` does not fail the build.
5. Tests: `Datadog.Trace.Tools.Analyzers.Tests` via
   `CSharpAnalyzerVerifier` / `CSharpCodeFixVerifier`.
6. Compile: `CompileManagedSrc` or `BuildAndRunManagedUnitTests` with a filter
   on the new test class.

Do not add Nuke targets, Azure stages, GitHub workflows, CodeQL config, or
`static-analysis.datadog.yml` entries.

## 6. PR description

State: category (style / convention / correctness / performance), diagnostic
ID, what is forbidden, the replacement, and **why**. If performance, cite the
allocation/hot-path evidence in one sentence. If style, say so and do not
claim runtime impact.

## Checklist

- [ ] Category is explicit; style is not labeled Performance
- [ ] Perf claims meet §2; otherwise no perf language in the diagnostic
- [ ] Enforcement is **"core"** production code only; analyzer unit tests still added
- [ ] Managed: Roslyn analyzer or `BannedSymbols.txt`, not a new CI job
- [ ] Native: CppCheck and/or existing Clang-Tidy surface only
- [ ] Severity matches whether the rule should block PRs
- [ ] Analyzer tests cover a flag case and a no-flag case
- [ ] Existing violations fixed in the same change
- [ ] Generated-code analysis left `None`; concurrent execution enabled
