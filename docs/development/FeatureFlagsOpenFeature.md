# OpenFeature provider development

## Synchronous provider evaluation

The experimental synchronous methods share `FeatureFlagsSdk.Resolve<T>` with the
asynchronous provider methods. The synchronous path also runs the provider's
post-resolution hooks in reverse order: After on success, Error on failure, and
Finally in either case. An After failure produces the caller's default and error
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

## Instrumented coverage

`Samples.OpenFeature.Evaluator.ExtraChecks` compares synchronous string and JSON
resolution against the asynchronous client after initialization.
`FeatureFlagsTests.FfeEnabled` runs that sample with native instrumentation and
mock-agent remote configuration and requires successful completion. This covers
the production evaluation connection that the fixture unit tests replace.
Run the existing integration test with a built tracer home and the sample for the
selected framework. This is local mock-agent coverage, not deployment evidence.

When adding a project to `Datadog.Trace.sln`, run
`./tracer/build.sh RegenerateSolutions` and commit the generated build solution.
CI discovers unit-test projects through that generated solution.
