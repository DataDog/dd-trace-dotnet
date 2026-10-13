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

EVP captures the targeting key and consented, bounded attributes in provider
resolution, before customer After hooks run. OpenFeature 2.3.0 passes the original
context to Finally even when Before hooks changed the evaluated context. A
provider-hook-local weak association keyed by the result metadata carries the
capture to Finally, which removes it even when delivery is rejected. If OpenFeature
replaces the result after a hook failure, the weak association can be collected.
No full evaluation context is retained or added to public flag metadata.

## Agent-only evaluation events

Provider activation subscribes to the tracer's shared Agent discovery service.
The `flagevaluation` writer starts only after discovery confirms EVP support at
the configured Agent address. Until then, evaluations still work, but the EVP
hook skips context capture and queueing; there is no startup buffer. A later
successful discovery enables future observations. Agent-address changes close
admission until that address is discovered, without replacing the writer or its
service tags. Flush and shutdown still drain observations already accepted.

Discovery reports capabilities, not continuous health: a later Agent outage
retains the existing bounded, best-effort sends. This does not add direct-intake
fallback or change the separate exposure transport.

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
