// <copyright file="SecurityCoordinatorHelpers.Core.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Reflection;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Microsoft.AspNetCore.Http;

namespace Datadog.Trace.AppSec.Coordinator;

internal static class SecurityCoordinatorHelpers
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(SecurityCoordinatorHelpers));

    internal static readonly Type? SessionFeature = Assembly.GetAssembly(typeof(IHeaderDictionary))?.GetType("Microsoft.AspNetCore.Http.Features.ISessionFeature", throwOnError: false);

    internal static void CheckAndBlock(this Security security, HttpContext context, Span span)
    {
        if (security.AppsecEnabled)
        {
            var transport = new SecurityCoordinator.HttpTransport(context);
            if (!transport.IsBlocked)
            {
                var securityCoordinator = SecurityCoordinator.Get(security, span, context);
                var result = securityCoordinator.Scan();
                securityCoordinator.BlockAndReport(result);
            }
        }
    }

    internal static void CheckReturnedHeaders(this Security security, Span span, IHeaderDictionary headers)
    {
        try
        {
            if (security.AppsecEnabled && CoreHttpContextStore.Instance.Get() is { } httpContext)
            {
                var transport = new SecurityCoordinator.HttpTransport(httpContext);
                if (!transport.IsBlocked)
                {
                    var securityCoordinator = SecurityCoordinator.Get(security, span, transport);

                    // If the request-phase scan never ran (e.g. an MVC request short-circuited by an
                    // authorization/resource filter before ActionResponseFilter.OnActionExecuting, and the
                    // end-pipeline fallback didn't fire because an endpoint produced the response), fold the
                    // basic request addresses (method, URI, query, headers, cookies, IP) into this
                    // response-phase call so request-based rules still evaluate. This keeps coverage to a
                    // single WAF call and can still block, since FireOnStarting runs before the response is
                    // committed.
                    var requestScanCompleted = span.Context?.TraceContext?.AppSecRequestContext.RequestScanCompleted ?? false;
                    var args = requestScanCompleted
                                   ? new Dictionary<string, object>()
                                   : securityCoordinator.GetBasicRequestArgsForWaf();

                    args[AddressesConstants.ResponseStatus] = httpContext.Response.StatusCode.ToString();

                    var extractedHeaders = SecurityCoordinator.ExtractHeadersFromRequest(headers);
                    if (extractedHeaders is not null)
                    {
                        args[AddressesConstants.ResponseHeaderNoCookies] = extractedHeaders;
                    }

                    // Include response body stashed by ActionResponseFilter.OnActionExecuted
                    var pendingResponseBody = span.Context?.TraceContext?.AppSecRequestContext.TakePendingResponseBody();
                    if (pendingResponseBody is not null)
                    {
                        args[AddressesConstants.ResponseBody] = pendingResponseBody;
                    }

                    var result = securityCoordinator.RunWaf(args, true);
                    securityCoordinator.BlockAndReport(result);

                    if (!requestScanCompleted && span.Context?.TraceContext is { } traceContext)
                    {
                        traceContext.AppSecRequestContext.RequestScanCompleted = true;
                    }
                }
            }
        }
        catch (BlockException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error extracting HTTP headers to create header tags.");
        }
    }

    /// <summary>
    /// Performs the consolidated request-phase WAF scan, combining basic request data
    /// (method, URI, query, headers, cookies, IP), path parameters, session ID, and request body.
    /// May legitimately run more than once for a request when the data differs between hooks: for
    /// Razor Pages the route-matched event runs it first with basic data (no bound body), then the
    /// model-binding hook (DefaultModelBindingContext_SetResult_Integration) runs it again once the
    /// body has been model-bound. Each call sets RequestScanCompleted so the response-phase scan and
    /// the end-pipeline fallback know a request-phase scan has happened.
    /// </summary>
    internal static void RunRequestScan(this Security security, HttpContext context, Span span, IDictionary<string, object>? pathParams, object? requestBody)
    {
        if (!security.AppsecEnabled)
        {
            return;
        }

        var appSecRequestContext = span.Context?.TraceContext?.AppSecRequestContext;

        var transport = new SecurityCoordinator.HttpTransport(context);
        if (transport.IsBlocked)
        {
            return;
        }

        var securityCoordinator = SecurityCoordinator.Get(security, span, transport);
        var args = securityCoordinator.GetBasicRequestArgsForWaf();

        if (pathParams?.Count > 0)
        {
            args[AddressesConstants.RequestPathParams] = pathParams;
        }

        if (requestBody is not null)
        {
            args[AddressesConstants.RequestBody] = requestBody;
        }

        // we need to check context.Features.Get<ISessionFeature> as accessing the Session item if session has not been configured for the application is throwing InvalidOperationException
        var sessionFeature = context.Features[SessionFeature];
        Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore.UserEvents.ISessionFeature? sessionFeatureProxy = null;
        if (sessionFeature is not null)
        {
            sessionFeatureProxy = sessionFeature.DuckCast<ClrProfiler.AutoInstrumentation.AspNetCore.UserEvents.ISessionFeature>();
        }

        IResult? result;
        if (sessionFeatureProxy?.Session?.IsAvailable == true)
        {
            result = securityCoordinator.RunWaf(args, sessionId: sessionFeatureProxy.Session.Id);
        }
        else
        {
            result = securityCoordinator.RunWaf(args);
        }

        securityCoordinator.BlockAndReport(result);

        if (appSecRequestContext is not null)
        {
            appSecRequestContext.RequestScanCompleted = true;
        }
    }
}
#endif
