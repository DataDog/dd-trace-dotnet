// <copyright file="ProduceBacklogTagCacheKey.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka;

/// <summary>
/// Value-type cache key for produce backlog tags. Using a named struct avoids boxing and
/// is compatible with all supported target frameworks.
/// </summary>
internal readonly struct ProduceBacklogTagCacheKey : IEquatable<ProduceBacklogTagCacheKey>
{
    public readonly string ClusterId;
    public readonly int Partition;
    public readonly string Topic;

    public ProduceBacklogTagCacheKey(string clusterId, int partition, string topic)
    {
        ClusterId = clusterId;
        Partition = partition;
        Topic = topic;
    }

    public bool Equals(ProduceBacklogTagCacheKey other)
        => ClusterId == other.ClusterId && Partition == other.Partition && Topic == other.Topic;

    public override bool Equals(object? obj)
        => obj is ProduceBacklogTagCacheKey other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + ClusterId.GetHashCode();
            hash = (hash * 31) + Partition;
            hash = (hash * 31) + Topic.GetHashCode();
            return hash;
        }
    }
}
