// <copyright file="EndpointsCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.AppSec.ApiSec.DuckType;
using Datadog.Trace.DiagnosticListeners;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry;

namespace Datadog.Trace.AppSec;

internal sealed class EndpointsCollection
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<EndpointsCollection>();

    public static void CollectEndpoints(IReadOnlyList<object> endpoints)
    {
        var maxEndpoints = Security.Instance.ApiSecurity.GetEndpointsCollectionMessageLimit();

        List<AppEndpointData> discoveredEndpoints = [];

        for (var i = 0; i < endpoints.Count && discoveredEndpoints.Count < maxEndpoints; i++)
        {
            CollectEndpoint(endpoints[i], discoveredEndpoints, maxEndpoints);
        }

        var mapEndpoints = MapEndpointsCollection.GetMapEndpointsAndClean();

        for (var j = 0; j < mapEndpoints.Count && discoveredEndpoints.Count < maxEndpoints; j++)
        {
            InsertEndpoint("*", mapEndpoints[j], discoveredEndpoints);
        }

        // Send To telemetry
        ReportEndpoints(discoveredEndpoints);
    }

    private static void CollectEndpoint(object endpoint, List<AppEndpointData> discoveredEndpoints, int maxEndpoints)
    {
        if (!endpoint.TryDuckCast<RouteEndpoint>(out var routeEndpoint))
        {
            return;
        }

        if (!routeEndpoint.RoutePattern.TryDuckCast<RoutePattern>(out var routePattern))
        {
            return;
        }

        string? path;

        if (routeEndpoint.RoutePattern.TryDuckCast<RoutePatternRequiredValues>(out var routePatternV3))
        {
            // >= 3.0
            var areaName = routePatternV3.RequiredValues.TryGetValue("area", out var area) ? area as string : null;
            var controllerName = routePatternV3.RequiredValues.TryGetValue("controller", out var controller) ? controller as string : null;
            var actionName = routePatternV3.RequiredValues.TryGetValue("action", out var action) ? action as string : null;
            path = AspNetCoreResourceNameHelper.SimplifyRoutePattern(routePattern, routePatternV3.RequiredValues, areaName, controllerName, actionName, false);
        }
        else if (routeEndpoint.Metadata.TryDuckCast<IEndpointMetadataCollectionRouteValuesAddressMetadata>(out var endpointMetadataCollectionRouteValuesAddressMetadata)
              && endpointMetadataCollectionRouteValuesAddressMetadata.GetRouteValuesAddressMetadata() is { RequiredValues: { } address })
        {
            // Only 2.2
            var areaName = address.TryGetValue("area", out var area) ? area as string : null;
            var controllerName = address.TryGetValue("controller", out var controller) ? controller as string : null;
            var actionName = address.TryGetValue("action", out var action) ? action as string : null;
            path = AspNetCoreResourceNameHelper.SimplifyRoutePattern(routePattern, address, areaName, controllerName, actionName, false);
        }
        else
        {
            path = routePattern.RawText;
        }

        if (path is null)
        {
            return;
        }

        // Check if the endpoint have constrained HTTP methods inside the metadata
        if (routeEndpoint.Metadata.TryDuckCast<IEndpointMetadataCollectionHttpMethodMetadata>(out var endpointMetadataCollectionHttpMethodMetadata)
            && endpointMetadataCollectionHttpMethodMetadata.GetHttpMethodMetadata() is { HttpMethods: { } httpMethods })
        {
            for (var i = 0; i < httpMethods.Count && discoveredEndpoints.Count < maxEndpoints; i++)
            {
                InsertEndpoint(httpMethods[i], path, discoveredEndpoints);
            }
        }
        else
        {
            InsertEndpoint("*", path, discoveredEndpoints);
        }
    }

    private static void InsertEndpoint(string httpMethod, string path, List<AppEndpointData> discoveredEndpoints)
    {
        try
        {
            discoveredEndpoints.Add(new AppEndpointData(httpMethod, path));
        }
        catch (Exception e)
        {
            Log.Debug(e, "Failed to add endpoint to the list");
        }
    }

    private static void ReportEndpoints(List<AppEndpointData> discoveredEndpoints)
    {
        Tracer.Instance.TracerManager.Telemetry.RecordAppEndpoints(discoveredEndpoints);
    }
}

#endif
