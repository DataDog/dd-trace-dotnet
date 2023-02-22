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
using Datadog.Trace.AspNet;
using Datadog.Trace.Logging;
using Microsoft.AspNetCore.Http;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore;

internal class BlockingMiddleware
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<BlockingMiddleware>();

    private readonly bool _endPipeline;
    // if we add support for ASP.NET Core on .NET Framework, we can't directly reference RequestDelegate, so this would need to be written
    private readonly RequestDelegate? _next;

    internal BlockingMiddleware(RequestDelegate? next = null, bool endPipeline = false)
    {
        _next = next;
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
            httpResponse.ContentType = "application/json";

            foreach (var header in context.Request.Headers)
            {
                if (string.Equals(header.Key, "Accept", StringComparison.OrdinalIgnoreCase))
                {
                    var textHtmlContentType = MimeTypes.TextHtml;
                    foreach (var value in header.Value)
                    {
                        if (value.Contains(textHtmlContentType))
                        {
                            httpResponse.ContentType = textHtmlContentType;
                            template = settings.BlockedHtmlTemplate;
                            break;
                        }
                    }

                    break;
                }
            }

            endedResponse = true;
            return httpResponse.WriteAsync(template);
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

        if (security.Settings.Enabled)
        {
            if (Tracer.Instance?.ActiveScope?.Span is Span span)
            {
                var securityCoordinator = new SecurityCoordinator(security, context, span);
                if (_endPipeline && !context.Response.HasStarted)
                {
                    context.Response.StatusCode = 404;
                }

                var result = securityCoordinator.Scan();
                if (result?.ShouldBeReported is true)
                {
                    if (result.ShouldBlock)
                    {
                        await WriteResponse(context, security.Settings, out endedResponse).ConfigureAwait(false);
                        securityCoordinator.MarkBlocked();
                    }

                    securityCoordinator.Report(result.Data, result.AggregatedTotalRuntime, result.AggregatedTotalRuntimeWithBindings, endedResponse);
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
            catch (BlockException e)
            {
                await WriteResponse(context, security.Settings, out endedResponse).ConfigureAwait(false);
                if (security.Settings.Enabled)
                {
                    if (Tracer.Instance?.ActiveScope?.Span is Span span)
                    {
                        var securityCoordinator = new SecurityCoordinator(security, context, span);
                        if (!e.Reported)
                        {
                            securityCoordinator.Report(e.TriggerData, e.AggregatedTotalRuntime, e.AggregatedTotalRuntimeWithBindings, endedResponse);
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
}
#endif
