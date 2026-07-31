# WebRequest OTLP Snapshot Tests Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add OTLP snapshot coverage for `Samples.WebRequest` so the HTTP semantic-convention attributes added on this branch are verified as they appear on the wire in OTLP, not just in Datadog msgpack.

**Architecture:** Extract the OTLP payload-normalization logic currently private to `OpenTelemetrySdkTests` into a shared `OtlpSnapshotHelper`, then add a four-case `SubmitsOtlpTraces` theory to `WebRequestTests` that exports through the `test-agent` container and snapshots the normalized JSON. `OpenTelemetrySdkTests` must keep producing byte-identical snapshots throughout.

**Tech Stack:** xUnit (`SkippableTheory`), Verify/VerifyXunit snapshots, FluentAssertions, `Datadog.Trace.Vendors.Newtonsoft.Json.Linq`, `ddapm-test-agent` docker container.

**Spec:** `docs/superpowers/specs/2026-07-30-webrequest-otlp-snapshots-design.md`

## Global Constraints

- **Never regenerate or modify an existing snapshot.** `WebRequestTests_v0`, `WebRequestTests_v1`, `WebRequestTests_otel`, `WebRequestTests_netfx_*`, and every `OpenTelemetrySdkTests.*` snapshot must remain byte-identical. If one changes, the change is a bug — revert and rethink.
- Copyright header on every new file, matching the repo's exact format (see any existing file under `tracer/test/`).
- Follow `.editorconfig` and `tracer/stylecop.json`. Use `is not null` over `!= null`. Add `using` directives rather than fully-qualified type names.
- Use `Datadog.Trace.Vendors.Newtonsoft.Json` / `.Linq` — **not** `Newtonsoft.Json`. This is what `OpenTelemetrySdkTests` uses and the only JSON library referenced by the test project.
- The new helper files use `#nullable enable`, but the code being moved into them came from a file that does not. Expect nullability warnings on the copied bodies (`CS8602` on `span[key].ToString()`, `CS8600` on `JToken previousResourceAttributes = null`). Resolve them with `?` on locals and the `!` null-forgiving operator — both are compile-time only and cannot change behavior. Do **not** restructure the copied logic to satisfy the compiler.
- `OtlpFieldNames` is passed **by value**, never as an `in`/`ref` parameter. Lambdas cannot capture `in` parameters, and several call sites close over it.
- The test-agent OTLP HTTP endpoint is always port **4318** for both `http/json` and `http/protobuf`. Host comes from `TEST_AGENT_HOST`, defaulting to `127.0.0.1`.
- gRPC is out of scope: `ExporterSettings` only maps `HttpProtobuf`/`HttpJson` to an OTLP traces encoding and silently falls back to Datadog v0.4 otherwise.

## Prerequisites

Start the test-agent before running anything in Task 1, 2, or 4:

```bash
docker compose up -d test-agent
curl -sf http://127.0.0.1:4318/test/session/clear && echo OK
```

## File Structure

| File | Responsibility |
| --- | --- |
| `tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/Helpers/OtlpFieldNames.cs` (create) | Maps a protocol to the OTLP field-name casing the test-agent renders (`resourceSpans` vs `resource_spans`, etc.) |
| `tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/Helpers/OtlpSnapshotHelper.cs` (create) | Test-agent session I/O, protobuf→json scrubbers, OTLP payload normalization, request merging, attribute lookup |
| `tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/Helpers/TestAgentOtlpCollection.cs` (create) | xUnit collection that serializes every class sharing the test-agent OTLP session |
| `tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/OpenTelemetrySdkTests.cs` (modify) | Loses its private OTLP plumbing; delegates to the helper. Behavior unchanged. |
| `tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/WebRequestTests.cs` (modify) | Gains `SubmitsOtlpTraces` plus WebRequest-specific normalization |
| `tracer/test/snapshots/WebRequestTests.SubmitsOtlpTraces_DD.verified.txt` (create) | Snapshot, semantics off |
| `tracer/test/snapshots/WebRequestTests.SubmitsOtlpTraces_DD_OtelSemantics.verified.txt` (create) | Snapshot, semantics on |

---

### Task 1: Extract test-agent I/O and protocol scrubbers

Pure code motion. Everything moved here is currently private to `OpenTelemetrySdkTests` and used verbatim by its traces, metrics, and logs tests.

**Files:**
- Create: `tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/Helpers/OtlpFieldNames.cs`
- Create: `tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/Helpers/OtlpSnapshotHelper.cs`
- Modify: `tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/OpenTelemetrySdkTests.cs`

**Interfaces:**
- Consumes: nothing
- Produces:
  - `OtlpFieldNames.For(bool isJson) -> OtlpFieldNames` with `string` properties `ResourceSpans`, `ScopeSpans`, `StringValue`, `IntValue`, `TraceId`, `SpanId`, `ParentSpanId`, `StartTimeUnixNano`, `EndTimeUnixNano`, `TimeUnixNano`, and `bool IsJson`
  - `OtlpSnapshotHelper.ClearTestAgentSessionAsync(string testAgentHost, int maxRetries = 5, int delayMs = 1000) -> Task`
  - `OtlpSnapshotHelper.WaitForTestAgentDataAsync(string url, int timeoutSeconds = 60, int pollIntervalMs = 500) -> Task<JToken>`
  - `OtlpSnapshotHelper.AddProtobufToJsonScrubbers(VerifySettings settings) -> void`

- [ ] **Step 1: Create `OtlpFieldNames.cs`**

```csharp
// <copyright file="OtlpFieldNames.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.ClrProfiler.IntegrationTests.Helpers
{
    /// <summary>
    /// The test agent renders an OTLP http/json payload with camelCase field names and an
    /// http/protobuf payload with snake_case field names. This maps a protocol to the casing
    /// used when walking the rendered JSON.
    /// </summary>
    internal readonly struct OtlpFieldNames
    {
        private OtlpFieldNames(bool isJson)
        {
            IsJson = isJson;
        }

        public bool IsJson { get; }

        public string ResourceSpans => IsJson ? "resourceSpans" : "resource_spans";

        public string ScopeSpans => IsJson ? "scopeSpans" : "scope_spans";

        public string StringValue => IsJson ? "stringValue" : "string_value";

        public string IntValue => IsJson ? "intValue" : "int_value";

        public string TraceId => IsJson ? "traceId" : "trace_id";

        public string SpanId => IsJson ? "spanId" : "span_id";

        public string ParentSpanId => IsJson ? "parentSpanId" : "parent_span_id";

        public string StartTimeUnixNano => IsJson ? "startTimeUnixNano" : "start_time_unix_nano";

        public string EndTimeUnixNano => IsJson ? "endTimeUnixNano" : "end_time_unix_nano";

        public string TimeUnixNano => IsJson ? "timeUnixNano" : "time_unix_nano";

        public static OtlpFieldNames For(bool isJson) => new(isJson);
    }
}
```

- [ ] **Step 2: Create `OtlpSnapshotHelper.cs` with the moved I/O and scrubbers**

The two mapping tables and all three method bodies are copied verbatim from `OpenTelemetrySdkTests` — do not re-derive them.

```csharp
// <copyright file="OtlpSnapshotHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using VerifyTests;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.Helpers
{
    /// <summary>
    /// Shared plumbing for tests that snapshot OTLP payloads captured by the ddapm test agent.
    /// </summary>
    internal static class OtlpSnapshotHelper
    {
        // Single source of truth for translating an OTLP http/protobuf payload (rendered as JSON
        // by the test agent with snake_case field names and string-form enum values) to the
        // OTLP http/json shape (camelCase field names, integer enum values). When a new OTLP
        // field or enum reaches the serializer, add the mapping here.
        private static readonly (string From, string To)[] ProtobufToJsonFieldNameMappings =
        {
            ("\"resource_spans\"",        "\"resourceSpans\""),
            ("\"scope_spans\"",           "\"scopeSpans\""),
            ("\"trace_id\"",              "\"traceId\""),
            ("\"span_id\"",               "\"spanId\""),
            ("\"parent_span_id\"",        "\"parentSpanId\""),
            ("\"start_time_unix_nano\"",  "\"startTimeUnixNano\""),
            ("\"end_time_unix_nano\"",    "\"endTimeUnixNano\""),
            ("\"time_unix_nano\"",        "\"timeUnixNano\""),
            ("\"string_value\"",          "\"stringValue\""),
            ("\"double_value\"",          "\"doubleValue\""),
            ("\"int_value\"",             "\"intValue\""),
            ("\"bool_value\"",            "\"boolValue\""),
            ("\"array_value\"",           "\"arrayValue\""),
        };

        private static readonly (string From, string To)[] ProtobufToJsonEnumMappings =
        {
            ("\"kind\": \"SPAN_KIND_INTERNAL\"", "\"kind\": 1"),
            ("\"kind\": \"SPAN_KIND_SERVER\"",   "\"kind\": 2"),
            ("\"kind\": \"SPAN_KIND_CLIENT\"",   "\"kind\": 3"),
            ("\"kind\": \"SPAN_KIND_PRODUCER\"", "\"kind\": 4"),
            ("\"kind\": \"SPAN_KIND_CONSUMER\"", "\"kind\": 5"),
            ("\"code\": \"STATUS_CODE_UNSET\"",  "\"code\": 0"),
            ("\"code\": \"STATUS_CODE_OK\"",     "\"code\": 1"),
            ("\"code\": \"STATUS_CODE_ERROR\"",  "\"code\": 2"),
        };

        public static void AddProtobufToJsonScrubbers(VerifySettings settings)
        {
            foreach (var (from, to) in ProtobufToJsonFieldNameMappings)
            {
                settings.AddSimpleScrubber(from, to);
            }

            foreach (var (from, to) in ProtobufToJsonEnumMappings)
            {
                settings.AddSimpleScrubber(from, to);
            }
        }

        /// <summary>
        /// Clears the test-agent session, retrying if the agent is not yet ready.
        /// Ensures the OTLP HTTP endpoint is accepting connections before tests proceed.
        /// </summary>
        public static async Task ClearTestAgentSessionAsync(string testAgentHost, int maxRetries = 5, int delayMs = 1000)
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var url = $"http://{testAgentHost}:4318/test/session/clear";

            for (var attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var response = await httpClient.GetAsync(url);
                    response.EnsureSuccessStatusCode();
                    return;
                }
                catch (Exception) when (attempt < maxRetries)
                {
                    await Task.Delay(delayMs);
                }
            }

            // Final attempt -- let it throw if it fails
            var finalResponse = await httpClient.GetAsync(url);
            finalResponse.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// Polls the test-agent for data until non-empty results are returned or timeout is reached.
        /// The sample app exports data during shutdown, so there can be a brief delay
        /// between process exit and data appearing in the test-agent. The timeout is generous
        /// because first-time gRPC connections (TCP+HTTP/2+TLS handshake) plus tracer shutdown
        /// flushing can stack up on slower CI runners.
        /// </summary>
        public static async Task<JToken> WaitForTestAgentDataAsync(string url, int timeoutSeconds = 60, int pollIntervalMs = 500)
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);

            while (DateTime.UtcNow < deadline)
            {
                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var data = JToken.Parse(json);

                if (data.HasValues)
                {
                    return data;
                }

                await Task.Delay(pollIntervalMs);
            }

            // Final attempt -- return whatever we get so the caller's assertion shows the actual value
            var finalResponse = await httpClient.GetAsync(url);
            finalResponse.EnsureSuccessStatusCode();
            var finalJson = await finalResponse.Content.ReadAsStringAsync();
            return JToken.Parse(finalJson);
        }
    }
}
```

`AddSimpleScrubber` is an extension method on `VerifySettings` defined in `tracer/test/Datadog.Trace.TestHelpers.SharedSource/VerifyHelper.cs:202`, so `OtlpSnapshotHelper.cs` also needs `using Datadog.Trace.TestHelpers;`.

- [ ] **Step 3: Delete the moved members from `OpenTelemetrySdkTests.cs`**

Delete `ProtobufToJsonFieldNameMappings` (lines ~83-98), `ProtobufToJsonEnumMappings` (~100-110), `AddProtobufToJsonScrubbers` (~979-990), `ClearTestAgentSession` (~892-914), and `WaitForTestAgentData` (~923-949).

- [ ] **Step 4: Update the call sites in `OpenTelemetrySdkTests.cs`**

There are five call sites. Replace each:

| Old | New |
| --- | --- |
| `await ClearTestAgentSession(testAgentHost);` | `await OtlpSnapshotHelper.ClearTestAgentSessionAsync(testAgentHost);` |
| `await WaitForTestAgentData(...)` | `await OtlpSnapshotHelper.WaitForTestAgentDataAsync(...)` |
| `AddProtobufToJsonScrubbers(settings);` | `OtlpSnapshotHelper.AddProtobufToJsonScrubbers(settings);` |

Add `using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;` to the file's using block.

- [ ] **Step 5: Build**

Run: `dotnet build tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/Datadog.Trace.ClrProfiler.IntegrationTests.csproj -f net10.0`
Expected: build succeeds with no new warnings. `System.Net.Http` and `System.Text.RegularExpressions` usings in `OpenTelemetrySdkTests.cs` may now be unused — if the analyzer flags them, remove only the ones it flags (`Regex` is still used by the `_versionRegex` fields, so do not blanket-remove).

- [ ] **Step 6: Smoke-test one OTLP case**

Run:
```bash
dotnet test tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/Datadog.Trace.ClrProfiler.IntegrationTests.csproj \
  -f net10.0 --no-build \
  --filter "FullyQualifiedName~OpenTelemetrySdkTests.SubmitsOtlpTraces"
```
Expected: PASS. If the harness cannot locate the monitoring home, fall back to the documented Nuke path:
```bash
./tracer/build.sh BuildAndRunIntegrationTests --framework net10.0 \
  --filter "Datadog.Trace.ClrProfiler.IntegrationTests.OpenTelemetrySdkTests.SubmitsOtlpTraces" \
  --SampleName "Samples.OpenTelemetrySdk"
```

- [ ] **Step 7: Confirm no snapshot drifted**

Run: `git status --porcelain tracer/test/snapshots/`
Expected: **empty output**. Any modified or new `.received.txt` file means the extraction changed behavior — stop and fix before continuing.

- [ ] **Step 8: Commit**

```bash
git add tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/Helpers/OtlpFieldNames.cs \
        tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/Helpers/OtlpSnapshotHelper.cs \
        tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/OpenTelemetrySdkTests.cs
git commit -m "test: extract OTLP test-agent helpers from OpenTelemetrySdkTests"
```

---

### Task 2: Extract OTLP payload normalization and request merging

**Files:**
- Modify: `tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/Helpers/OtlpSnapshotHelper.cs`
- Modify: `tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/OpenTelemetrySdkTests.cs:340-535`

**Interfaces:**
- Consumes: `OtlpFieldNames`, `OtlpSnapshotHelper` from Task 1
- Produces:
  - `OtlpSnapshotHelper.NormalizeResourceAttributes(JToken tracesRequests, OtlpFieldNames names) -> void`
  - `OtlpSnapshotHelper.NormalizeSpans(JToken tracesRequests, OtlpFieldNames names, long applicationStartTimeUnixNano) -> void`
  - `OtlpSnapshotHelper.MergeDatadogRequests(JToken tracesRequests, OtlpFieldNames names, Func<IEnumerable<JToken>, IEnumerable<JToken>>? sortSpans = null) -> JToken`
  - `OtlpSnapshotHelper.SortSpansPerScope(JToken tracesRequests, OtlpFieldNames names) -> void`
  - `OtlpSnapshotHelper.GetAttributeStringValue(JToken span, OtlpFieldNames names, params string[] keys) -> string?`
  - `OtlpSnapshotHelper.SetAttributeStringValue(JToken span, OtlpFieldNames names, string key, string value) -> void`
  - `OtlpSnapshotHelper.SortSpanAttributes(JToken tracesRequests) -> void`

**Critical:** `MergeDatadogRequests`'s default sort must stay `OrderBy(s => s["name"]!.ToString())` with the default string comparer — exactly what `OpenTelemetrySdkTests` does today. Do not "improve" it to `StringComparer.Ordinal` here or its snapshots will reorder.

- [ ] **Step 1: Add the normalization methods to `OtlpSnapshotHelper`**

Bodies are lifted verbatim from `OpenTelemetrySdkTests.SubmitsOtlpTraces`, with the local `*Key` variables replaced by `names.*`.

```csharp
        public static void NormalizeResourceAttributes(JToken tracesRequests, OtlpFieldNames names)
        {
            var stringValueKey = names.StringValue;

            foreach (var attribute in tracesRequests.SelectTokens("$..resource.attributes[?(@.key == 'telemetry.sdk.version')]"))
            {
                attribute["value"]![stringValueKey] = "sdk-version";
            }

            foreach (var attribute in tracesRequests.SelectTokens("$..resource.attributes[?(@.key == 'telemetry.sdk.name')]"))
            {
                attribute["value"]![stringValueKey] = "sdk-name";
            }

            foreach (var attribute in tracesRequests.SelectTokens("$..resource.attributes[?(@.key == 'git.commit.sha')]"))
            {
                attribute["value"]![stringValueKey] = "normalized-git-commit-sha";
            }
        }

        public static void NormalizeSpans(JToken tracesRequests, OtlpFieldNames names, long applicationStartTimeUnixNano)
        {
            var isJson = names.IsJson;
            var stringValueKey = names.StringValue;
            var traceIdKey = names.TraceId;
            var spanIdKey = names.SpanId;
            var parentSpanIdKey = names.ParentSpanId;
            var startTimeUnixNanoKey = names.StartTimeUnixNano;
            var endTimeUnixNanoKey = names.EndTimeUnixNano;
            var timeUnixNanoKey = names.TimeUnixNano;

            foreach (var span in tracesRequests.SelectTokens("$..spans[*]"))
            {
                // Parse unstable information from the span
                string traceIdData = isJson ? span[traceIdKey].ToString()
                                            : ToTraceId(Convert.FromBase64String(span[traceIdKey].ToString()));
                string spanIdData = isJson ? span[spanIdKey].ToString()
                                            : ToSpanId(Convert.FromBase64String(span[spanIdKey].ToString()));
                var spanStartTimeUnixNano = long.Parse(span[startTimeUnixNanoKey].ToString());
                var spanEndTimeUnixNano = long.Parse(span[endTimeUnixNanoKey].ToString());

                // Add strong assertions on unstable span information
                spanStartTimeUnixNano.Should().BeGreaterThanOrEqualTo(applicationStartTimeUnixNano);
                spanEndTimeUnixNano.Should().BeGreaterThanOrEqualTo(spanStartTimeUnixNano);
                traceIdData.Should().MatchRegex(TraceIdRegex);
                spanIdData.Should().MatchRegex(SpanIdRegex);
                if (span[parentSpanIdKey] != null)
                {
                    string parentSpanIdData = isJson ? span[parentSpanIdKey]?.ToString()
                                                    : ToSpanId(Convert.FromBase64String(span[parentSpanIdKey].ToString()));
                    parentSpanIdData.Should().MatchRegex(SpanIdRegex);
                }

                // Normalize the unstable span information for our snapshots
                span[startTimeUnixNanoKey] = "0";
                span[endTimeUnixNanoKey] = "0";
                span[traceIdKey] = "normalized-trace-id";
                span[spanIdKey] = "normalized-span-id";
                if (span[parentSpanIdKey] != null)
                {
                    span[parentSpanIdKey] = "normalized-parent-span-id";
                }

                // Our JSON and Protobuf OTLP exporters differ in serialization behavior when there are no attributes.
                // Standardize them here by removing an empty array
                if (span["attributes"] is JArray attributes && attributes.Count == 0)
                {
                    ((JObject)span).Remove("attributes");
                }
            }

            foreach (var attribute in tracesRequests.SelectTokens("$..spans[*].attributes[?(@.key == 'otel.trace_id')]"))
            {
                attribute["value"]![stringValueKey] = "normalized-otel-trace-id";
            }

            foreach (var link in tracesRequests.SelectTokens("$..links[*]"))
            {
                if (isJson)
                {
                    link[traceIdKey].ToString().Should().MatchRegex(TraceIdRegex);
                    link[spanIdKey].ToString().Should().MatchRegex(SpanIdRegex);
                }

                link[traceIdKey] = "normalized-trace-id";
                link[spanIdKey] = "normalized-span-id";
            }

            foreach (var @event in tracesRequests.SelectTokens("$..events[*]"))
            {
                ((JObject)@event).Remove(timeUnixNanoKey);
                ((JObject)@event).AddFirst(new JProperty(timeUnixNanoKey, "0"));
            }
        }

        private static string ToHexString(byte[] bytes, int length)
        {
            bytes.Length.Should().Be(length);

            var traceId = new byte[length * 2];
            for (int i = 0; i < length; i++)
            {
                traceId[2 * i] = (byte)(bytes[i] >> 4);         // high 4 bits
                traceId[(2 * i) + 1] = (byte)(bytes[i] & 0x0F); // low 4 bits
            }

            // Convert each nibble (0-15) to its hex character
            var result = new char[length * 2];
            for (int i = 0; i < length * 2; i++)
            {
                result[i] = (char)(traceId[i] < 10 ? '0' + traceId[i] : 'a' + traceId[i] - 10);
            }

            return new string(result);
        }

        private static string ToTraceId(byte[] bytes) => ToHexString(bytes, 16);

        private static string ToSpanId(byte[] bytes) => ToHexString(bytes, 8);
```

Add these static fields alongside the mapping tables (moved from `OpenTelemetrySdkTests`' instance fields `_traceIdRegex` / `_spanIdRegex`):

```csharp
        private static readonly Regex TraceIdRegex = new(@"^([a-fA-F0-9]{32})$");
        private static readonly Regex SpanIdRegex = new(@"^([a-fA-F0-9]{16})$");
```

New usings for this file: `System.Collections.Generic`, `System.Linq`, `System.Text.RegularExpressions`, `Datadog.Trace.Vendors.Newtonsoft.Json`, `FluentAssertions`.

The original `foreach (var link ...)` had a commented-out `else` branch for the protobuf case. It is dead code that was never enabled — drop the comment block and keep the `if (isJson)` guard, as written above.

- [ ] **Step 2: Add merge, sort, and attribute helpers to `OtlpSnapshotHelper`**

```csharp
        /// <summary>
        /// Collapses every captured request into the first one. Asserts that each request carries
        /// identical resource attributes and a single instrumentation scope first, which holds for
        /// the Datadog SDK because it emits one application-level resource and does not yet track
        /// per-library scopes.
        /// </summary>
        public static JToken MergeDatadogRequests(
            JToken tracesRequests,
            OtlpFieldNames names,
            Func<IEnumerable<JToken>, IEnumerable<JToken>>? sortSpans = null)
        {
            var resourceSpansKey = names.ResourceSpans;
            var scopeSpansKey = names.ScopeSpans;

            // First, for the DD SDK, assert that the resource attributes for all requests are identical
            // This is analogous to DD_SERVICE, DD_VERSION, DD_ENV, etc. that define
            // metadata for the telemetry at an application and host level.
            JToken previousResourceAttributes = null;
            foreach (var tracesRequest in tracesRequests)
            {
                tracesRequest[resourceSpansKey].Should().HaveCount(1);
                var resourceAttributes = tracesRequest[resourceSpansKey][0]["resource"]["attributes"];

                if (previousResourceAttributes == null)
                {
                    previousResourceAttributes = resourceAttributes;
                }
                else
                {
                    JToken.DeepEquals(previousResourceAttributes, resourceAttributes).Should().BeTrue();
                    previousResourceAttributes = resourceAttributes;
                }
            }

            // Next, assert that we only have a singular InstrumentationScope in each request.
            // In OpenTelemetry, an InstrumentationScope is a way to group spans by the library that produced them.
            // We should be respecting this for each library/ActivitySource, but right now the DD SDK doesn't
            // keep track of that information, so consolidate them into one single, empty InstrumentationScope.
            // TODO: Properly track spans per instrumentation scope.
            JArray firstSpans = null;
            foreach (var tracesRequest in tracesRequests)
            {
                tracesRequest[resourceSpansKey][0][scopeSpansKey].Should().HaveCount(1);
                var spans = tracesRequest[resourceSpansKey][0][scopeSpansKey][0]["spans"] as JArray;

                if (firstSpans == null)
                {
                    firstSpans = spans;
                }
                else
                {
                    foreach (var span in spans)
                    {
                        firstSpans.Add(span);
                    }
                }
            }

            // Now re-order and trim down to one single request
            // This means the output is not a true 1:1 mapping of the input spans, but it's good enough for now
            // and will make the results stable.
            sortSpans ??= spans => spans.OrderBy(s => s["name"]!.ToString());
            var sortedSpans = new JArray(sortSpans(firstSpans));
            tracesRequests[0][resourceSpansKey][0][scopeSpansKey][0]["spans"] = sortedSpans;
            return tracesRequests[0];
        }

        /// <summary>
        /// Sorts spans by name within each scope, leaving the request structure intact. Used when the
        /// payload comes from a real OTel SDK, which emits genuinely distinct scopes.
        /// </summary>
        public static void SortSpansPerScope(JToken tracesRequests, OtlpFieldNames names)
        {
            foreach (var scopeSpan in tracesRequests.SelectTokens($"$..{names.ScopeSpans}[*]"))
            {
                if (scopeSpan["spans"] is JArray spansArray)
                {
                    var sorted = new JArray(spansArray.OrderBy(s => s["name"]?.ToString()));
                    scopeSpan["spans"] = sorted;
                }
            }
        }

        /// <summary>
        /// Returns the string value of the first attribute matching any of <paramref name="keys"/>,
        /// or null when the span carries none of them. Accepts several keys because a tag's name
        /// changes with the semantic conventions in play (for example http.url vs url.full).
        /// </summary>
        public static string? GetAttributeStringValue(JToken span, OtlpFieldNames names, params string[] keys)
        {
            if (span["attributes"] is not JArray attributes)
            {
                return null;
            }

            foreach (var key in keys)
            {
                foreach (var attribute in attributes)
                {
                    if (attribute["key"]?.ToString() == key)
                    {
                        return attribute["value"]?[names.StringValue]?.ToString();
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Sets a string attribute on a span, appending it when absent.
        /// </summary>
        public static void SetAttributeStringValue(JToken span, OtlpFieldNames names, string key, string value)
        {
            if (span["attributes"] is not JArray attributes)
            {
                attributes = new JArray();
                ((JObject)span)["attributes"] = attributes;
            }

            foreach (var attribute in attributes)
            {
                if (attribute["key"]?.ToString() == key)
                {
                    attribute["value"] = new JObject { [names.StringValue] = value };
                    return;
                }
            }

            attributes.Add(new JObject
            {
                ["key"] = key,
                ["value"] = new JObject { [names.StringValue] = value },
            });
        }

        /// <summary>
        /// Sorts every span's attribute array by key. Attribute order otherwise follows tag
        /// enumeration order, which is not guaranteed stable across runtimes.
        /// </summary>
        public static void SortSpanAttributes(JToken tracesRequests)
        {
            foreach (var span in tracesRequests.SelectTokens("$..spans[*]"))
            {
                if (span["attributes"] is JArray attributes)
                {
                    ((JObject)span)["attributes"] = new JArray(
                        attributes.OrderBy(a => a["key"]?.ToString() ?? string.Empty, StringComparer.Ordinal));
                }
            }
        }
```

- [ ] **Step 3: Replace the inlined logic in `OpenTelemetrySdkTests.SubmitsOtlpTraces`**

Delete everything from `// Normalize the data in resource attributes and spans` (the block of `*Key` local variables) through the end of the `else` branch that assigns `finalJson`, and replace with:

```csharp
                var names = OtlpFieldNames.For(isJson);
                OtlpSnapshotHelper.NormalizeResourceAttributes(tracesRequests, names);
                OtlpSnapshotHelper.NormalizeSpans(tracesRequests, names, applicationStartTimeUnixNano);

                string finalJson;
                if (datadogTracesEnabled.Equals("true"))
                {
                    finalJson = OtlpSnapshotHelper.MergeDatadogRequests(tracesRequests, names)
                                                  .ToString(Formatting.Indented);
                }
                else
                {
                    OtlpSnapshotHelper.SortSpansPerScope(tracesRequests, names);
                    finalJson = tracesRequests.ToString(Formatting.Indented);
                }
```

Then delete the now-unused `_traceIdRegex` and `_spanIdRegex` instance fields.

- [ ] **Step 4: Build**

Run: `dotnet build tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/Datadog.Trace.ClrProfiler.IntegrationTests.csproj -f net10.0`
Expected: build succeeds. Remove any using directives the analyzer now reports as unused.

- [ ] **Step 5: Run the full OTLP traces theory**

Run:
```bash
dotnet test tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/Datadog.Trace.ClrProfiler.IntegrationTests.csproj \
  -f net10.0 --no-build \
  --filter "FullyQualifiedName~OpenTelemetrySdkTests.SubmitsOtlpTraces"
```
Expected: all cases PASS. This covers both the http/json and http/protobuf paths and both the merged (Datadog) and per-scope (OTel SDK) branches.

- [ ] **Step 6: Confirm no snapshot drifted**

Run: `git status --porcelain tracer/test/snapshots/`
Expected: **empty output**.

- [ ] **Step 7: Commit**

```bash
git add tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/Helpers/OtlpSnapshotHelper.cs \
        tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/OpenTelemetrySdkTests.cs
git commit -m "test: extract OTLP payload normalization into OtlpSnapshotHelper"
```

---

### Task 3: Serialize test-agent OTLP consumers into one xUnit collection

xUnit runs distinct collections in parallel, and this project sets no `CollectionBehavior`. `ClearTestAgentSessionAsync` wipes the shared test-agent session globally, so once `WebRequestTests` also uses it, a clear from one class can delete another class's in-flight traces. Today `OpenTelemetrySdkTests` is safe only because all its tests live in one implicit per-class collection.

In CI this costs almost nothing: the non-docker job filters `OpenTelemetrySdkTests` out entirely (`RequiresDockerDependency!=true`), and the docker job filters out `WebRequestTests`' msgpack tests, so the only serialization that actually happens is between OTLP tests — which is the intent.

**Files:**
- Create: `tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/Helpers/TestAgentOtlpCollection.cs`
- Modify: `tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/OpenTelemetrySdkTests.cs:28`
- Modify: `tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/WebRequestTests.cs:21-22`

**Interfaces:**
- Consumes: nothing
- Produces: collection name `TestAgentOtlpCollection` for use in `[Collection(nameof(TestAgentOtlpCollection))]`

- [ ] **Step 1: Create the collection definition**

```csharp
// <copyright file="TestAgentOtlpCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Xunit;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.Helpers
{
    /// <summary>
    /// Serializes every test class that reads from the shared ddapm test-agent OTLP session.
    /// Those tests call /test/session/clear, which wipes the session for everyone, so they must
    /// not run concurrently with each other.
    /// </summary>
    [CollectionDefinition(nameof(TestAgentOtlpCollection), DisableParallelization = true)]
    public class TestAgentOtlpCollection
    {
    }
}
```

- [ ] **Step 2: Move `WebRequestTests` into the shared collection**

Replace lines 21-22 of `WebRequestTests.cs`:

```csharp
    [CollectionDefinition(nameof(WebRequestTests), DisableParallelization = true)]
    [Collection(nameof(WebRequestTests))]
```

with:

```csharp
    [Collection(nameof(TestAgentOtlpCollection))]
```

The existing collection contained only this one class and existed purely to disable parallelization, which the new collection also does.

- [ ] **Step 3: Add `OpenTelemetrySdkTests` to the shared collection**

Add `[Collection(nameof(TestAgentOtlpCollection))]` to the attribute list on the class (alongside the existing `[UsesVerify]`).

- [ ] **Step 4: Verify test discovery is unchanged**

Run:
```bash
dotnet test tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/Datadog.Trace.ClrProfiler.IntegrationTests.csproj \
  -f net10.0 --list-tests --filter "FullyQualifiedName~WebRequestTests|FullyQualifiedName~OpenTelemetrySdkTests" \
  | grep -c "WebRequestTests\|OpenTelemetrySdkTests"
```
Expected: a non-zero count, and no discovery errors. A class in two collections is a runtime error, so a clean listing confirms the attributes are correct.

- [ ] **Step 5: Commit**

```bash
git add tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/Helpers/TestAgentOtlpCollection.cs \
        tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/OpenTelemetrySdkTests.cs \
        tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/WebRequestTests.cs
git commit -m "test: serialize test-agent OTLP consumers into a shared xunit collection"
```

---

### Task 4: Add `WebRequestTests.SubmitsOtlpTraces` and generate the snapshots

**Files:**
- Modify: `tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/WebRequestTests.cs`
- Create: `tracer/test/snapshots/WebRequestTests.SubmitsOtlpTraces_DD.verified.txt`
- Create: `tracer/test/snapshots/WebRequestTests.SubmitsOtlpTraces_DD_OtelSemantics.verified.txt`

**Interfaces:**
- Consumes: everything produced by Tasks 1-3
- Produces: nothing downstream

- [ ] **Step 1: Write the test method (it will fail — no snapshot exists yet)**

Add to `WebRequestTests`, after `SubmitsTracesV1WithOpenTelemetrySemantics`:

```csharp
        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RequiresDockerDependency", "true")]
        [Trait("DockerGroup", "1")]
        [InlineData("http/json", false)]
        [InlineData("http/json", true)]
        [InlineData("http/protobuf", false)]
        [InlineData("http/protobuf", true)]
        public async Task SubmitsOtlpTraces(string protocol, bool openTelemetrySemanticsEnabled)
        {
            SetInstrumentationVerification();

            var isJson = protocol == "http/json";
            var names = OtlpFieldNames.For(isJson);
            var testAgentHost = Environment.GetEnvironmentVariable("TEST_AGENT_HOST") ?? "127.0.0.1";

            await OtlpSnapshotHelper.ClearTestAgentSessionAsync(testAgentHost);

            var httpPort = TcpPortProvider.GetOpenPort();
            Output.WriteLine($"Assigning port {httpPort} for the httpPort.");

            // OpenTelemetry semantics unilaterally force the v0 schema, so pin v0 for the
            // semantics-off case too and keep the two snapshots directly comparable.
            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", "v0");
            SetEnvironmentVariable("DD_TRACE_OTEL_SEMANTICS_ENABLED", openTelemetrySemanticsEnabled.ToString());

            // OTEL_TRACES_EXPORTER=otlp is what makes the Datadog SDK emit OTLP instead of msgpack
            SetEnvironmentVariable("OTEL_TRACES_EXPORTER", "otlp");
            SetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL", protocol);
            SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", $"http://{testAgentHost}:4318");

            var applicationStartTimeUnixNano = DateTimeOffset.UtcNow.ToUnixTimeNanoseconds();

            // Traces go to the test-agent over OTLP, but telemetry still goes to the mock agent
            using var telemetry = this.ConfigureTelemetry();
            using var agent = EnvironmentHelper.GetMockAgent();
            using ProcessResult processResult = await RunSampleAndWaitForExit(agent, arguments: $"Port={httpPort}");

            var tracesRequests = await OtlpSnapshotHelper.WaitForTestAgentDataAsync($"http://{testAgentHost}:4318/test/session/traces");
            tracesRequests.Should().NotBeNullOrEmpty();

            OtlpSnapshotHelper.NormalizeResourceAttributes(tracesRequests, names);
            OtlpSnapshotHelper.NormalizeSpans(tracesRequests, names, applicationStartTimeUnixNano);
            NormalizeWebRequestSpans(tracesRequests, names);
            OtlpSnapshotHelper.SortSpanAttributes(tracesRequests);

            // Sort by name, then by the request URL, then by the span's own normalized JSON.
            // IDs and timestamps are already normalized, so the last key is total: any two spans
            // that still tie are byte-identical and their order cannot affect the snapshot.
            var merged = OtlpSnapshotHelper.MergeDatadogRequests(
                tracesRequests,
                names,
                spans => spans.OrderBy(s => s["name"]?.ToString() ?? string.Empty, StringComparer.Ordinal)
                              .ThenBy(s => OtlpSnapshotHelper.GetAttributeStringValue(s, names, "url.full", "http.url") ?? string.Empty, StringComparer.Ordinal)
                              .ThenBy(s => s.ToString(Formatting.None), StringComparer.Ordinal));

            var finalJson = merged.ToString(Formatting.Indented);

            var settings = VerifyHelper.GetSpanVerifierSettings();
#if NETCOREAPP
            // different TFMs use different underlying handlers, which we don't really care about for the snapshots
            settings.AddSimpleScrubber("System.Net.Http.HttpClientHandler", "System.Net.Http.SocketsHttpHandler");
#endif
            if (!isJson)
            {
                OtlpSnapshotHelper.AddProtobufToJsonScrubbers(settings);
            }

            var suffix = openTelemetrySemanticsEnabled ? "_OtelSemantics" : string.Empty;
            await Verifier.Verify(finalJson, settings)
                          .UseFileName($"{nameof(WebRequestTests)}.{nameof(SubmitsOtlpTraces)}_DD{suffix}")
                          .DisableRequireUniquePrefix();

            await telemetry.AssertIntegrationEnabledAsync(IntegrationId.WebRequest);
            VerifyInstrumentation(processResult.Process);
        }
```

New usings for `WebRequestTests.cs`: `System`, `Datadog.Trace.ClrProfiler.IntegrationTests.Helpers` (already present), `Datadog.Trace.ExtensionMethods` (for `ToUnixTimeNanoseconds`), `Datadog.Trace.Vendors.Newtonsoft.Json` (for `Formatting`), `Datadog.Trace.Vendors.Newtonsoft.Json.Linq` (for `JToken`/`JObject`/`JTokenType`).

- [ ] **Step 2: Add the WebRequest-specific normalization**

Add as a private method on `WebRequestTests`:

```csharp
        /// <summary>
        /// Normalizes the parts of the OTLP payload that are specific to this sample: the randomly
        /// assigned listener port, and the one span whose shape changed on .NET 9.
        /// </summary>
        private void NormalizeWebRequestSpans(JToken tracesRequests, OtlpFieldNames names)
        {
            // The sample's HttpListener binds a random port each run. url.full is covered by
            // VerifyHelper's localhost:<port> scrubber, but server.port carries the bare number.
            foreach (var attribute in tracesRequests.SelectTokens("$..spans[*].attributes[?(@.key == 'server.port')]"))
            {
                if (attribute["value"] is JObject value)
                {
                    foreach (var property in value.Properties())
                    {
                        // Preserve the value kind (stringValue vs intValue) so http/json and
                        // http/protobuf still render identically after scrubbing.
                        property.Value = property.Value.Type == JTokenType.String ? (JToken)"8080" : (JToken)8080;
                    }
                }
            }

#if NET9_0_OR_GREATER
            // .NET 9.0 changed the behaviour when AllowWriteStreamBuffering=false
            // The net result is that we end up creating a "WebRequest" span instead
            // of an "HttpClient" span in one of the cases. Rather than creating a whole
            // separate set of snapshots for .NET 9+, just "fixing" that one span instead.
            var rogueSpan = tracesRequests
                           .SelectTokens("$..spans[*]")
                           .SingleOrDefault(s => OtlpSnapshotHelper.GetAttributeStringValue(s, names, "url.full", "http.url")
                                                                   ?.EndsWith("?BeginGetResponseAsync_NoBuffering") == true);

            // it should never be null, but fall through to fail the snapshots for easier debuggability if it is
            if (rogueSpan is not null)
            {
                Output.WriteLine("Updating span with HttpClient tags");
                OtlpSnapshotHelper.SetAttributeStringValue(rogueSpan, names, "component", "HttpMessageHandler"); // previously "WebRequest"
                OtlpSnapshotHelper.SetAttributeStringValue(rogueSpan, names, "http-client-handler-type", "System.Net.Http.SocketsHttpHandler"); // previously not set
            }
#endif
        }
```

`SortSpanAttributes` runs *after* this method in Step 1 precisely so the appended `http-client-handler-type` lands in key order rather than at the end of the array.

- [ ] **Step 3: Build and run one case to watch it fail**

Run:
```bash
dotnet build tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/Datadog.Trace.ClrProfiler.IntegrationTests.csproj -f net10.0
dotnet test tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/Datadog.Trace.ClrProfiler.IntegrationTests.csproj \
  -f net10.0 --no-build \
  --filter "FullyQualifiedName~WebRequestTests.SubmitsOtlpTraces"
```
Expected: FAIL — Verify reports a new `.received.txt` with no matching `.verified.txt`. Any *other* failure (no traces returned, assertion on trace ID format, `SingleOrDefault` throwing on multiple matches) is a real bug: fix it before accepting anything.

- [ ] **Step 4: Inspect the received snapshots before accepting them**

Run:
```bash
ls tracer/test/snapshots/WebRequestTests.SubmitsOtlpTraces*.received.txt
grep -nE '"(stringValue|string_value)": "[^"]*(:[0-9]{4,5})' tracer/test/snapshots/WebRequestTests.SubmitsOtlpTraces_DD.received.txt | head
grep -n "server.port" -A 3 tracer/test/snapshots/WebRequestTests.SubmitsOtlpTraces_DD.received.txt | head -8
```
Expected: exactly two `.received.txt` files. No raw port numbers, GUIDs, absolute paths, hostnames, or non-zero `timeUnixNano` values anywhere. `server.port` renders as `8080`. Confirm `url.full`/`http.url` shows `localhost:00000`.

Also confirm the two files genuinely differ in the expected way — the `_OtelSemantics` one should carry `http.request.method`, `url.full`, `server.address`, `server.port`, `http.response.status_code`; the other should carry the v0 Datadog tag names:

```bash
diff <(grep -oE '"key": "[^"]+"' tracer/test/snapshots/WebRequestTests.SubmitsOtlpTraces_DD.received.txt | sort -u) \
     <(grep -oE '"key": "[^"]+"' tracer/test/snapshots/WebRequestTests.SubmitsOtlpTraces_DD_OtelSemantics.received.txt | sort -u)
```

- [ ] **Step 5: Accept the snapshots**

```bash
for f in tracer/test/snapshots/WebRequestTests.SubmitsOtlpTraces*.received.txt; do
  mv "$f" "${f%.received.txt}.verified.txt"
done
```

- [ ] **Step 6: Re-run twice to prove stability**

Run the Step 3 test command twice in a row.
Expected: PASS both times, and `git status --porcelain tracer/test/snapshots/` shows only the two new `.verified.txt` files as untracked — no `.received.txt` files. A `.received.txt` appearing here means the output is not deterministic; the sort key or a normalization step is incomplete.

- [ ] **Step 7: Regression-check the existing msgpack tests**

Run:
```bash
dotnet test tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/Datadog.Trace.ClrProfiler.IntegrationTests.csproj \
  -f net10.0 --no-build \
  --filter "FullyQualifiedName~WebRequestTests.SubmitsTraces|FullyQualifiedName~WebRequestTests.TracingDisabled"
git status --porcelain tracer/test/snapshots/
```
Expected: all PASS, and `git status` lists only the two new untracked `.verified.txt` files. `WebRequestTests_v0/_v1/_otel` must be unmodified.

- [ ] **Step 8: Commit**

```bash
git add tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/WebRequestTests.cs \
        tracer/test/snapshots/WebRequestTests.SubmitsOtlpTraces_DD.verified.txt \
        tracer/test/snapshots/WebRequestTests.SubmitsOtlpTraces_DD_OtelSemantics.verified.txt
git commit -m "test: add OTLP snapshot tests for Samples.WebRequest"
```

---

## Final verification

- [ ] `git status --porcelain tracer/test/snapshots/` is clean apart from the two intended new files.
- [ ] `git diff --stat HEAD~4 -- tracer/test/snapshots/` shows **only** additions of the two new snapshots — zero modifications to existing ones.
- [ ] `dotnet build tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/Datadog.Trace.ClrProfiler.IntegrationTests.csproj -f net10.0` is warning-clean.
- [ ] `docker compose stop test-agent` when finished.
