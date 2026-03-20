# APMSVLS-58: CI Failure Analysis

**Branch**: `lpimentel/APMSVLS-58-azfunc-host-parenting`
**PR**: #7628
**Date**: February 2026

## Overview

This document analyzes CI test failures in the Azure Functions integration tests after the span parenting changes in PR #7628. There are two distinct failure categories.

## Failure 1: ASP.NET Core Tests — Span Count Mismatch

**Affected tests**: `IsolatedRuntimeV4AspNetCore`, `IsolatedRuntimeV4AspNetCoreV1`
**Symptom**: Expected 26 spans, got 31

### Root Cause

The test asserts on unfiltered span count (`spans.Should().HaveCount(expectedSpanCount)` at `AzureFunctionsTests.cs:397`) but the `expectedSpanCount = 26` only accounts for spans after SocketsHttpHandler filtering.

With the observer now enabled for isolated v4 workers, 5 new `aspnet_core.request` spans are created (one per HTTP trigger). The snapshot (`AzureFunctionsTests.Isolated.V4.AspNetCore.verified.txt`) was updated to include these, totaling 26 spans after filtering. However, the SocketsHttpHandler spans (filtered out at line 392) push the raw total to ~31.

The non-ASP.NET Core tests (`IsolatedRuntimeV4` at line 301) correctly assert on `filteredSpans.Should().HaveCount(expectedSpanCount)`, but the ASP.NET Core tests assert on unfiltered `spans`.

### Fix

Either:
1. Update `expectedSpanCount` to match the unfiltered total (~31), or
2. Change the assertion to check `filteredSpans.Count` instead of `spans.Count` (consistent with non-ASP.NET Core tests)

Option 2 is preferred since SocketsHttpHandler span count is non-deterministic (noted in comments at lines 387-391).

### Relevant Code

- `AzureFunctionsTests.cs:383` — `const int expectedSpanCount = 26;`
- `AzureFunctionsTests.cs:392-394` — Filters out SocketsHttpHandler spans
- `AzureFunctionsTests.cs:397` — Asserts on unfiltered `spans` (should use `filteredSpans`)
- `AzureFunctionsTests.cs:251` — Same issue for V1 variant

## Failure 2: Non-ASP.NET Core Tests — Missing Spans

**Affected tests**: `IsolatedRuntimeV4`, `IsolatedRuntimeV4HostLogsDisabled`, `IsolatedRuntimeV4SdkV1`
**Symptom**: Expected 21 spans, got 14 (7 missing)

### Root Cause

`SkipAspNetCoreDiagnosticObserver()` in `Instrumentation.cs:504-549` was changed from:
```csharp
// master: always skip for Azure Functions
return EnvironmentHelpers.IsAzureFunctions();
```
to a nuanced check that enables the observer for ALL isolated v4 workers:
```csharp
// PR: skip only for in-process, host, and non-v4
// returns false (don't skip) for all isolated v4 workers
```

This enables `AspNetCoreDiagnosticObserver` in non-ASP.NET Core isolated workers where it was previously disabled. The non-ASP.NET Core worker (`Samples.AzureFunctions.V4Isolated`) does NOT reference `Microsoft.AspNetCore.App`, but `Microsoft.Azure.Functions.Worker` v2.2.0 may transitively bring in ASP.NET Core dependencies (via `Grpc.AspNetCore` or similar), causing the observer to create unwanted `aspnet_core.request` spans for internal gRPC communication.

The CI failure indicates `azure_functions.invoke` spans with `Http <TriggerName>` resource names are missing, replaced by `GET /api/...` resource names characteristic of `aspnet_core.request` spans.

### Impact on `CreateIsolatedFunctionScope` Logic

The refactored `CreateIsolatedFunctionScope` (`AzureFunctionsCommon.cs:204-355`) should handle the non-ASP.NET Core case correctly:
- `GetAspNetCoreScope()` returns null (no `HttpRequestContext` in `FunctionContext.Items`)
- Falls to else branch, creates `azure_functions.invoke` with `extractedContext.SpanContext` as parent
- Logic is equivalent to master for the non-ASP.NET Core path

The issue is likely caused by the observer creating `aspnet_core.request` spans that interfere with span collection or timing (e.g., `WaitForSpansAsync(21)` returning early with a mix of wanted and unwanted spans).

### The Core Design Problem

`SkipAspNetCoreDiagnosticObserver()` runs at startup and cannot distinguish between ASP.NET Core and non-ASP.NET Core isolated workers. Both share:
- `FUNCTIONS_WORKER_RUNTIME=dotnet-isolated`
- `FUNCTIONS_EXTENSION_VERSION=~4`

The ASP.NET Core variant is only detectable at runtime per-request (via `HttpRequestContext` in `FunctionContext.Items`).

### Fix Options

1. **Assembly detection at startup** — Check if `Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore` assembly is loaded in `AppDomain.CurrentDomain.GetAssemblies()`. Concern: assembly may not be loaded yet at initialization time.

2. **Revert observer to disabled for all Azure Functions** — Keep `return EnvironmentHelpers.IsAzureFunctions()` and find another mechanism for ASP.NET Core span creation.

3. **Keep observer enabled for all v4 isolated** — But ensure non-ASP.NET Core workers handle extra spans gracefully. Requires understanding exactly why 7 spans are lost.

## Span Count Reference

### Non-ASP.NET Core Isolated (21 spans)

From `AzureFunctionsTests.Isolated.verified.txt`:

**Worker (Guid_1)**: 16 spans
- 1x `azure_functions.invoke` Timer TriggerAllTimer
- 1x `azure_functions.invoke` Http TriggerCaller
- 4x `azure_functions.invoke` Http (SimpleHttpTrigger, Exception, ServerError, BadRequest)
- 5x `http.request` outgoing (trigger, simple, exception, error, badrequest)
- 5x Manual spans (Trigger, Simple, Exception, ServerError, BadRequest)

**Host (Guid_2)**: 5 spans
- 5x `azure_functions.invoke` with `GET /api/...` resources

### ASP.NET Core Isolated (26 spans after filtering)

From `AzureFunctionsTests.Isolated.V4.AspNetCore.verified.txt`:

**Worker (Guid_1)**: 21 spans
- 5x `aspnet_core.request` (NEW — one per HTTP trigger endpoint)
- 5x `azure_functions.invoke` Http (children of aspnet_core.request)
- 5x Manual spans
- 5x `http.request` outgoing
- 1x `azure_functions.invoke` Timer TriggerAllTimer

**Host (Guid_2)**: 5 spans
- 5x `azure_functions.invoke` with `GET /api/...` resources

New hierarchy: `aspnet_core.request` -> `azure_functions.invoke` -> Manual/http.request

## Key Files

| File | Lines | Description |
|------|-------|-------------|
| `Instrumentation.cs` | 504-549 | `SkipAspNetCoreDiagnosticObserver()` — controls observer enablement |
| `AzureFunctionsCommon.cs` | 204-355 | `CreateIsolatedFunctionScope()` — span creation logic |
| `AzureFunctionsCommon.cs` | 357-397 | `GetAspNetCoreScope()` — HttpContext.Items bridge |
| `AspNetCoreHttpRequestHandler.cs` | 155-168 | Scope storage in HttpContext.Items |
| `AzureFunctionsTests.cs` | 251, 383 | `expectedSpanCount = 26` (needs update) |
| `AzureFunctionsTests.cs` | 296, 344 | `expectedSpanCount = 21` (non-ASP.NET Core) |

## Open Questions

1. Does `Microsoft.Azure.Functions.Worker` v2.2.0 transitively include ASP.NET Core (via `Grpc.AspNetCore`)? If so, the observer would create spans for gRPC communication in non-ASP.NET Core workers.

2. If the assembly detection approach (option 1) is chosen, is `Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore` reliably loaded by the time `StartDiagnosticManager()` runs?

3. Should we investigate the exact 14 spans received in the non-ASP.NET Core failure to confirm which spans are present vs missing?
