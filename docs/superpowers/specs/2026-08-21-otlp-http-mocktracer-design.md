# OTLP/HTTP Support in MockTracerAgent — Design

**Branch:** `otlp-http-mocktracer` (based on `otel-aspnetcore`, not `master`)
**Source RFC:** [OTLP HTTP Support in MockTracerAgent](https://docs.google.com/document/d/1CX72Zo5Lii5w6Sa8t5ic60Xplg_7AkGTra4l7rW2rj8/edit)

## Why

Today, HTTP OTLP integration tests send payloads to the Docker `ddapm-test-agent` and normalize/snapshot raw JSON. The in-process `MockTracerAgent` already receives Datadog traces, stores test-friendly DTOs, and provides async wait helpers. Supporting OTLP/HTTP in the same mock agent gives tests typed spans instead of transport-specific snapshots, and removes a Docker dependency from the OTLP AspNetCore HTTP suites.

## Scope

In scope: OTLP/HTTP JSON and OTLP/HTTP protobuf decoding of `/v1/traces` into typed DTOs, raw capture of `/v1/metrics` and `/v1/logs`, and migrating the existing `OtlpAspNetCoreTestBase`-based suites off the Docker test agent onto this new infra as the end-to-end validation of the work.

Non-goals (per RFC): OTLP over gRPC (stays on Docker `ddapm-test-agent`; `HttpListener` cannot serve HTTP/2 gRPC), OTLP metric/log decoding, any change to existing `/v0.4/traces` MessagePack behavior.

## 1. Routing

`MockTracerAgent.HandleHttpRequest` (`tracer/test/Datadog.Trace.TestHelpers/MockTracerAgent.cs:515-591`) is an if/else-if chain; anything not matching a known prefix falls through to `HandlePotentialTraces` (MessagePack decode). Add explicit branches before that catch-all so OTLP traffic never reaches the MessagePack decoder:

```csharp
else if (request.PathAndQuery.StartsWith("/v1/traces"))
{
    HandlePotentialOtlpTraces(request);
}
else if (request.PathAndQuery.StartsWith("/v1/metrics") || request.PathAndQuery.StartsWith("/v1/logs"))
{
    HandleOtlpRawSignal(request); // stores raw bytes only, no decode
}
else
{
    HandlePotentialTraces(request); // unchanged
}
```

## 2. Proto types

Generated OTLP protobuf C# types currently live only in `tracer/test/Datadog.Trace.Tests/OpenTelemetry/Traces/Generated/*.g.cs` (`TraceService`, `Trace`, `Common`, `Resource`), consumed today by `OtlpTracesProtobufSerializerTests.cs`. `MockTracerAgent` lives in the lower-level `Datadog.Trace.TestHelpers` project, which cannot depend on `Datadog.Trace.Tests`.

Move the `Generated/*.g.cs` files into `Datadog.Trace.TestHelpers/OpenTelemetry/Generated/`. `Datadog.Trace.Tests` already project-references `TestHelpers`, so `OtlpTracesProtobufSerializerTests.cs` only needs a `using` update. `TestHelpers`' existing (currently orphaned) `Google.Protobuf` 3.25.1 package reference becomes live.

## 3. Decode pipeline

New `HandlePotentialOtlpTraces(MockHttpRequest request)`, mirroring `HandlePotentialTraces`'s existing guard → body reader → decode → event → immutable-storage shape:

- Gated by new `ShouldDeserializeOtlpTraces` (`public bool`, default `true`).
- `var body = request.ReadStreamBody();` — gzip is already handled transparently by `MockHttpRequest.ReadStreamBody()` based on `Content-Encoding: gzip`; no new code needed for gzip.
- Branch on `Content-Type`:
  - `application/x-protobuf` → `ExportTraceServiceRequest.Parser.ParseFrom(body)`
  - `application/json` → normalize hex trace/span IDs to base64 → `ExportTraceServiceRequest.Parser.ParseJson(...)`
  - anything else → respond with a clear "unsupported content type" error (not a silent 200)
- Map the parsed `ExportTraceServiceRequest` into `MockOtlpTraceRequest.Create(...)`, raise `OtlpRequestDeserialized`, then under `lock (this)`: append to `OtlpTraceRequests`, flatten spans into `OtlpSpans`, append the request's headers to `OtlpTraceRequestHeaders`.

`HandleOtlpRawSignal(MockHttpRequest request)` for `/v1/metrics` and `/v1/logs`: read body via `ReadStreamBody()`, store into `OtlpMetricsRequests`/`OtlpLogsRequests` as a `MockOtlpRawRequest` (body + headers + content-type), return 200. No decoding.

## 4. DTO tree (full RFC scope, single pass)

```
MockOtlpTraceRequest
└── ResourceSpans: IImmutableList<MockOtlpResourceSpans>
    ├── Resource: MockOtlpResource (attributes, dropped-attribute count)
    ├── SchemaUrl
    └── ScopeSpans: IImmutableList<MockOtlpScopeSpans>
        ├── Scope: MockOtlpInstrumentationScope (name, version, attributes)
        ├── SchemaUrl
        └── Spans: IImmutableList<MockOtlpSpan>
            (traceId/spanId/parentSpanId as lowercase hex, traceState, flags,
             name, kind, start/end timestamps, typed attributes, events, links, status)
```

`MockOtlpTraceRequest` also exposes the underlying `ExportTraceServiceRequest` it was built from (needed for the AspNetCore snapshot bridge, §8).

Public `MockTracerAgent` surface:

```csharp
public bool ShouldDeserializeOtlpTraces { get; set; } = true;
public event EventHandler<EventArgs<MockOtlpTraceRequest>> OtlpRequestDeserialized;
public IImmutableList<MockOtlpTraceRequest> OtlpTraceRequests { get; }
public IImmutableList<MockOtlpSpan> OtlpSpans { get; }               // flattened view
public IImmutableList<NameValueCollection> OtlpTraceRequestHeaders { get; }
public IImmutableList<MockOtlpRawRequest> OtlpMetricsRequests { get; }
public IImmutableList<MockOtlpRawRequest> OtlpLogsRequests { get; }
```

```csharp
public sealed record MockOtlpRawRequest(byte[] Body, NameValueCollection Headers, string ContentType);
```

Attributes stay typed (string/bool/int/double/bytes/array/kvlist) — no stringification — matching how `OtlpTracesJsonSerializer`/`OtlpTracesProtobufSerializer` (the real tracer's hand-written OTLP encoders, `tracer/src/Datadog.Trace/OpenTelemetry/Traces/`) encode them, so round-trip tests are exact.

Trace/span/parent IDs use 32/16-character lowercase hex strings (matching `MockSpan`'s existing ID convention), decoded from the wire's base64 (JSON) or raw bytes (protobuf).

## 5. Response shapes

| Request encoding | Response |
|---|---|
| JSON | `200`, `application/json`, `{}` |
| protobuf | `200`, `application/x-protobuf`, zero-byte body |
| unsupported `Content-Type` | non-200 with a clear error message |

## 6. Wait helper

```csharp
public async Task<IImmutableList<MockOtlpSpan>> WaitForOtlpSpansAsync(
    int count, int timeoutInMilliseconds = 20000, string operationName = null,
    DateTimeOffset? minDateTime = null, bool returnAllOperations = false, bool failOnTimeout = true)
```

Polls `OtlpSpans` every 250ms like `WaitForSpansAsync`. On success, asserts every captured `OtlpTraceRequestHeaders` entry has `Content-Type` of `application/json` or `application/x-protobuf` — no `X-Datadog-Trace-Count` assertion (no OTLP equivalent). `minDateTime` filtering works the same way `WaitForSpansAsync` filters `MockSpan.Start` — this is the isolation mechanism reused in §8, not a separate reset/clear method.

## 7. Unit tests

New tests land at `tracer/test/Datadog.Trace.Tests/OpenTelemetry/Traces/`, next to `OtlpTracesProtobufSerializerTests.cs`, covering the RFC's full matrix: protobuf decode, JSON decode, encoding equivalence, JSON ID normalization, typed attributes, events/links, envelope preservation, flat-span view, wait-helper polling, headers, gzip, response shapes, invalid content-type, metrics/logs isolation, and Datadog+OTLP coexistence regression.

## 8. Migrating `OtlpAspNetCoreTestBase` off Docker (validation)

This is the practical proof the new infra is correct end-to-end, using the real tracer OTLP exporters — and directly realizes the RFC's stated success criteria ("HTTP/JSON and HTTP/protobuf integration tests use `MockTracerAgent`... Docker test agent remains only for gRPC coverage").

**Current state**: `OtlpAspNetCoreTestBase` (`tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/AspNetCore/OtlpAspNetCoreTestBase.cs`) drives its three concrete suites (`OtlpAspNetCoreMvc21Tests`, `OtlpAspNetCoreMvc31Tests`, `OtlpAspNetCoreMinimalApisTests`) through `AspNetCoreTestFixture.OtlpSession`, an `OtlpTestAgentSession` (`tracer/test/Datadog.Trace.TestHelpers.AutoInstrumentation/OtlpTestAgentSession.cs`) that talks to the Docker `ddapm-test-agent`'s session API (`/test/session/{start,clear,traces}`, keyed by `X-Datadog-Test-Session-Token`).

**Isolation**: no new reset/clear method on `MockTracerAgent`. The existing non-OTLP pattern (`AspNetCoreTestFixture.WaitForSpans`, `tracer/test/Datadog.Trace.TestHelpers.AutoInstrumentation/AspNetCoreTestFixture.cs:245-257`) captures `DateTimeOffset.UtcNow` immediately before sending each request and passes it as `minDateTime` to `WaitForSpansAsync`; `Spans` just accumulates for the fixture's whole lifetime and old entries are filtered out by timestamp. The OTLP migration copies this exactly via `WaitForOtlpSpansAsync(minDateTime: now, ...)` — `OtlpTraceRequests`/`OtlpSpans` accumulate the same way `Spans` does today.

**Fixture wiring**: `AspNetCoreTestFixture` already owns a `MockTracerAgent` (`Agent`) for the Datadog protocol. Reuse that same instance/port for OTLP — `ConfigureOtlpExport` (`TestHelper.cs:390-396`) changes to set `OTEL_EXPORTER_OTLP_TRACES_ENDPOINT` to `http://127.0.0.1:{Agent.Port}/v1/traces` instead of building a Docker session endpoint via `OtlpSession.GetExporterEndpoint(...)`. The `OTEL_EXPORTER_OTLP_HEADERS`/session-token header is dropped — there's no external shared session to disambiguate.

**Snapshot bridge**: `OtlpSnapshotHelper`/`OtlpFieldNames` (`tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/Helpers/`) walk the raw OTLP-JSON wire shape (protobuf's JSON mapping) via JSONPath, keyed by a snake_case/camelCase lookup table, because the ddapm test-agent renders protobuf-protocol payloads as snake_case JSON. To reuse this logic unmodified: `MockOtlpTraceRequest` exposes its underlying `ExportTraceServiceRequest`; the harness serializes each captured request with `Google.Protobuf.JsonFormatter.Default.Format(...)` (camelCase protobuf-JSON mapping) into a `JArray` shaped like `OtlpTestAgentSession.WaitForSpansAsync`'s old return value (one array element per export request), then feeds it into the same `OtlpSnapshotHelper` functions, switching `OtlpFieldNames.For(isJson: false)` → `.For(isJson: true)`. Existing `.verified.txt` snapshots for the three suites are regenerated once (accepted diff) for the snake_case→camelCase switch; content should otherwise be equivalent.

**Cleanup**: drop `[Trait("RequiresDockerDependency","true")]`, `[Trait("DockerGroup","1")]`, and `[Collection(nameof(TestAgentOtlpCollection))]` from the three concrete suites — there's no longer a shared external session requiring serialized access.

**Explicitly out of scope for this migration**: `OpenTelemetrySdkTests` (exercises gRPC protocol and metrics/logs endpoints — outside this RFC) stays on `OtlpTestAgentSession`/Docker unchanged. `OpenTelemetryWebRequestTests`/`OpenTelemetryHttpClientTests` (traces-only, HTTP-only, same pattern) are good candidates for the same treatment but are optional follow-up, not required for this PR's success criteria.

## Success criteria

- HTTP/JSON and HTTP/protobuf `OtlpAspNetCore*` integration tests run against `MockTracerAgent`, no Docker dependency, and pass (with regenerated snapshots as needed).
- New unit tests assert typed `MockOtlpSpan` objects per the RFC's test matrix.
- The Docker test agent remains in use only for gRPC coverage (`OpenTelemetrySdkTests`) and any suites explicitly left as follow-up.
