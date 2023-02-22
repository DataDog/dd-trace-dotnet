// <copyright file="SecurityCoordinatorHelpers.Core.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;

namespace Datadog.Trace.AppSec.Coordinator;

internal static class SecurityCoordinatorHelpers
{
    internal static void CheckAndBlock(this Security security, HttpContext context, Span span)
    {
        if (security.Settings.Enabled)
        {
            var transport = new SecurityCoordinator.HttpTransport(context);
            if (!transport.IsBlocked)
            {
                var securityCoordinator = new SecurityCoordinator(security, context, span, transport);
                var result = securityCoordinator.Scan();
                securityCoordinator.CheckAndBlock(result);
            }
        }
    }

    internal static void CheckPathParams(this Security security, HttpContext context, Span span, IDictionary<string, object> pathParams)
    {
        if (security.Settings.Enabled)
        {
            var transport = new SecurityCoordinator.HttpTransport(context);
            if (!transport.IsBlocked)
            {
                var securityCoordinator = new SecurityCoordinator(security, context, span, transport);
                var args = new Dictionary<string, object> { { AddressesConstants.RequestPathParams, pathParams } };
                var result = securityCoordinator.RunWaf(args);
                securityCoordinator.CheckAndBlock(result);
            }
        }
    }

    internal static void CheckUser(this Security security, HttpContext context, Span span, string userId)
    {
        if (security.Settings.Enabled)
        {
            var transport = new SecurityCoordinator.HttpTransport(context);
            if (!transport.IsBlocked)
            {
                var securityCoordinator = new SecurityCoordinator(security, context, span, transport);
                var args = new Dictionary<string, object> { { AddressesConstants.UserId, userId } };
                var result = securityCoordinator.RunWaf(args);
                securityCoordinator.CheckAndBlock(result);
            }
        }
    }

    internal static void CheckPathParamsFromAction(this Security security, HttpContext context, Span span, IList<ParameterDescriptor>? actionPathParams, RouteValueDictionary routeValues)
    {
        if (security.Settings.Enabled && actionPathParams != null)
        {
            var transport = new SecurityCoordinator.HttpTransport(context);
            if (!transport.IsBlocked)
            {
                var securityCoordinator = new SecurityCoordinator(security, context, span, transport);
                var pathParams = new Dictionary<string, object>(actionPathParams.Count);
                for (var i = 0; i < actionPathParams.Count; i++)
                {
                    var p = actionPathParams[i];
                    if (routeValues.ContainsKey(p.Name))
                    {
                        pathParams.Add(p.Name, routeValues[p.Name]);
                    }

                    var args = new Dictionary<string, object> { { AddressesConstants.RequestPathParams, pathParams } };
                    var result = securityCoordinator.RunWaf(args);
                    securityCoordinator.CheckAndBlock(result);
                }
            }
        }
    }

    internal static void CheckBody(this Security security, HttpContext context, Span span, object body)
    {
        var transport = new SecurityCoordinator.HttpTransport(context);
        if (!transport.IsBlocked)
        {
            var securityCoordinator = new SecurityCoordinator(security, context, span, transport);
            var keysAndValues = ObjectExtractor.Extract(body);
            var args = new Dictionary<string, object> { { AddressesConstants.RequestBody, keysAndValues } };
            var result = securityCoordinator.RunWaf(args);
            securityCoordinator.CheckAndBlock(result);
        }
    }
}
#endif
