# APMSVLS-58: AsyncLocal Context Flow Issue in Isolated Azure Functions

**Status**: Under Investigation
**Branch**: `lpimentel/APMSVLS-58-azfunc-host-parenting`
**Date Started**: October 2025

## Problem Description

When using isolated Azure Functions with ASP.NET Core Integration, the worker's `azure_functions.invoke` span is incorrectly created as a root span in a separate trace instead of being parented to the worker's `aspnet_core.request` span.

### Current (Incorrect) Behavior

```
ROOT: azure_functions.invoke: GET /api/httptest [PID 27 - HOST]
  ├─ http.request: GET localhost:40521/api/HttpTest [HOST → WORKER HTTP call]
  └─ azure_functions.invoke: Http HttpTest [PID 56 - WORKER] ❌ WRONG PARENT
      ├─ test_span [WORKER]
      └─ http.request: GET jsonplaceholder... [WORKER]
```

### Expected (Correct) Behavior

```
ROOT: azure_functions.invoke: GET /api/httptest [PID 27 - HOST]
  └─ http.request: GET localhost:40521/api/HttpTest [HOST → WORKER HTTP call]
      └─ aspnet_core.request: GET /api/HttpTest [WORKER]  ✓ Correctly parented
          └─ azure_functions.invoke: Http HttpTest [PID 56 - WORKER] ✓ Should be parented here
              ├─ test_span [WORKER]
              └─ http.request: GET jsonplaceholder... [WORKER]
```

## Root Cause: AsyncLocal Context Flow Issue

The AsyncLocal scope set by `AspNetCoreDiagnosticObserver` is **not visible** to the CallTarget integration (`FunctionExecutionMiddleware`) even though they run in the same process.

### How It Should Work

1. **ASP.NET Core DiagnosticObserver creates scope** (at T+0ms):
   - Calls `tracer.StartActiveInternal()` which calls `TracerManager.ScopeManager.Activate()`
   - Sets `_activeScope.Value = scope` using `AsyncLocal<Scope>`
   - Scope should be available to any code running in the same async context

2. **Azure Functions CallTarget integration runs** (at T+175ms):
   - Calls `tracer.InternalActiveScope` which returns `TracerManager.ScopeManager.Active`
   - Returns `_activeScope.Value` from the same `AsyncLocal<Scope>` instance
   - Should see the scope created by DiagnosticObserver

### What Actually Happens

The Azure Functions CallTarget integration sees `tracer.InternalActiveScope` return **null** even though:
- The scope is still active (doesn't close until T+494ms)
- Both components run in the same process
- They access the same `AsyncLocal<Scope>` instance

### Evidence from Logs

```
19:05:24.218: ASP.NET Core span started (s_id: 1a6fc4db0f963415)
19:05:24.393: Azure Functions integration: "tracer.InternalActiveScope is null"
19:05:24.393: Azure Functions span started with p_id: null (separate trace)
19:05:24.712: ASP.NET Core span closed
```

The scope was active for 494ms, and the Azure Functions integration ran right in the middle of that time window at 175ms, yet couldn't see the scope.

## Investigation Status (as of 2025-10-10)

### ✅ Confirmed Working

1. **HTTP Proxying**: `ToRpcHttp()` returns empty gRPC message when `isHttpProxying` is true
2. **HTTP Client Instrumentation**: Creates `http.request` span in host process
3. **Trace Context Injection**: HTTP client instrumentation injects trace context headers (`x-datadog-trace-id`, `x-datadog-parent-id`)
4. **Worker ASP.NET Core Instrumentation**: Extracts context from HTTP headers and creates `aspnet_core.request` span
5. **ASP.NET Core Parenting**: Worker's `aspnet_core.request` span is **correctly parented** to host's `http.request` span

**Example from logs (trace `68e948220000000047fef7bad8bb854e`)**:
```
Host (PID 27):
├─ aspnet_core.request (s_id: 8ec7..., p_id: null)                ROOT
├─ azure_functions.invoke (s_id: 10c8..., p_id: 8ec7...)         Child of root
└─ http.request (s_id: 2ac3..., p_id: 10c8...)                   HTTP call to worker

Worker (PID 56):
└─ aspnet_core.request (s_id: 9ddf..., p_id: 2ac3...)            ✓ Correctly parented to host's http.request!
```

### ❌ Current Problem

Worker's Azure Functions integration creates a **separate trace** instead of continuing the existing trace:
- Creates `azure_functions.invoke` span with `p_id: null` (root span)
- This span appears in trace `68e948220000000020a394ff4ef60e6e` (different trace)
- It should be parented to worker's `aspnet_core.request` span in trace `68e948220000000047fef7bad8bb854e`

## Why AsyncLocal Isn't Flowing

The AsyncLocal context is not flowing properly between the DiagnosticObserver and CallTarget integration, likely due to:

### Potential Causes

1. **Thread/execution context boundaries**: Azure Functions middleware may switch threads or execution contexts in a way that breaks AsyncLocal flow
2. **Task scheduler behavior**: The isolated worker process may use custom task schedulers that don't preserve ExecutionContext
3. **Async/await boundaries**: There may be `.ConfigureAwait(false)` calls or similar that break the context chain
4. **Middleware pipeline architecture**: The Functions middleware pipeline may explicitly suppress context flow for isolation

### Relevant Code Locations

**DiagnosticObserver creates scope**:
- File: `tracer/src/Datadog.Trace/DiagnosticListeners/AspNetCoreDiagnosticObserver.cs`
- Method: `OnNext()` → creates scope via `tracer.StartActiveInternal()`
- Code: `PlatformHelpers/AspNetCoreHttpRequestHandler.cs:125`

**CallTarget integration checks scope**:
- File: `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/Azure/Functions/AzureFunctionsCommon.cs`
- Method: `CreateSpan()` → checks `tracer.InternalActiveScope`
- Code: Lines 278-323

**AsyncLocal implementation**:
- File: `tracer/src/Datadog.Trace/AsyncLocalScopeManager.cs`
- Property: `Active` getter returns `_activeScope.Value`
- Code: Lines 15-21

## Potential Solutions

### 1. Store Scope in FunctionContext.Features

**Approach**: Pass the scope explicitly through the Azure Functions pipeline instead of relying on AsyncLocal.

**Implementation**:
- DiagnosticObserver stores scope in `FunctionContext.Features`
- CallTarget integration retrieves scope from features
- Falls back to `InternalActiveScope` if not found

**Pros**:
- Explicitly passes context through Azure Functions pipeline
- Not dependent on ExecutionContext flow

**Cons**:
- Requires access to `FunctionContext` in DiagnosticObserver
- Couples instrumentation to Azure Functions infrastructure

### 2. Use Activity.Current

**Approach**: System.Diagnostics.Activity uses a different async-local mechanism (`AsyncLocal<Activity>`) that might flow better.

**Implementation**:
- DiagnosticObserver creates and starts an Activity
- CallTarget integration checks `Activity.Current`
- Convert Activity to span context for parenting

**Pros**:
- Activity is designed for distributed tracing
- May have better ExecutionContext flow

**Cons**:
- Still relies on AsyncLocal (different instance)
- May have same context flow issues

### 3. Fix Context Flow

**Approach**: Identify where the async context is being lost and ensure ExecutionContext.Capture/Restore are used correctly.

**Investigation needed**:
- Review Azure Functions middleware pipeline for context suppression
- Check for custom SynchronizationContext or TaskScheduler
- Look for `.ConfigureAwait(false)` in middleware chain
- Examine Azure Functions host source for ExecutionContext handling

**Pros**:
- Fixes the root issue
- Benefits all AsyncLocal usage

**Cons**:
- May be in Azure Functions runtime (outside our control)
- Complex debugging required

### 4. Create Scope in CallTarget

**Approach**: Have the CallTarget integration create the ASP.NET Core span instead of the DiagnosticObserver.

**Implementation**:
- Disable AspNetCoreDiagnosticObserver for isolated Azure Functions
- CallTarget integration extracts HTTP context and creates both spans
- Ensures parent-child relationship without AsyncLocal

**Pros**:
- Avoids AsyncLocal context flow entirely
- Full control over span creation

**Cons**:
- Duplicates ASP.NET Core instrumentation logic
- May miss ASP.NET Core-specific details

## Code Changes Made

### 1. Enable AspNetCoreDiagnosticObserver for Isolated Functions

**File**: `tracer/src/Datadog.Trace/ClrProfiler/Instrumentation.cs`
**Lines**: 464-498

Modified to enable `AspNetCoreDiagnosticObserver` for isolated functions while keeping it disabled for in-process functions:

```csharp
// Check if this is an in-process Azure Function (not isolated)
var isInProcessFunction = !string.IsNullOrEmpty(functionsExtensionVersion)
                       && !string.IsNullOrEmpty(functionsWorkerRuntime)
                       && !functionsWorkerRuntime.Equals("dotnet-isolated", StringComparison.OrdinalIgnoreCase);

if (isInProcessFunction)
{
    Log.Debug("Skipping AspNetCoreDiagnosticObserver in in-process Azure Functions.");
}
else
{
    // For isolated functions, enable AspNetCoreDiagnosticObserver
    observers.Add(new AspNetCoreDiagnosticObserver());
    observers.Add(new QuartzDiagnosticObserver());
}
```

### 2. Add Debug Logging to Azure Functions Span Creation

**File**: `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/Azure/Functions/AzureFunctionsCommon.cs`
**Lines**: 278-323

Added detailed debug logging and attempted explicit parenting:

```csharp
// For HTTP triggers with ASP.NET Core integration, extractedContext.SpanContext will be null
// because the gRPC message is empty. In this case, we need to explicitly parent to the
// active ASP.NET Core span (if any) by getting it from InternalActiveScope.
var parentContext = extractedContext.SpanContext;

if (parentContext != null)
{
    // We have a parent context from the gRPC message (non-proxying scenario)
    tags.SetAnalyticsSampleRate(IntegrationId, tracer.Settings, enabledWithGlobalSetting: false);
    scope = tracer.StartActiveInternal(OperationName, tags: tags, parent: parentContext);
}
else
{
    // No gRPC parent context (HTTP proxying scenario with ASP.NET Core integration)
    var activeScope = tracer.InternalActiveScope;

    Log.Debug(
        "Azure Functions span creation debug: tracer={Tracer}, TracerManager={TracerManager}, ScopeManager={ScopeManager}, Active={Active}",
        tracer.GetHashCode(),
        tracer.TracerManager.GetHashCode(),
        tracer.TracerManager.ScopeManager.GetHashCode(),
        activeScope?.GetHashCode() ?? 0);

    if (activeScope != null)
    {
        Log.Debug("HTTP trigger with ASP.NET Core integration - parenting to active scope: {SpanId}", activeScope.Span.SpanId);
        scope = tracer.StartActiveInternal(OperationName, tags: tags, parent: activeScope.Span.Context);
    }
    else
    {
        Log.Debug("HTTP trigger with no active scope - creating root span");
        tags.SetAnalyticsSampleRate(IntegrationId, tracer.Settings, enabledWithGlobalSetting: false);
        scope = tracer.StartActiveInternal(OperationName, tags: tags);
    }
}
```

**Result**: Debug logs confirm that `activeScope` is null, proving the AsyncLocal context flow issue.

## How to Reproduce

### Prerequisites

- Isolated Azure Functions app with ASP.NET Core integration
- `Datadog.AzureFunctions` NuGet package installed
- Function app deployed to Azure

### Test Function

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

### Steps

1. Deploy the function app with the test function
2. Trigger the function via HTTP:
   ```bash
   curl https://<function-app>.azurewebsites.net/api/HttpTest
   ```
3. Download and analyze logs:
   ```bash
   az functionapp log download \
     --name <function-app> \
     --resource-group <resource-group> \
     --log-path logs.zip

   unzip logs.zip -d LogFiles

   # Find trace ID from host logs
   grep "Span started" LogFiles/datadog/*WebHost*.log

   # Check if worker spans are in same trace
   grep "<trace-id>" LogFiles/datadog/*dotnet-*.log
   ```
4. Examine the `parent_id` of worker spans - they will incorrectly be `null` (root span) in a separate trace

### Alternative: Query via Datadog API

```bash
curl -G "https://api.datadoghq.com/api/v2/spans/events" \
  --data-urlencode "filter[query]=service:<function-app> resource_name:*HttpTest*" \
  --data-urlencode "filter[from]=now-10m" \
  --data-urlencode "page[limit]=10" \
  -H "DD-API-KEY: <key>" \
  -H "DD-APPLICATION-KEY: <app-key>"
```

Look for spans with different `trace_id` values when they should be in the same trace.

## Next Steps

1. **Investigate ExecutionContext flow**: Debug through Azure Functions middleware to identify where context is lost
2. **Test Activity.Current**: Verify if Activity-based context flows better than Scope-based AsyncLocal
3. **Prototype FunctionContext.Features**: Test explicit scope passing through Features collection
4. **Review Azure Functions source**: Check for custom SynchronizationContext or ExecutionContext suppression
5. **Consider CallTarget-based ASP.NET Core instrumentation**: Evaluate creating ASP.NET Core span in CallTarget integration

## Related Files

- `tracer/src/Datadog.Trace/ClrProfiler/Instrumentation.cs` - Controls which DiagnosticObservers are enabled
- `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/Azure/Functions/AzureFunctionsCommon.cs` - Span creation logic
- `tracer/src/Datadog.Trace/DiagnosticListeners/AspNetCoreDiagnosticObserver.cs` - ASP.NET Core instrumentation
- `tracer/src/Datadog.Trace/PlatformHelpers/AspNetCoreHttpRequestHandler.cs` - ASP.NET Core span creation
- `tracer/src/Datadog.Trace/AsyncLocalScopeManager.cs` - AsyncLocal<Scope> implementation
- `tracer/src/Datadog.Trace/Tracer.cs` - Tracer.InternalActiveScope property

## References

- Azure Functions ASP.NET Core Integration: https://learn.microsoft.com/en-us/azure/azure-functions/dotnet-isolated-process-guide?tabs=hostbuilder%2Cwindows#aspnet-core-integration
- YARP (Yet Another Reverse Proxy): https://microsoft.github.io/reverse-proxy/
- Azure Functions Host source: https://github.com/Azure/azure-functions-host
- AsyncLocal documentation: https://learn.microsoft.com/en-us/dotnet/api/system.threading.asynclocal-1
