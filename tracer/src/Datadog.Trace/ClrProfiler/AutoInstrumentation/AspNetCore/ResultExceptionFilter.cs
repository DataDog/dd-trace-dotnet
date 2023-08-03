// <copyright file="ResultExceptionFilter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK

using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Coordinator;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore;

internal class ResultExceptionFilter : IResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
        if (context.Exception is BlockException be)
        {
            var security = Security.Instance;
            var action = security.GetBlockingAction(be.Result?.Actions[0] ?? "block", context.HttpContext.Request.Headers.GetCommaSeparatedValues("Accept"));
            // todo: to check how to make it safe with async and also maybe have this write response function somewhere else
            BlockingMiddleware.WriteResponse(action, context.HttpContext, out var endedResponse).ConfigureAwait(false).GetAwaiter().GetResult();
            context.HttpContext.Items["block"] = true;
        }
    }
}
#endif
