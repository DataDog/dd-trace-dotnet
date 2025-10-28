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
                if (!transport.IsBlocked && httpContext.Response is not null)
                {
                    var securityCoordinator = SecurityCoordinator.Get(security, span, transport);

                    var args = new Dictionary<string, object>
                    {
                        { AddressesConstants.ResponseStatus, httpContext.Response.StatusCode.ToString() },
                    };

                    var extractedHeaders = SecurityCoordinator.ExtractHeadersFromRequest(headers);
                    if (extractedHeaders is not null)
                    {
                        args.Add(AddressesConstants.ResponseHeaderNoCookies, extractedHeaders);
                    }

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
