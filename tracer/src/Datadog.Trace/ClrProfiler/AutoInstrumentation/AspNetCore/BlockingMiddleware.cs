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
using Microsoft.Net.Http.Headers;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore;

/// <summary>
/// Note that this middleware will be shortcircuited by the DeveloperMiddleware which is inserted at aspnetcore startup in development mode in general : app.UseDeveloperExceptionPage();
/// </summary>
internal sealed class BlockingMiddleware
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<BlockingMiddleware>();

    // if we add support for ASP.NET Core on .NET Framework, we can't directly reference RequestDelegate, so this would need to be written
    private readonly RequestDelegate? _next;
    private readonly bool _endPipeline;

    internal BlockingMiddleware(RequestDelegate? next = null, bool endPipeline = false)
    {
        _next = next;
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

            endedResponse = true;

            if (action.IsRedirect)
            {
                httpResponse.Headers[HeaderNames.Location] = action.RedirectLocation;

                return Task.CompletedTask;
            }

            httpResponse.ContentType = action.ContentType;

            return httpResponse.WriteAsync(action.ResponseContent);
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

        if (security.AppsecEnabled)
        {
            if (Tracer.Instance?.ActiveScope?.Span is Span span)
            {
                var securityCoordinator = SecurityCoordinator.Get(security, span, new SecurityCoordinator.HttpTransport(context));
                if (_endPipeline && !context.Response.HasStarted)
                {
                    context.Response.StatusCode = 404;
                }

                // _endPipeline: true won't happen unless the EndpointMiddleware couldn't find an endpoint to serve. Most of the time this middleware will be called just at the beginning of the pipeline. We still want it in the end to run discovery scans checks.
                var result = securityCoordinator.Scan(_endPipeline);
                if (result is not null)
                {
                    if (result.ShouldBlock)
                    {
                        var action = security.GetBlockingAction(context.Request.Headers.GetCommaSeparatedValues("Accept"), result.BlockInfo, result.RedirectInfo);
                        await WriteResponse(action, context, out endedResponse).ConfigureAwait(false);
                        securityCoordinator.MarkBlocked();
                    }

                    securityCoordinator.Reporter.TryReport(result, endedResponse);
                    // security will be disposed in endrequest of diagnostic observer in any case
                }
            }
            else
            {
                Log.Debug("No span available, can't check the request");
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
                // Use blockinfo here
                var action = security.GetBlockingAction(context.Request.Headers.GetCommaSeparatedValues("Accept"), blockException.BlockInfo, null);
                await WriteResponse(action, context, out endedResponse).ConfigureAwait(false);
                if (security.AppsecEnabled)
                {
                    if (Tracer.Instance?.ActiveScope?.Span is Span span)
                    {
                        var securityReporter = new SecurityReporter(span, new SecurityCoordinator.HttpTransport(context));
                        if (!blockException.Reported)
                        {
                            securityReporter.TryReport(blockException.Result, endedResponse);
                        }

                        securityReporter.AddResponseHeadersToSpan();
                    }
                    else
                    {
                        Log.Debug("No span available, can't report the request");
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
