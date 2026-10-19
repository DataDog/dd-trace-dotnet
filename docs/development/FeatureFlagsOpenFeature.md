# OpenFeature provider development

## Synchronous provider evaluation

The experimental synchronous methods share `FeatureFlagsSdk.Resolve<T>` with the
asynchronous provider methods. The synchronous path also runs the provider's
post-resolution hooks in reverse order: After on success, Error on an error result,
and Finally for both. A canceled token or a null flag key throws before any hook
runs. An After failure produces the caller's default and error
details; Error and Finally failures do not prevent the remaining hooks from
running. Each hook completes before resolution returns.

Both built-in hooks currently complete synchronously. The helper also waits for
incomplete hook tasks, so future provider hooks must avoid capturing a caller's
synchronization context. The synchronous path runs no Before hooks or hooks
registered on the OpenFeature API or client. Callers supply the complete context.
The normal asynchronous client continues to own its hook lifecycle, avoiding
duplicate metrics and span enrichment.

## Focused tests

Use the SDK pinned in `global.json`:

```sh
dotnet test tracer/test/Datadog.FeatureFlags.OpenFeature.Tests/Datadog.FeatureFlags.OpenFeature.Tests.csproj -c Release -p:GeneratePackageOnBuild=false
```

The tests link the same `ufc-config.json` and `evaluation-cases/*.json` snapshot
used by `FeatureFlagsEvaluatorTests`. Every case exercises both public sync and
async methods and checks values, reasons, variants when specified, and error
codes against the JSON expectations. An internal constructor connects the
provider to the production tracer evaluator while preserving provider context
and result conversion. The fixtures are linked directly, so snapshot updates
apply to both suites.

The `netcoreapp3.1` target consumes the provider's `netstandard2.0` asset, covering
the internal `ExperimentalAttribute` compatibility definition. The net8.0 and
net9.0 targets cover the BCL attribute. Compiler tests verify the opt-in diagnostic,
while resolution and hook tests cover defaults, cancellation, hook ordering and
failure behavior, completion before return, and exactly-once evaluation metrics.

## Evaluation telemetry opt-out tests

`EvaluationTelemetryOptOutTests` uses the real OpenFeature client, Datadog provider,
production evaluator, metrics hook, span hook, span accumulator, and exposure cache.
The tests count metrics, span-admission calls, raw exposures, and deduplicated exposures.
Internal callbacks replace only the evaluator and span-accumulator connections made
by the native profiler. These tests do not prove native CallTarget loading.

When opt-out is enabled, `GetProviderHooks` creates wrappers for each evaluation.
Each wrapper captures its provider Before context. OpenFeature versions before 2.5.0 supply the
original context to Finally, which can differ from the context used for evaluation.
Fresh wrappers isolate nested and concurrent calls. The synchronous methods also
request fresh wrappers, but use their supplied context because they skip Before.
Disabled providers continue to return their cached hooks.

The nullable provider option distinguishes an omitted value from explicit `false`.
An omitted value reads `DD_FEATURE_FLAGS_EVALUATION_TELEMETRY_PER_SUBJECT_OPT_OUT_ENABLED` once at construction.
The native integration returns the tracer's resolved configuration, including file and stable configuration sources.
Without instrumentation, the provider uses its environment fallback and the tracer's linked boolean parser.
Tests cover configuration precedence, parser aliases, invalid values, snapshots, and configuration telemetry.

Before OpenFeature 2.5.0, the provider cannot see a replacement context when a later Before hook throws.
Setting opt-out in the supplied context covers this aborted evaluation path on those versions.
The suite checks both this limitation and the corrected behavior in newer OpenFeature versions.

`EvaluationTelemetryOptOutAllTargetsTests` covers asynchronous and synchronous span suppression
on every test target, including the provider's `netstandard2.0` build.

## Instrumented coverage

`Evaluator.ExtraChecks` in `Samples.OpenFeature` compares synchronous string resolution
against the asynchronous client, and checks the synchronous JSON value, after initialization.
`FeatureFlagsTests.FfeEnabled` runs that sample with native instrumentation and
mock-agent remote configuration and requires successful completion. This covers
the production evaluation connection that the fixture unit tests replace.
Run the existing integration test with a built tracer home and the sample for the
selected framework. This is local mock-agent coverage, not deployment evidence.

`SpanEnrichmentIntegrationTests` runs the same sample with a root and child span.
The sample resolves `simple-string` synchronously inside the root span, so the
root-span serial-id assertion and span snapshot cover synchronous span enrichment.

When adding a project to `Datadog.Trace.sln`, run
`./tracer/build.sh RegenerateSolutions` and commit the generated build solution.
CI discovers unit-test projects through that generated solution.
