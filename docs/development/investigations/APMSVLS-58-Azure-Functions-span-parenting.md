# APMSVLS-58: Azure Functions Span Parenting Issue

**Status**: ✅ **RESOLVED** (Phase 3 Complete)
**Branch**: `lpimentel/APMSVLS-58-azfunc-host-parenting`
**Date Started**: October 2025
**Date Resolved**: November 12, 2025

## Related Documentation

- [AzureFunctions.md](../AzureFunctions.md) - Azure Functions integration guide
- [AzureFunctions-Architecture.md](../AzureFunctions-Architecture.md) - Architecture deep dive
- [QueryingDatadogAPIs.md](../QueryingDatadogAPIs.md) - Query Datadog APIs for debugging

## Problem Description

In isolated Azure Functions with ASP.NET Core Integration, the worker's `azure_functions.invoke` span is incorrectly parented, resulting in broken distributed traces.

### Current (Incorrect) Behavior

**After enabling AspNetCoreDiagnosticObserver in worker** (Phase 2):

```
ROOT: azure_functions.invoke [HOST]
  └─ http.request [HOST → WORKER]
      └─ aspnet_core.request [WORKER] ✓ Correct (now exists!)

Worker azure_functions.invoke in same trace but WRONG parent:
└─ azure_functions.invoke [WORKER]
   └─ parent: host's root span ❌ Should be child of aspnet_core.request
      ├─ test_span [WORKER]
      └─ http.request [WORKER]
```

### Desired Behavior

```
ROOT: aspnet_core.request [WORKER]
  └─ azure_functions.invoke [WORKER] ✓ Correct
      ├─ test_span [WORKER]
      └─ http.request [WORKER]

Note: Host-side azure_functions.invoke and http.request spans should be removed
```

**Key observations**:
- All spans tagged with `aas.function.process:host` or `aas.function.process:worker`
- Worker's `azure_functions.invoke` is parented to **host's root span** instead of `aspnet_core.request`
- Need to: (1) Fix worker span parenting (high priority), (2) Remove host-side spans

## Root Cause (RESOLVED)

**Primary Issue**: Looking for wrong key in `FunctionContext.Items`
- ❌ Was using: `"__AspNetCoreHttpContext__"` (doesn't exist)
- ✅ Should use: `"HttpRequestContext"` (actual key set by `FunctionsHttpProxyingMiddleware`)

**Secondary Issue**: Stale header extraction
- In ASP.NET Core mode, headers in gRPC message contain host's root span context
- These stale headers were used as fallback when HttpContext.Items lookup failed
- Worker's `azure_functions.invoke` span incorrectly parented to host's root span

**Underlying Cause**: AsyncLocal context doesn't flow through Azure Functions middleware, so HttpContext.Items bridge is required.

## Investigation Findings

### Confirmed Working ✅

1. HTTP proxying: Host returns empty gRPC message when `isHttpProxying` is true
2. HTTP client instrumentation creates `http.request` span and injects trace context headers
3. Worker's `aspnet_core.request` span correctly parented to host's `http.request` span

### Current Issues ❌

1. **AsyncLocal doesn't flow**: `tracer.InternalActiveScope` is null when creating `azure_functions.invoke` span
2. **Activity.Current is broken in Azure Functions**: Cannot rely on `System.Diagnostics.Activity.Current`
3. **Incorrect parent extraction**: Worker's span gets host's root span context instead of `aspnet_core.request` context

## Trace Analysis

### Phase 1: Original Behavior (Before AspNetCoreDiagnosticObserver Enabled)

**Location**: [`1-original/`](1-original/)

Before enabling AspNetCoreDiagnosticObserver in the worker process, the `aspnet_core.request` span was **not created at all** because the observer was disabled in all Azure Functions processes (both host and worker).

**Captured traces**:
- [trace_payload_1762897459231.json](1-original/trace_payload_1762897459231.json) - Worker spans
- [trace_payload_1762897460146.json](1-original/trace_payload_1762897460146.json) - Host spans

**Analysis of trace `1910270346618876437`**:
```
HOST PROCESS (runtime_id: 1ecec7fe, process: 27):
├─ azure_functions.invoke (span: 1978509666546725896) [ROOT] ❌
│  resource: "GET /api/httptest"
│  duration: 635ms
│
└─ http.request (span: 5775791465145012166) ❌
   resource: "GET localhost:43239/api/HttpTest"
   duration: 413ms
   parent: 1978509666546725896 (host's azure_functions.invoke)

WORKER PROCESS (runtime_id: 3e375b7a, process: 58):
└─ azure_functions.invoke (span: 18286165385944622934) ⚠️
   resource: "Http HttpTest"
   duration: 159ms
   parent: 1978509666546725896 (HOST's root span!)
   │
   └─ test_span (span: 13925130946451077401)
      duration: 136ms
      │
      └─ http.request (span: 4052593309550270339)
         resource: "GET jsonplaceholder.typicode.com/users/?"
         duration: 115ms
```

**Key issues**:
- ❌ No `aspnet_core.request` span exists in worker process
- ❌ Worker's `azure_functions.invoke` is parented to HOST's root span (1978509666546725896)
- ❌ Host creates unnecessary `azure_functions.invoke` and `http.request` spans
- ✓ All spans are in the same trace (distributed tracing working via headers)

### Phase 2: After Enabling AspNetCoreDiagnosticObserver in Worker

**Location**: [`2-after-enabling-aspnetcore-observer-in-worker-process/`](2-after-enabling-aspnetcore-observer-in-worker-process/)

After enabling AspNetCoreDiagnosticObserver in the worker process, the `aspnet_core.request` span is now created, but the worker's `azure_functions.invoke` span has incorrect parenting.

**Captured traces**:
- [trace_payload_1762824450957.json](2-after-enabling-aspnetcore-observer-in-worker-process/trace_payload_1762824450957.json) - Worker spans
- [trace_payload_1762824451432.json](2-after-enabling-aspnetcore-observer-in-worker-process/trace_payload_1762824451432.json) - Host spans

**Analysis of trace `11227614026327825028`**:
```
Host spans:
├─ azure_functions.invoke (span: 12539548146409755932) [ROOT] ❌ Should not exist
└─ http.request (span: 17651134856053342758) [child of above] ❌ Should not exist

Worker spans (all in same trace ✓):
├─ aspnet_core.request (span: 5312880851904873807) ✅ NOW EXISTS!
│  └─ parent: 17651134856053342758 (host's http.request) ✓ Correct!
└─ azure_functions.invoke (span: 727838728754785615)
   └─ parent: 12539548146409755932 (host's root) ❌ Should be child of aspnet_core.request!
```

**Progress made**:
- ✅ `aspnet_core.request` span now exists in worker process
- ✅ `aspnet_core.request` correctly parented to host's `http.request` span
- ❌ Worker's `azure_functions.invoke` still parented to **host's root span** instead of `aspnet_core.request`
- ❌ Host still creates unnecessary `azure_functions.invoke` and `http.request` spans

### Phase 3: Fixed HttpContext.Items Bridge ✅ RESOLVED

**Commit**: `1d3179aa5` - Fix Azure Functions span parenting with ASP.NET Core

**Root cause identified**: Looking for wrong key in `FunctionContext.Items`
- ❌ Old: `"__AspNetCoreHttpContext__"` (doesn't exist)
- ✅ Fixed: `"HttpRequestContext"` (actual key set by Azure Functions Worker SDK)

**Changes made**:
1. Skip stale gRPC header extraction when ASP.NET Core integration detected (`AzureFunctionsCommon.cs:242-243`)
2. Use correct `"HttpRequestContext"` key for HttpContext lookup (`AzureFunctionsCommon.cs:292`)
3. Added comprehensive debug logging for troubleshooting

**Analysis of trace `14656220060439490006`** (Nov 12, 2025):
```
Worker spans:
├─ aspnet_core.request (span: 10174520177415259312)
│  └─ azure_functions.invoke (span: 1243553223469235235) ✅ CORRECT PARENT!
│     └─ test_span (span: 11352266471047046875)
│        └─ http.request (span: 3397667712105060208)
```

**Result**:
- ✅ Worker's `azure_functions.invoke` correctly parented to `aspnet_core.request`
- ✅ Proper span hierarchy in worker process
- ✅ All spans in same trace
- ⚠️ Host spans still present (secondary priority)

## Code Changes Made

### 1. Enable AspNetCoreDiagnosticObserver in Worker Process

**File**: `tracer/src/Datadog.Trace/ClrProfiler/Instrumentation.cs:473-493`

Modified to enable `AspNetCoreDiagnosticObserver` in isolated worker process only:

```csharp
var isInAzureFunctionsHost = EnvironmentHelpers.IsRunningInAzureFunctionsHost();
var shouldSkipAspNetCore = isInAzureFunctionsHost ||
    (EnvironmentHelpers.IsAzureFunctions() && !EnvironmentHelpers.IsAzureFunctionsIsolated());

if (shouldSkipAspNetCore)
{
    Log.Debug("Skipping AspNetCoreDiagnosticObserver in Azure Functions (host or in-process).");
}
else
{
    observers.Add(new AspNetCoreDiagnosticObserver());
}
```

### 2. Add Process Identification Tag

**File**: `tracer/src/Datadog.Trace/Agent/MessagePack/SpanMessagePackFormatter.cs:742-754`

Added `aas.function.process` tag to distinguish host vs worker spans:
- Host spans: `aas.function.process: host`
- Worker spans: `aas.function.process: worker`

## Testing Workflow

### Environment

**All Function Apps** (Resource Group: `lucas.pimentel`, Location: Canada Central):

| Name | Purpose |
|------|---------|
| lucasp-premium-linux-isolated-aspnet | **Primary test app** - Isolated .NET 8 with ASP.NET Core Integration |
| lucasp-premium-linux-isolated | Isolated .NET 8 (no ASP.NET Core) |
| lucasp-premium-linux-inproc | In-process .NET 6 |
| lucasp-premium-windows-isolated-aspnet | Windows isolated with ASP.NET Core |
| lucasp-premium-windows-isolated | Windows isolated (no ASP.NET Core) |
| lucasp-premium-windows-inproc | Windows in-process |
| lucasp-consumption-windows-isolated | Windows consumption plan |
| lucasp-flex-consumption-isolated | Flex consumption plan |

**Primary Test App:**
- **Name**: `lucasp-premium-linux-isolated-aspnet`
- **Resource Group**: `lucas.pimentel`
- **Source**: `D:\source\datadog\serverless-dev-apps\azure\functions\dotnet\isolated-dotnet8-aspnetcore`

### Steps

1. **Build NuGet package**:
   ```powershell
   .\tracer\tools\Build-AzureFunctionsNuget.ps1 -CopyTo D:\temp\nuget -Verbose
   ```

2. **Deploy test app**:
   ```bash
   cd D:/source/datadog/serverless-dev-apps/azure/functions/dotnet/isolated-dotnet8-aspnetcore
   dotnet restore
   func azure functionapp publish lucasp-premium-linux-isolated
   ```

   Wait 1-2 minutes for worker restart after deployment.

3. **Trigger function**:
   ```bash
   curl https://lucasp-premium-linux-isolated.azurewebsites.net/api/HttpTest
   ```

4. **Download logs**:
   ```bash
   az functionapp log download \
     --name lucasp-premium-linux-isolated \
     --resource-group lucas.pimentel \
     --log-path D:/temp/logs-$(date +%H%M%S).zip
   ```

5. **Query Datadog** (optional):
   ```bash
   # Filter by process type
   curl -X POST https://api.datadoghq.com/api/v2/spans/events/search \
     -H "DD-API-KEY: ${DD_API_KEY}" \
     -H "DD-APPLICATION-KEY: ${DD_APPLICATION_KEY}" \
     -H "Content-Type: application/json" \
     -d '{"data":{"attributes":{"filter":{"query":"service:lucasp-premium-linux-isolated @aas.function.process:worker","from":"now-10m","to":"now"}},"type":"search_request"}}'
   ```

### Verification ✅

**Expected trace structure** (after fix):
```
Worker aspnet_core.request (ROOT)
└─ Worker azure_functions.invoke (child) ✓
   └─ test_span
      └─ http.request
```

**Verified** (Trace ID: `14656220060439490006`, Nov 12 2025):
- ✅ Worker's `azure_functions.invoke` parent_id matches `aspnet_core.request` span_id
- ✅ All spans in same trace
- ✅ Proper span hierarchy in worker process
- ⚠️ Host spans still present (secondary priority to remove)

## Minimal Reproduction

```csharp
[Function(nameof(HttpTest))]
public async Task<IActionResult> HttpTest([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest request)
{
    using (var scope = Tracer.Instance.StartActive("test_span"))
    {
        using var httpClient = new HttpClient();
        await httpClient.GetStringAsync("https://jsonplaceholder.typicode.com/users/1");
        return new OkObjectResult(new { message = "success" });
    }
}
```

**Requirements**:
- Isolated Azure Functions (.NET Isolated)
- ASP.NET Core Integration (`ConfigureFunctionsWebApplication()`)
- HTTP Trigger
- `Datadog.AzureFunctions` NuGet package

## Goal

Now that we have an `aspnet_core.request` span in the worker process (Phase 2), we need to:

1. **Make worker's `azure_functions.invoke` span a child of `aspnet_core.request`** (high priority)
   - Currently: Worker's `azure_functions.invoke` is parented to host's root span
   - Goal: Worker's `azure_functions.invoke` should be parented to worker's `aspnet_core.request` span
   - This will create the correct hierarchy: `aspnet_core.request` → `azure_functions.invoke` → user code

2. **Remove host-side spans** (secondary priority)
   - Host's `azure_functions.invoke` and `http.request` spans are unnecessary when ASP.NET Core integration is enabled
   - These spans represent HTTP proxying overhead, not the actual function execution
   - Detect HTTP proxying in host process and skip span creation

## Implementation Plan

### Approach: Use HttpContext.Items as Bridge

Since AsyncLocal context doesn't flow through Azure Functions middleware, use `HttpContext.Items` to explicitly pass span context between middleware layers.

**Step 1: Store scope in HttpContext.Items**
- File: `tracer/src/Datadog.Trace/PlatformHelpers/AspNetCoreHttpRequestHandler.cs:125`
- After creating scope, store in `httpContext.Items["__Datadog.Trace.AspNetCore.ActiveScope"]`
- Use `__` prefix to avoid conflicts with user code (same pattern as `TracingHttpModule.cs`)

**Step 2: Retrieve scope in Azure Functions middleware**
- File: `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/Azure/Functions/AzureFunctionsCommon.cs:262-279`
- Get HttpContext from `functionContext.GetHttpContext()`
- Retrieve scope from `httpContext.Items["__Datadog.Trace.AspNetCore.ActiveScope"]`
- Use as parent before falling back to extraction logic

**Step 3: Skip host span creation when proxying**
- File: `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/Azure/Functions/AzureFunctionsExecutorTryExecuteAsyncIntegration.cs`
- Detect `IsRunningInAzureFunctionsHost()` + HTTP proxying enabled
- Skip span creation in host when ASP.NET Core integration is active in worker

**Why this works:**
- HttpContext.Items persists throughout request lifecycle
- Both ASP.NET Core and Azure Functions middleware access same HttpContext instance
- No reliance on AsyncLocal or Activity.Current
- Explicit and debuggable

**Fallback approaches:**
- Use `FunctionContext.Features` to store/retrieve scope
- Custom middleware to bridge AsyncLocal → Features before context is lost

## Implementation Progress

### Phase 3: HttpContext.Items Bridge (In Progress)

**Status**: Code implemented, testing pending

**Changes Made** (Commit: d0e31854a):

1. **Store scope in HttpContext.Items** ✅
   - File: `tracer/src/Datadog.Trace/PlatformHelpers/AspNetCoreHttpRequestHandler.cs:142`
   - Added: `httpContext.Items["__Datadog.Trace.AspNetCore.ActiveScope"] = scope;`
   - Stores the AspNetCore scope immediately after creation

2. **Add Items property to IFunctionContext** ✅
   - File: `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/Azure/Functions/Isolated/IFunctionContext.cs:22`
   - Added: `IDictionary<object, object>? Items { get; }`
   - Allows duck-typed access to FunctionContext.Items

3. **Retrieve scope from HttpContext.Items** ✅
   - File: `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/Azure/Functions/AzureFunctionsCommon.cs:262-297`
   - Logic:
     1. Check if `tracer.InternalActiveScope == null` (AsyncLocal didn't flow)
     2. Try to get HttpContext from `context.Items["__AspNetCoreHttpContext__"]`
     3. Try to get scope from `httpContext.Items["__Datadog.Trace.AspNetCore.ActiveScope"]`
     4. Use scope as parent if found, otherwise fall back to header extraction
   - Only uses HttpContext.Items when AsyncLocal fails (maintains backward compatibility)

**Next Steps**:
1. Deploy updated NuGet package to `lucasp-premium-linux-isolated-aspnet`
2. Trigger HttpTest function
3. Download and analyze traces to verify:
   - Worker's `azure_functions.invoke` is now child of `aspnet_core.request`
   - All spans in same trace
   - Correct parent_id relationships

**Pending**:
- Step 3: Skip host span creation when HTTP proxying is detected (secondary priority)

## References

- [Azure Functions ASP.NET Core Integration](https://learn.microsoft.com/en-us/azure/azure-functions/dotnet-isolated-process-guide?tabs=hostbuilder%2Cwindows#aspnet-core-integration)
- [Azure Functions Host source](https://github.com/Azure/azure-functions-host)
- [YARP (Yet Another Reverse Proxy)](https://microsoft.github.io/reverse-proxy/)
