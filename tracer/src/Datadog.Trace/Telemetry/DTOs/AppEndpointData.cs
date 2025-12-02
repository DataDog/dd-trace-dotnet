// <copyright file="AppEndpointData.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.Telemetry;

internal sealed class AppEndpointData
{
    private static readonly string[] ValidMethods = [
        "GET",
        "POST",
        "PUT",
        "DELETE",
        "PATCH",
        "HEAD",
        "OPTIONS",
        "TRACE",
        "CONNECT",
        "*"
    ];

    public AppEndpointData(string method, string path)
    {
        if (!ValidMethods.Contains(method))
        {
            throw new ArgumentException($"Invalid method {nameof(method)}");
        }

        Type = "REST";
        Method = method;
        Path = path;
        OperationName = "http.request";
        ResourceName = method == "*" ? path : $"{method} {path}";
    }

    /// <summary>
    /// Gets or sets the type of the endpoint.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets the Method of the endpoint.
    /// The wildcard '*' can be used to represent all allowed methods.
    /// </summary>
    public string Method { get; set; }

    /// <summary>
    /// Gets or sets the Path of the endpoint.
    /// It should match the <see cref="Tags.HttpRoute"/> span tag.
    /// </summary>
    public string Path { get; set; }

    /// <summary>
    /// Gets or sets the operation name for the endpoint.
    /// </summary>
    public string OperationName { get; set; }

    /// <summary>
    /// Gets or sets the resource name for the endpoint.
    /// </summary>
    public string ResourceName { get; set; }
}
