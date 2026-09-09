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
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;

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
                    if (!securityCoordinator.ShouldScanResponse())
                    {
                        return;
                    }

                    var args = BuildResponseArgs(httpContext.Response.StatusCode, headers);
                    var result = securityCoordinator.RunWaf(args, true);
                    securityCoordinator.BlockAndReport(result);
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

    // Last chance to send the response status on servers with no response start hook, like HTTP.sys. The
    // response is already on the wire, so a block can only be reported. A response that hasn't started is
    // left to the hook, which runs after the pipeline and can still block.
    internal static void CheckResponseAtRequestEnd(this in SecurityCoordinator securityCoordinator, HttpContext httpContext)
    {
        try
        {
            if (securityCoordinator.IsBlocked)
            {
                return;
            }

            if (!httpContext.Response.HasStarted && !HasNoResponseStartHook(httpContext.Features.Get<IHttpResponseFeature>()?.GetType().FullName))
            {
                return;
            }

            if (!securityCoordinator.ShouldScanResponse())
            {
                return;
            }

            var args = BuildResponseArgs(httpContext.Response.StatusCode, httpContext.Response.Headers);
            if (securityCoordinator.RunWaf(args, true) is { } result)
            {
                securityCoordinator.Reporter.TryReport(result, blocked: false);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error running the security checks on the response at the end of the request.");
        }
    }

    // Only HTTP.sys, and only positively: any other server keeps the previous behaviour rather than
    // risking that this runs before a hook that would have been able to block.
    internal static bool HasNoResponseStartHook(string? responseFeatureTypeName) =>
        responseFeatureTypeName?.StartsWith("Microsoft.AspNetCore.Server.HttpSys.", StringComparison.Ordinal) == true;

    private static Dictionary<string, object> BuildResponseArgs(int statusCode, IHeaderDictionary headers)
    {
        var args = new Dictionary<string, object>(2) { { AddressesConstants.ResponseStatus, statusCode.ToString() } };

        if (SecurityCoordinator.ExtractHeadersFromRequest(headers) is { } extractedHeaders)
        {
            args.Add(AddressesConstants.ResponseHeaderNoCookies, extractedHeaders);
        }

        return args;
    }

    internal static void CheckPathParamsAndSessionId(this Security security, HttpContext context, Span span, IDictionary<string, object> pathParams)
    {
        if (security.AppsecEnabled)
        {
            var transport = new SecurityCoordinator.HttpTransport(context);
            if (!transport.IsBlocked)
            {
                var securityCoordinator = SecurityCoordinator.Get(security, span, transport);
                var args = new Dictionary<string, object> { { AddressesConstants.RequestPathParams, pathParams } };
                IResult? result;
                // we need to check context.Features.Get<ISessionFeature> as accessing the Session item if session has not been configured for the application is throwing InvalidOperationException
                var sessionFeature = context.Features[SessionFeature];
                Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore.UserEvents.ISessionFeature? sessionFeatureProxy = null;
                if (sessionFeature is not null)
                {
                    sessionFeatureProxy = sessionFeature.DuckCast<ClrProfiler.AutoInstrumentation.AspNetCore.UserEvents.ISessionFeature>();
                }

                if (sessionFeatureProxy?.Session?.IsAvailable == true)
                {
                    result = securityCoordinator.RunWaf(args, sessionId: sessionFeatureProxy.Session.Id);
                }
                else
                {
                    result = securityCoordinator.RunWaf(args);
                }

                securityCoordinator.BlockAndReport(result);
            }
        }
    }

    internal static void CheckPathParamsFromAction(this Security security, HttpContext context, Span span, IList<ParameterDescriptor>? actionPathParams, RouteValueDictionary routeValues)
    {
        if (security.AppsecEnabled && actionPathParams != null)
        {
            var transport = new SecurityCoordinator.HttpTransport(context);
            if (!transport.IsBlocked)
            {
                var securityCoordinator = SecurityCoordinator.Get(security, span, transport);
                var pathParams = new Dictionary<string, object>(actionPathParams.Count);
                for (var i = 0; i < actionPathParams.Count; i++)
                {
                    var p = actionPathParams[i];
                    if (routeValues.TryGetValue(p.Name, out var value))
                    {
                        pathParams.Add(p.Name, value);
                    }
                }

                if (pathParams.Count == 0)
                {
                    return;
                }

                var args = new Dictionary<string, object> { { AddressesConstants.RequestPathParams, pathParams } };
                var result = securityCoordinator.RunWaf(args);
                securityCoordinator.BlockAndReport(result);
            }
        }
    }

    internal static object? CheckBody(this Security security, HttpContext context, Span span, object body, bool response)
    {
        if (response && !security.Settings.ApiSecurityParseResponseBody)
        {
            return null;
        }

        var transport = new SecurityCoordinator.HttpTransport(context);
        if (!transport.IsBlocked)
        {
            var securityCoordinator = SecurityCoordinator.Get(security, span, transport);
            var keysAndValues = ObjectExtractor.Extract(body);

            if (keysAndValues is not null)
            {
                var args = new Dictionary<string, object> { { response ? AddressesConstants.ResponseBody : AddressesConstants.RequestBody, keysAndValues } };
                var result = securityCoordinator.RunWaf(args);
                securityCoordinator.BlockAndReport(result);
                return keysAndValues;
            }
        }

        return null;
    }
}
#endif
