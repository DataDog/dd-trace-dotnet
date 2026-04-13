// <copyright file="ProduceEdgeTagCacheKey.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka;

/// <summary>
/// Value-type cache key for produce edge tags. Using a named struct avoids boxing and
/// is compatible with all supported target frameworks.
/// </summary>
internal readonly struct ProduceEdgeTagCacheKey : IEquatable<ProduceEdgeTagCacheKey>
{
    public readonly string ClusterId;
    public readonly string Topic;

    public ProduceEdgeTagCacheKey(string clusterId, string topic)
    {
        ClusterId = clusterId;
        Topic = topic;
    }

    public bool Equals(ProduceEdgeTagCacheKey other)
        => ClusterId == other.ClusterId && Topic == other.Topic;

    public override bool Equals(object? obj)
        => obj is ProduceEdgeTagCacheKey other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + (ClusterId?.GetHashCode() ?? 0);
            hash = (hash * 31) + (Topic?.GetHashCode() ?? 0);
            return hash;
        }
    }
}
