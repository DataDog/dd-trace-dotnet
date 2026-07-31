# OTLP snapshot tests for `Samples.WebRequest`

**Date:** 2026-07-30
**Branch:** `otel-httpclient`

## Goal

`WebRequestTests` currently snapshots the Datadog msgpack payload produced by
`Samples.WebRequest` (~134 spans) in three configurations: `v0`, `v1`, and
`otel` (`DD_TRACE_OTEL_SEMANTICS_ENABLED=true`). None of them exercise the OTLP
export path, so the HTTP semantic-convention attributes added on this branch
(`http.request.method`, `http.response.status_code`, `url.full`,
`server.address`, `server.port`, `http.request.method_original`) are never
verified as they appear on the wire in OTLP.

Add OTLP snapshot coverage for the same sample, following the pattern
established by `OpenTelemetrySdkTests.SubmitsOtlpTraces`.

Non-goal: changing, removing, or regenerating any existing msgpack snapshot.

## Scope

| In scope | Out of scope |
| --- | --- |
| New `SubmitsOtlpTraces` theory on `WebRequestTests` | Changing the existing `SubmitsTracesV0/V1(...)` tests |
| Extracting OTLP normalization into a shared helper | Changing `OpenTelemetrySdkTests`' observable behavior or its snapshots |
| Two new `.verified.txt` snapshots | gRPC protocol coverage (unsupported by the DD SDK trace exporter) |
| | `DD_AGENT_HOST` fallback coverage (already covered by `OpenTelemetrySdkTests`) |

## Test matrix

Four test cases, two snapshot files:

| `protocol` | `openTelemetrySemanticsEnabled` | Snapshot |
| --- | --- | --- |
| `http/json` | `false` | `WebRequestTests.SubmitsOtlpTraces_DD` |
| `http/protobuf` | `false` | `WebRequestTests.SubmitsOtlpTraces_DD` |
| `http/json` | `true` | `WebRequestTests.SubmitsOtlpTraces_DD_OtelSemantics` |
| `http/protobuf` | `true` | `WebRequestTests.SubmitsOtlpTraces_DD_OtelSemantics` |

The two protocols share a snapshot: the test-agent renders an http/protobuf
payload as JSON with snake_case field names and string-form enum values, and the
existing `ProtobufToJsonFieldNameMappings` / `ProtobufToJsonEnumMappings` tables
scrub that into the http/json shape. This is the same arrangement
`OpenTelemetrySdkTests.SubmitsOtlpTraces` uses.

gRPC is excluded deliberately: `ExporterSettings` maps only `HttpProtobuf` and
`HttpJson` to an OTLP traces encoding and falls back to Datadog v0.4 otherwise.

The metadata schema is pinned to `v0` for both cases. With
`DD_TRACE_OTEL_SEMANTICS_ENABLED=true` the tracer already forces v0, so pinning
it keeps the semantics-off baseline directly comparable.

## Test method

```csharp
[SkippableTheory]
[Trait("Category", "EndToEnd")]
[Trait("RequiresDockerDependency", "true")]
[Trait("DockerGroup", "1")]
[InlineData("http/json",     false)]
[InlineData("http/json",     true)]
[InlineData("http/protobuf", false)]
[InlineData("http/protobuf", true)]
public async Task SubmitsOtlpTraces(string protocol, bool openTelemetrySemanticsEnabled)
```

### Trait placement

`RequiresDockerDependency` and `DockerGroup` go on the **method**, not the class.
CI partitions the integration-test run with
`(RequiresDockerDependency=true)` / `(RequiresDockerDependency!=true)`
(`tracer/build/_build/Build.Steps.cs`) and then further by
`DockerGroup=$(dockerGroup)` (`.azure-pipelines/ultimate-pipeline.yml`). A
class-level trait would pull the existing non-docker `WebRequestTests` into the
docker job. A docker test with no `DockerGroup` trait runs in neither group, so
the trait is required, not optional. `test-agent` is a dependency of both
`StartDependencies.Group1` and `Group2`, so group 1 is an arbitrary but valid
choice matching `OpenTelemetrySdkTests`.

No `RunOnWindows` trait — the OTLP tests in `OpenTelemetrySdkTests` omit it too,
so these run on Linux only in CI. Consequence: no `_netfx` snapshot variant.

### Environment

```
OTEL_TRACES_EXPORTER          = otlp
OTEL_EXPORTER_OTLP_PROTOCOL   = <protocol>
OTEL_EXPORTER_OTLP_ENDPOINT   = http://<TEST_AGENT_HOST>:4318
DD_TRACE_OTEL_SEMANTICS_ENABLED = <openTelemetrySemanticsEnabled>
DD_TRACE_SPAN_ATTRIBUTE_SCHEMA  = v0
```

`TEST_AGENT_HOST` falls back to `127.0.0.1` when unset, matching
`SubmitsOtlpTraces`. The port is always 4318 (http/json and http/protobuf both
use the HTTP endpoint).

`DD_TRACE_HTTP_CLIENT_ERROR_STATUSES=410-499` and `SetServiceVersion("1.0.0")`
are inherited from the existing constructor and stay as-is.

### Flow

1. `ClearTestAgentSession(testAgentHost)` — with retries, so a not-yet-ready
   test-agent doesn't fail the test.
2. Allocate `httpPort` via `TcpPortProvider.GetOpenPort()` for the sample's
   `HttpListener`, as the existing tests do.
3. Construct `MockTracerAgent` and `RunSampleAndWaitForExit(agent, arguments: $"Port={httpPort}")`.
   The mock agent is still needed: telemetry does not travel over OTLP, so
   `telemetry.AssertIntegrationEnabledAsync(IntegrationId.WebRequest)` and
   `VerifyInstrumentation(processResult.Process)` continue to work unchanged.
   Only trace payloads divert to the test-agent.
4. `WaitForTestAgentData("http://<host>:4318/test/session/traces")` — polls,
   because the tracer flushes during shutdown.
5. Normalize (below), then `Verifier.Verify(finalJson, settings)` with
   `.UseFileName(...)` and `.DisableRequireUniquePrefix()`.

## Shared helper

New `OtlpSnapshotHelper` in `Datadog.Trace.ClrProfiler.IntegrationTests`, holding
what is currently private to `OpenTelemetrySdkTests`:

- `ProtobufToJsonFieldNameMappings` / `ProtobufToJsonEnumMappings` tables and
  `AddProtobufToJsonScrubbers(settings)`
- `ClearTestAgentSession(host, maxRetries, delayMs)`
- `WaitForTestAgentData(url, timeoutSeconds, pollIntervalMs)`
- Resource-attribute normalization (`telemetry.sdk.version`,
  `telemetry.sdk.name`, `git.commit.sha`)
- Per-span normalization: base64→hex conversion with the existing
  `_traceIdRegex` / `_spanIdRegex` assertions and monotonic-timestamp
  assertions, followed by flattening to placeholders
- Merging every request into a single `resource_spans` entry after asserting
  the resource attributes and instrumentation scope are identical across
  requests

`OpenTelemetrySdkTests` is rewired to call the helper. **Its snapshots must stay
byte-identical**; verify by re-running its OTLP tests and confirming no diff.

The one behavioral risk in the extraction is span ordering: `OpenTelemetrySdkTests`
sorts by `name` only. The helper therefore takes an **optional sort-key selector
defaulting to name-only**, preserving current behavior, and `WebRequestTests`
passes the composite key described below.

## Test isolation

`ClearTestAgentSession` clears the test-agent session **globally**. xUnit runs
distinct collections in parallel and this project declares no
`CollectionBehavior`, so once a second class starts clearing the session, a
clear from one class can delete another class's in-flight traces.

Today `OpenTelemetrySdkTests` is safe only by accident: all of its OTLP tests
share one implicit per-class collection. Adding OTLP tests to `WebRequestTests`
breaks that.

Fix: a shared `TestAgentOtlpCollection` with `DisableParallelization = true`,
applied to both classes. `WebRequestTests`' existing single-class
`CollectionDefinition` is replaced by it — that collection existed only to
disable parallelization, which the shared one also does.

The CI cost is close to zero. The non-docker job filters `OpenTelemetrySdkTests`
out entirely via `RequiresDockerDependency!=true`, and the docker job filters out
`WebRequestTests`' msgpack tests, so the only work actually serialized is OTLP
tests against each other — which is the point.

## Determinism

Four sources of instability, each handled explicitly.

### 1. Span ordering

Under OTLP the span `name` is `Span.ResourceName` (see
`OtlpTracesJsonSerializer`), which for this sample is mostly `POST`/`GET` —
name-only sorting is nowhere near deterministic across 134 spans.

Sort by: `name` → the `url.full` attribute value (empty string when absent) →
the span's own normalized JSON text.

IDs and timestamps are normalized *before* sorting, which makes the third key
total: any two spans that still tie are byte-identical, so their relative order
cannot change the output. The first two keys exist only to make the snapshot
readable.

### 2. Dynamic listener port

`VerifyHelper.SpanScrubbers` already rewrites `localhost:\d+` → `localhost:00000`
and `127.0.0.1:\d+` → `localhost:00000`, which covers `url.full`.
`ScrubInlineGuids` covers the per-run GUID in the request path.

`server.port` is a separate attribute carrying the raw port number, and it is
not a text match for those regexes. Normalize it via a JToken lookup on
`key == 'server.port'`, setting the value to a fixed `8080` — mirroring the
`server.port: \d+` regex scrubber the msgpack test already uses.

### 3. TFM differences

The existing msgpack test handles two TFM-dependent differences that apply
equally to the OTLP payload:

- 49 spans carry `http-client-handler-type`. On .NET Core the test scrubs
  `System.Net.Http.HttpClientHandler` → `System.Net.Http.SocketsHttpHandler`.
- On .NET 9+, the `?BeginGetResponseAsync_NoBuffering` request produces a
  `WebRequest` span instead of an `HttpClient` span. The existing test patches
  that single span's `component` to `HttpMessageHandler` and adds
  `http-client-handler-type = System.Net.Http.SocketsHttpHandler`.

Both must be replicated on the OTLP JSON (the handler-type as a simple string
scrubber; the .NET 9 fixup as a JToken edit locating the span by its `url.full`
attribute suffix). Without them, .NET 9 needs its own snapshot pair.

### 4. IDs and timestamps

Flattened to fixed placeholders (`normalized-trace-id`, `normalized-span-id`,
`normalized-parent-span-id`, `"0"` for start/end times), matching
`OpenTelemetrySdkTests`. This erases parent→child structure from the snapshot;
that structure is already asserted by the existing msgpack snapshots, and the
OTLP snapshot's job is to verify attributes and span shape on the wire.

The hex-format and monotonic-timestamp assertions run *before* flattening, so
the real values are still validated.

## Assertions beyond the snapshot

Carried over from the existing `RunTest`:

- `telemetry.AssertIntegrationEnabledAsync(IntegrationId.WebRequest)`
- `VerifyInstrumentation(processResult.Process)` (via `SetInstrumentationVerification()`)
- `tracesRequests.Should().NotBeNullOrEmpty()`

`ValidateIntegrationSpans` is **not** applicable: it operates on `MockSpan`,
which is the msgpack representation. The OTLP payload has no `MockSpan`
equivalent, and the snapshot covers the same ground.

## Known trade-off: snapshot size

OTLP JSON is far more verbose than the Verify span format — roughly six lines
per attribute versus one. The existing `WebRequestTests_otel.verified.txt` is
3,130 lines for this same data; each OTLP snapshot is expected to land around
10–12k lines, for two files. Accepted in exchange for full-fidelity coverage;
the alternative (filtering to HTTP-client spans only) was considered and
rejected.

## Verification plan

1. `docker compose up -d test-agent` locally (macOS, Docker confirmed running;
   `artifacts/monitoring-home` is already built).
2. Run the four new cases to generate the two `.verified.txt` files; inspect
   them for leaked ports, GUIDs, timestamps, or machine-specific paths.
3. Re-run each case a second time to confirm the snapshots are stable
   (ordering, merged-request handling).
4. Re-run `OpenTelemetrySdkTests.SubmitsOtlpTraces` and confirm its snapshots
   are unchanged after the helper extraction.
5. Confirm the existing `SubmitsTracesV0/V1` msgpack tests still pass and their
   snapshots are untouched.
