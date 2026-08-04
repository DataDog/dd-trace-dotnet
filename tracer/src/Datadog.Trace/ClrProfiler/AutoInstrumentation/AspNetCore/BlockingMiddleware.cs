// <copyright file="BlockingMiddleware.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
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

    /// <summary>
    /// Applies the blocking action to the response and hands back the body that is still to be written,
    /// if any. Everything here is an <see cref="HttpContext"/> access and nothing writes to the body, so
    /// the caller can guard this against a recycled context without also swallowing failures from the
    /// write itself.
    /// </summary>
    /// <returns>The response body to write, or <c>null</c> when there is nothing left to write.</returns>
    private static string? PrepareBlockingResponse(BlockingAction action, HttpContext context, HttpResponse httpResponse, out bool endedResponse)
    {
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

                return null;
            }

            httpResponse.ContentType = action.ContentType;

            return action.ResponseContent;
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

        return null;
    }

    /// <summary>
    /// ASP.NET Core pools <see cref="HttpContext"/> instances and uninitializes them (setting their feature
    /// collection to null) once a request is over, so a context we're still holding can become unreadable at
    /// any point. Every member then throws from inside ASP.NET Core itself, and letting that escape would
    /// surface our middleware as a 500 in the customer's application.
    /// </summary>
    private static bool IsRecycledContextException(Exception e) => e is NullReferenceException or ObjectDisposedException;

    private static void TrySetEndPipelineStatusCode(HttpContext context)
    {
        try
        {
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = 404;
            }
        }
        catch (Exception e) when (IsRecycledContextException(e))
        {
            Log.Debug(e, "Exception while trying to set the status code of a Context, skipping it.");
        }
    }

    /// <returns><c>true</c> if the response was ended, meaning the rest of the pipeline must not run.</returns>
    private static async Task<bool> TryWriteBlockingResponse(Security security, HttpContext context, Dictionary<string, object?>? blockInfo, Dictionary<string, object?>? redirectInfo)
    {
        // only the HttpContext accesses are guarded, and each one as narrowly as possible: an exception from
        // our own GetBlockingAction, or from the body write (which runs on a context we have just proved
        // readable, and which invokes the customer's response features), is a real error we must not
        // misreport as a recycled context and swallow
        string[]? acceptHeaders;
        HttpResponse httpResponse;
        try
        {
            acceptHeaders = context.Request.Headers.GetCommaSeparatedValues("Accept");
            httpResponse = context.Response;
        }
        catch (Exception e) when (IsRecycledContextException(e))
        {
            return CouldNotWriteBlockingResponse(e);
        }

        var action = security.GetBlockingAction(acceptHeaders, blockInfo, redirectInfo);

        string? responseContent;
        bool endedResponse;
        try
        {
            responseContent = PrepareBlockingResponse(action, context, httpResponse, out endedResponse);
        }
        catch (Exception e) when (IsRecycledContextException(e))
        {
            return CouldNotWriteBlockingResponse(e);
        }

        if (responseContent is not null)
        {
            await httpResponse.WriteAsync(responseContent).ConfigureAwait(false);
        }

        return endedResponse;
    }

    private static bool CouldNotWriteBlockingResponse(Exception e)
    {
        // we decided to block, so if we can't write the response we still have to stop the pipeline:
        // failing open here would let a request we flagged as malicious be served
        Log.Debug(e, "Exception while trying to write the blocking response to a Context.");
        return true;
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
                if (_endPipeline)
                {
                    TrySetEndPipelineStatusCode(context);
                }

                // _endPipeline: true won't happen unless the EndpointMiddleware couldn't find an endpoint to serve. Most of the time this middleware will be called just at the beginning of the pipeline. We still want it in the end to run discovery scans checks.
                var result = securityCoordinator.Scan(_endPipeline);
                if (result is not null)
                {
                    if (result.ShouldBlock)
                    {
                        endedResponse = await TryWriteBlockingResponse(security, context, result.BlockInfo, result.RedirectInfo).ConfigureAwait(false);
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
                endedResponse = await TryWriteBlockingResponse(security, context, blockException.BlockInfo, null).ConfigureAwait(false);
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
