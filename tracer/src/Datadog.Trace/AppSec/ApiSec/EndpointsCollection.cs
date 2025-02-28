// <copyright file="EndpointsCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP2_2_OR_GREATER

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using Datadog.Trace.AppSec.ApiSec.DuckType;
using Datadog.Trace.DiagnosticListeners;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry;
using Microsoft.AspNetCore.Routing;

namespace Datadog.Trace.AppSec;

internal class EndpointsCollection
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<EndpointsCollection>();

    public static void CollectEndpoints(IReadOnlyList<object> endpoints)
    {
        var maxEndpoints = Security.Instance.ApiSecurity.GetEndpointsCollectionMessageLimit();

        List<AsmEndpointData> discoveredEndpoints = [];

        for (var i = 0; i < endpoints.Count && discoveredEndpoints.Count < maxEndpoints; i++)
        {
            CollectEndpoint(endpoints[i], discoveredEndpoints, maxEndpoints);
        }

        var mapEndpoints = MapEndpointsCollection.GetMapEndpointsAndClean();

        for (var j = 0; j < mapEndpoints.Count && discoveredEndpoints.Count < maxEndpoints; j++)
        {
            discoveredEndpoints.Add(new AsmEndpointData("*", mapEndpoints[j]));
        }

        // Send To telemetry
        ReportEndpoints(discoveredEndpoints);
    }

    private static void CollectEndpoint(object endpoint, List<AsmEndpointData> discoveredEndpoints, int maxEndpoints)
    {
        if (!endpoint.TryDuckCast<RouteEndpoint>(out var routeEndpoint))
        {
            return;
        }

        var routePattern = routeEndpoint.RoutePattern;
        var endpointMetadataCollection = routeEndpoint.Metadata.DuckCast<IEndpointMetadataCollection>();
        string path;

#if NETCOREAPP3_0_OR_GREATER
        // >= 3.0
        var areaName = routePattern.RequiredValues.TryGetValue("area", out var area) ? area as string : null;
        var controllerName = routePattern.RequiredValues.TryGetValue("controller", out var controller) ? controller as string : null;
        var actionName = routePattern.RequiredValues.TryGetValue("action", out var action) ? action as string : null;
        path = AspNetCoreResourceNameHelper.SimplifyRoutePattern(routePattern, routePattern.RequiredValues, areaName, controllerName, actionName, false);
#elif NETCOREAPP2_2_OR_GREATER
        // Only 2.2
        if (endpointMetadataCollection.GetRouteValuesAddressMetadata() is { RequiredValues: { } address })
        {
            var areaName = address.TryGetValue("area", out var area) ? area as string : null;
            var controllerName = address.TryGetValue("controller", out var controller) ? controller as string : null;
            var actionName = address.TryGetValue("action", out var action) ? action as string : null;
            path = AspNetCoreResourceNameHelper.SimplifyRoutePattern(routePattern, address, areaName, controllerName, actionName, false);
        }
        else
        {
            path = routePattern.RawText;
        }
#endif

        // Check if the endpoint have constrained HTTP methods inside the metadata
        if (endpointMetadataCollection.GetHttpMethodMetadata() is { HttpMethods: { } httpMethods })
        {
            for (var i = 0; i < httpMethods.Count && discoveredEndpoints.Count < maxEndpoints; i++)
            {
                discoveredEndpoints.Add(new AsmEndpointData(httpMethods[i], path));
            }
        }
        else
        {
            discoveredEndpoints.Add(new AsmEndpointData("*", path));
        }
    }

    private static void ReportEndpoints(List<AsmEndpointData> discoveredEndpoints)
    {
        Tracer.Instance.TracerManager.Telemetry.RecordAsmEndpoints(discoveredEndpoints);
    }
}

#endif
