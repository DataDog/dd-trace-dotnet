# APMSVLS-58: Proposed Solution - Use Activity.Current for Parent Context

**Status**: Ready for Testing
**Date**: October 31, 2025
**Branch**: `lpimentel/APMSVLS-58-azfunc-host-parenting`

## Problem Summary

In isolated Azure Functions with ASP.NET Core Integration, the worker's `azure_functions.invoke` span is incorrectly created as a root span in a separate trace instead of being parented to the worker's `aspnet_core.request` span.

**Root Cause**: `AsyncLocal<Scope>` context doesn't flow properly through the Azure Functions middleware pipeline. When the Azure Functions CallTarget integration checks `tracer.InternalActiveScope`, it returns null even though the ASP.NET Core DiagnosticObserver created an active scope earlier in the request pipeline.

## Proposed Solution: Use Activity.Current

Instead of relying solely on `AsyncLocal<Scope>`, extract the parent context from `System.Diagnostics.Activity.Current`.

### Rationale

1. **ASP.NET Core creates Activities**: The `AspNetCoreDiagnosticObserver` creates Activity instances that set `Activity.Current`
2. **Different AsyncLocal mechanism**: Activity uses its own AsyncLocal storage that may flow more reliably through the Azure Functions middleware
3. **Standard .NET pattern**: Activity.Current is the standard distributed tracing mechanism in .NET
4. **Non-invasive**: Doesn't require modifying DiagnosticObserver or Azure Functions infrastructure
5. **Backward compatible**: Falls back to existing behavior if Activity isn't available

## Implementation Details

### 1. Modified Span Creation Logic

**File**: `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/Azure/Functions/AzureFunctionsCommon.cs`
**Lines**: 262-290

The span creation logic now follows this sequence:

1. First checks if there's a parent from the gRPC message (`extractedContext.SpanContext`)
2. If no gRPC parent AND no active scope, tries to extract parent from `Activity.Current`
3. Creates span with the extracted parent context

```csharp
// Determine the parent context for the Azure Functions span
SpanContext? parentContext = extractedContext.SpanContext;

if (parentContext == null && tracer.InternalActiveScope == null)
{
    // No parent from gRPC message and no active scope
    // For HTTP triggers with ASP.NET Core integration, try to get parent from Activity.Current
    // This handles the case where AsyncLocal context doesn't flow through Azure Functions middleware
    parentContext = TryExtractSpanContextFromActivity(tracer);
}

if (parentContext != null || tracer.InternalActiveScope == null)
{
    // Create span with explicit parent (from gRPC, Activity, or null for root)
    tags.SetAnalyticsSampleRate(IntegrationId, tracer.CurrentTraceSettings.Settings, enabledWithGlobalSetting: false);
    scope = tracer.StartActiveInternal(OperationName, tags: tags, parent: parentContext);
}
else
{
    // InternalActiveScope exists, use it as parent (shouldn't normally be hit)
    scope = tracer.StartActiveInternal(OperationName);
    // ... set tags ...
}
```

### 2. New Helper Method: TryExtractSpanContextFromActivity

**File**: `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/Azure/Functions/AzureFunctionsCommon.cs`
**Lines**: 477-543

This method:
- Checks if `Activity.Current` exists and is in W3C format
- Extracts TraceId and SpanId from the Activity
- Converts them to Datadog's TraceId (128-bit) and SpanId (64-bit) formats
- Creates a SpanContext using `Tracer.CreateSpanContext`
- Includes comprehensive debug logging for troubleshooting
- Returns null if Activity is unavailable or extraction fails

```csharp
private static SpanContext? TryExtractSpanContextFromActivity(Tracer tracer)
{
    try
    {
        var currentActivity = System.Diagnostics.Activity.Current;

        if (currentActivity == null)
        {
            Log.Debug("Azure Functions: Activity.Current is null");
            return null;
        }

        // Only W3C format activities have TraceId and SpanId properties we can use
        if (currentActivity.IdFormat != System.Diagnostics.ActivityIdFormat.W3C)
        {
            Log.Debug("Azure Functions: Activity.Current is not W3C format (IdFormat={IdFormat})", currentActivity.IdFormat);
            return null;
        }

        var activityTraceId = currentActivity.TraceId.ToHexString();
        var activitySpanId = currentActivity.SpanId.ToHexString();

        if (string.IsNullOrEmpty(activityTraceId) || string.IsNullOrEmpty(activitySpanId))
        {
            Log.Debug("Azure Functions: Activity.Current has null or empty TraceId/SpanId");
            return null;
        }

        // Parse the hex strings into Datadog's trace and span ID formats
        if (!Util.HexString.TryParseTraceId(activityTraceId, out var traceId))
        {
            Log.Debug("Azure Functions: Failed to parse Activity TraceId: {TraceId}", activityTraceId);
            return null;
        }

        if (!Util.HexString.TryParseUInt64(activitySpanId, out var spanId))
        {
            Log.Debug("Azure Functions: Failed to parse Activity SpanId: {SpanId}", activitySpanId);
            return null;
        }

        Log.Debug(
            "Azure Functions: Extracted parent context from Activity.Current - TraceId: {TraceId}, SpanId: {SpanId}, OperationName: {OperationName}",
            activityTraceId,
            activitySpanId,
            currentActivity.OperationName);

        return tracer.CreateSpanContext(
            parent: SpanContext.None,
            serviceName: null,
            traceId: traceId,
            spanId: spanId,
            rawTraceId: activityTraceId,
            rawSpanId: activitySpanId);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Azure Functions: Error extracting SpanContext from Activity.Current");
        return null;
    }
}
```

### 3. Bug Fix

**File**: `tracer/src/Datadog.Trace/Util/EnvironmentHelpers.cs`
**Line**: 173

Fixed unrelated compilation error:
```csharp
// Before:
ConfigurationKeys.AzureFunctions.FunctionsWorkerRuntime

// After:
PlatformKeys.AzureFunctions.FunctionsWorkerRuntime
```

## Expected Behavior After Fix

### Before (Incorrect)

```
ROOT: azure_functions.invoke [HOST, trace: 68e948220000000047fef7bad8bb854e]
  └─ http.request [HOST → WORKER]

Separate Trace (wrong):
ROOT: azure_functions.invoke [WORKER, trace: 68e948220000000020a394ff4ef60e6e] ❌
  ├─ test_span [WORKER]
  └─ http.request [WORKER]
```

### After (Correct)

```
ROOT: azure_functions.invoke [HOST, trace: 68e948220000000047fef7bad8bb854e]
  └─ http.request [HOST → WORKER]
      └─ aspnet_core.request [WORKER] ✓ Correctly extracted from HTTP headers
          └─ azure_functions.invoke [WORKER] ✓ Now parented via Activity.Current
              ├─ test_span [WORKER]
              └─ http.request [WORKER]
```

All spans now appear in the same trace with correct parent-child relationships.

## Debug Logging

The solution includes extensive debug logging to help verify behavior:

- `"Activity.Current is null"` - Activity not available (expected for non-HTTP triggers)
- `"Activity.Current is not W3C format"` - Activity format issue
- `"Extracted parent context from Activity.Current"` - **Success!** Shows TraceId/SpanId/OperationName
- `"Error extracting SpanContext from Activity.Current"` - Extraction failed with exception

## Edge Cases Handled

1. **Non-HTTP triggers** (Timer, Queue, ServiceBus, etc.)
   - Activity.Current will be null
   - Falls back to creating root span (expected behavior)

2. **In-process functions**
   - AspNetCoreDiagnosticObserver is disabled
   - No Activity created, falls back to existing behavior

3. **Non-W3C Activity format**
   - Method returns null
   - Falls back to root span creation

4. **Activity without TraceId/SpanId**
   - Method returns null
   - Falls back gracefully to existing behavior

## Testing Plan

### 1. Build NuGet Package

```powershell
# From tracer repository root
.\tracer\tools\Build-AzureFunctionsNuget.ps1 -CopyTo D:\temp\nuget -Verbose
```

### 2. Deploy Test Application

```bash
# Navigate to test app
cd D:/source/datadog/serverless-dev-apps/azure/functions/dotnet/isolated-dotnet8-aspnetcore

# Restore with local package
dotnet restore

# Deploy to Azure
func azure functionapp publish lucasp-premium-linux-isolated
```

**Note**: Wait 1-2 minutes after deployment for worker process to restart and load new tracer version.

### 3. Trigger Function and Capture Trace ID

```bash
# Trigger the function
curl https://lucasp-premium-linux-isolated.azurewebsites.net/api/HttpTest

# Response will include trace_id for easy lookup:
# {
#   "message": "success",
#   "user": 123,
#   "trace_id": "68e948220000000047fef7bad8bb854e",
#   "span_id": "1234567890"
# }
```

### 4. Verify in Datadog

**Success Indicators**:
- Worker's `azure_functions.invoke` span has correct `parent_id` (not null)
- All spans appear in the same trace
- Span hierarchy matches expected structure
- All spans tagged with `aas.function.process:host` or `aas.function.process:worker`

**Debug logs to check**:
```bash
# Download logs from Azure
az functionapp log download \
  --name lucasp-premium-linux-isolated \
  --resource-group lucas.pimentel \
  --log-path D:/temp/logs-$(date +%H%M%S).zip

# Search for success message
grep "Extracted parent context from Activity.Current" D:/temp/LogFiles/datadog/*.log
```

Expected log output:
```
Azure Functions: Extracted parent context from Activity.Current -
  TraceId: 68e948220000000047fef7bad8bb854e,
  SpanId: 1a6fc4db0f963415,
  OperationName: Microsoft.AspNetCore.Hosting.HttpRequestIn
```

### 5. Query Datadog API (Optional)

Use the trace ID from the HTTP response to query spans directly:

```bash
curl -s -X POST https://api.datadoghq.com/api/v2/spans/events/search \
  -H "DD-API-KEY: ${DD_API_KEY}" \
  -H "DD-APPLICATION-KEY: ${DD_APPLICATION_KEY}" \
  -H "Content-Type: application/json" \
  -d '{
    "data": {
      "attributes": {
        "filter": {
          "query": "trace_id:68e948220000000047fef7bad8bb854e",
          "from": "now-10m",
          "to": "now"
        }
      },
      "type": "search_request"
    }
  }' | jq '.data[] | {operation: .attributes.operation_name, resource: .attributes.resource_name, parent_id: .attributes.parent_id}'
```

## Files Modified

1. **`tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/Azure/Functions/AzureFunctionsCommon.cs`**
   - Modified span creation logic (lines 262-290)
   - Added `TryExtractSpanContextFromActivity` helper method (lines 477-543)

2. **`tracer/src/Datadog.Trace/Util/EnvironmentHelpers.cs`**
   - Fixed ConfigurationKeys → PlatformKeys (line 173)

3. **`D:/source/datadog/serverless-dev-apps/azure/functions/dotnet/isolated-dotnet8-aspnetcore/Functions.cs`** (test app)
   - Modified to return trace_id and span_id in response for easier trace lookup

## Alternative Solutions Considered

1. **Store Scope in FunctionContext.Features** - Rejected: Requires access to FunctionContext in DiagnosticObserver, couples instrumentation to Azure Functions
2. **Fix ExecutionContext Flow** - Rejected: Likely issue in Azure Functions runtime (outside our control)
3. **Create Scope in CallTarget** - Rejected: Duplicates ASP.NET Core instrumentation logic, may miss ASP.NET Core-specific details

## Next Steps

1. ✅ Implement the solution (completed)
2. ✅ Verify code compiles (completed)
3. ⏳ Test with baseline (broken) code to establish issue baseline
4. ⏳ Apply fix and test to verify it resolves the issue
5. ⏳ Run Azure Functions integration tests
6. ⏳ Create pull request with findings

## References

- **Investigation Document**: [APMSVLS-58-Azure-Functions-span-parenting.md](./APMSVLS-58-Azure-Functions-span-parenting.md)
- **Azure Functions Guide**: [AzureFunctions.md](../AzureFunctions.md)
- **Azure Functions Architecture**: [AzureFunctions-Architecture.md](../AzureFunctions-Architecture.md)
- **Activity Handler Reference**: `tracer/src/Datadog.Trace/Activity/Handlers/ActivityHandlerCommon.cs:80-86`
