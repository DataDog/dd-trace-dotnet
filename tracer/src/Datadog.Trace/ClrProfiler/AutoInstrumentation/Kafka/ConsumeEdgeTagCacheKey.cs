// <copyright file="ConsumeEdgeTagCacheKey.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka;

/// <summary>
/// Value-type cache key for consume edge tags. Using a named struct avoids boxing and
/// is compatible with all supported target frameworks.
/// </summary>
internal readonly struct ConsumeEdgeTagCacheKey : IEquatable<ConsumeEdgeTagCacheKey>
{
    public readonly string GroupId;
    public readonly string Topic;
    public readonly string ClusterId;

    public ConsumeEdgeTagCacheKey(string groupId, string topic, string clusterId)
    {
        GroupId = groupId;
        Topic = topic;
        ClusterId = clusterId;
    }

    public bool Equals(ConsumeEdgeTagCacheKey other)
        => GroupId == other.GroupId && Topic == other.Topic && ClusterId == other.ClusterId;

    public override bool Equals(object? obj)
        => obj is ConsumeEdgeTagCacheKey other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + (GroupId?.GetHashCode() ?? 0);
            hash = (hash * 31) + (Topic?.GetHashCode() ?? 0);
            hash = (hash * 31) + (ClusterId?.GetHashCode() ?? 0);
            return hash;
        }
    }
}
