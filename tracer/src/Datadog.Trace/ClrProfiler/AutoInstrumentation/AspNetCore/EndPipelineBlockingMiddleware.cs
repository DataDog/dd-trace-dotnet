// <copyright file="EndPipelineBlockingMiddleware.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
#if !NETFRAMEWORK
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Microsoft.AspNetCore.Http;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore;

internal class EndPipelineBlockingMiddleware : BlockingMiddleware
{
    public EndPipelineBlockingMiddleware(RequestDelegate? requestDelegate, bool runSecurity)
        : base(requestDelegate, runSecurity)
    {
    }

    internal override Task Invoke(HttpContext context)
    {
        context.Response.StatusCode = 404;
        return base.Invoke(context);
    }
}
#endif
