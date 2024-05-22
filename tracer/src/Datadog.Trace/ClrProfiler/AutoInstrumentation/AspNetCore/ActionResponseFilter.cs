// <copyright file="ActionResponseFilter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if !NETFRAMEWORK

using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Coordinator;
using Datadog.Trace.DuckTyping;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore;

internal class ActionResponseFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        var security = Security.Instance;
        if (security.Enabled
            && context.Result.TryDuckCast<ObjectResult>(out var result)
            && result.Value is not null
            && Tracer.Instance.ActiveScope?.Span is Span currentSpan)
        {
            security.CheckBody(context.HttpContext, currentSpan, result.Value, response: true);
        }
    }
}
#endif
