// <copyright file="SqsEdgeTagCacheKey.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SQS;

/// <summary>
/// Value-type cache key for SQS edge tags. Using a named struct avoids boxing and
/// is compatible with all supported target frameworks.
/// <see cref="IsConsume"/> distinguishes produce (direction:out) from consume (direction:in)
/// so that both directions share a single cache type without key collision.
/// </summary>
internal readonly struct SqsEdgeTagCacheKey : IEquatable<SqsEdgeTagCacheKey>
{
    public readonly string QueueName;
    public readonly bool IsConsume;

    public SqsEdgeTagCacheKey(string queueName, bool isConsume)
    {
        QueueName = queueName;
        IsConsume = isConsume;
    }

    public bool Equals(SqsEdgeTagCacheKey other)
        => QueueName == other.QueueName && IsConsume == other.IsConsume;

    public override bool Equals(object? obj)
        => obj is SqsEdgeTagCacheKey other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + (QueueName?.GetHashCode() ?? 0);
            hash = (hash * 31) + IsConsume.GetHashCode();
            return hash;
        }
    }
}
