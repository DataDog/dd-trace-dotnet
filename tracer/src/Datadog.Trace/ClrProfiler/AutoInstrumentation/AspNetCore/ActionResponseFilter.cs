// <copyright file="ActionResponseFilter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if !NETFRAMEWORK

using System.Collections.Generic;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Coordinator;
using Datadog.Trace.DuckTyping;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore;

internal sealed class ActionResponseFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        var security = Security.Instance;
        if (!security.AppsecEnabled)
        {
            return;
        }

        // Use the local root span (the request span the WAF reports against), not the innermost active
        // scope, which may be a nested child created by user code inside the filter pipeline. The
        // SecurityCoordinator normalizes to the root via TryGetRoot anyway, but resolving it here keeps
        // this consistent with the response-phase scan in FireOnStartCommon and avoids silently skipping
        // the scan when no scope is active.
        if (Tracer.Instance.InternalActiveScope?.Root?.Span is not { } currentSpan)
        {
            return;
        }

        // Build path params filtered to action-declared parameters (excludes framework noise like controller/action/area)
        Dictionary<string, object>? pathParams = null;
        var actionParams = context.ActionDescriptor.Parameters;
        if (actionParams is { Count: > 0 })
        {
            var routeValues = context.RouteData.Values;
            pathParams = new Dictionary<string, object>(actionParams.Count);
            for (var i = 0; i < actionParams.Count; i++)
            {
                var p = actionParams[i];
                if (routeValues.TryGetValue(p.Name, out var value) && value is not null)
                {
                    pathParams[p.Name] = value;
                }
            }

            if (pathParams.Count == 0)
            {
                pathParams = null;
            }
        }

        // Get request body stashed by DefaultModelBindingContext_SetResult_Integration
        var pendingBody = currentSpan.Context?.TraceContext?.AppSecRequestContext.TakePendingRequestBody();

        security.RunRequestScan(context.HttpContext, currentSpan, pathParams, pendingBody);
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        var security = Security.Instance;
        if (security.AppsecEnabled
            && security.Settings.ApiSecurityParseResponseBody
            && context.Result.TryDuckCast<ObjectResult>(out var result)
            && result.Value is not null
            && Tracer.Instance.InternalActiveScope?.Root?.Span is { } currentSpan)
        {
            // Stash the extracted response body; it will be included in the response-phase WAF call
            // in CheckReturnedHeaders (FireOnStarting) to keep the total WAF runs to 2 per request.
            var extracted = ObjectExtractor.Extract(result.Value);
            if (extracted is not null)
            {
                currentSpan.Context?.TraceContext?.AppSecRequestContext.SetPendingResponseBody(extracted);
            }
        }
    }
}
#endif
