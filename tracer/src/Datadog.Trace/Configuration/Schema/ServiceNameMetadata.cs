// <copyright file="ServiceNameMetadata.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.Configuration.Schema;

/// <summary>
/// Encapsulates a resolved service name and its source attribution.
/// The source identifies which integration set the service name (e.g., "redis", "kafka", "http-client"),
/// or is null when the default service name is used.
/// </summary>
internal readonly struct ServiceNameMetadata
{
    internal const string OptServiceMapping = "opt.service_mapping";
    internal const string Manual = "m";

    public ServiceNameMetadata(string serviceName, string? source)
    {
        ServiceName = serviceName;
        Source = source;
    }

    public string ServiceName { get; }

    public string? Source { get; }

    internal static ServiceNameMetadata Resolve(
        string integrationKey,
        string defaultServiceName,
        IReadOnlyDictionary<string, string>? serviceNameMappings,
        bool useSuffix)
    {
        if (serviceNameMappings is not null && serviceNameMappings.TryGetValue(integrationKey, out var mappedName))
        {
            return new(mappedName, OptServiceMapping);
        }

        var name = useSuffix ? $"{defaultServiceName}-{integrationKey}" : defaultServiceName;
        return new(name, name != defaultServiceName ? integrationKey : null);
    }

    public void Deconstruct(out string serviceName, out string? source)
    {
        serviceName = ServiceName;
        source = Source;
    }
}
