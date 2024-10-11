// <copyright file="SecurityCoordinatorHelpers.Core.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using Datadog.Trace.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;

namespace Datadog.Trace.AppSec.Coordinator;

internal static class SecurityCoordinatorHelpers
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(SecurityCoordinatorHelpers));

    internal static void CheckAndBlock(this Security security, HttpContext context, Span span)
    {
        if (security.Enabled)
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
            if (security.Enabled && CoreHttpContextStore.Instance.Get() is { } httpContext)
            {
                var transport = new SecurityCoordinator.HttpTransport(httpContext);
                if (!transport.IsBlocked)
                {
                    var securityCoordinator = SecurityCoordinator.Get(security, span, transport);

                    var args = new Dictionary<string, object>
                    {
                        {
                            AddressesConstants.ResponseHeaderNoCookies,
                            SecurityCoordinator.ExtractHeadersFromRequest(headers)
                        },
                        { AddressesConstants.ResponseStatus, httpContext.Response.StatusCode.ToString() },
                    };

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

    internal static void CheckPathParams(this Security security, HttpContext context, Span span, IDictionary<string, object> pathParams)
    {
        if (security.Enabled)
        {
            var transport = new SecurityCoordinator.HttpTransport(context);
            if (!transport.IsBlocked)
            {
                var securityCoordinator = SecurityCoordinator.Get(security, span, transport);
                var args = new Dictionary<string, object> { { AddressesConstants.RequestPathParams, pathParams } };
                var result = securityCoordinator.RunWaf(args);
                securityCoordinator.BlockAndReport(result);
            }
        }
    }

    internal static void CheckUser(this Security security, HttpContext context, Span span, string userId)
    {
        if (security.Enabled)
        {
            var transport = new SecurityCoordinator.HttpTransport(context);
            if (!transport.IsBlocked)
            {
                var securityCoordinator = SecurityCoordinator.Get(security, span, transport);
                var args = new Dictionary<string, object> { { AddressesConstants.UserId, userId } };
                var result = securityCoordinator.RunWaf(args);
                securityCoordinator.BlockAndReport(result);
            }
        }
    }

    internal static void CheckPathParamsFromAction(this Security security, HttpContext context, Span span, IList<ParameterDescriptor>? actionPathParams, RouteValueDictionary routeValues)
    {
        if (security.Enabled && actionPathParams != null)
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
