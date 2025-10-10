# APMSVLS-58: AsyncLocal Context Flow Issue in Isolated Azure Functions

**Status**: Under Investigation
**Branch**: `lpimentel/APMSVLS-58-azfunc-host-parenting`
**Date Started**: October 2025

## Related Documentation

- [AzureFunctions.md](../AzureFunctions.md) - Azure Functions integration guide, instrumentation specifics, and troubleshooting
- [AzureFunctions-Architecture.md](../AzureFunctions-Architecture.md) - Deep dive into Azure Functions Host and .NET Worker architecture
- [QueryingDatadogAPIs.md](../QueryingDatadogAPIs.md) - Query Datadog APIs for spans and logs to verify instrumentation
- [CI/TroubleshootingCIFailures.md](../CI/TroubleshootingCIFailures.md) - Investigate Azure DevOps build and test failures

## Problem Description

When using isolated Azure Functions with ASP.NET Core Integration, the worker's `azure_functions.invoke` span is incorrectly created as a root span in a separate trace instead of being parented to the worker's `aspnet_core.request` span.

**Note**: All spans are tagged with `aas.function.process: host` or `aas.function.process: worker` to identify which process created them. This tag is helpful for filtering and troubleshooting multi-process traces.

### Current (Incorrect) Behavior

```
ROOT: azure_functions.invoke: GET /api/httptest [HOST, PID 27]
  ├─ http.request: GET localhost:40521/api/HttpTest [HOST → WORKER HTTP call]
  └─ azure_functions.invoke: Http HttpTest [WORKER, PID 56] ❌ WRONG PARENT
      ├─ test_span [WORKER]
      └─ http.request: GET jsonplaceholder... [WORKER]

Note: All host spans have tag aas.function.process:host
      All worker spans have tag aas.function.process:worker
```

### Expected (Correct) Behavior

```
ROOT: azure_functions.invoke: GET /api/httptest [HOST, PID 27]
  └─ http.request: GET localhost:40521/api/HttpTest [HOST → WORKER HTTP call]
      └─ aspnet_core.request: GET /api/HttpTest [WORKER]  ✓ Correctly parented
          └─ azure_functions.invoke: Http HttpTest [WORKER, PID 56] ✓ Should be parented here
              ├─ test_span [WORKER]
              └─ http.request: GET jsonplaceholder... [WORKER]

Note: All host spans have tag aas.function.process:host
      All worker spans have tag aas.function.process:worker
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
Host (PID 27) - tagged with aas.function.process:host:
├─ aspnet_core.request (s_id: 8ec7..., p_id: null)                ROOT
├─ azure_functions.invoke (s_id: 10c8..., p_id: 8ec7...)         Child of root
└─ http.request (s_id: 2ac3..., p_id: 10c8...)                   HTTP call to worker

Worker (PID 56) - tagged with aas.function.process:worker:
└─ aspnet_core.request (s_id: 9ddf..., p_id: 2ac3...)            ✓ Correctly parented to host's http.request!
```

### ❌ Current Problem (Updated 2025-10-31)

**Latest findings**: The `aspnet_core.request` span is not being created at all because `AspNetCoreDiagnosticObserver` is disabled in Azure Functions.

**Code location**: `tracer/src/Datadog.Trace/ClrProfiler/Instrumentation.cs:477-483`

The tracer explicitly skips `AspNetCoreDiagnosticObserver` when it detects Azure Functions environment variables:
```csharp
if (!string.IsNullOrEmpty(functionsExtensionVersion) && !string.IsNullOrEmpty(functionsWorkerRuntime))
{
    // Not adding the `AspNetCoreDiagnosticObserver` is particularly important for in-process Azure Functions.
    // The AspNetCoreDiagnosticObserver will be loaded in a separate Assembly Load Context, breaking the connection of AsyncLocal.
    // This is because user code is loaded within the functions host in a separate context.
    // Even in isolated functions, we don't want the AspNetCore spans to be created.
    Log.Debug("Skipping AspNetCoreDiagnosticObserver in Azure Functions.");
}
```

**Current trace structure** (trace `6905214100000000f48b7273ba29823e`):
```
azure_functions.invoke [host] (ROOT, span: 9349924773672680418)
├─ http.request [host] (span: 86692038000556982) ✓ Correct parent
└─ azure_functions.invoke [worker] (span: 2471114450846074276) ❌ Should be child of aspnet_core.request
   └─ test_span [worker] (span: 4528662242376985262) ✓ Correct parent
      └─ http.request [worker] (span: 8321837758356480840) ✓ Correct parent
```

**Key observation**: The `aspnet_core.request` span is missing entirely from the trace.

**Potential solution**: Enable `AspNetCoreDiagnosticObserver` in the isolated worker process (but not the host process). The comment mentions AsyncLocal issues with in-process functions due to separate Assembly Load Contexts, but isolated functions run in a separate process and may not have this issue.

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

### 3. Add Process Identification Tag (Prior Work)

**File**: `tracer/src/Datadog.Trace/Agent/MessagePack/SpanMessagePackFormatter.cs`
**Lines**: 742-754

Added `aas.function.process` tag to all spans to identify whether they originated from the host or worker process:

- **Host process spans**: Tagged with `aas.function.process: host`
- **Worker process spans**: Tagged with `aas.function.process: worker`

This tag is automatically applied during span serialization based on `EnvironmentHelpers.IsRunningInAzureFunctionsHost()`, which checks for the presence of `--workerId` or `--functions-worker-id` command-line flags.

**Benefits for troubleshooting**:
- Filter spans by process type in Datadog queries
- Quickly identify which process created each span in distributed traces
- Verify expected span distribution across host and worker processes

**See also**: [AzureFunctions.md - Detecting Host vs Worker Process](../AzureFunctions.md#detecting-host-vs-worker-process)

## Testing Workflow

When working on this issue, use the following workflow to test changes:

**Test Environment**:
- **Function App**: `lucasp-premium-linux-isolated`
- **Resource Group**: `lucas.pimentel`
- **Test App Location**: `D:\source\datadog\serverless-dev-apps\azure\functions\dotnet\isolated-dotnet8-aspnetcore`

### 1. Build the Datadog.AzureFunctions NuGet Package

Use the helper script to build the NuGet package with your local changes:

```powershell
# From the repository root
.\tracer\tools\Build-AzureFunctionsNuget.ps1 -CopyTo D:\temp\nuget -Verbose
```

This script:
- Cleans up previous builds
- Removes the package from NuGet cache
- Publishes `Datadog.Trace.dll` (net6.0 and net461) to the bundle folder
- Builds the `Datadog.AzureFunctions` NuGet package
- Copies the package to `D:\temp\nuget`

**Note**: The test app references this local NuGet package via a local package source. See [AzureFunctions.md - Building the Datadog.AzureFunctions NuGet Package](../AzureFunctions.md#building-the-datadogazurefunctions-nuget-package) for more details.

### 2. Deploy the Test Application

Deploy the test application located at `D:\source\datadog\serverless-dev-apps\azure\functions\dotnet\isolated-dotnet8-aspnetcore`:

```bash
# Navigate to the test app directory
cd D:/source/datadog/serverless-dev-apps/azure/functions/dotnet/isolated-dotnet8-aspnetcore

# Ensure the app references the newly built package
dotnet restore

# Deploy to Azure (requires Azure CLI and func tools)
func azure functionapp publish lucasp-premium-linux-isolated
```

**Important**: After deployment, wait 1-2 minutes for the worker process to restart and load the new tracer version before testing.

### 3. Trigger the Function

Trigger the test function via HTTP to generate traces:

```bash
# Note the current UTC time for log filtering
date -u

# Trigger the function
curl https://lucasp-premium-linux-isolated.azurewebsites.net/api/HttpTest

# Wait a few seconds for logs to be written
sleep 5
```

### 4. Pull Spans and Logs from Datadog

#### Option A: Use MCP (Atlassian) Tool

If you have the MCP tool configured, use it to search for recent traces:

```
# Search for recent spans from the function app
[Use MCP search tool with query: service:lucasp-premium-linux-isolated resource:HttpTest]
```

#### Option B: Download Logs from Azure

Download tracer logs directly from Azure for detailed analysis:

```bash
# Download logs to a timestamped file
az functionapp log download \
  --name lucasp-premium-linux-isolated \
  --resource-group lucas.pimentel \
  --log-path D:/temp/logs-$(date +%H%M%S).zip

# Extract the logs
unzip -q D:/temp/logs-$(date +%H%M%S).zip -d D:/temp/LogFiles
```

**See [AzureFunctions.md - Troubleshooting with Azure Logs](../AzureFunctions.md#troubleshooting-with-azure-logs) for comprehensive guidance on log analysis.**

#### Option C: Query Datadog API

Query the Datadog API for spans (requires `DD_API_KEY` and `DD_APPLICATION_KEY`):

```bash
curl -s -X POST https://api.datadoghq.com/api/v2/spans/events/search \
  -H "DD-API-KEY: ${DD_API_KEY}" \
  -H "DD-APPLICATION-KEY: ${DD_APPLICATION_KEY}" \
  -H "Content-Type: application/json" \
  -d '{
    "data": {
      "attributes": {
        "filter": {
          "query": "service:lucasp-premium-linux-isolated resource_name:*HttpTest*",
          "from": "now-10m",
          "to": "now"
        },
        "sort": "-timestamp",
        "page": {
          "limit": 20
        }
      },
      "type": "search_request"
    }
  }' | jq -r '.data[] | .attributes | "Trace: \(.trace_id)\nSpan: \(.span_id)\nParent: \(.parent_id)\nResource: \(.resource_name)\n---"'
```

**See [QueryingDatadogAPIs.md - Debugging Span Parenting](../QueryingDatadogAPIs.md#example-debugging-span-parenting) for detailed examples and query syntax.**

### 5. Inspect the Results

Analyze the traces to verify span parenting:

#### Check Trace Structure

Look for the following in the traces:

**Expected structure** (correct parenting):
```
Trace ID: 68e948220000000047fef7bad8bb854e

Host spans (PID 27) - all tagged with aas.function.process:host:
├─ aspnet_core.request (s_id: 8ec7..., p_id: null)           [ROOT]
├─ azure_functions.invoke (s_id: 10c8..., p_id: 8ec7...)
└─ http.request (s_id: 2ac3..., p_id: 10c8...)               [HTTP call to worker]

Worker spans (PID 56) - all tagged with aas.function.process:worker:
└─ aspnet_core.request (s_id: 9ddf..., p_id: 2ac3...)        [Child of http.request]
   └─ azure_functions.invoke (s_id: 114d..., p_id: 9ddf...)  [Child of aspnet_core] ✓
```

**Current (incorrect) structure**:
- Worker's `azure_functions.invoke` span has `p_id: null` (root span)
- Worker's `azure_functions.invoke` appears in a different trace ID

**Using the `aas.function.process` tag to filter spans**:

When querying Datadog, you can filter by process type to isolate host or worker spans:

```bash
# Get only host spans
curl -s -X POST https://api.datadoghq.com/api/v2/spans/events/search \
  -H "DD-API-KEY: ${DD_API_KEY}" \
  -H "DD-APPLICATION-KEY: ${DD_APPLICATION_KEY}" \
  -H "Content-Type: application/json" \
  -d '{
    "data": {
      "attributes": {
        "filter": {
          "query": "service:lucasp-premium-linux-isolated @aas.function.process:host",
          "from": "now-10m",
          "to": "now"
        },
        "sort": "-timestamp",
        "page": {"limit": 20}
      },
      "type": "search_request"
    }
  }' | jq -r '.data[] | .attributes | "\(.operation_name): \(.resource_name)"'

# Get only worker spans
curl -s -X POST https://api.datadoghq.com/api/v2/spans/events/search \
  -H "DD-API-KEY: ${DD_API_KEY}" \
  -H "DD-APPLICATION-KEY: ${DD_APPLICATION_KEY}" \
  -H "Content-Type: application/json" \
  -d '{
    "data": {
      "attributes": {
        "filter": {
          "query": "service:lucasp-premium-linux-isolated @aas.function.process:worker",
          "from": "now-10m",
          "to": "now"
        },
        "sort": "-timestamp",
        "page": {"limit": 20}
      },
      "type": "search_request"
    }
  }' | jq -r '.data[] | .attributes | "\(.operation_name): \(.resource_name)"'
```

#### Verify Tracer Version

Before analyzing behavior, confirm the worker loaded your updated tracer version:

```bash
# Find the most recent worker initialization
grep "Assembly metadata" D:/temp/LogFiles/datadog/dotnet-tracer-managed-dotnet-*.log | tail -1

# Check version in recent log entries (use your test timestamp)
grep "2025-10-31 15:30:" D:/temp/LogFiles/datadog/dotnet-tracer-managed-dotnet-*.log | grep "TracerVersion"
```

If the version is outdated, restart the Function App and re-test:

```bash
az functionapp restart --name lucasp-premium-linux-isolated --resource-group lucas.pimentel
```

#### Analyze Debug Logs

Search for the debug messages added to understand AsyncLocal behavior:

```bash
# Find debug messages at your test timestamp (adjust timestamp)
grep "2025-10-31 15:30:" D:/temp/LogFiles/datadog/dotnet-tracer-managed-dotnet-*.log | grep -E "Azure Functions span creation|HTTP trigger"
```

Look for:
- `"tracer.InternalActiveScope is null"` - Confirms AsyncLocal context flow issue
- `"HTTP trigger with ASP.NET Core integration - parenting to active scope"` - Would indicate success
- Span IDs and parent IDs to trace the span hierarchy

**See [AzureFunctions.md - Accessing Tracer Logs](../AzureFunctions.md#accessing-tracer-logs) for detailed log analysis techniques.**

## How to Reproduce

**Quick Start**: Follow the [Testing Workflow](#testing-workflow) above for the complete end-to-end process.

### Minimal Reproduction

The issue can be reproduced with any HTTP-triggered function in an isolated Azure Functions app with ASP.NET Core integration:

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

### Key Requirements

- **Isolated Azure Functions** (.NET Isolated, not in-process)
- **ASP.NET Core Integration** (app uses `ConfigureFunctionsWebApplication()`)
- **HTTP Trigger** (timer triggers don't exhibit this issue as they don't have ASP.NET Core spans)
- **Datadog.AzureFunctions** NuGet package installed

### Expected Symptom

When examining traces in Datadog:
- Worker's `azure_functions.invoke` span will have `parent_id: null` (root span)
- Worker's `azure_functions.invoke` will appear in a **separate trace** from the host spans
- Worker's `aspnet_core.request` span is correctly parented to host's `http.request` span

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

### Internal Documentation

- [AzureFunctions.md](../AzureFunctions.md) - Azure Functions integration guide
  - [Building the Datadog.AzureFunctions NuGet Package](../AzureFunctions.md#building-the-datadogazurefunctions-nuget-package)
  - [Accessing Tracer Logs](../AzureFunctions.md#accessing-tracer-logs)
  - [Troubleshooting with Azure Logs](../AzureFunctions.md#troubleshooting-with-azure-logs)
- [AzureFunctions-Architecture.md](../AzureFunctions-Architecture.md) - Deep dive into Azure Functions architecture
  - [ASP.NET Core Integration](../AzureFunctions-Architecture.md#aspnet-core-integration)
  - [Distributed Tracing Integration](../AzureFunctions-Architecture.md#distributed-tracing-integration)
  - [Middleware Model](../AzureFunctions-Architecture.md#middleware-model)
- [QueryingDatadogAPIs.md](../QueryingDatadogAPIs.md) - Query Datadog APIs for debugging
  - [Spans Search API](../QueryingDatadogAPIs.md#spans-search-api)
  - [Debugging Span Parenting](../QueryingDatadogAPIs.md#example-debugging-span-parenting)
  - [Logs Search API](../QueryingDatadogAPIs.md#logs-search-api)
  - [Azure Functions Logging Configuration](../QueryingDatadogAPIs.md#azure-functions-logging-configuration)
- [CI/TroubleshootingCIFailures.md](../CI/TroubleshootingCIFailures.md) - Investigate Azure DevOps CI failures
  - [Using Azure CLI](../CI/TroubleshootingCIFailures.md#using-azure-cli)
  - [Using Azure DevOps MCP](../CI/TroubleshootingCIFailures.md#using-azure-devops-mcp-ai-assistant-integration)

### External Documentation

- [Azure Functions ASP.NET Core Integration](https://learn.microsoft.com/en-us/azure/azure-functions/dotnet-isolated-process-guide?tabs=hostbuilder%2Cwindows#aspnet-core-integration)
- [YARP (Yet Another Reverse Proxy)](https://microsoft.github.io/reverse-proxy/)
- [Azure Functions Host source](https://github.com/Azure/azure-functions-host)
- [AsyncLocal documentation](https://learn.microsoft.com/en-us/dotnet/api/system.threading.asynclocal-1)
