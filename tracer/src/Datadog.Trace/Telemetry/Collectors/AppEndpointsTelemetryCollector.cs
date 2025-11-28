// <copyright file="AppEndpointsTelemetryCollector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.Telemetry.Collectors;

internal sealed class AppEndpointsTelemetryCollector
{
    private ICollection<AppEndpointData>? _endpoints;

    /// <summary>
    /// Records the endpoints to be collected.
    /// </summary>
    /// <param name="endpoints">The list of endpoints collected.</param>
    public void RecordEndpoints(ICollection<AppEndpointData> endpoints)
    {
        _endpoints = endpoints;
    }

    /// <summary>
    /// Returns the collected endpoints and clears the internal state.
    /// </summary>
    /// <returns>The collected endpoints.</returns>
    public ICollection<AppEndpointData>? GetData()
    {
        var endpoints = _endpoints;
        _endpoints = null;
        return endpoints;
    }
}
