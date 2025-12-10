// <copyright file="AppEndpointsPayload.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.Telemetry;

internal sealed class AppEndpointsPayload(ICollection<AppEndpointData> endpoints, bool isFirst) : IPayload
{
    /// <summary>
    /// Gets or sets a value indicating whether the payload is the first one sent
    /// during the lifecycle of the application.
    /// </summary>
    public bool IsFirst { get; set; } = isFirst;

    /// <summary>
    /// Gets or sets the endpoints collected of the application.
    /// </summary>
    public ICollection<AppEndpointData> Endpoints { get; set; } = endpoints;
}
