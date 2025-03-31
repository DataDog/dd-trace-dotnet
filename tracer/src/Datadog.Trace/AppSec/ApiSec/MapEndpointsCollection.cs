// <copyright file="MapEndpointsCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Datadog.Trace.AppSec;

internal static class MapEndpointsCollection
{
    private static readonly object Lock = new();
    private static Stack<string>? _buildingMapEndpoints = [];
    private static HashSet<string>? _mapEndpoints = [];

    public static void BeggingMapEndpoint(string path)
    {
        lock (Lock)
        {
            _buildingMapEndpoints?.Push(path);
        }
    }

    public static void EndMapEndpoint()
    {
        lock (Lock)
        {
            if (_buildingMapEndpoints?.Count > 0)
            {
                _buildingMapEndpoints.Pop();
            }
        }
    }

    public static void DetectedAvailableEndpoint()
    {
        lock (Lock)
        {
            if (_buildingMapEndpoints is null)
            {
                return;
            }

            var sb = new StringBuilder();

            for (var i = _buildingMapEndpoints.Count - 1; i >= 0; i--)
            {
                try
                {
                    sb.Append(_buildingMapEndpoints.ElementAt(i));
                }
                catch (Exception e)
                {
                    ApiSecurity.Log.Debug(e, "Error while building map endpoint");
                }
            }

            _mapEndpoints?.Add(sb.ToString());
        }
    }

    public static List<string> GetMapEndpointsAndClean()
    {
        lock (Lock)
        {
            if (_mapEndpoints is null)
            {
                return [];
            }

            var endpoints = _mapEndpoints.ToList();

            _buildingMapEndpoints = null;
            _mapEndpoints = null;

            return endpoints;
        }
    }
}
