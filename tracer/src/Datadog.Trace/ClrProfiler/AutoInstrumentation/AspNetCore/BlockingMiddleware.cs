// <copyright file="BlockingMiddleware.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
#if !NETFRAMEWORK
using System;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Coordinator;
using Datadog.Trace.Logging;
using Microsoft.AspNetCore.Http;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore;

internal class BlockingMiddleware
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<BlockingMiddleware>();

    private readonly bool _endPipeline;

    // if we add support for ASP.NET Core on .NET Framework, we can't directly reference RequestDelegate, so this would need to be written
    private readonly RequestDelegate? _next;
    private readonly bool _startPipeline;

    internal BlockingMiddleware(RequestDelegate? next = null, bool startPipeline = false, bool endPipeline = false)
    {
        _next = next;
        _startPipeline = startPipeline;
        _endPipeline = endPipeline;
    }

    private static Task WriteResponse(BlockingAction action, HttpContext context, out bool endedResponse)
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
            httpResponse.StatusCode = action.StatusCode;

            if (action.IsRedirect)
            {
                httpResponse.Redirect(action.RedirectLocation, action.IsPermanentRedirect);
                endedResponse = true;
            }
            else
            {
                httpResponse.ContentType = action.ContentType;
                endedResponse = true;
                return httpResponse.WriteAsync(action.ResponseContent);
            }

            return Task.CompletedTask;
        }

        try
        {
            context.Abort();
            endedResponse = true;
        }
        catch (Exception)
        {
            endedResponse = false;
        }

        return Task.CompletedTask;
    }

    internal async Task Invoke(HttpContext context)
    {
        var security = Security.Instance;
        var endedResponse = false;

        if (security.Enabled)
        {
            if (Tracer.Instance?.ActiveScope?.Span is Span span)
            {
                var securityCoordinator = new SecurityCoordinator(security, context, span);
                if (_endPipeline && !context.Response.HasStarted)
                {
                    context.Response.StatusCode = 404;
                }

                var result = securityCoordinator.Scan(_startPipeline);
                if (result is not null)
                {
                    if (result.ShouldBlock)
                    {
                        var action = security.GetBlockingAction(result.Actions[0], context.Request.Headers.GetCommaSeparatedValues("Accept"));
                        await WriteResponse(action, context, out endedResponse).ConfigureAwait(false);
                        securityCoordinator.MarkBlocked();
                    }

                    securityCoordinator.TryReport(result, endedResponse);
                    // security will be disposed in endrequest of diagnostic observer in any case
                }
            }
            else
            {
                Log.Error("No span available, can't check the request");
            }
        }

        if (_next != null && !endedResponse)
        {
            // unlikely that security is disabled and there's a block exception, but might happen as race condition
            try
            {
                await _next(context).ConfigureAwait(false);
            }
            catch (Exception e) when (GetBlockException(e) is { } blockException)
            {
                var action = security.GetBlockingAction(blockException!.Result.Actions[0], context.Request.Headers.GetCommaSeparatedValues("Accept"));
                await WriteResponse(action, context, out endedResponse).ConfigureAwait(false);
                if (security.Enabled)
                {
                    if (Tracer.Instance?.ActiveScope?.Span is Span span)
                    {
                        var securityCoordinator = new SecurityCoordinator(security, context, span);
                        if (!blockException.Reported)
                        {
                            securityCoordinator.TryReport(blockException.Result, endedResponse);
                        }

                        securityCoordinator.AddResponseHeadersToSpanAndCleanup();
                    }
                    else
                    {
                        Log.Error("No span available, can't report the request");
                    }
                }
            }
        }
    }

    private static BlockException? GetBlockException(Exception? exception)
    {
        while (exception is not null)
        {
            if (exception is BlockException b)
            {
                return b;
            }

            exception = exception.InnerException;
        }

        return null;
    }
}
#endif
