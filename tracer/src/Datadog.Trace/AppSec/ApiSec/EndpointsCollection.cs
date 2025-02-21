// <copyright file="EndpointsCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Datadog.Trace.DiagnosticListeners;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry;
using Microsoft.AspNetCore.Routing;

namespace Datadog.Trace.AppSec;

internal class EndpointsCollection
{
    private static readonly Type? HttpMethodMetadataType = Type.GetType("Microsoft.AspNetCore.Routing.HttpMethodMetadata, Microsoft.AspNetCore.Routing");
    private static readonly PropertyInfo? HttpMethodsProperty = HttpMethodMetadataType?.GetProperty("HttpMethods", BindingFlags.Public | BindingFlags.Instance);
    private static readonly Type? EndpointMetadataCollectionType = Type.GetType("Microsoft.AspNetCore.Http.EndpointMetadataCollection, Microsoft.AspNetCore.Routing.Abstractions");
    private static readonly MethodInfo? GetMetadataMethod = EndpointMetadataCollectionType?.GetMethod("GetMetadata", BindingFlags.Public | BindingFlags.Instance);
    private static readonly MethodInfo? GenericGetMetadataMethod = HttpMethodMetadataType != null ? GetMetadataMethod?.MakeGenericMethod(HttpMethodMetadataType) : null;

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<EndpointsCollection>();

    public static void CollectEndpoints(IReadOnlyList<object> endpoints)
    {
        var maxEndpoints = 300;

        if (!EnsureTypesExists())
        {
            Log.Warning("Api Sec: Types not found for gathering endpoints.");
            return;
        }

        // todo: payload: add
        List<AsmEndpointData> discoveredEndpoints = [];
        for (var i = 0; i < endpoints.Count && i < maxEndpoints; i++)
        {
            CollectEndpoint(endpoints[i], discoveredEndpoints);
        }

        // Send To telemetry
        ReportEndpoints(discoveredEndpoints);
    }

    private static bool EnsureTypesExists()
    {
        return HttpMethodMetadataType != null && EndpointMetadataCollectionType != null && GetMetadataMethod != null;
    }

    private static void CollectEndpoint(object endpoint, List<AsmEndpointData> discoveredEndpoints)
    {
        if (endpoint.TryDuckCast<RouteEndpoint>(out var routeEndpoint))
        {
            var routePattern = routeEndpoint.RoutePattern;
            var path = routePattern.RawText;
            if (path is null)
            {
                return;
            }

#if NETCOREAPP3_0_OR_GREATER
            var areaName = routePattern.RequiredValues.TryGetValue("area", out var area) ? area as string : null;
            var controllerName = routePattern.RequiredValues.TryGetValue("controller", out var controller) ? controller as string : null;
            var actionName = routePattern.RequiredValues.TryGetValue("action", out var action) ? action as string : null;
            path = AspNetCoreResourceNameHelper.SimplifyRoutePattern(routePattern, routePattern.RequiredValues, areaName, controllerName, actionName, false);
#endif

            // Check if the endpoint have constrained HTTP methods
            var metadata = GenericGetMetadataMethod?.Invoke(routeEndpoint.Metadata, null);

            if (metadata != null && HttpMethodsProperty?.GetValue(metadata) is IEnumerable<string> httpMethods)
            {
                discoveredEndpoints.AddRange(httpMethods.Select(method => new AsmEndpointData(method, path)));
            }
            else
            {
                discoveredEndpoints.Add(new AsmEndpointData("*", path));
            }
        }
    }

    private static void ReportEndpoints(List<AsmEndpointData> discoveredEndpoints)
    {
        Tracer.Instance.TracerManager.Telemetry.RecordAsmEndpoints(discoveredEndpoints);
    }
}

#endif
