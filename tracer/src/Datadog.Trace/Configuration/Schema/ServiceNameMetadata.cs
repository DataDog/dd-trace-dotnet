// <copyright file="ServiceNameMetadata.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Configuration.Schema;

/// <summary>
/// Encapsulates a resolved service name and its source attribution.
/// The source identifies which integration set the service name (e.g., "redis", "kafka", "http-client"),
/// or is null when the default service name is used.
/// </summary>
internal readonly struct ServiceNameMetadata
{
    public ServiceNameMetadata(string serviceName, string? source)
    {
        ServiceName = serviceName;
        Source = source;
    }

    public string ServiceName { get; }

    public string? Source { get; }

    public void Deconstruct(out string serviceName, out string? source)
    {
        serviceName = ServiceName;
        source = Source;
    }
}
