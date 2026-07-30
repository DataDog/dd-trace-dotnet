# OTel Semantics Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement `DD_TRACE_OTEL_SEMANTICS_ENABLED=true` opt-in flag that replaces Datadog HTTP span attribute names with OpenTelemetry semantic convention names (e.g. `http.request.method` instead of `http.method`).

**Architecture:** A new bool setting `OtelSemanticsEnabled` gates two code paths — HTTP client tags (set in `ScopeFactory.CreateInactiveOutboundHttpSpan`) and HTTP server tags (set in `SpanExtensions.DecorateWebServerSpan` and `SpanExtensions.SetHttpStatusCode`). A shared `HttpOtelHelper` class centralizes all OTel `SetTag` calls. When OTel mode is on, Datadog typed-tag properties are skipped and OTel attributes are set via `span.SetTag()` directly.

**Tech Stack:** C# / .NET; xUnit integration tests; `./tracer/build.sh` Nuke build; YAML-driven config source generators.

---

## File Map

**Created:**
- `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/Http/HttpOtelHelper.cs` — all OTel `SetTag` helpers

**Modified:**
- `tracer/src/Datadog.Trace/Configuration/supported-configurations.yaml` — add `DD_TRACE_OTEL_SEMANTICS_ENABLED` entry
- `tracer/src/Datadog.Trace/Configuration/TracerSettings.cs` — add `OtelSemanticsEnabled` property
- `tracer/src/Datadog.Trace/ClrProfiler/ScopeFactory.cs` — OTel branch for HTTP client tags
- `tracer/src/Datadog.Trace/ExtensionMethods/SpanExtensions.cs` — OTel branch in `DecorateWebServerSpan` and `SetHttpStatusCode`
- `tracer/src/Datadog.Trace/PlatformHelpers/AspNetCoreHttpRequestHandler.cs` — OTel branch after `AddIpToTags`
- `tracer/test/Datadog.Trace.TestHelpers/SpanMetadataAPI.cs` — add `"otel"` dispatch cases for HTTP integrations
- `tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/HttpMessageHandlerTests.cs` — add `SubmitsTracesOTel` test
- `tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/WebRequestTests.cs` — add `SubmitsTracesOTel` test
- `tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/AspNetCore/AspNetCoreMvcTestBase.cs` — add `"otel"` dispatch to `ValidateIntegrationSpan` and add `SubmitsTracesOTel` test

---

## Task 1: Add Config Key to YAML and Regenerate

**Files:**
- Modify: `tracer/src/Datadog.Trace/Configuration/supported-configurations.yaml`

- [ ] **Step 1: Open the YAML file and locate the `DD_APM_TRACING_ENABLED` entry (around line 146) as a formatting reference.**

```bash
grep -n "DD_APM_TRACING_ENABLED" tracer/src/Datadog.Trace/Configuration/supported-configurations.yaml
```

Expected output: a line number around 146.

- [ ] **Step 2: Add the new config key entry immediately before `DD_TRACE_OTEL_SEMANTICS_ENABLED`'s alphabetical neighbor in the file (or any convenient location near other `DD_TRACE_*` boolean keys). Search for a good insertion point:**

```bash
grep -n "DD_TRACE_SPAN_ATTRIBUTE_SCHEMA" tracer/src/Datadog.Trace/Configuration/supported-configurations.yaml
```

- [ ] **Step 3: Insert the new YAML entry after `DD_TRACE_SPAN_ATTRIBUTE_SCHEMA` (or an appropriate alphabetical location). The entry must be:**

```yaml
  DD_TRACE_OTEL_SEMANTICS_ENABLED:
  - implementation: A
    scope: managed
    type: boolean
    default: 'false'
    const_name: OtelSemanticsEnabled
    documentation: |-
      Configuration key for enabling OpenTelemetry semantic convention attribute names on HTTP spans.
      When true, HTTP span attributes use OTel naming (e.g. <c>http.request.method</c>)
      instead of Datadog naming (e.g. <c>http.method</c>). Supersedes <c>DD_TRACE_SPAN_ATTRIBUTE_SCHEMA</c>.
      Default value is <c>false</c> (disabled).
      <seealso cref="Datadog.Trace.Configuration.TracerSettings.OtelSemanticsEnabled"/>
```

- [ ] **Step 4: Build `Datadog.Trace` to trigger the source generator:**

```bash
dotnet build tracer/src/Datadog.Trace/Datadog.Trace.csproj
```

Expected: build succeeds. The generator produces/updates files under `tracer/src/Datadog.Trace/Generated/`.

- [ ] **Step 5: Verify the constant was generated:**

```bash
grep -r "OtelSemanticsEnabled" tracer/src/Datadog.Trace/Generated/
```

Expected output: a line like `public const string OtelSemanticsEnabled = "DD_TRACE_OTEL_SEMANTICS_ENABLED";` in `ConfigurationKeys.g.cs`.

- [ ] **Step 6: Commit**

```bash
git add tracer/src/Datadog.Trace/Configuration/supported-configurations.yaml
git add tracer/src/Datadog.Trace/Generated/
git commit -m "[Config] Add DD_TRACE_OTEL_SEMANTICS_ENABLED configuration key"
```

---

## Task 2: Add OtelSemanticsEnabled Property to TracerSettings

**Files:**
- Modify: `tracer/src/Datadog.Trace/Configuration/TracerSettings.cs`

The pattern to follow is the existing `ApmTracingEnabled` bool property (lines 99–101 for the constructor read, line 786 for the property declaration).

- [ ] **Step 1: Add the config read in the constructor. Find the `ApmTracingEnabled` read:**

```bash
grep -n "ApmTracingEnabled" tracer/src/Datadog.Trace/Configuration/TracerSettings.cs
```

- [ ] **Step 2: Add the `OtelSemanticsEnabled` read immediately after the `ApmTracingEnabled` read (around line 102). Insert:**

```csharp
            OtelSemanticsEnabled = config
                                         .WithKeys(ConfigurationKeys.OtelSemanticsEnabled)
                                         .AsBool(defaultValue: false);
```

- [ ] **Step 3: Add the property declaration. Find where `ApmTracingEnabled` property is declared:**

```bash
grep -n "internal bool ApmTracingEnabled" tracer/src/Datadog.Trace/Configuration/TracerSettings.cs
```

- [ ] **Step 4: Add `OtelSemanticsEnabled` property immediately after `ApmTracingEnabled` (around line 787):**

```csharp
        /// <summary>
        /// Gets a value indicating whether OTel semantic convention attribute names are used for HTTP spans.
        /// Default is <c>false</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.OtelSemanticsEnabled"/>
        internal bool OtelSemanticsEnabled { get; }
```

- [ ] **Step 5: Build to confirm no errors:**

```bash
dotnet build tracer/src/Datadog.Trace/Datadog.Trace.csproj
```

Expected: build succeeds, no errors.

- [ ] **Step 6: Commit**

```bash
git add tracer/src/Datadog.Trace/Configuration/TracerSettings.cs
git commit -m "[Config] Add OtelSemanticsEnabled TracerSettings property"
```

---

## Task 3: Create HttpOtelHelper

**Files:**
- Create: `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/Http/HttpOtelHelper.cs`

This helper centralizes all OTel `SetTag` calls. Note: the `url.query` attribute is omitted when the query string is empty, and `server.port` is derived from the parsed URI.

- [ ] **Step 1: Create the file:**

```csharp
// <copyright file="HttpOtelHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Http
{
    internal static class HttpOtelHelper
    {
        internal static void SetRequestMethod(ISpan span, string method)
            => span.SetTag("http.request.method", method);

        internal static void SetResponseStatusCode(ISpan span, int statusCode)
            => span.SetTag("http.response.status_code", statusCode.ToString());

        internal static void SetClientUrl(ISpan span, string rawUrl)
        {
            if (StringUtil.IsNullOrEmpty(rawUrl))
            {
                return;
            }

            span.SetTag("url.full", rawUrl);

            if (Uri.TryParse(rawUrl, out var uri))
            {
                span.SetTag("url.scheme", uri.Scheme);
                span.SetTag("url.path", uri.AbsolutePath);
                if (!StringUtil.IsNullOrEmpty(uri.Query))
                {
                    span.SetTag("url.query", uri.Query);
                }

                if (uri.Port > 0)
                {
                    span.SetTag("server.port", uri.Port.ToString());
                }
            }
        }

        internal static void SetServerUrl(ISpan span, string rawUrl)
        {
            if (StringUtil.IsNullOrEmpty(rawUrl))
            {
                return;
            }

            span.SetTag("url.full", rawUrl);

            if (Uri.TryParse(rawUrl, out var uri))
            {
                span.SetTag("url.scheme", uri.Scheme);
                span.SetTag("url.path", uri.AbsolutePath);
                if (!StringUtil.IsNullOrEmpty(uri.Query))
                {
                    span.SetTag("url.query", uri.Query);
                }

                if (uri.Port > 0)
                {
                    span.SetTag("server.port", uri.Port.ToString());
                }
            }
        }

        internal static void SetServerAddress(ISpan span, string host)
            => span.SetTag("server.address", host);

        internal static void SetUserAgent(ISpan span, string ua)
            => span.SetTag("user_agent.original", ua);

        internal static void SetClientAddress(ISpan span, string ip)
            => span.SetTag("client.address", ip);

        internal static void SetNetworkPeerAddress(ISpan span, string ip)
            => span.SetTag("network.peer.address", ip);
    }
}
```

- [ ] **Step 2: Build to confirm no errors:**

```bash
dotnet build tracer/src/Datadog.Trace/Datadog.Trace.csproj
```

Expected: build succeeds.

- [ ] **Step 3: Commit**

```bash
git add tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/Http/HttpOtelHelper.cs
git commit -m "[HTTP] Add HttpOtelHelper for OTel semantic convention tag helpers"
```

---

## Task 4: Modify ScopeFactory for HTTP Client OTel Tags

**Files:**
- Modify: `tracer/src/Datadog.Trace/ClrProfiler/ScopeFactory.cs`

In `CreateInactiveOutboundHttpSpan` (line 79), the HTTP client tags are set at lines 123–128:
```csharp
tags.HttpMethod = httpMethod?.ToUpperInvariant();
if (requestUri is not null)
{
    tags.HttpUrl = HttpRequestUtils.GetUrl(requestUri, tracer.TracerManager.QueryStringManager);
    tags.Host = HttpRequestUtils.GetNormalizedHost(requestUri.Host);
}
```

In OTel mode, these typed-tag properties must NOT be set (they would emit Datadog attribute names). Instead, we call `HttpOtelHelper` methods on the span directly.

- [ ] **Step 1: Add the `using` directive for `HttpOtelHelper`. Find the existing usings at the top of `ScopeFactory.cs` and add:**

```csharp
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Http;
```

- [ ] **Step 2: Replace the tag-setting block (lines 123–128) with an OTel branch. The new code:**

```csharp
                if (tracer.Settings.OtelSemanticsEnabled)
                {
                    HttpOtelHelper.SetRequestMethod(span, httpMethod?.ToUpperInvariant());
                    if (requestUri is not null)
                    {
                        HttpOtelHelper.SetClientUrl(span, HttpRequestUtils.GetUrl(requestUri, tracer.TracerManager.QueryStringManager));
                        HttpOtelHelper.SetServerAddress(span, HttpRequestUtils.GetNormalizedHost(requestUri.Host));
                    }
                }
                else
                {
                    tags.HttpMethod = httpMethod?.ToUpperInvariant();
                    if (requestUri is not null)
                    {
                        tags.HttpUrl = HttpRequestUtils.GetUrl(requestUri, tracer.TracerManager.QueryStringManager);
                        tags.Host = HttpRequestUtils.GetNormalizedHost(requestUri.Host);
                    }
                }
```

- [ ] **Step 3: Build to confirm no errors:**

```bash
dotnet build tracer/src/Datadog.Trace/Datadog.Trace.csproj
```

Expected: build succeeds.

- [ ] **Step 4: Commit**

```bash
git add tracer/src/Datadog.Trace/ClrProfiler/ScopeFactory.cs
git commit -m "[HTTP] Switch HTTP client span tags to OTel attributes when OtelSemanticsEnabled"
```

---

## Task 5: Modify SpanExtensions for HTTP Server OTel Tags

**Files:**
- Modify: `tracer/src/Datadog.Trace/ExtensionMethods/SpanExtensions.cs`

`DecorateWebServerSpan` (line 47) currently sets `tags.HttpMethod`, `tags.HttpRequestHeadersHost`, `tags.HttpUrl`, `tags.HttpUserAgent`. In OTel mode these must be replaced with OTel attributes.

`DecorateWebServerSpan` does not currently accept a settings/bool parameter. The caller `AspNetCoreHttpRequestHandler.StartAspNetCorePipelineScope` has `tracer`, so we add `bool otelSemanticsEnabled = false` to the signature.

`SetHttpStatusCode` (line 96) already takes `MutableSettings tracerSettings`, so it can read `OtelSemanticsEnabled` from that object.

- [ ] **Step 1: Add the `using` directive for `HttpOtelHelper`:**

```csharp
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Http;
```

- [ ] **Step 2: Replace `DecorateWebServerSpan` signature and body. The current signature is:**

```csharp
internal static void DecorateWebServerSpan(
    this ISpan span,
    string resourceName,
    string method,
    string host,
    string httpUrl,
    string userAgent,
    WebTags tags)
```

Replace with:

```csharp
internal static void DecorateWebServerSpan(
    this ISpan span,
    string resourceName,
    string method,
    string host,
    string httpUrl,
    string userAgent,
    WebTags tags,
    bool otelSemanticsEnabled = false)
{
    span.Type = SpanTypes.Web;
    span.ResourceName = resourceName?.Trim();

    if (otelSemanticsEnabled)
    {
        HttpOtelHelper.SetRequestMethod(span, method);
        HttpOtelHelper.SetServerUrl(span, httpUrl);
        HttpOtelHelper.SetServerAddress(span, host);
        HttpOtelHelper.SetUserAgent(span, userAgent);
    }
    else if (tags is not null)
    {
        tags.HttpMethod = method;
        tags.HttpRequestHeadersHost = host;
        tags.HttpUrl = httpUrl;
        tags.HttpUserAgent = userAgent;
    }
}
```

- [ ] **Step 3: Modify `SetHttpStatusCode` to use the OTel attribute name when OTel mode is on. The current block at line 104 is:**

```csharp
string statusCodeString = ConvertStatusCodeToString(statusCode);

if (span.Tags is IHasStatusCode statusCodeTags)
{
    statusCodeTags.HttpStatusCode = statusCodeString;
}
else
{
    span.SetTag(Tags.HttpStatusCode, statusCodeString);
}
```

Replace with:

```csharp
string statusCodeString = ConvertStatusCodeToString(statusCode);

if (tracerSettings.OtelSemanticsEnabled)
{
    span.SetTag("http.response.status_code", statusCodeString);
}
else if (span.Tags is IHasStatusCode statusCodeTags)
{
    statusCodeTags.HttpStatusCode = statusCodeString;
}
else
{
    span.SetTag(Tags.HttpStatusCode, statusCodeString);
}
```

- [ ] **Step 4: Build to confirm no errors:**

```bash
dotnet build tracer/src/Datadog.Trace/Datadog.Trace.csproj
```

Expected: build succeeds.

- [ ] **Step 5: Check that `MutableSettings` (the type of `tracerSettings`) exposes `OtelSemanticsEnabled`. `MutableSettings` is a read-only view of `TracerSettings`:**

```bash
grep -n "OtelSemanticsEnabled\|class MutableSettings" tracer/src/Datadog.Trace/Configuration/TracerSettings.cs | head -20
```

If `OtelSemanticsEnabled` is only on `TracerSettings` (not on an interface), verify that `MutableSettings` provides access to it. If not, add it to the appropriate interface or read it via `tracerSettings` directly (the exact type at the call site may be `ImmutableTracerSettings` or `MutableSettings`). Fix accordingly.

- [ ] **Step 6: Commit**

```bash
git add tracer/src/Datadog.Trace/ExtensionMethods/SpanExtensions.cs
git commit -m "[HTTP] Switch HTTP server span tags and status code to OTel attributes when OtelSemanticsEnabled"
```

---

## Task 6: Wire OtelSemanticsEnabled to DecorateWebServerSpan Callers

**Files:**
- Modify: `tracer/src/Datadog.Trace/PlatformHelpers/AspNetCoreHttpRequestHandler.cs`

The call site for `DecorateWebServerSpan` is line 138 in `StartAspNetCorePipelineScope`. The `tracer` variable is in scope there. Add `otelSemanticsEnabled: tracer.Settings.OtelSemanticsEnabled` to the call.

Also: after `AddIpToTags` (line 205), remap the IP tags to OTel attributes when OTel mode is on — by reading the values that were just set on `tags` and calling `HttpOtelHelper`, then nulling the Datadog typed-tag properties so they are not double-emitted.

- [ ] **Step 1: Update the `DecorateWebServerSpan` call at line 138. Change:**

```csharp
scope.Span.DecorateWebServerSpan(resourceName, httpMethod, host, url, userAgent, tags);
```

to:

```csharp
scope.Span.DecorateWebServerSpan(resourceName, httpMethod, host, url, userAgent, tags, otelSemanticsEnabled: tracer.Settings.OtelSemanticsEnabled);
```

- [ ] **Step 2: Update the IP tag handling. The `AddIpToTags` call is at line 205. After it, add the OTel remap block:**

```csharp
                Headers.Ip.RequestIpExtractor.AddIpToTags(peerIp, request.IsHttps, GetRequestHeaderFromKey, tracer.Settings.IpHeader, tags);

                if (tracer.Settings.OtelSemanticsEnabled)
                {
                    if (tags.NetworkClientIp is not null)
                    {
                        HttpOtelHelper.SetNetworkPeerAddress(scope.Span, tags.NetworkClientIp);
                        tags.NetworkClientIp = null;
                    }

                    if (tags.HttpClientIp is not null)
                    {
                        HttpOtelHelper.SetClientAddress(scope.Span, tags.HttpClientIp);
                        tags.HttpClientIp = null;
                    }
                }
```

- [ ] **Step 3: Add the `using` directive if not already present:**

```csharp
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Http;
```

- [ ] **Step 4: Build to confirm no errors:**

```bash
dotnet build tracer/src/Datadog.Trace/Datadog.Trace.csproj
```

Expected: build succeeds.

- [ ] **Step 5: Commit**

```bash
git add tracer/src/Datadog.Trace/PlatformHelpers/AspNetCoreHttpRequestHandler.cs
git commit -m "[HTTP] Pass OtelSemanticsEnabled to DecorateWebServerSpan and remap IP tags"
```

---

## Task 7: Add "otel" Dispatch Cases to SpanMetadataAPI

**Files:**
- Modify: `tracer/test/Datadog.Trace.TestHelpers/SpanMetadataAPI.cs`

The test framework dispatches schema version strings to the right validation rule method. We need `"otel"` cases for the four HTTP integrations.

- [ ] **Step 1: Find the `IsHttpMessageHandler` method (around line 212):**

```bash
grep -n "IsHttpMessageHandler\|IsWebRequest\|IsAspNetCore\b\|IsAspNetCoreMvc\b" tracer/test/Datadog.Trace.TestHelpers/SpanMetadataAPI.cs
```

- [ ] **Step 2: Add `"otel"` dispatch to `IsHttpMessageHandler`:**

```csharp
public static Result IsHttpMessageHandler(this MockSpan span, string metadataSchemaVersion) =>
    metadataSchemaVersion switch
    {
        "otel" => span.IsHttpClientRequestOTel(),
        "v1" => span.IsHttpMessageHandlerV1(),
        _ => span.IsHttpMessageHandlerV0(),
    };
```

- [ ] **Step 3: Add `"otel"` dispatch to `IsWebRequest`:**

```csharp
public static Result IsWebRequest(this MockSpan span, string metadataSchemaVersion) =>
    metadataSchemaVersion switch
    {
        "otel" => span.IsHttpClientRequestOTel(),
        "v1" => span.IsWebRequestV1(),
        _ => span.IsWebRequestV0(),
    };
```

- [ ] **Step 4: Add `"otel"` dispatch to `IsAspNetCore`:**

```csharp
public static Result IsAspNetCore(this MockSpan span, string metadataSchemaVersion, ISet<string> excludeTags = null) =>
    metadataSchemaVersion switch
    {
        "otel" => span.IsAspNetCoreOTel(excludeTags),
        "v1" => span.IsAspNetCoreV1(excludeTags),
        _ => span.IsAspNetCoreV0(excludeTags),
    };
```

- [ ] **Step 5: Add `"otel"` dispatch to `IsAspNetCoreMvc`:**

```csharp
public static Result IsAspNetCoreMvc(this MockSpan span, string metadataSchemaVersion) =>
    metadataSchemaVersion switch
    {
        "otel" => span.IsAspNetCoreMvcOTel(),
        "v1" => span.IsAspNetCoreMvcV1(),
        _ => span.IsAspNetCoreMvcV0(),
    };
```

- [ ] **Step 6: Build to confirm no errors:**

```bash
dotnet build tracer/test/Datadog.Trace.TestHelpers/Datadog.Trace.TestHelpers.csproj
```

Expected: build succeeds.

- [ ] **Step 7: Commit**

```bash
git add tracer/test/Datadog.Trace.TestHelpers/SpanMetadataAPI.cs
git commit -m "[Test] Add 'otel' dispatch cases to SpanMetadataAPI for HTTP integrations"
```

---

## Task 8: Add OTel Integration Test to HttpMessageHandlerTests

**Files:**
- Modify: `tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/HttpMessageHandlerTests.cs`

The existing `HttpClient_SubmitsTraces` test uses `[MetadataSchemaVersionData]` which only generates `"v0"` and `"v1"` combinations. Add a dedicated `[SkippableTheory]` test that sets `DD_TRACE_OTEL_SEMANTICS_ENABLED=true` and passes `"otel"` as schema version.

Looking at the existing test body (lines 56–145), the OTel test can reuse the same structure but simplify: we don't need the combinatorial `InstrumentationOptions` explosion for the OTel validation pass. A single instrumentation config is sufficient to verify the OTel tag names.

- [ ] **Step 1: Add the test method after `HttpClient_SubmitsTraces`. Insert before the end of the class:**

```csharp
[SkippableTheory]
[Trait("Category", "EndToEnd")]
[Trait("RunOnWindows", "True")]
[CombinatorialOrPairwiseData]
public async Task HttpClient_SubmitsTracesOTel(
    [CombinatorialMemberData(nameof(GetInstrumentationOptions))] InstrumentationOptions instrumentation,
    bool socketsHandlerEnabled)
{
    SetInstrumentationVerification();
    ConfigureInstrumentation(instrumentation, socketsHandlerEnabled);
    SetEnvironmentVariable("DD_TRACE_OTEL_SEMANTICS_ENABLED", "true");

    const string metadataSchemaVersion = "otel";
    var expectedAsyncCount = CalculateExpectedAsyncSpans(instrumentation);
    var expectedSyncCount = CalculateExpectedSyncSpans(instrumentation);
    var expectedSpanCount = expectedAsyncCount + expectedSyncCount;

    int httpPort = TcpPortProvider.GetOpenPort();
    Output.WriteLine($"Assigning port {httpPort} for the httpPort.");

    using var telemetry = this.ConfigureTelemetry();
    using var agent = EnvironmentHelper.GetMockAgent();
    using var processResult = await RunSampleAndWaitForExit(agent, arguments: $"Port={httpPort}");

    agent.SpanFilters.Add(s => s.Type == SpanTypes.Http);
    var spans = await agent.WaitForSpansAsync(expectedSpanCount);
    spans.Should().HaveCount(expectedSpanCount);

    // OTel mode: external span service naming still follows v0 convention
    var clientSpanServiceName = $"{EnvironmentHelper.FullSampleName}-http-client";
    ValidateIntegrationSpans(spans, metadataSchemaVersion, expectedServiceName: clientSpanServiceName, isExternalSpan: true);
    VerifyInstrumentation(processResult.Process);
}
```

- [ ] **Step 2: Build the integration test project to confirm no errors:**

```bash
dotnet build tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/Datadog.Trace.ClrProfiler.IntegrationTests.csproj
```

Expected: build succeeds.

- [ ] **Step 3: Commit**

```bash
git add tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/HttpMessageHandlerTests.cs
git commit -m "[Test] Add HttpClient_SubmitsTracesOTel integration test"
```

---

## Task 9: Add OTel Integration Test to WebRequestTests

**Files:**
- Modify: `tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/WebRequestTests.cs`

The existing pattern uses `RunTest("v0")` / `RunTest("v1")`. Add a `SubmitsTracesOTel` test that calls a `RunTest("otel")` path. The `RunTest` private method already handles the schema version plumbing.

- [ ] **Step 1: Add the public test method after `SubmitsTracesV1`:**

```csharp
[SkippableFact]
[Trait("Category", "EndToEnd")]
[Trait("RunOnWindows", "True")]
[Trait("SupportsInstrumentationVerification", "True")]
public Task SubmitsTracesOTel() => RunTestOTel();
```

- [ ] **Step 2: Add a `RunTestOTel` private method. Review the existing `RunTest(string metadataSchemaVersion)` method for exact structure. The OTel version sets `DD_TRACE_OTEL_SEMANTICS_ENABLED=true` instead of `DD_TRACE_SPAN_ATTRIBUTE_SCHEMA`:**

```csharp
private async Task RunTestOTel()
{
    SetInstrumentationVerification();

    var expectedAllSpansCount = 134;

    int httpPort = TcpPortProvider.GetOpenPort();
    Output.WriteLine($"Assigning port {httpPort} for the httpPort.");

    SetEnvironmentVariable("DD_TRACE_OTEL_SEMANTICS_ENABLED", "true");
    // OTel mode: service name follows v0 external span convention
    var clientSpanServiceName = $"{EnvironmentHelper.FullSampleName}-http-client";

    using var telemetry = this.ConfigureTelemetry();
    using var agent = EnvironmentHelper.GetMockAgent();
    using ProcessResult processResult = await RunSampleAndWaitForExit(agent, arguments: $"Port={httpPort}");

    var allSpans = (await agent.WaitForSpansAsync(expectedAllSpansCount, assertExpectedCount: false)).OrderBy(s => s.Start).ToList();

    allSpans.Should().OnlyHaveUniqueItems(s => new { s.SpanId, s.TraceId });
    var httpSpans = allSpans.Where(s => s.Type == SpanTypes.Http).ToList();
    ValidateIntegrationSpans(httpSpans, "otel", expectedServiceName: clientSpanServiceName, isExternalSpan: true);
    VerifyInstrumentation(processResult.Process);
}
```

- [ ] **Step 3: Build the integration test project:**

```bash
dotnet build tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/Datadog.Trace.ClrProfiler.IntegrationTests.csproj
```

Expected: build succeeds.

- [ ] **Step 4: Commit**

```bash
git add tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/WebRequestTests.cs
git commit -m "[Test] Add SubmitsTracesOTel integration test for WebRequest"
```

---

## Task 10: Add OTel Integration Test to AspNetCore Tests

**Files:**
- Modify: `tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/AspNetCore/AspNetCoreMvcTestBase.cs`

`ValidateIntegrationSpan` (line 92) switches on `span.Name` and dispatches to `IsAspNetCore` / `IsAspNetCoreMvc`. These methods already have `"otel"` dispatch after Task 7, so `ValidateIntegrationSpan` already handles `"otel"` without modification.

The task here is to add a `SubmitsTracesOTel` test method in `AspNetCoreMvcTestBase`. First, find a concrete subclass that runs these tests for `Samples.AspNetCoreMvc31` to understand where to add the actual test.

- [ ] **Step 1: Find the concrete test class for AspNetCoreMvc31:**

```bash
grep -rn "AspNetCoreMvc31\|Mvc31" tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/AspNetCore/ --include="*.cs" | head -20
```

- [ ] **Step 2: Identify the class that constructs with `"AspNetCoreMvc31"` sample. It should look like:**

```csharp
public class AspNetCoreMvc31Tests : AspNetCoreMvcTestBase
{
    public AspNetCoreMvc31Tests(...)
        : base("AspNetCoreMvc31", ...)
    { }
    
    [SkippableTheory]
    ...
    public async Task SubmitsTraces(...) { ... }
}
```

- [ ] **Step 3: Add a `SubmitsTracesOTel` method to `AspNetCoreMvcTestBase`. This method calls the test infrastructure with `metadataSchemaVersion = "otel"` and `DD_TRACE_OTEL_SEMANTICS_ENABLED=true`. Add this method to `AspNetCoreMvcTestBase.cs` (inside the `#if NETCOREAPP` guard):**

```csharp
protected async Task SubmitsTracesOTel()
{
    SetEnvironmentVariable("DD_TRACE_OTEL_SEMANTICS_ENABLED", "true");
    const string metadataSchemaVersion = "otel";

    var testName = GetTestName(nameof(SubmitsTracesOTel));
    using var fixture = Fixture;

    await fixture.TryStartApp(this, enableSecurity: false);
    var spans = await fixture.WaitForSpansAsync();

    ValidateIntegrationSpans(spans.Where(s => s.Type == SpanTypes.Web || s.Name is "aspnet_core.request" or "aspnet_core_mvc.request").ToList(), metadataSchemaVersion);
}
```

> **Note:** Look at an existing `SubmitsTraces` method in the concrete subclass for the exact invocation pattern (fixture startup, span collection, validation call) and adapt accordingly. The key differences are: set `DD_TRACE_OTEL_SEMANTICS_ENABLED=true` and pass `"otel"` as schema version to `ValidateIntegrationSpans`.

- [ ] **Step 4: In the concrete `AspNetCoreMvc31Tests` class (and the RazorPages class), override or add a test that calls `await SubmitsTracesOTel()`:**

```csharp
[SkippableFact]
[Trait("Category", "EndToEnd")]
[Trait("RunOnWindows", "True")]
public async Task SubmitsTracesOTel_Test() => await SubmitsTracesOTel();
```

- [ ] **Step 5: Find and repeat for `AspNetCoreRazorPages` test class:**

```bash
grep -rn "RazorPages\|Samples.AspNetCoreRazorPages" tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/AspNetCore/ --include="*.cs" | head -10
```

Add the same `SubmitsTracesOTel_Test` method to the RazorPages test class.

- [ ] **Step 6: Build:**

```bash
dotnet build tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/Datadog.Trace.ClrProfiler.IntegrationTests.csproj
```

Expected: build succeeds.

- [ ] **Step 7: Commit**

```bash
git add tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/AspNetCore/
git commit -m "[Test] Add SubmitsTracesOTel integration tests for AspNetCore MVC and RazorPages"
```

---

## Task 11: Full Build

Before running integration tests, rebuild the tracer home to pick up all the changes.

- [ ] **Step 1: Build the full tracer:**

```bash
./tracer/build.sh
```

Expected: build completes without errors. This produces the tracer home artifacts.

- [ ] **Step 2: If the build fails with a warning treated as error, investigate and fix before proceeding. Common issues:**
  - Missing `using` directive
  - Property not visible on `MutableSettings` — check if `OtelSemanticsEnabled` is on the right class

---

## Task 12: E2E Verification Loop

Run each HTTP integration test suite against `net10.0` to verify the full OTel path.

- [ ] **Step 1: HttpMessageHandler tests:**

```bash
./tracer/build.sh BuildAndRunIntegrationTests \
  --framework net10.0 \
  --filter "HttpMessageHandlerTests" \
  --SampleName "Samples.HttpMessageHandler"
```

Expected: all tests pass, including `HttpClient_SubmitsTracesOTel`.

- [ ] **Step 2: If a test fails, check the span output. The most common cause is a required tag missing (e.g., `http.request.method` not present). To debug:**
  - Set a breakpoint or add logging in `ScopeFactory.CreateInactiveOutboundHttpSpan`
  - Verify `tracer.Settings.OtelSemanticsEnabled` is true when the `DD_TRACE_OTEL_SEMANTICS_ENABLED=true` env var is set
  - Check that `HttpOtelHelper.SetRequestMethod` is actually being called

- [ ] **Step 3: WebRequest tests:**

```bash
./tracer/build.sh BuildAndRunIntegrationTests \
  --framework net10.0 \
  --filter "WebRequestTests" \
  --SampleName "Samples.WebRequest"
```

Expected: all tests pass, including `SubmitsTracesOTel`.

- [ ] **Step 4: AspNetCore MVC tests:**

```bash
./tracer/build.sh BuildAndRunIntegrationTests \
  --framework net10.0 \
  --filter "AspNetCoreTests" \
  --SampleName "Samples.AspNetCoreMvc31"
```

Expected: all tests pass, including `SubmitsTracesOTel_Test`.

- [ ] **Step 5: AspNetCore RazorPages tests:**

```bash
./tracer/build.sh BuildAndRunIntegrationTests \
  --framework net10.0 \
  --filter "AspNetCoreTests" \
  --SampleName "Samples.AspNetCoreRazorPages"
```

Expected: all tests pass, including `SubmitsTracesOTel_Test`.

- [ ] **Step 6: Verify no regressions in v0/v1 tests. Run a quick smoke check on HttpMessageHandler v0:**

```bash
./tracer/build.sh BuildAndRunIntegrationTests \
  --framework net10.0 \
  --filter "HttpMessageHandlerTests.HttpClient_SubmitsTraces" \
  --SampleName "Samples.HttpMessageHandler"
```

Expected: v0/v1 tests still pass (OTel mode is off by default).

---

## Reference: OTel Attribute Mapping

### HTTP Client spans (`http.out` type):

| Set when OTel=false (Datadog) | Set when OTel=true (OTel) | Source |
|---|---|---|
| `http.method` (via `tags.HttpMethod`) | `http.request.method` | `ScopeFactory` |
| `http.url` (via `tags.HttpUrl`) | `url.full`, `url.scheme`, `url.path`, `url.query`* | `ScopeFactory` / `HttpOtelHelper.SetClientUrl` |
| `out.host` (via `tags.Host`) | `server.address` | `ScopeFactory` / `HttpOtelHelper.SetServerAddress` |
| *(from url)* | `server.port` | `HttpOtelHelper.SetClientUrl` |
| `http.status_code` | `http.response.status_code` | `SpanExtensions.SetHttpStatusCode` |
| `http-client-handler-type` | `http-client-handler-type` | unchanged |
| `peer.service` | `peer.service` | unchanged |
| `_dd.peer.service.source` | `_dd.peer.service.source` | unchanged |
| `peer.service.remapped_from` | `peer.service.remapped_from` | unchanged |

### HTTP Server spans (`web` type):

| Set when OTel=false (Datadog) | Set when OTel=true (OTel) | Source |
|---|---|---|
| `http.method` | `http.request.method` | `SpanExtensions.DecorateWebServerSpan` |
| `http.url` | `url.full`, `url.scheme`, `url.path`, `url.query`* | `SpanExtensions.DecorateWebServerSpan` |
| `http.request.headers.host` | `server.address` | `SpanExtensions.DecorateWebServerSpan` |
| *(from url)* | `server.port` | `SpanExtensions.DecorateWebServerSpan` / `HttpOtelHelper.SetServerUrl` |
| `http.useragent` | `user_agent.original` | `SpanExtensions.DecorateWebServerSpan` |
| `http.status_code` | `http.response.status_code` | `SpanExtensions.SetHttpStatusCode` |
| `network.client.ip` | `network.peer.address` | `AspNetCoreHttpRequestHandler` (post-`AddIpToTags`) |
| `http.client_ip` | `client.address` | `AspNetCoreHttpRequestHandler` (post-`AddIpToTags`) |
| `http.route` | `http.route` | unchanged |

*`url.query` omitted if the query string is empty.
