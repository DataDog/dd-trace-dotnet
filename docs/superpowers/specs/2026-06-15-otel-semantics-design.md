# OTel Semantics Feature Design

**Date:** 2026-06-15
**Scope:** HTTP integrations only
**Status:** Approved

## Overview

A new opt-in feature controlled by `DD_TRACE_OTEL_SEMANTICS_ENABLED=true` that causes HTTP span tags to be emitted using OpenTelemetry semantic convention attribute names instead of Datadog convention names. When enabled, OTel mode supersedes `DD_TRACE_SPAN_ATTRIBUTE_SCHEMA` (v0/v1) for tag naming. Only tags change — span name and type are unaffected.

## Configuration

- **Environment variable:** `DD_TRACE_OTEL_SEMANTICS_ENABLED`
- **Default:** `false`
- **C# constant:** `ConfigurationKeys.OtelSemanticsEnabled`
- **TracerSettings property:** `bool OtelSemanticsEnabled { get; }`
- **Registration:** Added to `supported-configurations.yaml` and read via `config.WithKeys(ConfigurationKeys.OtelSemanticsEnabled).AsBool(defaultValue: false)`
- **Schema version interaction:** When `OtelSemanticsEnabled=true`, schema version is ignored for tag naming decisions.

## Attribute Mapping

### HTTP Client (`HttpMessageHandler`, `WebRequest`)

| Datadog attribute | OTel attribute | Notes |
|---|---|---|
| `http.method` | `http.request.method` | |
| `http.status_code` | `http.response.status_code` | |
| `http.url` | `url.full` | Full URL string |
| `http.url` | `url.scheme` | Decomposed from URL |
| `http.url` | `url.path` | Decomposed from URL |
| `http.url` | `url.query` | Decomposed from URL, omitted if empty |
| `out.host` | `server.address` | |
| `peer.service` | `peer.service` | Unchanged |
| `http-client-handler-type` | `http-client-handler-type` | Unchanged |
| `_dd.peer.service.source` | `_dd.peer.service.source` | Unchanged |
| `peer.service.remapped_from` | `peer.service.remapped_from` | Unchanged |

### HTTP Server (`AspNetCore`, `AspNet`, `AspNetWebApi2`)

| Datadog attribute | OTel attribute | Notes |
|---|---|---|
| `http.method` | `http.request.method` | |
| `http.status_code` | `http.response.status_code` | |
| `http.url` | `url.full` | Full URL string |
| `http.url` | `url.scheme` | Decomposed from URL |
| `http.url` | `url.path` | Decomposed from URL |
| `http.url` | `url.query` | Decomposed from URL, omitted if empty |
| `http.useragent` | `user_agent.original` | |
| `http.request.headers.host` | `server.address` | |
| `http.client_ip` | `client.address` | |
| `network.client.ip` | `network.peer.address` | |
| `http.route` | `http.route` | Unchanged — same in both conventions |

## Shared Helper

`HttpOtelHelper` (new static class in `Datadog.Trace/ClrProfiler/AutoInstrumentation/Http/`) centralizes all OTel tag-setting:

```csharp
internal static class HttpOtelHelper
{
    public static void SetRequestMethod(ISpan span, string method)
        => span.SetTag("http.request.method", method);

    public static void SetResponseStatusCode(ISpan span, int statusCode)
        => span.SetTag("http.response.status_code", statusCode.ToString());

    public static void SetUrl(ISpan span, string rawUrl)
    {
        span.SetTag("url.full", rawUrl);
        if (Uri.TryParse(rawUrl, out var uri))
        {
            span.SetTag("url.scheme", uri.Scheme);
            span.SetTag("url.path", uri.AbsolutePath);
            if (!string.IsNullOrEmpty(uri.Query))
                span.SetTag("url.query", uri.Query);
        }
    }

    public static void SetServerAddress(ISpan span, string host)
        => span.SetTag("server.address", host);

    public static void SetUserAgent(ISpan span, string ua)
        => span.SetTag("user_agent.original", ua);

    public static void SetClientAddress(ISpan span, string ip)
        => span.SetTag("client.address", ip);

    public static void SetNetworkPeerAddress(ISpan span, string ip)
        => span.SetTag("network.peer.address", ip);
}
```

## Integration Touch Points

Each integration checks `tracer.Settings.OtelSemanticsEnabled` and branches:

```csharp
if (tracer.Settings.OtelSemanticsEnabled)
    HttpOtelHelper.SetRequestMethod(span, method);
else
    span.SetTag(Tags.HttpMethod, method);
```

**Files modified:**
- `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/Http/HttpClient/HttpMessageHandlerCommon.cs`
- `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/Http/WebRequest/WebRequestIntegration.cs`
- `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/AspNetCore/AspNetCoreHttpRequestHandler.cs`
- `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/AspNet/AspNetIntegration.cs`
- `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/AspNetWebApi2/AspNetWebApi2Integration.cs`
- `tracer/src/Datadog.Trace/Configuration/ConfigurationKeys.cs`
- `tracer/src/Datadog.Trace/Configuration/TracerSettings.cs`
- `tracer/src/Datadog.Trace/Configuration/supported-configurations.yaml`

**Files created:**
- `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/Http/HttpOtelHelper.cs`

## Test Strategy

### Test Infrastructure

`SpanMetadataAPI.cs` gets an `"otel"` dispatch case for each HTTP integration:

```csharp
public static Result IsHttpMessageHandler(this MockSpan span, string metadataSchemaVersion) =>
    metadataSchemaVersion switch
    {
        "otel" => span.IsHttpClientRequestOTel(),
        "v1"   => span.IsHttpMessageHandlerV1(),
        _      => span.IsHttpMessageHandlerV0(),
    };
```

Applied to: `IsHttpMessageHandler`, `IsWebRequest`, `IsAspNetCore`, `IsAspNetCoreMvc`, `IsAspNet`, `IsAspNetWebApi2`.

### Integration Tests

Each HTTP integration test class gets a new `[Theory]` method that:
1. Sets `DD_TRACE_OTEL_SEMANTICS_ENABLED=true` in the environment
2. Passes `"otel"` as `metadataSchemaVersion` to `ValidateIntegrationSpan`
3. Validates spans against `SpanMetadataOTelRules` via the dispatch above

> **Note:** `AspNet` and `AspNetWebApi2` are .NET Framework-only integrations. Their tag-switching code will be implemented, but their integration tests require a Windows/.NET Framework environment and are **out of scope** for the `net10.0` E2E verification loop below.

### E2E Verification Loop

```bash
# 1. Build tracer home
./tracer/build.sh

# 2. HTTP client — HttpMessageHandler
./tracer/build.sh BuildAndRunIntegrationTests \
  --framework net10.0 \
  --filter "HttpMessageHandlerTests" \
  --SampleName "Samples.HttpMessageHandler"

# 3. HTTP client — WebRequest
./tracer/build.sh BuildAndRunIntegrationTests \
  --framework net10.0 \
  --filter "WebRequestTests" \
  --SampleName "Samples.WebRequest"

# 4. HTTP server — AspNetCore MVC
./tracer/build.sh BuildAndRunIntegrationTests \
  --framework net10.0 \
  --filter "AspNetCoreTests" \
  --SampleName "Samples.AspNetCoreMvc31"

# 5. HTTP server — AspNetCore Razor Pages
./tracer/build.sh BuildAndRunIntegrationTests \
  --framework net10.0 \
  --filter "AspNetCoreTests" \
  --SampleName "Samples.AspNetCoreRazorPages"
```
