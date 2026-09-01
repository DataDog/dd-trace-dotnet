# First-class support for `xunit.v3` 4.x

## Status

- Planning base: `master` at `1fc3a46b9c3c` on 2026-09-01.
- Target package: `xunit.v3` 4.0.0 and subsequent compatible 4.x releases.
- Implementation branch: `feat/xunit-v4-support` in the isolated worktree `/private/tmp/dd-trace-dotnet-xunit-v4`.
- Current state: runtime support, package generation, baseline events, retries, deterministic parallel isolation, Impacted Tests, Exception Replay, and both runner surfaces are implemented and pass the focused local validation listed below.
- Review checkpoint: the final reviews fixed `NotRun` precedence, made the global ATR budget count only retries that are actually scheduled, and aligned `RunSummary` normalization with the execution selected by `RetryMessageBus`. They also added fail-closed V3/V4 compatibility checks in both directions and coverage for ordered per-case output, all-failed fallback, fail/skip ordering, threshold abort, and disposal. The real V4 ITR skip path is covered. The branch-based Impacted Tests fixture requires a clean worktree, no longer stashes developer changes, and restores both the index and working tree on every exit path.
- Exception Replay resolution: xUnit 4 framework assemblies were missing from Dynamic Instrumentation's third-party module catalog. Adding the `xunit.v3.*` assemblies prevents assertion-framework frames from being treated as customer code; the real 4.0.0 Exception Replay retry test now passes without an additional lifecycle hook or request-context change.
- Release state: not releasable until the repository CI matrix passes. The SDK 10 solution normalization was removed, and only the new parallel sample project was added to `Datadog.Trace.sln` and `Datadog.Trace.Samples.g.sln`; `Datadog.Trace.Build.g.sln` remains unchanged. Windows/Linux coverage and the comprehensive package-feature matrix remain CI-owned; no commit, push, or remote CI run is part of this worktree implementation.

## Objective

Add first-class Test Optimization support for `xunit.v3` 4.x without regressing the existing 1.x–3.x integration.

The final reviews found three intentional shared-policy corrections. xUnit represents an unexecuted test as `NotRun = 1` and `Failed = 0`, so the shared policy checks `NotRun` before success and does not count it as a pass. The global ATR budget consumes one slot only when a failed execution schedules a retry, never underflows its zero boundary, and a configured budget of `N` now permits exactly `N` retry attempts. Finally, the framework summary preserves the first completed result unless a later execution passes, matching the message-bus selection policy. These corrections also apply to 1.x–3.x instead of preserving known incorrect behavior solely for byte-for-byte equivalence.

First-class support means:

- The package and assembly support matrix reports 4.x as supported and tested.
- The tracer creates correct session, module, suite, and test events.
- xUnit-specific behavior for Intelligent Test Runner (ITR), Early Flake Detection (EFD), Automatic Test Retries (ATR), Test Management, Impacted Tests, and Exception Replay has explicit 4.x coverage. Framework-independent policy, including coverage collection and backfill, remains covered at its shared seam rather than duplicated for each package version.
- Retries remain correct with xUnit 4 parallel execution, including multiple theory rows from the same method.
- The supported in-process and VSTest execution surfaces are covered.
- Representative 1.x, 2.x, and 3.x tests remain green.

## Implementation checkpoint

Observed locally on macOS arm64 with .NET SDK 10.0.101 and the `net8.0` integration-test target:

| Gate | Result | Evidence |
|---|---|---|
| Managed and native instrumentation | Pass | `Datadog.Trace` built for `net461`, `netstandard2.0`, `netcoreapp3.1`, and `net6.0`; the repository CallTarget generator completed; macOS native compilation and publish completed with the final seven hooks. |
| V4 summary/message-bus and third-party catalog unit tests | Pass | 29/29 rows in `XUnitV4IntegrationTests` on both `net6.0` and `net8.0`, including atomic ATR budget accounting without underflow, ordered fail/skip selection, and non-creating metadata lookup; 2/2 shared summary tests and focused `xunit.v3.*` catalog assertions also pass |
| In-process/MTP baseline | Pass | `SubmitTraces(4.0.0, ...)`: EVP v2 gzip and EVP v4, 16 tests, two suites, one module |
| VSTest adapter 4.x | Pass | Dedicated 4.0.0 smoke: 16 tests, two suites, one module |
| ITR skip and forced run | Pass | Dedicated 4.0.0 self-executable runs prove a backend-selected test is skipped with the expected correlation/tags/counts, and assert `test.unskippable:true`, `test.forced_run:true`, and the passing framework result for the forced-run case |
| Test Management disabled | Pass | Dedicated 4.0.0 self-executable run proves a backend-disabled test is reported as skipped with its reason and `test.test_management.is_test_disabled:true` |
| ATR | Pass | Real 4.0.0 retries with correct execution counts and final results |
| Full-parallel theory-row isolation | Pass | 20/20 `all` repetitions across conservative/aggressive scheduling plus 2/2 focused `none` and `collections` runs. Every run uses deterministic rendezvous, asserts 32 events and five suites, covers four theory rows with isolated retries, and validates method/class lifecycle plus the active cancellation context within both the original execution and its retry, without wall-clock assertions or test-level timeouts. |
| Impacted Tests | Pass (partial local matrix) | Base SHA, disabled-by-env, and enabled-by-settings scenarios pass 3/3 without polling or elapsed-time thresholds; branch-mutating scenario intentionally left to clean CI |
| Exception Replay | Pass | `FlakyRetriesWithExceptionReplay(4.0.0)` passes with the expected exception hash/id and captured-debug-info assertions. |
| TFM and runner smoke matrix | Pass | On both `net9.0` and `net10.0`, the 4.0.0 self-executable/MTP retry path and the dedicated VSTest-adapter path pass (2/2 per TFM). |
| Historical retry compatibility | Pass | 8/8 focused rows: 1.0.1, 1.1.0, 2.0.3, 3.0.1, 3.1.0, 3.2.2, and 4.0.0 ATR, plus 4.0.0 Exception Replay. |
| Cross-platform and complete package matrix | Pending CI | Windows/Linux and the comprehensive package-feature matrix have not been observed on the final diff. |

A temporary V4 `InvokeTestMethod` hook and request-context handoff were evaluated and then removed. Probe diagnostics showed that callbacks already shared the test request context; the actual incompatibility was that `xunit.v3.assert` was not classified as third-party code. The final implementation adds the xUnit 4 assembly family to [`ThirdPartyModules.Names.cs`](../../tracer/src/Datadog.Trace/Debugger/ThirdParty/ThirdPartyModules.Names.cs) and retains exactly the seven planned V4 hooks.

## Resolved Exception Replay investigation

The original failure looked like an `ExecutionContext` propagation issue because the final diagnostic was `EmptyCallStackTreeWhileCollecting`. Direct callback logging disproved that hypothesis:

- Exception Replay callbacks ran on the expected thread and request tree.
- The 4.0.0 stack was classified as three customer frames: the sample test plus two `xunit.v3.assert` overloads.
- Those framework callbacks did not form the customer stack expected by Exception Replay, so the collected tree was rejected.
- The same test passed on xUnit 2.x, whose `xunit.assert` assembly was already in the third-party module catalog.

The final fix adds these framework modules to the existing third-party catalog:

- `xunit.v3.assert`
- `xunit.v3.common`
- `xunit.v3.core`
- `xunit.v3.msbuildtasks`
- `xunit.v3.mtp-v2`
- `xunit.v3.runner.common`
- `xunit.v3.runner.inproc.console`
- `xunit.v3.runner.utility.netcore`
- `xunit.v3.runner.utility.netfx`

This keeps framework mechanics outside the captured customer stack and avoids new state, allocations, public APIs, or hot-path branches. Focused catalog unit tests and the real 4.0.0 Exception Replay integration test prove the change.

## Scope

### Included

- A new `Testing/XUnit/V4` implementation boundary for package ABI 4.x.
- Version-specific CallTarget definitions, duck types, and `RunSummary` handling.
- The minimum shared retry refactor needed to avoid duplicating V3 behavior.
- Concurrency hardening required by xUnit 4 full parallel execution.
- Package generation, support-matrix generation, samples, integration tests, unit tests, and snapshots.
- Self-executable xUnit runner coverage and VSTest adapter coverage.
- Microsoft Testing Platform v2 coverage when it is the execution path selected by the xUnit 4 self-executable runner.

### Explicit non-goals

- Native AOT support. xUnit 4 provides separate AOT-oriented packages, while Datadog auto-instrumentation depends on a runtime that can be profiled and rewritten. Treat AOT as a separate architectural spike and do not advertise it as supported by this work.
- Support for pre-release 5.x packages.
- Refactoring the older xUnit v2 integration.
- Unrelated cleanup in CI Visibility or the package-version generator.
- Changing public Datadog APIs or telemetry integration names.

## Evidence from the current implementation

The existing implementation has six relevant xUnit v3 hooks under [`Testing/XUnit/V3`](../../tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/Testing/XUnit/V3). The implemented V4 boundary uses seven hooks because static skips and executed tests require separate seams:

| Responsibility | Current target | xUnit 4 assessment | Planned action |
|---|---|---|---|
| Assembly lifecycle | `TestAssemblyRunner<TContext,...>.Run(TContext)` | Signature appears compatible | Add a V4-scoped attribute/class and reuse shared lifecycle logic |
| Class lifecycle | `TestClassRunner<TContext,...>.Run(TContext)` | Signature appears compatible | Add a V4-scoped attribute/class and reuse shared lifecycle logic |
| Test output | `TestOutputHelper.QueueTestOutput(string)` | Signature appears compatible | Add a V4-scoped attribute/class and reuse the existing shared handler |
| Individual test | `TestRunner<TContext,TTest>.RunTest(TContext)` plus `TestRunnerBase<TContext,TTest>.Run(TContext)` | `RunTest` preserves exceptions for executed tests but is bypassed for static skips | Keep executed tests on `RunTest`; add a narrow enclosing hook which emits only tests that the returned summary confirms were skipped |
| Message bus replacement | `XunitTestMethodRunnerContext` constructor with 7 parameters | The concrete xUnit 4 constructor has 10 parameters, but the compatible base `CoreTestMethodRunnerContext<TTestCase,TTest>` constructor has 8 | Instrument the 8-parameter base constructor so CallTarget can replace the `ref` message-bus argument; the slow handler used above eight arguments cannot safely perform that replacement |
| Per-case retries | `XunitTestMethodRunnerBase<TContext,TTestCase,TTest>.RunTestCase(TContext,TTestCase)` | Target removed in xUnit 4 | Instrument `XunitTestMethodRunnerBaseContext<TTestCase,TTest>.RunTestCase(TTestCase)` |

The implementation must not rely on the first four signatures being compatible merely because their names match. Phase 0 must verify their exact assembly, type, arity, parameters, return type, and actual invocation using the 4.0.0 binary.

Current gaps that materially affect the design:

- [`XUnitTestMethodRunnerBaseRunTestCaseV3Integration.cs`](../../tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/Testing/XUnit/V3/XUnitTestMethodRunnerBaseRunTestCaseV3Integration.cs) owns most retry behavior and recursively invokes the V3 runner. Copying it into V4 would duplicate retry policy and future fixes.
- [`RunSummaryUnsafeStruct.cs`](../../tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/Testing/XUnit/V3/RunSummaryUnsafeStruct.cs) mirrors the V1–V3 layout: four `int` counters followed by a `decimal` time value. The 4.0.0 binary uses a different layout and time representation, so the V3 converter must never be used for V4.
- [`RetryMessageBus.cs`](../../tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/Testing/XUnit/RetryMessageBus.cs) uses a normal `Dictionary` and mutable lists without synchronization.
- V3 messages expose both `TestCaseUniqueID` and `TestMethodUniqueID`, but the current V3 path correlates several operations by method ID. Theory rows share a method ID, so this is unsafe under case-level parallelism.
- [`XUnitRetriesTestsV3.cs`](../../tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/CI/XUnitRetriesTestsV3.cs) intentionally does not run Exception Replay.
- [`XUnitImpactedTests.cs`](../../tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/CI/XUnitImpactedTests.cs) only covers the older `Samples.XUnitTests` project. Its base helper also hard-codes that sample path and line numbers.
- The package matrix currently stops before 4.0.0 in [`PackageVersionsGeneratorDefinitions.json`](../../tracer/build/PackageVersionsGeneratorDefinitions.json).
- The existing xUnit v3 samples target `net8.0`, `net9.0`, and `net10.0`, use `xunit.v3` through `$(ApiVersion)`, and pin `xunit.runner.visualstudio` 3.0.1.

## Design decisions

### 1. `V4` is an ABI boundary, not a new product integration

Create:

```text
tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/Testing/XUnit/
├── V3/
├── V4/
│   ├── V4 CallTarget integrations
│   ├── V4 duck types
│   └── V4 RunSummary adapter
├── RetryMessageBus.cs
├── XUnitIntegration.cs
└── shared retry policy/adapters
```

Keep `XUnitIntegration.IntegrationName` and customer-facing telemetry unchanged. `xunit.v3` is still the package family; `V4` describes its incompatible package ABI.

### 2. Widen the existing package-test entries

Update the existing `XUnitV3` and `XUnitRetriesV3` generator entries to:

- `MaxVersionExclusive: 5.0.0`
- `SpecificVersions: ["2.*.*", "3.*.*", "4.*.*"]`

Do not create duplicate `XUnitV4` package-test entries unless the build pipeline proves it needs a distinct scheduling bucket. Widening the current entries automatically runs the existing full feature matrix against 4.x and avoids duplicating [`XUnitEvpTestsV3.cs`](../../tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/CI/XUnitEvpTestsV3.cs), which is already more than 700 lines.

Keep the current sample default `ApiVersion` during implementation. Generated package-version tests pass the version explicitly; changing the default would create unrelated local-test churn.

### 3. Share policy, isolate framework mechanics

Extract only version-independent retry policy from the V3 integration. The shared component should own:

- Feature selection and precedence: EFD, ATR, and Test Management.
- Retry-count initialization and ATR budget decisions.
- Stop conditions for pass, `NotRun`, exhausted budget, and threshold abort.
- Selection of the framework-visible final status.
- Rules for quarantined, disabled, and attempt-to-fix results.
- Exception Replay wait decision.

V3 and V4 adapters remain responsible for:

- Reading their framework context and test case.
- Building `TestRunnerStruct`.
- Applying a skip reason.
- Invoking one additional execution.
- Reading, aggregating, and rewriting their version-specific `RunSummary`.
- Flushing messages for the correct test case.

The shared API must be internal and small. Prefer a coordinator operating on normalized values over a hierarchy of framework-specific base classes. Do not introduce a public abstraction.

### 4. Correlate retries by test case

Use `TestCaseUniqueID` as the primary key for metadata and buffered messages. Use a method identifier only for messages that genuinely have no case identifier, and do not put those messages into a case buffer unless their ownership is unambiguous.

This is required to isolate:

- Multiple inline-data rows from the same theory.
- Multiple discovered cases from custom data sources.
- Concurrent cases belonging to the same method.

### 5. Preserve xUnit 4 execution state

The V4 retry adapter must invoke `RunTestCase(testCase)` on the existing `XunitTestMethodRunnerBaseContext` instance. It must not construct a replacement context or call a lower-level core runner directly. This preserves:

- `CancellationTokenSource`
- `ParallelMode`
- `ExecutionScheduler`
- Constructor arguments
- Class and method fixture mappings
- The active exception aggregator and message bus

### 6. Use a V4-specific `RunSummary` adapter

Create an exact mirror of the observed 4.0.0 layout and a V4-only converter. Its one-time compatibility gate must verify:

- The target is a value type with sequential layout.
- Total size matches.
- All instance fields, including non-public fields, have the expected names or unambiguous roles, types, and offsets.
- The four counters and underlying time storage round-trip correctly.

If any check fails, fail closed: do not modify the result, flush buffered messages safely, and emit a diagnostic log. Do not silently reinterpret a V4 summary as the V3 struct.

### 7. Make the message bus parallel-safe without a global serialization point

Replace the mutable global dictionary with a concurrent lookup and protect mutable state per test case. Each case owns:

- Retry metadata
- One message buffer per execution
- The set of lifecycle message types already forwarded
- Flush/dispose state
- A case-local synchronization object

Requirements:

- Preserve message order within an execution.
- Select the first passing execution, otherwise the first completed execution, matching current behavior.
- Flush at most once.
- Make disposal idempotent.
- Forward unknown and non-case messages immediately.
- Do not hold a case lock while invoking the inner message bus.
- Do not introduce one lock covering all tests.

## Implementation phases

### Phase 0 — Lock the xUnit 4 contract

Tasks:

1. Restore `xunit.v3` 4.0.0 and the matching supported runner packages into a disposable build location.
2. Record the exact signatures and declaring assemblies for all six hooks.
3. Confirm the real call path for:
   - Normal facts and theories
   - Static and dynamic skips
   - The 10-parameter concrete method-runner context constructor and the 8-parameter base constructor used for message-bus replacement
   - `XunitTestMethodRunnerBaseContext<TTestCase,TTest>.RunTestCase`
4. Confirm the `RunSummary` field layout and time units from the binary.
5. Confirm which executable path uses Microsoft Testing Platform v2 and which path uses VSTest.
6. Add focused instrumentation-shape or duck-type tests that fail if those contracts drift.

Exit gate:

- Every proposed hook is proven to execute in a 4.0.0 sample.
- The retry seam preserves the existing context and returns `ValueTask<RunSummary>`.
- Any mismatch updates this document before implementation continues.

Estimated effort: 0.5–1 day.

### Phase 1 — Characterize and isolate existing retry behavior

Tasks:

1. Add V3 characterization tests before moving logic:
   - Passing first execution
   - Failing all executions
   - Passing after a retry
   - `NotRun`
   - EFD count selection and threshold abort
   - ATR per-test and global budget exhaustion
   - Quarantined/disabled result hiding
   - Attempt-to-fix final status
   - Exception Replay wait decision
2. Add direct tests for V3 `RunSummary` aggregation and rewriting, including non-zero time.
3. Extract the shared retry coordinator.
4. Prove unchanged results for representative 1.x, 2.x, and 3.2.2 packages before adding V4 hooks.

Exit gate:

- Existing V3 snapshots are unchanged.
- The shared coordinator contains policy but no V3/V4 runtime types.
- No package support range has been widened yet.

Estimated effort: 1.5–2 days.

### Phase 2 — Add the `V4` instrumentation boundary

Expected production files:

- `V4/XUnitTestAssemblyRunnerRunV4Integration.cs`
- `V4/XUnitTestClassRunnerRunV4Integration.cs`
- `V4/XUnitTestOutputHelperQueueTestOutputV4Integration.cs`
- `V4/XUnitTestRunnerV4Integration.cs`
- `V4/XUnitTestRunnerBaseRunV4Integration.cs`
- `V4/XunitTestMethodRunnerContextCtorV4Integration.cs`
- `V4/XUnitTestMethodRunnerBaseContextRunTestCaseV4Integration.cs`
- The minimum V4 context/test/test-case duck types required by those hooks
- `V4/RunSummaryUnsafeStructV4.cs` and its converter/adapter

All V4 `[InstrumentMethod]` attributes use:

- `AssemblyName = "xunit.v3.core"`
- `MinimumVersion = "4.0.0"`
- `MaximumVersion = "4.*.*"`
- The existing xUnit integration name

Tasks:

1. Add the four lifecycle/output/test hooks with V4-specific attributes.
2. Instrument the 8-parameter `CoreTestMethodRunnerContext<TTestCase,TTest>` base constructor and replace only the message bus argument. Do not target the 10-parameter concrete constructor: it selects the CallTarget slow handler, which cannot replace its `ref` message-bus argument.
3. Implement the context-based retry hook.
4. Extract method information and row arguments through the V4 test object rather than removed context properties.
5. Integrate the V4 `RunSummary` adapter.
6. Add negative compatibility tests proving the V3 and V4 summary converters reject each other's layouts.

Exit gate:

- Baseline 4.0.0 execution produces the expected 16 test events, two suites, and one module from the existing sample.
- Output, traits, parameters, source, and error information match existing normalized snapshots.
- Version 3.2.2 still binds only to V3 hooks; version 4.0.0 binds only to V4 hooks.

Estimated effort: 1.5–2 days.

### Phase 3 — Parallel-safe retries and message handling

Tasks:

1. Change retry metadata correlation from method ID to case ID where the framework provides it.
2. Make `RetryMessageBus` case state concurrency-safe.
3. Add a dedicated V4 sample suite with only V4-specific stress cases, rather than changing the existing 16-case compatibility sample:
   - Two concurrent facts in one class
   - Several classes in one collection and in different collections
   - Multiple rows from the same theory, with distinct pass/fail/retry outcomes
   - Output written concurrently by each case
   - Class and method fixtures with observable construction/disposal counts
   - Dynamic skip and deterministic cancellation-context availability during the original execution and its retry
   - No wall-clock timeout scenario: timeout policy is xUnit-owned, while the instrumentation contract is proven without elapsed-time assertions
4. Exercise `parallelMode` values supported by 4.0.0, including full case-level parallelism.
5. Exercise supported scheduler choices discovered in Phase 0.
6. Run the focused full-parallel/retry scenario 20 times. Bound the containing test job at the infrastructure level; do not add sleeps, deadline races, or elapsed-time assertions to the sample. Do not repeat the full integration suite.

Assertions:

- No theory row receives another row's metadata, output, retry count, or final status.
- Each test event has the correct parameters and unique ID.
- Module and suite events close exactly once.
- Fixture construction/disposal counts match xUnit expectations after retries.
- There are no duplicate or missing framework messages.
- `Test.Current`, suite context, and module context do not leak between cases.

Exit gate:

- Twenty focused repetitions pass on the primary development platform.
- One focused pass succeeds on each platform used by the existing test-framework CI matrix.

Estimated effort: 1.5–2.5 days.

### Phase 4 — Complete feature parity

Widen the generated package matrix only after Phases 0–3 pass. Reuse the existing xUnit v3 feature tests by adding 4.x to their generated data.

Required feature coverage:

| Area | Required 4.x scenarios |
|---|---|
| Baseline events | Pass, fail, static skip, dynamic skip, theory rows, traits, output, source, code owners, parameters, errors |
| Hierarchy | One session/module, correct suites, correct parent IDs, exactly-once close |
| Transport | EVP proxy v2 with gzip and EVP proxy v4 |
| Telemetry | V4 baseline capability tags; integration enable/disable behavior at the shared instrumentation seam |
| Coverage | Coverage IPC, tags, and skipped-test backfill at the shared framework-independent seam; the V4 baseline must not assert an IPC payload while settings return `code_coverage:false` |
| ITR | Skippable, unskippable, forced run, suite/module skip counts |
| EFD | V4 all-new tests, bypass list, and EFD plus ATR precedence; slow-test and faulty-session policy with deterministic synthetic-duration/shared-policy tests, never wall-clock thresholds |
| ATR | Always pass, always fail, eventually pass, per-test retry limit, global budget, final status |
| Test Management | Quarantined, disabled, attempt-to-fix, combined flags, framework-visible result rewriting |
| Impacted Tests | Enabled by settings/env, disabled, base SHA from PR, branch-based detection, exact `test.is_modified` tags |
| Exception Replay | Failed retries contain expected DI/exception metadata; passing executions do not |

Specific test changes:

1. Keep [`XUnitEvpTestsV3.cs`](../../tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/CI/XUnitEvpTestsV3.cs) as the shared `xunit.v3` package-family matrix and feed it generated 4.x rows.
2. Extend [`XUnitRetriesTestsV3.cs`](../../tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/CI/XUnitRetriesTestsV3.cs) with a real V4 Exception Replay theory. Do not turn the existing V1–V3 limitation into a false passing test.
3. Parameterize [`TestingFrameworkImpactedTests.cs`](../../tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/CI/TestingFrameworkImpactedTests.cs) with the sample application name, source path, execution mode, and stable modification markers. Replace hard-coded line-number insertion only as far as needed to run the xUnit v3-package sample through its required `dotnet exec` path.
4. Add an impacted-tests fixture for the `Samples.XUnitTestsV3` sample and restrict its package data to 4.x initially. Expanding older coverage is optional and outside this task.
5. Add V4-only parallel/scheduler tests in a dedicated integration-test class so the standard snapshot count remains stable.

Exit gate:

- Every xUnit-specific row in the feature table has an automated 4.x assertion; framework-independent policy rows have automated coverage at their shared seam.
- Exception Replay is either green or blocks first-class support; it must not be silently skipped.
- Impacted Tests no longer rely on the old xUnit-only sample.

Estimated effort: 2–3 days.

### Phase 5 — Runner, framework, and platform matrix

Avoid a full Cartesian product. Use the following bounded matrix:

| Axis | Full feature run | Focused smoke |
|---|---|---|
| Package | 4.0.0 and generated latest 4.x | Earliest/latest representative 1.x, 2.x, 3.x |
| TFM | `net8.0` | `net9.0`, `net10.0` |
| Runner | Current self-executable/`dotnet exec` path | VSTest with the compatible 4.x adapter |
| MTP | The MTP v2 path confirmed in Phase 0 | Alternate supported runner path |
| Parallel mode | Full V4-specific matrix on `net8.0` | Full-parallel smoke on other TFMs/platforms |
| OS | Full run on the primary CI OS | Existing Windows/Linux test-framework jobs; macOS where already scheduled |

Update the sample project so the Visual Studio adapter version is selected by `ApiVersion`:

- Preserve the current adapter for package versions below 4.0.0.
- Use the compatible 4.x adapter for package versions 4.0.0 and above.
- Do not assume a runner-package version until Phase 0 confirms the supported pairing.

Exit gate:

- Both runner surfaces discover the same expected cases.
- Baseline event semantics are equivalent across runner surfaces.
- The bounded matrix adds no unsupported TFM or OS claim.

Estimated effort: 0.5–1 day, overlapping with Phase 4.

### Phase 6 — Generate metadata and complete validation

Tasks:

1. Change only the source definitions in `PackageVersionsGeneratorDefinitions.json`.
2. Run the repository generator, scoped to `xunit.v3` when supported:

   ```shell
   ./tracer/build.sh GeneratePackageVersions --include-packages xunit.v3
   ```

3. Inspect generated changes rather than editing them manually:
   - `PackageVersionsLatestMajors.g.props`
   - `PackageVersionsLatestMinors.g.props`
   - `PackageVersionsLatestSpecific.g.props`
   - Generated package-version test data
   - `supported_versions.json`
   - Generated instrumentation definitions affected by V4 attributes
4. Verify the support matrix reports:
   - Assembly `xunit.v3.core` through 4.x
   - NuGet package `xunit.v3` with 4.0.0 supported and tested
5. Review every new or changed snapshot. Do not approve snapshots as a bulk update.
6. Run `git diff --check` and inspect the complete task diff.

Recommended local proof sequence:

```shell
dotnet build tracer/test/test-applications/integrations/Samples.XUnitTestsV3/Samples.XUnitTestsV3.csproj -p:ApiVersion=4.0.0 -f net8.0
./tracer/build.sh BuildAndRunManagedUnitTests --filter "FullyQualifiedName~XUnit"
./tracer/build.sh BuildAndRunIntegrationTests --framework net8.0 --filter "FullyQualifiedName~XUnitEvpTestsV3|FullyQualifiedName~XUnitRetriesTestsV3|FullyQualifiedName~XUnitV4"
./tracer/build.sh BuildAndRunIntegrationTests --framework net8.0 --filter "FullyQualifiedName~XUnit" --test-all-package-versions true
git diff --check
```

Confirm the exact Nuke filter syntax with `./tracer/build.sh --help` before execution; the intended test classes and package scope must remain unchanged if the syntax differs.

CI proof:

- Run the repository's comprehensive test-framework pipeline with `run_all_test_frameworks=true` and package-version coverage enabled.
- Limit the manual run to the required build, package, unit-test, and integration-test stages.
- Do not claim Windows, Linux, or macOS support unless the corresponding job result is observed.

Exit gate:

- Generated output is deterministic and contains only expected xUnit changes.
- Focused local tests pass after the final generated state.
- Comprehensive CI results are recorded before the support change is considered releasable.

Estimated effort: 0.5–1 day plus CI time.

## Expected file scope

The implementation should normally remain within:

- `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/Testing/XUnit/V4/`
- The shared xUnit retry/message-bus files under `Testing/XUnit/`
- `tracer/src/Datadog.Trace/Debugger/ThirdParty/ThirdPartyModules.Names.cs` for the xUnit 4 framework assembly catalog
- V3 retry integration only where needed to delegate to shared policy or use case IDs
- `tracer/test/Datadog.Trace.Tests/Ci/` for focused unit tests
- `tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/CI/` for xUnit feature and V4-specific tests
- `tracer/test/test-applications/integrations/Samples.XUnitTestsV3/`
- `tracer/test/test-applications/integrations/Samples.XUnitTestsRetriesV3/`
- One small V4-specific sample project for concurrency/scheduler behavior
- `tracer/test/snapshots/`
- `tracer/build/PackageVersionsGeneratorDefinitions.json` and its generated outputs
- Solution/project registration required by the new sample

Stop and reassess before changing:

- Public Datadog APIs
- Generic CI Visibility behavior used by NUnit or MSTest
- Other `Debugger/ExceptionAutoInstrumentation` internals
- Native profiler code
- Packaging or installer projects
- External documentation repositories

## Delivery sequence and rollback

Prefer three independently reviewable changes, as commits in one delivery or as stacked pull requests. Changes 2 and 3 must ship together; merging V4 hooks without the feature gates would expose partial runtime support.

1. **V3 safety and shared retry policy**
   - Characterization tests, case-ID correlation, concurrency-safe message bus, and policy extraction.
   - No support-range change.
   - Rollback: revert the refactor as one unit; supported versions remain unchanged.
2. **V4 hooks and focused V4 tests**
   - New folder, duck types, summary adapter, parallel sample, and baseline tests.
   - V4 attributes may exist, but do not publish the package support range until feature gates pass.
   - Rollback: remove the V4 boundary without touching V3 behavior.
3. **Feature matrix and support declaration**
   - Package range, runner pairing, generated files, snapshots, Impacted Tests, Exception Replay, and CI proof.
   - Rollback: restore the generator cap and V4 support declaration while retaining harmless test/refactor improvements.

Do not combine a failing Exception Replay or parallelism workaround with the support declaration. Those are release blockers, not follow-up polish.

## Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Wrong V4 retry seam | Retries bypass fixtures, scheduler, or cancellation | Prove the call path in Phase 0 and invoke the existing context |
| Incorrect `RunSummary` layout | Corrupt return values or incorrect process exit code | Exact V4 mirror, full field/offset checks, fail closed, round-trip tests |
| Theory-row ID collisions | Crossed retries, messages, or final statuses | Case-ID correlation and parallel same-method theory tests |
| Message ordering races | Missing/duplicate framework events | Per-case synchronization, flush-once state, no inner-bus call under lock |
| Shared refactor regresses V3 | Existing supported packages change behavior | Characterization tests and representative package gate before V4 |
| Runner adapter incompatibility | Discovery or execution failure | Conditional runner version and separate self-runner/VSTest smoke tests |
| xUnit framework frames enter Exception Replay | Captured trees are invalid or empty | Classify the complete `xunit.v3.*` assembly family as third-party and prove it with the real retry test |
| CI matrix explosion | Excessive runtime and flakiness | Full matrix on `net8.0`; bounded smoke across other axes |

## Definition of done

The work is complete only when all of the following are true:

- All seven V4 hooks are verified against the 4.0.0 runtime path.
- V3 and V4 instrumentation version ranges do not overlap.
- `RunSummary` conversion and aggregation are proven with synthetic non-zero durations and every final status.
- All xUnit-specific feature-table rows have automated 4.x coverage, and shared policy rows have automated coverage at their framework-independent seam.
- Full parallel mode passes the bounded repeated test without cross-case contamination.
- Exception Replay has a real passing V4 test.
- Impacted Tests has a real `xunit.v3` 4.x test.
- Self-executable/MTP v2 and VSTest paths have observed proof.
- `net8.0`, `net9.0`, and `net10.0` coverage follows the bounded matrix.
- Representative 1.x, 2.x, and 3.2.2 tests remain green.
- Generated files come from `GeneratePackageVersions` and were reviewed.
- `supported_versions.json` reports 4.0.0 as supported and tested.
- The final diff contains no unrelated changes, temporary files, or manually edited generated code.

## References

- [xUnit.net v3 Core Framework 4.0.0 release notes](https://xunit.net/releases/v3/4.0.0)
- [xUnit 4.0 `XunitTestMethodRunnerContext` API](https://api.xunit.net/v3/4.0.0/Xunit.v3.XunitTestMethodRunnerContext.html)
- [xUnit 4.0 `XunitTestMethodRunnerBaseContext<TTestCase,TTest>` API](https://api.xunit.net/v3/4.0.0/Xunit.v3.XunitTestMethodRunnerBaseContext-2.html)
- [xUnit v3 test configuration](https://xunit.net/docs/config-testconfig-json)
- [Automatic instrumentation development guide](AutomaticInstrumentation.md)
- [Duck typing development guide](DuckTyping.md)
