# Azure Functions Span Parenting Fix - Test Results
**Date**: 2026-01-29
**Trace ID**: `697be6d40000000000710c25e093bbc4`

## Test Execution

1. ✅ Built NuGet package: `Datadog.AzureFunctions.3.37.0.nupkg`
2. ✅ Deployed to: `lucasp-premium-linux-isolated-aspnet`
3. ✅ Triggered function: `https://lucasp-premium-linux-isolated-aspnet.azurewebsites.net/api/HttpTest`
4. ✅ Retrieved spans from Datadog API

## Trace Analysis

### Span Hierarchy

**HOST PROCESS** (process_id: 27):
```
azure_functions.invoke (span: 14649688245808190204) [ROOT]
└─ parent_id: 0
   └─ http.request (span: 4769262256366565961) - not in query results
```

**WORKER PROCESS** (process_id: 58):
```
aspnet_core.request (span: 6489312288077679870)
└─ parent_id: 4769262256366565961 (host's http.request)
   │
   └─ azure_functions.invoke (span: 15431669645236193747) ✅ CORRECT!
      └─ parent_id: 6489312288077679870 (aspnet_core.request)
         │
         └─ test_span (span: 2340021095460581344)
            └─ parent_id: 15431669645236193747
```

## Verification Results

✅ **PRIMARY FIX VERIFIED**: Worker's `azure_functions.invoke` span (15431669645236193747) is correctly parented to `aspnet_core.request` span (6489312288077679870)

✅ **All spans in same trace**: All spans share trace_id `697be6d40000000000710c25e093bbc4`

✅ **Proper span hierarchy**: 
- `aspnet_core.request` → `azure_functions.invoke` → `test_span`

✅ **HttpContext.Items bridge working**: The span context successfully flowed from AspNetCore middleware to Azure Functions middleware via HttpContext.Items

⚠️ **Host span still present**: The host's `azure_functions.invoke` span (14649688245808190204) is still created. This is a secondary priority item to address in a future update.

## Code Changes Validated

1. ✅ Skip stale header extraction when ASP.NET Core integration detected (checks for `"HttpRequestContext"` key)
2. ✅ Store scope in `HttpContext.Items[HttpContextActiveScopeKey]` 
3. ✅ Retrieve scope via `GetAspNetCoreScope()` helper
4. ✅ Use retrieved scope as parent for `azure_functions.invoke` span

## Conclusion

The primary issue is **RESOLVED**. The worker's `azure_functions.invoke` span is now correctly parented to the `aspnet_core.request` span, creating the proper trace hierarchy for isolated Azure Functions with ASP.NET Core integration.
