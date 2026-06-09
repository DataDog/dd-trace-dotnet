# ITR Code Coverage Backfill Analysis for .NET

## Scope

This document analyzes how the .NET tracer could implement Intelligent Test Runner code coverage backfill based on:

- `/Users/tony.redondo/Downloads/[Approved] ITR Code Coverage - BE RFC.md`
- Java reference PR: https://github.com/DataDog/dd-trace-java/pull/7367
- The current `dd-trace-dotnet` implementation in this checkout.

No product code was changed while preparing this document.

## Done Criteria Used for This Analysis

- Identify the backend contract that matters to the tracer.
- Identify the behavior Java added and which parts are portable to .NET.
- Compare that behavior with the current .NET code paths.
- Propose one or more implementation strategies.
- Call out risks, ambiguities, and validation requirements before writing code.

## Second-Pass Revision

This section records the result of a fresh review after deciding that .NET should support the full surface: Datadog internal coverage and external tools.

The first-pass analysis was correct about the core RFC contract, but it under-scoped the desired .NET deliverable. If the goal is parity with Java plus .NET tool coverage, the implementation cannot stop at setting a corrected Datadog session metric. The implementation needs one shared line-coverage backfill engine and separate adapters for every source/sink of coverage data:

- Datadog's internal collector and global JSON coverage files.
- Coverlet's in-memory `CoverageResult.Modules` model.
- Cobertura XML reports.
- OpenCover XML reports.
- Microsoft CodeCoverage XML reports, when the exported report contains enough line-level data.

For external tools, "support" should mean both of these:

- The Datadog test session gets the corrected `test.code_coverage.lines_pct`.
- The customer-visible report object or report file is reconciled before downstream consumers read it.

There is one non-negotiable correctness limit: exact backfill requires local executable-line data. If a tool/report only exposes aggregate percentages or aggregate line counts, the tracer cannot accurately OR backend skipped-test line bitmaps into it. In that case the implementation must not claim success. The correct behavior is to avoid skipping tests when coverage correctness depends on an unsupported aggregate-only report, or to require/configure a line-capable output format such as Cobertura/OpenCover.

## Third-Pass Review Addendum

This pass found four additional implementation blockers that must be explicit in the plan.

First, backend `meta.coverage` is already OR-aggregated for the tests returned by the skippable-tests endpoint. If .NET locally filters out any returned skippable test after deserialization, the aggregate coverage map may still include coverage for a test that .NET will not skip. That would over-backfill. The implementation must either guarantee that backend aggregation already reflects the final filtered set, or treat local filtering as an unsafe condition for coverage backfill and avoid skipping/reporting backfilled coverage.

Second, the merge cannot blindly count every backend bit as covered. Backend skipped-test bits must be intersected with the local executable bitmap before they affect the executed count. In the current global model, `FileCoverageInfo.IncrementCounts()` counts executable and executed bitmaps independently; that is safe only if executed bits are always a subset of executable bits. Backfill code must enforce that invariant explicitly.

Third, one generic percentage calculation is not enough for every external report. The shared model is useful for matching and OR-merging lines, but the reported session percentage should come from the source-native post-mutation summary when a tool has its own semantics. Coverlet, OpenCover, Cobertura, and Microsoft CodeCoverage can differ in how duplicate sequence points, partially covered lines, and aggregate fields are counted.

Fourth, `test.code_coverage.backfilled` should be aligned with Java/product semantics. The useful signal is "the reported coverage was produced by a backend-aware ITR coverage path", not only "at least one previously zero bit flipped". If skipped-test backend coverage fully overlaps locally executed coverage, Java would still mark the ITR coverage path as backfilled. .NET should tag true when test skipping is active, backend coverage data was available for the coverage source, and the reported percentage was computed through the backfill-aware path.

## Fourth-Pass Review Addendum

This pass found five more details that should be part of the implementation contract.

First, .NET currently uses `CodeCoverageEnabled` for two different reasons: collecting per-test line coverage needed by ITR, and reporting a session-level coverage percentage. Under `dd-trace ci run`, test skipping can make `CodeCoverageEnabled` true even when the customer did not ask Datadog to publish an internal coverage percentage. The implementation must separate "collect coverage for ITR decisions" from "this source is selected to publish `test.code_coverage.lines_pct`".

Second, the plan needs source arbitration. If the Datadog collector, Coverlet IPC, Vanguard IPC, and `DD_CIVISIBILITY_EXTERNAL_CODE_COVERAGE_PATH` are all possible inputs, `DotnetCommon.FinalizeSession()` and `TestSession.OnIpcMessageReceived()` must not let a lower-priority internal result overwrite the customer-selected external report result. There should be one coverage result aggregator with explicit source priority.

Third, the RFC only gives line coverage for skipped tests. Backfill can correct line coverage percentages and line hit entries; it cannot reconstruct branch, method, condition, or complexity coverage for external tools. External report mutation must preserve those non-line fields or leave them unchanged, and documentation/tests should not imply that branch thresholds are corrected by this feature.

Fourth, `DD_CIVISIBILITY_CODE_COVERAGE_MODE=LineCallCount` needs explicit handling. Backend coverage is a line-execution bitmap, not call counts. For percentage purposes, local call counts should be treated as covered when the count is greater than zero, and backfill should only change a zero count to a covered value such as `1`; it must not overwrite non-zero counts.

Fifth, multiple reports must be combined by counts, not by "last message wins". `ManagedVanguardStopIntegration` can see multiple XML output files, and multiple data collector processes can send IPC messages. The session percentage should be derived from aggregated covered/executable counts for the selected source, not whichever IPC message arrives last.

## Fifth-Pass Review Addendum

This pass found three more implementation details that affect correctness under real .NET test runs.

First, the implementation must distinguish skippable candidates from tests that were actually skipped. Some tests returned by the backend may still run because they are unskippable, missing line coverage, or blocked by a fail-closed coverage decision. Coverage backfill and `_dd.ci.itr.tests_skipped` must be driven by actual ITR skips, not merely by `HasSkippableTests()`.

Second, the shared local model cannot be a simple `path -> file` map. The same repository-relative source file can appear in multiple local coverage entries, for example different assemblies, target frameworks, modules, or report nodes. Backend coverage should match by normalized path, then fan out to each compatible local entry while preserving the source's native denominator semantics.

Third, changing `CoverageReporter.Handler` dynamically is riskier than a single static field. `CoverageReporter<TMeta>` caches a module value from `CoverageReporter.GlobalContainer` in its static constructor. If the handler is swapped after any instrumented module has initialized its generic reporter, counters can remain attached to the old handler/container. The implementation should either initialize the final handler before any instrumented coverage reporter type is touched, or avoid handler replacement after initialization.

## Sixth-Pass Review Addendum

This pass did not find a new source category or a new product-scope decision beyond the full internal-plus-external support target. The remaining issue was execution drift: several Fifth-Pass constraints were present in the design narrative but not yet enforced in the risks, validation plan, and work breakdown.

The plan now treats these as implementation requirements:

1. actual applied skips, not backend candidates, drive skip/backfill tags;
2. one backend path can update multiple compatible local entries;
3. `CoverageReporter<TMeta>` initialization order is a testable lifecycle requirement; and
4. negative integration tests must prove that blocked or forced-run candidates do not produce false skipped/backfilled reporting.

## Seventh-Pass Review Addendum

This pass found three execution-order gaps in the plan.

First, backend `meta.coverage` represents the OR-aggregated coverage for the skippable response, not necessarily the exact set of tests whose coverage is missing from this .NET execution. Candidates that are returned by the backend but never observed locally because of command filters, targeted assemblies, target framework selection, or other runner-side subsetting can make the aggregate unsafe. The implementation must either prove the skippable request is scoped to the exact execution set, or fail closed for coverage-active skipping when the command can run only a subset. Tracking actual skips is necessary but not sufficient; the merge must not use aggregate coverage for candidates that were not skipped or otherwise covered locally.

Second, the IPC path needs a finalization barrier. `SessionCodeCoverageMessage` is read by a polling IPC thread, while `DotnetCommon.FinalizeSession()` closes the session immediately after the test command returns. A delayed Coverlet/Vanguard/data-collector message can otherwise arrive after the session is marked finished and be ignored. The source arbiter needs an explicit drain/wait phase before final tags are set and the session is closed.

Third, `DD_CIVISIBILITY_CODE_COVERAGE_PATH` currently means "internal Datadog coverage files are the reporting source" inside `DotnetCommon.FinalizeSession()`, and it causes `DD_CIVISIBILITY_EXTERNAL_CODE_COVERAGE_PATH` to be ignored. Under full support, .NET may still need internal Datadog coverage files for ITR backfill while an external tool remains the selected user-facing report. The implementation needs separate storage/configuration for internal backfill data versus the selected reporting source, or the source arbiter must take ownership before the old precedence branch runs.

## Eighth-Pass Review Addendum

This pass found three more implementation details that matter once the plan is executed in the current .NET runner architecture.

First, the local skippable-tests cache must be treated as part of correctness. `FileTestOptimizationClient` currently stores `getSkippableTests.json` under a run/workspace/session-name salt, while the skippable request and the coverage aggregate depend on service, environment, repository URL, commit SHA, runtime/test configurations, custom configurations, and potentially the effective test subset. A stale or cross-scope cached coverage aggregate would be as unsafe as an over-broad backend response. The cache key or a dedicated coverage-backfill file must include every dimension that affects the backend response and local execution scope, or the implementation must avoid cache reuse for coverage-active skipping.

Second, external-tool support means the Datadog coverage collector and the customer-selected coverage collector/report generator can run in the same test command. Datadog still needs per-test line coverage for future ITR decisions, but Coverlet, Microsoft CodeCoverage, or another collector may also rewrite/instrument the same assemblies. The plan must explicitly validate collector coexistence, ordering, and failure behavior so Datadog's internal collector does not corrupt or suppress the external report that we are trying to correct.

Third, the IPC message extension should stay deliberately small and delivery failures must be observable. The current circular channel has a bounded buffer and `TrySendMessage()` can fail, while callers currently do not act on the boolean result. Large diagnostics or accidental line-map payloads in IPC could be dropped. The design should keep line maps in shared files, keep IPC to source/count/percentage/status data, and treat failed delivery of the selected coverage result as a report-safety failure.

## Ninth-Pass Review Addendum

This pass found four additional edges that should be explicit before implementation starts.

First, external report correction has a timing boundary. Rewriting an XML file in `DotnetCommon.FinalizeSession()` can correct post-command consumers, but it cannot fix a coverage threshold that the external tool already evaluated inside the test command. Coverlet data collector and MSBuild modes can be supported only if the in-process `CoverageResult.Modules` hook mutates the model before Coverlet reporters and thresholds run. Other modes that enforce thresholds before Datadog can mutate the line model should be treated as not backfillable for coverage-active skipping, or documented as report-file-only correction.

Second, external XML paths are not guaranteed to already be Git-root-relative. Cobertura class filenames, OpenCover file paths, and Microsoft report paths can be absolute, project-relative, report-directory-relative, source-link based, or produced from deterministic build paths. Each XML adapter needs a source-root resolver that maps report paths to repository-relative backend keys unambiguously. Ambiguous or unresolved paths should fail closed for backfill instead of guessing.

Third, the implementation must distinguish a missing `meta.coverage` field from a present but empty coverage map. A present empty map can be valid when all actually skipped tests have line coverage but do not cover any executable source line in the selected local model. A missing field is different: if coverage correctness depends on backfill and skippable tests are present, it should remain unsafe unless the active coverage source does not require line backfill.

Fourth, diagnostics need to be designed deliberately. `TestOptimizationClient.GetSkippableTestsAsync()` currently logs the full response body at debug level. Once `meta.coverage` is present, that can log large bitmap payloads and repository file paths. The implementation should redact or summarize coverage maps in logs, add telemetry for accepted backfill, disabled backfill reasons, parse failures, path-resolution failures, and report-rewrite failures, and keep enough context to debug without leaking full coverage payloads.

## Tenth-Pass Review Addendum

This pass found two scope and collection details that affect Java-equivalent correctness.

First, `LineCallCount` affects more than the final merge. The current .NET coverage handlers build executed-line bitmaps with `filesLines[i] == 1` in both `DefaultCoverageEventHandler` and `DefaultWithGlobalCoverageEventHandler`. In call-count mode, a line executed more than once can have a value greater than one, so treating only `1` as covered can under-report both the local session percentage and the per-test coverage events that the backend later uses to create `meta.coverage`. The implementation must fix every bitmap-producing path to treat `> 0` as covered for `LineCallCount`, not only the new backfill adapter.

Second, the skippable-tests request and lookup scope must be explicit. Java sends an explicit `test_level` and carries module/test-bundle scoped execution settings. Current .NET `SkippableTestsQuery` does not include `test_level`, and `TestOptimizationSkippableFeature` indexes returned tests by suite/name only. For backfill, this is not just a skip-selection detail: `meta.coverage` is aggregated for the response scope. The .NET implementation should send an explicit `test_level` matching the local skip granularity, include a module/bundle dimension when the backend and local runner can distinguish it, and include that same dimension in local lookup, cache scope, and aggregate-safety checks. If a run cannot prove that suite/name/parameters are unique across the response scope, coverage-active skipping should fail closed for that ambiguous scope.

## Eleventh-Pass Review Addendum

This pass found one more feature-interaction boundary. Backfill must be scoped to skips caused by Intelligent Test Runner, not every test that does not execute.

The current .NET tracer has other Test Optimization features that can change execution independently of ITR. Test Management disabled tests can be skipped before the test body runs in NUnit, xUnit, and MSTest. Quarantined tests can run and then have their final status masked. Attempt-to-fix, Early Flake Detection, and automatic test retry can run extra attempts. Backend `meta.coverage` comes from the skippable-tests endpoint and describes ITR skippable coverage only, so it must not be used to backfill user skips, Test Management disabled skips, quarantined status masking, or retry behavior.

The precise equivalence target is "the same command with ITR skipping disabled and every other enabled feature, user filter, target framework, and selected report source unchanged." When another Datadog feature would skip a test in both runs, ITR backfill does not need to reconstruct that test's coverage. A full-suite baseline that ignores those other features too is outside the ITR `meta.coverage` contract and would need separate feature-specific coverage data.

Retry features are not a blocker as long as the local model aggregates all actual executions with OR semantics before the backend merge. Extra attempts must not inflate executable denominators, must not double count covered lines, and must not cause `_dd.ci.itr.tests_skipped` or `test.code_coverage.backfilled` when no test was actually skipped by ITR.

## Executive Summary

The RFC expects tracers to preserve `test.code_coverage.lines_pct` when ITR skips tests. The backend will return the line coverage of the skipped tests in the existing `POST /api/v2/ci/tests/skippable` response under `meta.coverage`. The tracer should merge that skipped-test coverage with the locally observed coverage and overwrite the session coverage percentage before the session is sent to CITESTCYCLE.

The current .NET tracer does not consume `meta.coverage`. It already sends per-test line bitmaps to the code coverage intake, and it already has a global coverage model capable of OR-merging file bitmaps. However, .NET intentionally disables the global coverage percentage path when test skipping is enabled because the result would be incomplete. That is the main integration point: under ITR, the tracer needs to re-enable or replace that global aggregation path, then OR the backend-provided skipped-test bitmaps into the local executed-line bitmaps before calculating the percentage.

The Java PR proves the shape of the implementation: parse `meta.coverage`, preserve it with execution settings, prevent skipping when line coverage is missing, merge backend lines into local coverage lines, keep the local coverage model as the denominator, and tag the result as backfilled. A .NET implementation can follow the same contract, but it needs more adapters because .NET has several coverage sources and report formats.

The full-support target should be:

1. Add shared backend coverage parsing, storage, path normalization, and missing-line-coverage skip safety.
2. Separate coverage collection for ITR from the selected source that publishes a session-level coverage percentage.
3. Add a shared local coverage model that represents executable and executed lines per normalized source path, while allowing multiple local entries for the same path.
4. Convert Datadog internal coverage, Coverlet modules, Cobertura XML, OpenCover XML, and line-capable Microsoft CodeCoverage XML into that model.
5. Merge backend skipped-test coverage into the local model by masking it with local executable lines, then OR-ing it into local executed lines.
6. Push the merged data back into the original report object/file where possible, so external reports are corrected too.
7. Track actual skipped tests separately from skippable candidates.
8. Ensure the backend aggregate represents only coverage missing from the current execution, or disable coverage-active skipping when that cannot be guaranteed.
9. Ensure cache keys, shared backfill files, and IPC messages preserve the same execution scope instead of widening it accidentally.
10. Validate that Datadog's own coverage collection can coexist with the supported external tools.
11. Resolve external report paths to backend repository-relative paths unambiguously before merging.
12. Mutate external report objects before tool thresholds/reporters when the tool evaluates coverage inside the test command; otherwise fail closed or limit support to post-command report consumers.
13. Redact large backend coverage payloads from logs and emit structured telemetry for backfill success and fail-closed reasons.
14. Fix `LineCallCount` bitmap production so all count values greater than zero are treated as covered before data is sent to the backend or used for local session coverage.
15. Make skippable-tests request, lookup, cache, and backfill scope explicit for `test_level` and module/bundle identity.
16. Scope backfill and skip metrics to actual ITR skips under the same non-ITR feature set, without using `meta.coverage` for Test Management or user-level skips.
17. Set `test.code_coverage.lines_pct` and `test.code_coverage.backfilled=true` from the selected backend-aware merged result or source-native post-mutation summary only when actual skips require it.

## Backend RFC Findings

### Required tracer behavior

The RFC's target behavior is that enabling ITR must not change the session value of `test.code_coverage.lines_pct`. If the same command with ITR skipping disabled reports 80%, then the ITR run should also report 80% even when some tests are skipped by ITR. Other enabled features and user filters are part of that baseline.

The selected design is tracer-side computation:

- The backend returns the coverage of skipped tests in the same skippable-tests response.
- The tracer already has local coverage for the tests that ran.
- The tracer merges the backend skipped-test coverage into the local coverage.
- The tracer overwrites the session coverage percentage before sending the session payload.

Important RFC details:

- The response field is `meta.coverage`.
- `meta.coverage` is a map from repository-relative source path to base64-encoded bitmap.
- The backend pre-aggregates coverage with OR by file across all tests that are skippable.
- The backend intentionally returns 5xx if it cannot process line coverage, because wrong coverage is considered worse than not skipping tests.
- Individual skippable tests may include `_is_missing_line_code_coverage: true`; tracers should use that to avoid skipping tests when line coverage is required for a correct percentage.
- The file paths returned are from the Git root, not hashed.
- The skippable-tests request should declare the test level used by the tracer so the backend response scope matches the local skip granularity.
- Response size can grow significantly, so parsing should avoid unnecessary copies.

The relevant response shape is:

```json
{
  "data": [
    {
      "id": "22ab8cb462b17410",
      "type": "test",
      "attributes": {
        "name": "test_foo_1",
        "suite": "src/calculator.x"
      }
    }
  ],
  "meta": {
    "correlation_id": "eda523ea1c1953101c5e3a0815fdff53",
    "coverage": {
      "src/calculator.x": "/8AA/w==",
      "src/utils/math.x": "AAAAf+AA/A=="
    }
  }
}
```

### Invariants implied by the RFC

The tracer should treat backend coverage as executed-line coverage only. It should not use backend-only files to increase the executable-line denominator unless it also has a reliable local source of executable-line data for those files. Java follows this rule by using the local coverage bundle as the denominator and only using backend bitmaps to fill execution gaps for files that exist in the local bundle.

The tracer should merge coverage by OR, not by summing line counts. OR prevents double counting when a line is covered by both a locally executed test and a skipped test.

The tracer should be conservative when coverage data is incomplete. If line coverage is required and a skippable test is marked `_is_missing_line_code_coverage`, skipping that test can still produce a wrong coverage percentage. Java avoids skipping those tests under line coverage.

## Java Reference Findings

The Java PR added a full backfill path around its Jacoco coverage calculator. The important behaviors to port conceptually are:

- Parse `meta.coverage` from the skippable-tests response as `Map<String, BitSet>`.
- Store that map in execution settings so the coverage calculator can access it later.
- Normalize coverage file paths enough to match local coverage paths.
- Treat `_is_missing_line_code_coverage` as a skip blocker when line coverage is enabled.
- Merge backend coverage into local coverage using OR.
- Keep the local coverage bundle as the denominator for executable lines.
- Ignore backend coverage entries whose files are not present in the local coverage bundle.
- Set `test.code_coverage.lines_pct` after the merge.
- Mark the result with `test.code_coverage.backfilled` when the reported percentage comes from the backend-aware ITR coverage path.

The most important design point is the denominator rule. Java does not compute:

```text
covered = local covered lines + backend covered lines
total = local total lines + backend total lines
```

Instead, it computes:

```text
total = executable lines from the local coverage bundle
covered = local executed lines OR backend skipped-test executed lines for matching files
```

That rule prevents backend-only files or stale backend coverage from inventing denominator data the current process did not observe.

## Java-Parity Checklist for .NET

Applying this document should produce Java-equivalent behavior if the implementation satisfies all items below:

1. The skippable-tests response parser reads `meta.coverage`.
2. The backend coverage map survives until coverage reporting time, including child/data-collector processes.
3. `_is_missing_line_code_coverage` is modeled on skippable tests.
4. Tests marked `_is_missing_line_code_coverage` are not skipped whenever line coverage is required to correct the active coverage report.
5. Every supported coverage source is converted into a local file-line model containing:
   - executable lines
   - executed lines
   - a normalized repository-relative path
6. Backend executed-line bitmaps are OR-merged into matching local files only.
7. Backend-only files are ignored for percentage purposes because they do not provide a reliable local denominator.
8. The merged coverage is written back to the source report object or report file when that source belongs to an external tool.
9. `test.code_coverage.lines_pct` is computed from the merged internal model or the supported tool's source-native post-mutation summary.
10. `test.code_coverage.backfilled=true` is set when the reported percentage was produced by the backend-aware ITR backfill path.
11. Internal Datadog collection used for ITR does not overwrite a selected external coverage result.
12. Multiple coverage reports for the same selected source are aggregated by covered/executable counts before the session tag is set.
13. Actual skipped-test state is tracked separately from the existence of skippable candidates.
14. The local coverage model preserves multiple local entries for the same normalized backend path.
15. The backend aggregate is used only when it represents the current execution's missing coverage, not unobserved tests outside the command's effective selection.
16. IPC-delivered coverage results are drained before the session is finalized.
17. Skippable-tests and backfill caches are scoped to all request and execution dimensions that affect `meta.coverage`.
18. Datadog coverage instrumentation is validated to coexist with every supported external coverage source.
19. External report path adapters resolve paths to repository-relative backend keys without ambiguity.
20. External tool modes that enforce thresholds before Datadog can mutate their line model are either hooked before threshold evaluation or marked not backfillable.
21. Logging and telemetry summarize `meta.coverage` and backfill outcomes without dumping full file maps and bitmaps.
22. `LineCallCount` treats any positive count as covered in every local bitmap-producing path, including per-test coverage events uploaded for future ITR decisions.
23. The skippable-tests request declares the intended test level, and local lookup/backfill scope includes module or bundle identity when the execution model can produce duplicate suite/name pairs.
24. Backfill scope is limited to actual ITR skips; non-ITR user skips, Test Management disabled/quarantined behavior, and retry attempts are accounted for in the local baseline rather than patched with ITR `meta.coverage`.

This is the .NET equivalent of Java's `ExecutionSettings -> JacocoCoverageCalculator -> build-system session/module tags` flow. The difference is that .NET needs multiple adapters feeding the shared merge engine instead of a single Jacoco adapter.

## Current .NET State

### Skippable tests response path

`TestOptimizationClient.GetSkippableTestsAsync()` posts to `api/v2/ci/tests/skippable` and deserializes `DataArrayEnvelope<Data<SkippableTest>>`.

Current behavior:

- It reads `deserializedResult.Meta?.CorrelationId`.
- It returns `SkippableTestsResponse(correlationId, tests)`.
- It ignores any `meta.coverage` value.
- `SkippableTestsResponse` contains only `CorrelationId` and `Tests`.

Relevant files:

- `tracer/src/Datadog.Trace/Ci/Net/TestOptimizationClient.GetSkippableTestsAsync.cs`
- `tracer/src/Datadog.Trace/Ci/Net/TestOptimizationClient.cs`

`TestOptimizationSkippableFeature` stores skippable tests by suite and name and carries only the correlation id. It has no place to store backend coverage.

The current request model also does not include an explicit `test_level`, and the local lookup does not include module or bundle identity. That differs from Java's execution settings model, where the backend response and local execution are scoped more explicitly. For backfill, this scope has to be corrected or proven safe because the returned `meta.coverage` is aggregated for the response scope.

Relevant file:

- `tracer/src/Datadog.Trace/Ci/TestOptimizationSkippableFeature.cs`

### Skippable test model and skip decision

`SkippableTest` does not currently model `_is_missing_line_code_coverage`.

`Common.ShouldSkip()` checks whether the current test matches a skippable test by suite, name, and parameters. It does not check whether skipping that test would make line coverage incomplete.

Relevant files:

- `tracer/src/Datadog.Trace/Ci/SkippableTest.cs`
- `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/Testing/Common.cs`

Framework-specific integrations call this shared helper:

- xUnit: `XUnitIntegration.ShouldSkip(...)`
- NUnit: `NUnitIntegration.ShouldSkip(...)`
- MSTest: `MsTestIntegration.ShouldSkip(...)`

That shared helper is the right place for a framework-independent missing-line-coverage gate.

The implementation also needs a feature-origin distinction around this gate. Framework integrations can skip tests for ITR and can also skip or mask tests for Test Management disabled/quarantined states. Only skips closed with `IntelligentTestRunnerTags.SkippedByReason` should become actual ITR skips for backfill, `_dd.ci.itr.tests_skipped`, or `test.code_coverage.backfilled` purposes. Disabled or user-skipped tests may still be skipped in the baseline run and should not be backfilled from ITR `meta.coverage`.

### Datadog code coverage payloads

.NET already emits per-test file bitmaps through `DefaultCoverageEventHandler`. For each file it creates:

- `filename`: relative path from the source root
- `bitmap`: executed-line bitmap bytes

This is semantically aligned with the RFC's required line coverage events. The same bitmap orientation must be verified against backend expectations, but the current .NET intake path is already sending byte arrays for file line coverage.

One existing detail must be fixed before relying on those events for future backfill: in `LineCallCount` mode, current handlers treat only counter value `1` as covered. A positive call count greater than one should also set the executed bit, otherwise the backend can later return incomplete skipped-test coverage.

Relevant files:

- `tracer/src/Datadog.Trace/Ci/Coverage/DefaultCoverageEventHandler.cs`
- `tracer/src/Datadog.Trace/Ci/Agent/MessagePack/TestCoverageMessagePackFormatter.cs`

### Global coverage aggregation

.NET already has a global coverage model:

- `GlobalCoverageInfo`
- `ComponentCoverageInfo`
- `FileCoverageInfo`
- `FileBitmap`

`DefaultWithGlobalCoverageEventHandler.GetCodeCoveragePercentage()` builds a global coverage payload from captured module data:

- `ExecutableBitmap`: executable lines from metadata
- `ExecutedBitmap`: lines observed as executed
- `GetTotalPercentage()`: `executed / total * 100`

`FileCoverageInfo.AggregateExecutedBitmap()` already OR-merges executed bitmaps.

This model is a good fit for backend backfill because backend coverage is also just executed-line bitmap data.

Relevant files:

- `tracer/src/Datadog.Trace/Ci/Coverage/DefaultWithGlobalCoverageEventHandler.cs`
- `tracer/src/Datadog.Trace/Ci/Coverage/Models/Global/GlobalCoverageInfo.cs`
- `tracer/src/Datadog.Trace/Ci/Coverage/Models/Global/FileCoverageInfo.cs`
- `tracer/src/Datadog.Trace/Ci/Coverage/Util/FileBitmap.cs`

### Current ITR blocker

`CoverageReporter` selects the default handler like this:

```csharp
private static CoverageEventHandler _handler =
    TestOptimization.Instance.Settings.TestsSkippingEnabled == true
        ? new DefaultCoverageEventHandler()
        : new DefaultWithGlobalCoverageEventHandler();
```

So when test skipping is enabled, .NET uses the per-test coverage handler but not the global coverage handler. That makes sense today because a global percentage would be wrong after skipped tests.

`CiUtils` reinforces the same rule: it only sets `DD_CIVISIBILITY_CODE_COVERAGE_PATH` when test skipping is not enabled, with the comment that global coverage is reliable only when tests are not skipped.

Relevant files:

- `tracer/src/Datadog.Trace/Ci/Coverage/CoverageReporter.cs`
- `tracer/src/Datadog.Trace.Tools.Runner/CiUtils.cs`

Backfill changes this assumption. When backend skipped-test coverage is available and safely merged, global coverage can become reliable under ITR again.

### Session and module reporting

`TestModule.InternalClose()` only sets `test.code_coverage.lines_pct` from global coverage for a fake/internal session when ITR is disabled. The comment explicitly says normal customer sessions never report percentage of total lines on modules.

`DotnetCommon.FinalizeSession()` sets the session coverage percentage from:

- Datadog global JSON coverage files under `DD_CIVISIBILITY_CODE_COVERAGE_PATH`, or
- an external XML file under `DD_CIVISIBILITY_EXTERNAL_CODE_COVERAGE_PATH`.

`TestSession.OnIpcMessageReceived()` accepts `SessionCodeCoverageMessage` and sets only `test.code_coverage.lines_pct`.

Relevant files:

- `tracer/src/Datadog.Trace/Ci/TestModule.cs`
- `tracer/src/Datadog.Trace/Ci/TestSession.cs`
- `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/Testing/DotnetTest/DotnetCommon.cs`
- `tracer/src/Datadog.Trace/Ci/Ipc/Messages/SessionCodeCoverageMessage.cs`

### Existing external tool support

.NET currently supports external code coverage percentages, but mostly as final aggregate numbers:

- Coverlet: `CoverageGetCoverageResultIntegration` instruments `Coverlet.Core.Coverage.GetCoverageResult()`, calls Coverlet's `CoverageSummary.CalculateLineCoverage(modules)`, and sends only the final percentage by IPC.
- Microsoft CodeCoverage Vanguard: `ManagedVanguardStopIntegration` reads output XML and sends only the final percentage by IPC.
- External XML path: `DotnetCommon.TryGetCoveragePercentageFromXml()` reads OpenCover, Cobertura, or Microsoft CodeCoverage XML and extracts only aggregate percentage/counts.

These paths do not retain enough information to merge backend line bitmaps. A final percentage cannot be corrected with skipped-test bitmaps because the merge must happen at file and line level.

The good news is that several external paths already expose line data before .NET throws it away:

- Coverlet's `CoverageResult.Modules` contains document paths and per-line hit counts. The current integration already has access to `modules`; it just stops at `CoverageSummary.CalculateLineCoverage(modules)`.
- Cobertura XML has class filenames and `<line number="..." hits="...">` entries.
- OpenCover XML has file ids and `<SequencePoint vc="..." sl="..." fileid="...">` entries.

Those formats can be made Java-equivalent by adapting them into the shared line model, merging backend coverage, and writing the merged line hits back before calculating/reporting the percentage.

Microsoft CodeCoverage is less clear from the current code. The current parser only reads aggregate module attributes. If the actual XML emitted by supported collector modes has line entries, it can use the same XML adapter pattern. If it only has aggregate counts, it cannot be corrected exactly from that file alone.

Relevant files:

- `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/Testing/DotnetTest/CoverageGetCoverageResultIntegration.cs`
- `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/Testing/DotnetTest/ManagedVanguardStopIntegration.cs`
- `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/Testing/DotnetTest/DotnetCommon.cs`

## Implementation Strategy

The implementation target is full support, not an internal-only MVP. The right shape is a shared backfill engine plus source-specific adapters.

### Common engine

Add a shared internal model:

```text
LineCoverageModel
  files: map normalized relative path -> one or more FileLineCoverage entries

FileLineCoverage
  executable bitmap
  executed bitmap
  source/component/module identity
  original path/report node references when report mutation is needed
```

Then implement one merge function:

```text
Merge(localModel, backendSkippedCoverage):
  for each backend coverage entry:
    localEntries = localModel.files[backend.normalizedPath]
    if localEntries exists:
      for each compatible local entry:
        maskedBackend = backend.executed AND local.executable
        local.executed = local.executed OR maskedBackend
      local.backfilled = true
  ignore backend files that do not exist in localModel
  return localModel
```

This is the Java algorithm adapted to .NET. The executable denominator always comes from the local tool/model; backend coverage only fills executed-line gaps.

The model must preserve source-native cardinality. A normalized repository path is a lookup key, not a unique file identity. If the same source file appears in two assemblies, target frameworks, modules, or report nodes, the backend bitmap can apply to both local entries, but the final denominator and summary must still follow the source's existing counting rules.

### Adapters that read local coverage

Implement adapters that convert each current source into `LineCoverageModel`:

- Datadog internal global coverage: `GlobalCoverageInfo` already has executable and executed bitmaps.
- Datadog JSON coverage files: reuse the same `GlobalCoverageInfo` model after `CoverageUtils` combines files.
- Coverlet: read `CoverageResult.Modules`, whose shape is module -> document -> class -> method -> line hit counts.
- Cobertura XML: read class `filename` values and `<line number="..." hits="...">` entries.
- OpenCover XML: read `<File uid fullPath>` and `<SequencePoint vc sl fileid>` entries.
- Microsoft CodeCoverage XML: support the line-capable XML export. If the XML only has aggregate module attributes such as `lines_covered`, `lines_partially_covered`, and `lines_not_covered`, it is not sufficient for exact line backfill.

### Adapters that write merged coverage back

For internal Datadog coverage, writing back means setting the merged percentage and, when applicable, writing merged `GlobalCoverageInfo` JSON.

For external tools, writing back means mutating the tool/report before downstream readers consume it:

- Coverlet: mutate `CoverageResult.Modules` in `CoverageGetCoverageResultIntegration.OnMethodEnd()` before calculating the percentage and before Coverlet reporters/thresholds use the returned result.
- Cobertura XML: update matching `<line hits>` entries and recalculate aggregate attributes on class, package, and root nodes.
- OpenCover XML: update matching `<SequencePoint vc>` entries and recalculate relevant `<Summary>` attributes.
- Microsoft CodeCoverage XML: update and recalculate only if the XML exposes line-level entries. Aggregate-only XML cannot be made exact.

For all external tools, only existing local executable lines should be flipped from not-covered to covered. Do not add new line entries from backend-only data.

### Skip safety for unsupported coverage paths

If a configured coverage path cannot be backfilled accurately, the tracer must prevent ITR from creating an inaccurate report. The practical rule is:

- If coverage reporting is active and the current coverage source is known to be line-capable, allow skipping and backfill later.
- If coverage reporting is active but the current coverage source is aggregate-only or unknown, do not skip tests.
- If a matching skippable test has `_is_missing_line_code_coverage=true`, do not skip it when any active coverage source requires backfill.

This is stricter than the current .NET behavior, but it is the only way to support "all" without producing false coverage reports.

## Proposed .NET Design

### Backend coverage representation

Introduce a small internal representation for backend skipped-test coverage:

```text
CoverageBackfillData
  - IReadOnlyDictionary<string, byte[]> ExecutedLinesByRelativePath
  - bool HasCoverage
```

Path normalization should be centralized:

- Convert `\` to `/`.
- Remove a leading `/` if present, matching Java's behavior.
- Preserve case initially. On Windows, consider a comparer strategy only after checking how `CIEnvironmentValues.Instance.MakeRelativePathFromSourceRoot(..., false)` behaves for source paths.
- Do not hash paths; the RFC explicitly chose plain repository-relative paths.

Base64 parsing should happen once at response parsing time. Invalid base64 should be treated as a failed skippable-tests response if it means coverage is required for correctness. That aligns with the RFC's "wrong coverage is worse than no skipping" rule.

### Skippable-tests response parsing

Extend the deserialization model used by `TestOptimizationClient.GetSkippableTestsAsync()`:

- `Metadata` should include `coverage`.
- `SkippableTestsResponse` should carry decoded coverage.
- `TestOptimizationSkippableFeature.SkippableTestsDictionary` should expose the decoded coverage to later code.

The current `Metadata` class is shared with correlation id handling, so the change should be made there rather than by manually parsing JSON a second time.

The current method also applies a local custom-configuration filter after deserializing the backend response. That is safe for test selection today, but it is dangerous for coverage backfill because `meta.coverage` is an aggregate over the backend response. The implementation must make this an explicit invariant:

- Preferred behavior: ensure the backend only returns tests and aggregate coverage for the exact requested configurations, so local filtering never removes items when coverage backfill is required.
- Safe fallback: if local filtering removes any test and `meta.coverage` is present, mark the response unsafe for backfill and do not skip tests whose skipped coverage would be needed for a correct report.
- Do not keep the filtered test list while using the unfiltered aggregate coverage map. That would add coverage for tests that still ran locally.

The same invariant applies to runner-side test selection, not only custom configurations. If `dotnet test --filter`, `vstest.console /TestCaseFilter`, targeted assemblies, target framework selection, or any other command-level subset means some backend candidates will never be observed in this session, the aggregate coverage can include lines for tests whose coverage is not actually missing. The implementation must choose one of these safe approaches:

- include the effective selection in the backend request so `meta.coverage` is aggregated only for candidates in this execution;
- prove that the backend response is already scoped to the same module/assembly/test set that the local runner will execute; or
- disable coverage-active skipping for commands where an unscoped subset can make `meta.coverage` unsafe.

The request must also explicitly include the test granularity that the .NET tracer is going to apply locally. If the backend expects `test_level` and .NET relies on an implicit default, a backend-side default change or mismatch can make both skippable candidates and `meta.coverage` semantically wrong. The safe target is to send `test_level: "test"` when .NET is doing test-level skipping and to include module/bundle identity when that identity is available.

The local skippable lookup should also include the same scope. Suite/name/parameters are not guaranteed to be globally unique across assemblies, target frameworks, or modules. If module or bundle identity cannot be added for a mode, the coverage-capability service should treat duplicate suite/name matches across the response scope as ambiguous and disable coverage-active skipping for that scope.

Recording actual applied skips remains required, but it cannot fix an already aggregated bitmap after the fact. If the tracer discovers at the end that some backend candidates were neither skipped nor observed with local line coverage, it should treat the backfilled result as invalid and should not publish or mutate a coverage report that claims to be corrected.

If coverage reporting is active and the skippable response contains tests but no `meta.coverage`, the tracer should also treat skipping as unsafe unless the active coverage source does not need line backfill. The RFC says the backend should return an error when it cannot process line coverage; .NET should still fail closed if it receives a structurally successful but coverage-less response while coverage correctness depends on backfill.

Do not collapse all "no backend coverage" cases into the same state. A missing `meta.coverage` field means the backend did not provide the contract needed for backfill. A present empty map means the backend provided the contract but the OR-aggregated skipped-test coverage contains no covered lines. The latter can be safe if every actually skipped test is not marked `_is_missing_line_code_coverage` and all other response-safety checks pass.

Logging must change with this parser. The current debug log writes the full skippable-tests response body. With `meta.coverage`, normal debug output should summarize counts and byte sizes, not print every file path and bitmap. Full payload logging should remain unavailable by default or be guarded by a narrower troubleshooting switch.

The coverage data also has to be available outside the parent test process. Java serializes it inside execution settings. In .NET, the most reliable equivalent is:

- keep it on `TestOptimizationSkippableFeature` for in-process test framework integrations;
- persist it in the existing `.dd/<runId>/...` cache or in a dedicated compact `coverage-backfill.json` file;
- propagate the path with an environment variable so data collector domains, Coverlet MSBuild tasks, and child test processes can read the same backend coverage without issuing a second request.

The current `FileTestOptimizationClient` already caches `getSkippableTests.json`; adding coverage to `SkippableTestsResponse` should make cache reuse possible. A dedicated coverage-backfill file is still safer because it removes dependency on cache-key salt matching in tool-specific child processes.

Do not reuse `DD_CIVISIBILITY_CODE_COVERAGE_PATH` as the only internal storage signal without also changing `DotnetCommon.FinalizeSession()`. Today that variable makes internal Datadog JSON coverage win over `DD_CIVISIBILITY_EXTERNAL_CODE_COVERAGE_PATH`. Backfill storage for ITR and the selected source that publishes `test.code_coverage.lines_pct` must remain separate concepts.

The cache key is part of the correctness contract. The cached skippable-tests response or dedicated backfill file must be scoped to every backend request dimension and every local execution-scope dimension that can change `meta.coverage`, including service, environment, repository URL, commit SHA, test level, module or bundle identity, runtime/test configurations, custom configurations, selected assemblies, target framework, and explicit test filters when they are supported. If the implementation cannot encode those dimensions reliably, coverage-active skipping should bypass the shared cache rather than reuse a possibly broader aggregate.

### Missing-line-coverage safety

Add `_is_missing_line_code_coverage` to `SkippableTest`.

Then, in `Common.ShouldSkip()`, after a test matches by suite/name/parameters:

- If coverage backfill is required and the matched skippable test is missing line coverage, return `false`.
- Otherwise return `true`.

Define "coverage backfill is required" as:

```text
TestsSkippingEnabled == true
AND any of:
  - resolved Test Optimization settings say line coverage is enabled
  - DD_CIVISIBILITY_CODE_COVERAGE_ENABLED is true
  - DD_CIVISIBILITY_CODE_COVERAGE_PATH is active
  - DD_CIVISIBILITY_EXTERNAL_CODE_COVERAGE_PATH is active
  - Coverlet integration is active in data collector or MSBuild task mode
  - Microsoft CodeCoverage/Vanguard integration is active
```

This rule is intentionally broad because the decision has been made to support internal and external coverage tools. If the tracer cannot determine the active coverage source early, it should use the conservative path: do not skip tests that are missing line coverage.

The capability decision should use resolved `TestOptimizationSettings`, active collector/tool detection, and command-line/report configuration together. Raw environment variables alone are not enough because `dd-trace ci run` can enable Datadog line collection for ITR even when internal Datadog percentage reporting is not the selected output.

### Backfill merge algorithm

Use the shared `LineCoverageModel` for all sources. `GlobalCoverageInfo` can be one adapter into that model.

1. Build local coverage normally.
2. Build an index of local files by normalized repository-relative path, allowing multiple local entries per path.
3. For each backend coverage entry:
   - Find all matching compatible local entries.
   - If there is no local match, ignore it for the percentage.
   - For each match, AND backend bytes with that local entry's executable bitmap.
   - OR that masked backend bitmap into that local entry's executed bitmap.
   - Do not modify any local executable bitmap.
4. Clear cached coverage percentage data after the merge.
5. Compute percentage from the merged model for internal Datadog coverage, or from the source-native post-mutation summary for external tools.
6. Write the merged executed lines back into the original coverage object/file when the source is an external tool.

Ignoring backend-only files is important. Without local executable-line data, adding backend-only files would either miss denominator lines or require trusting stale backend state. Java avoids that by using the local Jacoco bundle as the denominator.

Masking backend executed lines with local executable lines is equally important. It prevents malformed, stale, or differently indexed backend bytes from making the executed count larger than the local denominator. This is especially relevant for the existing `GlobalCoverageInfo` path because `FileCoverageInfo.IncrementCounts()` counts active bits in `ExecutableBitmap` and `ExecutedBitmap` independently.

The merge engine should return both the percentage and a boolean telling whether the reported coverage was produced by the backend-aware backfill path. A separate diagnostic value can track whether any new executed bit was flipped, but that should not be the only driver for the product tag.

### Handler lifecycle

The current static selection in `CoverageReporter` is risky for this feature because it decides the handler type based only on `TestsSkippingEnabled` at initialization time.

There is a second lifecycle risk: `CoverageReporter<TMeta>` caches a `ModuleValue` from `CoverageReporter.GlobalContainer` in its static constructor. If the implementation swaps `CoverageReporter.Handler` after any instrumented generic reporter type has initialized, that cached module value can still point at the old handler's global container.

The implementation should avoid relying on that static initializer. Safer alternatives:

- Add an explicit initialization step after Test Optimization settings are finalized.
- Or make the handler selection dynamic and include "test skipping with coverage backfill" as a distinct mode.
- Or use a handler that can both emit per-test coverage and retain global coverage when needed.

The chosen approach must preserve the existing per-test coverage payload behavior because the backend still needs per-test coverage events for future ITR decisions. It must also ensure the final handler/container is established before any instrumented module can initialize `CoverageReporter<TMeta>`, or else avoid replacing the handler after initialization.

### Session tag behavior

Add a tag constant for:

```text
test.code_coverage.backfilled
```

Set it when all of these are true:

- test skipping is active;
- at least one test was actually skipped by ITR in the session;
- backend skipped-test coverage was available and accepted as safe for the active coverage source;
- the session percentage was computed after running the backend-aware merge path.

Do not require a newly flipped bit to set the tag. If the skipped tests only cover lines that were also covered by executed tests, the corrected percentage is numerically unchanged, but the report still came from the ITR backfill path. That is closer to the Java behavior and avoids hiding the fact that the coverage calculation depended on backend data.

For .NET, the customer-visible target is the test session because `DotnetCommon.FinalizeSession()` and `SessionCodeCoverageMessage` already set session-level coverage. This plan should not add new normal-customer module-level coverage reporting just to mirror Java's build-module surface, because current .NET explicitly avoids module coverage percentages for normal customer sessions. If an existing .NET path already produces module-level or fake-session coverage through `TestModule.InternalClose()`, that path should use the same merge engine and the same backfilled tag semantics.

This requires actual-skip tracking. The current code has several places that use `HasSkippableTests()` as a proxy for `_dd.ci.itr.tests_skipped`; that is not precise enough once some candidates are intentionally forced to run for coverage correctness. The implementation should increment or record actual ITR skips only after framework-level checks such as unskippable traits and coverage fail-closed decisions have allowed the skip to be applied.

### External coverage and IPC

The current IPC message carries only a `double Value`. That is not enough for line-level backfill.

Use this design:

1. Tool-specific instrumentation applies backfill in the process that owns the report object/file.
2. That process sends `SessionCodeCoverageMessage` with:
   - source kind
   - corrected percentage
   - covered and executable line counts when available
   - `backfilled` flag
   - optional diagnostics
3. `TestSession.OnIpcMessageReceived()` records the result with the coverage source arbiter.
4. The arbiter sets `test.code_coverage.lines_pct` and `test.code_coverage.backfilled` once the selected source result is final.

This keeps large line maps out of IPC. The backend coverage map must therefore be readable from child/tool processes through the shared backfill cache described above.

The session process also needs a finalization barrier. The IPC server polls for messages asynchronously, and `FinalizeSession()` currently closes the session immediately after the wrapped command returns. Before the arbiter publishes final coverage tags, it should drain already-written IPC messages and, when a selected external source is expected, wait for a bounded interval or an explicit completion signal from that source. Otherwise a valid corrected percentage can arrive after `_finished` is set and be ignored.

The IPC contract should stay small. `SessionCodeCoverageMessage` should carry source kind, counts, percentage, backfilled state, and compact diagnostics only. It should never carry per-file or per-line maps. Callers that send the selected coverage result must check the `TrySendMessage()` return value and log/report failure so the parent session can fail closed or use a deterministic fallback rather than silently publishing stale coverage.

Do not let IPC messages directly overwrite the session tag without source arbitration. The IPC message should include enough structured data for the session process to aggregate and prioritize results:

- source kind, for example `DatadogInternal`, `Coverlet`, `Cobertura`, `OpenCover`, or `MicrosoftCodeCoverage`;
- covered line count and executable line count, when the source can provide counts;
- corrected percentage;
- `backfilled` flag;
- optional diagnostics such as report path or tool name.

If a tool can produce multiple reports in one run, the child/tool process should either send one aggregated message or the session process should merge messages by source before setting `test.code_coverage.lines_pct`. The final result must not depend on IPC arrival order.

For Coverlet:

- `CoverageGetCoverageResultIntegration` already runs after `Coverage.GetCoverageResult()` and before the result is consumed.
- It should duck type deeper into `CoverageResult.Modules`.
- It should convert document line hit counts into `LineCoverageModel`.
- It should merge backend coverage.
- It should mutate existing line hit entries from `0` to `1` for backfilled lines before calling Coverlet's own summary calculation.
- It must not add line entries that Coverlet did not already report as executable.
- The IPC percentage should come from Coverlet's own `CoverageSummary.CalculateLineCoverage(modules)` after mutation, not from a generic distinct-line calculation, so Datadog matches the corrected Coverlet report.

The supported Coverlet modes should be explicit. The current integration only runs for the data collector domain and MSBuild task paths around `Coverlet.Core.Coverage.GetCoverageResult()`. Coverlet console or other modes should be supported through generated XML reports when they are routed through the external-file parser, or treated as unsupported for coverage-active skipping.

For XML reports:

- Replace `TryGetCoveragePercentageFromXml()` with a parser/rewriter abstraction.
- Keep aggregate-only percentage extraction only for non-ITR or non-backfill fallback cases.
- Under ITR backfill, require a structured parser that can read and write per-line data.
- Resolve report paths to repository-relative paths through a shared source-root resolver before matching backend coverage.
- Reject ambiguous or unresolved path matches for backfill instead of guessing.
- After mutation, recalculate aggregate percentage fields before the report file is left for customer tooling.
- The session percentage should come from the recalculated XML summary fields using that format's own semantics.

XML-file support only guarantees correction before consumers that read the file after `dd-trace ci run` finalization. If the coverage tool enforces thresholds inside the wrapped test command before Datadog can rewrite the file, that mode is not equivalent to Java/Coverlet in-process support. The safe behavior is to hook the tool before threshold evaluation, or disable coverage-active skipping for that mode.

For Microsoft CodeCoverage/Vanguard:

- `ManagedVanguardStopIntegration` already sees output coverage files after `Stop()`.
- It should run the same parser/rewriter over each XML file.
- If multiple XML files are produced, it should aggregate line counts across all rewritten files and send one source-level IPC result, or send structured count-based messages that the session aggregator can merge.
- If the Microsoft XML file is aggregate-only, exact backfill is impossible from that file. The implementation should either require a line-capable export format or prevent skipping when that mode is detected/configured.

### Collector coexistence

Under ITR, .NET enables Datadog coverage collection even when the user-facing coverage source is external, because the backend still needs per-test line coverage for future skip decisions. That can put Datadog's `DatadogCoverage` collector in the same run as Coverlet, Microsoft CodeCoverage, or another collector.

The implementation must validate this coexistence before claiming full external-tool support:

- Datadog assembly rewriting does not prevent the external tool from instrumenting or reporting.
- External tool rewriting does not prevent Datadog from recording per-test line coverage.
- Collector order does not change the corrected line percentage.
- If coexistence fails for a tool/mode, coverage-active skipping is disabled for that mode instead of producing an inaccurate external report.

This validation is separate from report mutation. Even a perfect XML or Coverlet adapter is not enough if the local run cannot reliably collect both Datadog ITR coverage and the customer-facing coverage data in the same command.

### Coverage source arbitration

Introduce one internal component responsible for deciding which coverage result can publish `test.code_coverage.lines_pct`. It should distinguish these concepts:

- coverage collection required for ITR and future skippability decisions;
- internal Datadog coverage percentage reporting;
- customer-selected external coverage reporting.

The current code conflates the first two in places. `CiUtils` sets `CodeCoverageEnabled` to true when tests skipping is enabled because ITR needs line coverage events. That should not automatically mean that the internal Datadog global coverage JSON path is allowed to overwrite a Coverlet, Vanguard, or external XML percentage.

The source-selection rule should be explicit and test-covered. A safe default is:

1. If an external tool/report is the configured user-facing coverage source, mutate that report and use its source-native corrected summary.
2. Else if internal Datadog coverage reporting is explicitly enabled, use the internal Datadog merged model.
3. Else collect and upload per-test line coverage for ITR, but do not publish `test.code_coverage.lines_pct`.

Use that priority unless a future product decision changes it explicitly in the source arbiter. Do not leave precedence as an accidental result of whether IPC arrived before `DotnetCommon.FinalizeSession()` or whether `DD_CIVISIBILITY_CODE_COVERAGE_PATH` happened to be set.

This also means `CiUtils` must not restore the current "set internal coverage path only when tests are not skipped" rule by accident. Under ITR, internal storage can be needed for backfill, but that storage must not make internal Datadog coverage the reporting source when an external report is selected.

### Line-only boundary

The backend response contains line coverage only. The implementation can correct:

- `test.code_coverage.lines_pct`;
- line hit counts in Coverlet, Cobertura, OpenCover, and line-capable Microsoft reports.

It cannot reconstruct branch, condition, method, or complexity coverage for skipped tests. External report rewriters should preserve those fields unless the format requires line-summary recalculation that is independent of branch data. If a customer coverage gate is based on branch coverage, this feature cannot make that gate equivalent to a full non-ITR run.

### `LineCallCount` mode

The internal collector supports `LineExecution` and `LineCallCount`. Backend skipped-test coverage is a line-execution bitmap, so the backfill engine should normalize both modes to covered/not-covered for percentage calculation.

For `LineCallCount`:

- local counters with any value greater than zero should be considered executed;
- backfill should turn a zero counter into a covered value, for example `1`;
- backfill must not overwrite non-zero local call counts;
- existing per-test and global coverage handlers must change their bitmap extraction from `== 1` to `> 0` before the data is used locally or uploaded to the backend;
- tests should cover local counts greater than one, because treating only `== 1` as covered would undercount repeated line execution.

## Risks and Boundaries

### External report mutation scope

The decision for this revision is to support internal and external tools. The complete implementation should cover the line-capable formats that .NET already recognizes or can instrument directly.

The implementable set is:

- Coverlet in-memory modules.
- Cobertura XML.
- OpenCover XML.
- Microsoft CodeCoverage XML only when line-level entries are available.

Exact support for Microsoft aggregate-only XML is not implementable from the aggregate file alone. The tracer would need a different source of local executable/executed line data or would need to force a line-capable output format.

### Bitmap compatibility

.NET's `FileBitmap` uses MSB-first bit placement inside each byte. The backend coverage returned for .NET should come from the same .NET CITESTCOV bitmap bytes, so it should match. Still, this deserves a focused test with a known set of lines and a base64 response.

### Path matching

Local coverage uses `CIEnvironmentValues.Instance.MakeRelativePathFromSourceRoot(moduleFile.Path, false)`. Backend paths are Git-root-relative. These are intended to match, but the RFC already calls out file normalization as a possible issue.

Tests must cover:

- `src/Foo.cs`
- `/src/Foo.cs`
- `src\Foo.cs`
- source-root-relative path generation on Windows and non-Windows
- source files outside `SourceRoot`, where `MakeRelativePathFromSourceRoot()` currently falls back to an absolute path
- collisions where two local paths normalize to the same repository-relative key

External reports add more path forms. The adapters must cover absolute paths, project-relative paths, report-directory-relative paths, source-link/deterministic build paths, and paths that are already repository-relative. A path should be eligible for backfill only after it maps to exactly one repository-relative key used by the backend coverage map.

### Denominator correctness

The implementation should not add backend-only files to the denominator. If a skipped test covers a file not loaded by the current local coverage bundle, the tracer cannot infer the executable-line count for that file from the backend executed-line bitmap alone.

The implementation should also not count backend-only bits within a matched file. Backend coverage must be masked by the local executable bitmap before it is OR-merged into local executed lines.

### Duplicate local entries per path

A normalized repository path can map to more than one local coverage entry. The same source file can appear in multiple assemblies, target frameworks, modules, or report nodes. The merge must not collapse those entries into one file-level denominator. Backend coverage should fan out to each compatible local entry and then let the active source adapter recompute that source's native summary.

### Local skippable-test filtering

The backend coverage map is aggregated by OR before the tracer sees it. That means it cannot be decomposed after the response is received. If .NET drops any skippable test locally due to custom configuration filtering, the coverage map may no longer correspond to the tests that will actually be skipped.

This is a correctness blocker, not just an optimization detail. The implementation needs an explicit guard:

- prove with backend contract/tests that `meta.coverage` is aggregated after configuration filtering; or
- when local filtering removes tests, disable coverage backfill and avoid unsafe skipping for coverage-active runs.

### Command-level test subsetting

The same aggregate-safety problem exists when the command only runs a subset of tests. Examples include `dotnet test --filter`, `vstest.console /TestCaseFilter`, a command targeting only some assemblies, or a multi-targeted project where only one target framework is executed. If the backend response is broader than the effective local selection, `meta.coverage` can include tests that were not skipped and not covered locally. Coverage-active skipping should be disabled unless the implementation can prove that the backend aggregate is scoped to the exact local execution set.

### Test-level and module scope

The backend response scope must match the local skip scope. .NET should not rely on an implicit backend `test_level`, and it should not treat suite/name as globally unique when the runner can execute multiple assemblies, modules, or target frameworks. The same scope dimensions used in the request must be present in the local skippable lookup, cache key, shared backfill file, and aggregate-safety validation.

If module or bundle identity is unavailable in a particular framework integration, the implementation should detect duplicate suite/name/parameter candidates across the response scope and fail closed for coverage-active skipping rather than applying a service-wide aggregate to one local module.

### Cache scope

The skippable-tests cache and any dedicated backfill cache must not widen the backend response. If two processes or modules share a cache entry but differ in configurations, test level, module or bundle identity, target framework, selected assemblies, or test filters, the cached `meta.coverage` can contain the wrong aggregate. Cache hits used for coverage-active skipping should validate the full scope key, not just the run id and workspace.

### Actual skip tracking

`HasSkippableTests()` means the backend returned candidates, not that the tracer actually skipped those tests. Framework-level checks can still force a candidate to run, and coverage safety can also block a skip. `_dd.ci.itr.tests_skipped`, `test.code_coverage.backfilled`, and any backfilled session percentage must be driven by actual applied ITR skips, not by the presence of candidates in the response.

### Non-ITR execution changes

The coverage contract should compare ITR against the same command with ITR disabled, not against an idealized run where every feature and every user skip is disabled. Test Management disabled tests, user-level skips, and framework-level skip reasons are not covered by ITR `meta.coverage` and must not be backfilled from it. Test Management quarantined tests and retry features can change final status or execution count, so the local coverage model should aggregate actual executions idempotently before applying skipped-test backend coverage.

A full-suite baseline that ignores Test Management disabled or quarantined behavior is outside this plan. That requirement would need coverage data for those feature decisions separately from the ITR skippable-tests response.

### Partial coverage semantics

The current Microsoft CodeCoverage XML parser computes:

```text
covered / (covered + partiallyCovered + notCovered)
```

It does not count partially covered lines as covered. Any Microsoft CodeCoverage backfill path needs to preserve or intentionally revise that semantic after checking the actual report format.

Coverlet and OpenCover can also have multiple sequence points or method line entries for the same source line. The shared model should compute the Datadog session percentage by distinct file lines where possible. Report mutation should only flip existing report entries and then let the report format recalculate according to its native semantics.

For external tools, the final Datadog percentage should match the mutated external report whenever the tracer claims to support that tool. A generic distinct-line percentage is acceptable only for internal Datadog coverage or for formats whose native line summary is exactly the same as the generic model.

### Coverage source precedence

Internal Datadog coverage exists for ITR even when another coverage tool is the customer-visible source. If both internal JSON coverage and an external report result are available, the implementation needs deterministic precedence. Without a central arbiter, the session tag can be overwritten by whichever path runs last.

Current `DotnetCommon.FinalizeSession()` gives `DD_CIVISIBILITY_CODE_COVERAGE_PATH` precedence over `DD_CIVISIBILITY_EXTERNAL_CODE_COVERAGE_PATH`. That behavior is not compatible with full support if the internal path is used only as temporary backfill storage. The plan must replace that branch with the source arbiter before enabling internal global storage under ITR.

### Report rewrite safety

External report mutation should be atomic enough that a failed rewrite does not leave a corrupt customer report. XML rewriters should preserve encoding, namespaces, and unrelated branch/method data where practical. If rewriting fails after tests were skipped, the tracer should avoid marking coverage as backfilled and should surface a clear warning.

### External threshold timing

Some external tools evaluate coverage thresholds before `DotnetCommon.FinalizeSession()` can rewrite an XML file. Those modes cannot be fully corrected by post-command XML mutation because the process exit code may already reflect an unbackfilled coverage percentage. Full support requires an in-process hook before threshold evaluation, such as the Coverlet `CoverageResult.Modules` path. Without that hook, coverage-active skipping should be disabled for threshold-enforcing modes or the support contract should be explicitly limited to correcting the report file after the command.

### IPC finalization

Coverage messages from Coverlet, Vanguard, and data collector processes are delivered through an asynchronous polling IPC server. `FinalizeSession()` should not close the session until the selected source result has been received, a completion signal has been processed, or a bounded timeout has elapsed. Otherwise the session can ignore a valid corrected coverage message after `_finished` is set.

IPC send failures are also correctness failures for selected external sources. The sender must keep messages below the channel limit, check `TrySendMessage()`, and surface a clear failure if the corrected result could not be delivered.

### Collector coexistence

Datadog's own coverage collector may run beside Coverlet or Microsoft CodeCoverage when ITR and external coverage are both active. Double instrumentation and collector ordering must be validated per supported mode. A mode where Datadog collection and the external tool cannot coexist should be marked not backfillable, causing conservative no-skip behavior.

### Static handler initialization

Because `CoverageReporter.Handler` is initialized statically, the implementation must verify the real initialization order. If the handler is created before remote settings update `TestsSkippingEnabled` or `CodeCoverageEnabled`, a simple condition change may be insufficient.

The generic `CoverageReporter<TMeta>` cache makes this stricter. Once a generic reporter has initialized, it can retain a `ModuleValue` tied to the old global container. The implementation should not depend on replacing the handler after instrumentation has already touched any generic reporter type unless that cache behavior is changed and tested.

### Response size

The RFC warns that `meta.coverage` can add 1-2 MB to the response. The .NET parser should avoid serializing the response again unnecessarily in normal paths. Current debug logging can log the full response body; that may become expensive and potentially sensitive when coverage maps are large.

### Diagnostics and telemetry

Backfill decisions need observable reasons. The implementation should record whether backfill was accepted, disabled because coverage was missing, disabled because a source was unsupported, disabled because paths could not be resolved, disabled because collector coexistence failed, or failed during report rewrite/IPC delivery. Logs should summarize these reasons and sizes without dumping full coverage maps.

### Early skip decision

External reports are often generated at the end of the run, but the decision to skip tests happens before that. The tracer therefore needs an early capability decision. If a coverage mode is configured and the tracer cannot guarantee line-capable backfill for that mode, the skip decision must be conservative and avoid skipping.

## Validation Plan

### Unit tests

Add tests for skippable response parsing:

- `meta.coverage` is decoded from base64 into bytes.
- missing `meta.coverage` is handled.
- empty `meta.coverage` is handled.
- present empty `meta.coverage` is distinguished from a missing `meta.coverage` field.
- invalid base64 fails safely.
- debug logging redacts or summarizes coverage maps instead of writing every file path and bitmap.
- leading slash and backslash normalization work.
- local custom-configuration filtering does not leave an unfiltered aggregate coverage map in use.
- command-level filters or targeted test subsets do not allow an unscoped aggregate coverage map to be used for coverage-active skipping.
- cache hits are rejected when service, environment, repository URL, commit SHA, test level, module or bundle identity, runtime/test configurations, custom configurations, target framework, selected assemblies, or explicit test filters differ.
- a response with skippable tests but missing `meta.coverage` fails closed when active coverage requires backfill.

Add tests for coverage merging:

- backend coverage ORs into local executed bitmap.
- overlapping lines are counted once.
- backend-only files are ignored for percentage.
- local files with no executed lines can become covered from backend coverage.
- backend lines outside the local executable bitmap are ignored.
- backend coverage for one normalized path updates multiple compatible local entries without collapsing their denominators.
- backend bitmap byte ordering is verified with known lines such as 1, 2, 8, and 9.
- `LineCallCount` treats local counts greater than zero as covered and backfills only zero-count lines.
- existing per-test and global coverage extraction in `LineCallCount` mode marks counter values greater than one as covered before sending coverage to the backend or calculating local coverage.
- cached percentage data is invalidated after merge.

Add tests for source arbitration:

- ITR-only coverage collection uploads per-test coverage but does not publish `test.code_coverage.lines_pct` unless a reporting source is selected.
- a selected external report result is not overwritten by Datadog internal JSON coverage.
- internal Datadog JSON files used as backfill storage do not cause `DD_CIVISIBILITY_EXTERNAL_CODE_COVERAGE_PATH` to be ignored.
- multiple IPC coverage messages for the same source are combined by covered/executable counts instead of last-write-wins.
- delayed IPC messages are drained before final session coverage tags are published.
- failed `TrySendMessage()` delivery for the selected coverage source is treated as a coverage-reporting failure, not silently ignored.
- oversized IPC diagnostics are rejected in tests so line maps cannot accidentally move through IPC.
- source priority is deterministic when Coverlet, Vanguard, external XML path, and internal coverage are all present.
- fail-closed reasons are logged and emitted through telemetry without including full backend coverage maps.

Add tests for skip safety:

- `_is_missing_line_code_coverage=true` prevents skipping when code coverage backfill is required.
- the same test remains skippable when code coverage is not required.
- a candidate blocked by coverage safety does not count as an actual ITR skip.
- an unskippable candidate that is forced to run does not count as an actual ITR skip.
- `_dd.ci.itr.tests_skipped` and `test.code_coverage.backfilled` are not set from candidates alone.
- a backend candidate that is never observed in the local execution makes aggregate backfill unsafe unless the request was proven to be exactly scoped.
- skippable-tests requests include the expected `test_level` and fail tests if the field is accidentally removed.
- duplicate suite/name/parameter candidates across modules or target frameworks are not skipped under coverage-active backfill unless the local lookup includes module or bundle identity.
- Test Management disabled tests and user-level skips do not count as actual ITR skips and do not trigger ITR backfill.
- Test Management quarantined tests that execute still contribute local coverage normally, but their masked final status does not cause ITR backfill unless the skip reason is the ITR reason.
- Early Flake Detection, automatic test retry, and attempt-to-fix attempts aggregate local covered lines idempotently and do not set backfilled coverage tags without an actual ITR skip.
- parameterized test matching preserves existing behavior.

### Integration tests

Create or extend an ITR integration test with:

- a mock skippable-tests response that includes `meta.coverage`
- one test that runs and one test skipped by ITR
- a known local source file with deterministic line coverage
- assertion that `test.code_coverage.lines_pct` equals the full-run expected percentage
- assertion that `test.code_coverage.backfilled=true`

Also add a negative test:

- same setup but no backend coverage, or missing line coverage for the skipped test
- assert that the tracer does not report a falsely backfilled percentage
- assert that the tracer does not claim tests were skipped when every candidate was forced to run
- assert that a filtered run does not mutate or publish coverage using backend lines for tests outside the filter

### Tool-specific tests

Coverlet:

- use a small Coverlet module model or sample test run
- verify that local file-line coverage is extracted before the final percentage is sent by IPC
- verify that `CoverageResult.Modules` is mutated before Coverlet reports/thresholds run
- verify corrected percentage under ITR
- verify that no backend-only line is added to the Coverlet model
- verify that Datadog's IPC percentage equals Coverlet's own post-mutation line percentage
- verify branch and method coverage fields are not claimed as corrected by line-only backfill
- verify supported data collector and MSBuild task modes coexist with Datadog's `DatadogCoverage` collector
- verify unsupported Coverlet modes, such as modes not passing through `Coverage.GetCoverageResult()` or an external XML parser, fail closed for coverage-active skipping

Cobertura:

- add a fixture with class-level and root-level aggregate attributes
- add fixtures where filenames are absolute, project-relative, report-directory-relative, and repository-relative
- verify `hits` updates on matching existing line entries
- verify ambiguous or unresolved report paths fail closed instead of matching the wrong backend file
- verify `line-rate`, `lines-covered`, and related aggregate attributes are recalculated
- verify the Datadog session percentage matches the recalculated XML summary
- verify branch-rate or other non-line fields are preserved unless the format requires a line-independent recalculation

OpenCover:

- add a fixture with file ids and sequence points
- add fixtures for absolute paths, relative paths, and deterministic/source-link style paths
- verify `vc` updates on matching existing sequence points
- verify ambiguous or unresolved file ids fail closed instead of matching the wrong backend file
- verify summary attributes are recalculated at method/class/module/root levels where present
- verify duplicate sequence point behavior follows OpenCover summary semantics
- verify branch point data is left unchanged by line-only backfill
- verify reports produced while Datadog coverage collection is also active are still parseable and corrected

Microsoft CodeCoverage:

- capture representative XML from the supported Microsoft collector modes
- implement and test structured backfill for any line-capable XML shape
- add a test proving aggregate-only XML does not allow backfill and causes conservative no-skip behavior when coverage correctness depends on it
- verify multiple XML output files are aggregated by counts before one session percentage is published
- verify Microsoft CodeCoverage/Vanguard can coexist with Datadog coverage collection, or mark the conflicting mode as not backfillable
- verify any mode that enforces thresholds before report rewrite is either hooked before threshold evaluation or disables coverage-active skipping

## Suggested Work Breakdown

1. Add `meta.coverage` parsing and a shared backend coverage map type.
2. Add response-safety checks for missing `meta.coverage`, invalid coverage, and local post-filtering of skippable tests.
3. Persist backend coverage so parent, child, data collector, and MSBuild task processes can all read it.
4. Make skippable-tests/backfill cache keys include every backend request and execution-scope dimension that can affect `meta.coverage`, or bypass cache reuse for coverage-active skipping.
5. Separate internal backfill storage from the selected reporting source so internal Datadog JSON does not accidentally override external coverage reports.
6. Add `_is_missing_line_code_coverage` to `SkippableTest`.
7. Add a coverage-capability service that decides whether the active coverage mode is line-capable, can be backfilled, and can coexist with Datadog's own coverage collector.
8. Add aggregate-safety checks for local custom-configuration filtering, command-level test filters, targeted assemblies, and target-framework subsetting.
9. Add skip safety in `Common.ShouldSkip()` using `_is_missing_line_code_coverage`, response safety, aggregate safety, collector-coexistence safety, and the coverage-capability service.
10. Track observed candidates and actual applied ITR skips separately from backend candidates and non-ITR skips, and make `_dd.ci.itr.tests_skipped` and `test.code_coverage.backfilled` use actual ITR skips.
11. Add the shared `LineCoverageModel` and merge engine, with support for multiple local entries per normalized backend path.
12. Make the merge engine fan backend coverage out to compatible local entries and mask backend executed lines with each local executable bitmap before OR.
13. Fix existing `LineCallCount` bitmap extraction in per-test and global Datadog coverage handlers so any positive count is covered.
14. Add explicit `LineExecution` and `LineCallCount` adapters, with count-mode covered/not-covered normalization.
15. Add a `GlobalCoverageInfo` adapter for Datadog internal coverage and Datadog JSON coverage files.
16. Make the skippable-tests request include explicit `test_level`, add module/bundle scope where available, and use the same scope in local lookup.
17. Adjust coverage handler selection so Datadog global coverage can be computed under ITR when backend coverage is available, and ensure the final handler/container is selected before any `CoverageReporter<TMeta>` static cache is initialized.
18. Add a coverage source arbiter that separates ITR collection from session percentage reporting and enforces source priority.
19. Extend `SessionCodeCoverageMessage` and `TestSession.OnIpcMessageReceived()` to carry source, counts, percentage, `test.code_coverage.backfilled`, and compact completion/diagnostic information.
20. Make IPC/session aggregation drain delayed messages, detect failed selected-source delivery, and combine multiple messages by source before setting the final session tag.
21. Add a Coverlet adapter that reads and mutates `CoverageResult.Modules`, then uses Coverlet's own post-mutation summary for the IPC percentage.
22. Replace aggregate-only XML parsing with parser/rewriter adapters for Cobertura and OpenCover, including source-root path resolution for external report paths.
23. Add a Microsoft CodeCoverage adapter for line-capable XML and conservative fallback for aggregate-only XML.
24. Detect external tool modes that evaluate thresholds before Datadog can mutate coverage, and mark them unsupported for coverage-active skipping unless an in-process pre-threshold hook exists.
25. Wire `DotnetCommon.FinalizeSession()`, `CoverageGetCoverageResultIntegration`, and `ManagedVanguardStopIntegration` through the shared merge engine and source arbiter.
26. Replace full skippable-response debug logging with summarized/redacted logging and add telemetry for backfill accepted/disabled/failure reasons.
27. Add parser, merge, skip-safety, aggregate-safety, cache-scope, test-level/module-scope, collector-coexistence, actual-skip tracking, non-ITR skip/retry interactions, handler-lifecycle, IPC-finalization, source-native summary, source-arbitration, path-resolution, threshold-timing, diagnostics, and report-mutation unit tests.
28. Add end-to-end ITR coverage integration tests for internal coverage, Coverlet, Cobertura/OpenCover external files, Microsoft line-capable XML, filtered/subset executions, cache-scope separation, delayed or failed IPC coverage messages, collector coexistence, external path resolution, ambiguous module/test-level scopes, Test Management disabled/quarantined interactions, EFD/ATR/ATF retry interactions, threshold-enforcing unsupported modes, and mixed-source precedence.

## Recommendation

Implement full support through the shared engine and adapters described above. This can be split across PRs for reviewability, but the design should not create an internal-only path that has to be replaced later.

The minimum complete Java-equivalent .NET implementation is:

- parse and store backend skipped-test coverage;
- prevent unsafe skipping when line coverage is missing or the active coverage source is not backfillable;
- prevent unsafe skipping when the backend aggregate is broader than the current local execution;
- make the skippable-tests request and local lookup scope explicit for test level and module or bundle identity;
- distinguish missing backend coverage from present empty backend coverage;
- scope caches and shared backfill files to the same execution dimensions as the backend aggregate;
- validate coexistence between Datadog coverage collection and each supported external coverage tool;
- resolve external report paths to backend repository-relative paths without ambiguity;
- correct external reports before threshold evaluation when the tool enforces thresholds in-process, or disable coverage-active skipping for that mode;
- fix `LineCallCount` collection so positive counts greater than one are not dropped from per-test backend coverage or local global coverage;
- track actual applied ITR skips separately from backend candidates;
- keep non-ITR skip and retry behavior in the local baseline instead of patching it with ITR `meta.coverage`;
- merge backend coverage into local executable-line models using the Java denominator rule;
- select exactly one source for session coverage reporting so ITR collection does not overwrite external tool results;
- drain selected coverage-source IPC before closing the session;
- avoid logging full backend coverage maps and emit structured fail-closed diagnostics;
- update Datadog session tags;
- mutate external report objects/files for the supported line-capable tools.

The hard technical boundaries are aggregate-only Microsoft CodeCoverage XML and any non-line coverage metric such as branch or method coverage. Exact backfill is not possible there unless another local source provides the missing line-level or branch-level data.
