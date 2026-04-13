// <copyright file="ServiceBusEdgeTagCacheKey.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.ServiceBus;

/// <summary>
/// Value-type cache key for Azure Service Bus consume edge tags. Using a named struct avoids boxing and
/// is compatible with all supported target frameworks.
/// </summary>
internal readonly struct ServiceBusEdgeTagCacheKey : IEquatable<ServiceBusEdgeTagCacheKey>
{
    public readonly string EntityPath;

    public ServiceBusEdgeTagCacheKey(string entityPath)
    {
        EntityPath = entityPath;
    }

    public bool Equals(ServiceBusEdgeTagCacheKey other)
        => EntityPath == other.EntityPath;

    public override bool Equals(object? obj)
        => obj is ServiceBusEdgeTagCacheKey other && Equals(other);

    public override int GetHashCode()
        => EntityPath?.GetHashCode() ?? 0;
}
