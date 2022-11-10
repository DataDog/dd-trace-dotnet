// <copyright file="BlockingMiddleware.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
#if !NETFRAMEWORK
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Transports;
using Microsoft.AspNetCore.Http;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore;

internal class BlockingMiddleware
{
    private readonly bool _runSecurity;
    private readonly bool _endPipeline;
    private readonly RequestDelegate? _next;

    internal BlockingMiddleware(RequestDelegate? next = null, bool runSecurity = false, bool endPipeline = false)
    {
        _next = next;
        _runSecurity = runSecurity;
        _endPipeline = endPipeline;
    }

    private static Task WriteResponse(HttpContext context, SecuritySettings settings, out bool endedResponse)
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

    internal async Task Invoke(HttpContext context)
    {
        var security = Security.Instance;
        var endedResponse = false;
        if (security.Settings.Enabled)
        {
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
                    if (result.Block)
                    {
                        await WriteResponse(context, security.Settings, out endedResponse).ConfigureAwait(false);
                        securityTransport.MarkBlocked();
                    }

                    securityTransport.Report(result, endedResponse);
                }
            }

            if (_next != null && !endedResponse)
            {
                // unlikely that security is disabled and there's a blockexception, but might happen as race condition
                try
                {
                    await _next(context).ConfigureAwait(false);
                }
                catch (BlockException e)
                {
                    await WriteResponse(context, security.Settings, out endedResponse).ConfigureAwait(false);
                    if (!e.Reported)
                    {
                        securityTransport.Report(e.Result, endedResponse);
                    }

                    securityTransport.Cleanup();
                }
            }
        }
        else
        {
            if (_next != null)
            {
                await _next(context).ConfigureAwait(false);
            }
        }
    }
}
#endif
