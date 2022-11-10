// <copyright file="BlockingMiddleware.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
#if !NETFRAMEWORK
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Waf;
using Microsoft.AspNetCore.Http;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore;

internal class BlockingMiddleware
{
    private readonly bool _runSecurity;
    private readonly bool _endPipeline;

    internal BlockingMiddleware(RequestDelegate? next = null, bool runSecurity = false, bool endPipeline = false)
    {
        Next = next;
        _runSecurity = runSecurity;
        _endPipeline = endPipeline;
    }

    protected RequestDelegate? Next { get; }

    protected static Task WriteResponse(HttpContext context, SecuritySettings settings, out bool endedResponse)
    {
        var httpResponse = context.Response;
        if (!httpResponse.HasStarted)
        {
            httpResponse.Clear();
            foreach (var cookie in context.Request.Cookies)
            {
                httpResponse.Cookies.Delete(cookie.Key);
            }

            httpResponse.Headers.Clear();
            httpResponse.StatusCode = 403;
            var template = settings.BlockedJsonTemplate;
            if (context.Request.Headers["Accept"].ToString().Contains("text/html"))
            {
                httpResponse.ContentType = "text/html";
                template = settings.BlockedHtmlTemplate;
            }
            else
            {
                httpResponse.ContentType = "application/json";
            }

            endedResponse = true;
            return httpResponse.WriteAsync(template);
        }

        context.Response.Body.Dispose();
        endedResponse = true;
        return Task.CompletedTask;
    }

    internal virtual async Task Invoke(HttpContext context)
    {
        var security = Security.Instance;
        if (security.Settings.Enabled)
        {
            var endedResponse = false;
            var securityTransport = new SecurityTransport(security, context, Tracer.Instance.ActiveScope.Span as Span);

            if (_runSecurity)
            {
                if (_endPipeline && !context.Response.HasStarted)
                {
                    context.Response.StatusCode = 404;
                }

                using var result = securityTransport.ShouldBlock();
                if (result.ShouldBeReported)
                {
                    var shouldBlock = result.ReturnCode == ReturnCode.Block;
                    if (shouldBlock)
                    {
                        await WriteResponse(context, security.Settings, out endedResponse).ConfigureAwait(false);
                    }

                    securityTransport.Report(result, endedResponse);
                    securityTransport.AddResponseHeaderTags();
                }
            }

            if (Next != null && !endedResponse)
            {
                try
                {
                    await Next(context).ConfigureAwait(false);
                }
                catch (BlockException e)
                {
                    await WriteResponse(context, security.Settings, out endedResponse).ConfigureAwait(false);
                    securityTransport.Report(e.Result, endedResponse);
                    securityTransport.AddResponseHeaderTags();
                }
            }
        }
    }
}
#endif
