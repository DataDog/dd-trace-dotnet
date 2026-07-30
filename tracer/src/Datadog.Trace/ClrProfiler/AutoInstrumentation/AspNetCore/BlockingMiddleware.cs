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

        // The end-pipeline middleware handles the no-endpoint (404) fallback: if no request scan has run
        // yet (because no action filter fired), perform the only WAF call for this request here.
        // For normal matched requests, RequestScanCompleted is true and we skip to avoid a third WAF call
        // (the two canonical calls are RunRequestScan in ActionResponseFilter.OnActionExecuting and
        // CheckReturnedHeaders in FireOnStarting).
        if (_endPipeline && security.AppsecEnabled)
        {
            if (Tracer.Instance?.ActiveScope?.Span is Span span)
            {
                var appSecRequestContext = span.Context?.TraceContext?.AppSecRequestContext;
                var requestScanCompleted = appSecRequestContext?.RequestScanCompleted ?? false;
                if (!requestScanCompleted)
                {
                    var securityCoordinator = SecurityCoordinator.Get(security, span, new SecurityCoordinator.HttpTransport(context));
                    if (!context.Response.HasStarted)
                    {
                        context.Response.StatusCode = 404;
                    }

                    // Mark the request-phase scan as done so the response-phase scan (CheckReturnedHeaders)
                    // doesn't redundantly re-add the basic request addresses for this 404/no-endpoint request.
                    if (appSecRequestContext is not null)
                    {
                        appSecRequestContext.RequestScanCompleted = true;
                    }

                    var result = securityCoordinator.Scan(true);
                    if (result is not null)
                    {
                        if (result.ShouldBlock)
                        {
                            var action = security.GetBlockingAction(context.Request.Headers.GetCommaSeparatedValues("Accept"), result.BlockInfo, result.RedirectInfo);
                            await WriteResponse(action, context, out endedResponse).ConfigureAwait(false);
                            securityCoordinator.MarkBlocked();
                        }

                        securityCoordinator.Reporter.TryReport(result, endedResponse);
                    }
                }
            }
            else
            {
                Log.Debug("No span available, can't check the request");
            }
        }

        if (_next != null && !endedResponse)
        {
            // Catch BlockException thrown from within the pipeline (e.g. from ActionResponseFilter.OnActionExecuting
            // or CheckReturnedHeaders / FireOnStarting) and write the blocking response here at the outermost
            // middleware boundary.
            try
            {
                await _next(context).ConfigureAwait(false);
            }
            catch (Exception e) when (GetBlockException(e) is { } blockException)
            {
                // Use the full result's block/redirect info so a redirect action (whose info is stored in
                // blockException.BlockInfo as RedirectInfo) is written as a redirect, not as a block body with
                // the redirect's 3xx status code.
                var action = security.GetBlockingAction(context.Request.Headers.GetCommaSeparatedValues("Accept"), blockException.Result.BlockInfo, blockException.Result.RedirectInfo);
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
