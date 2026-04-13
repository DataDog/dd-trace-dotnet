// <copyright file="RabbitMQConsumeEdgeTagCacheKey.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.RabbitMQ;

/// <summary>
/// Value-type cache key for RabbitMQ consume edge tags. Using a named struct avoids boxing and
/// is compatible with all supported target frameworks.
/// </summary>
internal readonly struct RabbitMQConsumeEdgeTagCacheKey : IEquatable<RabbitMQConsumeEdgeTagCacheKey>
{
    public readonly string TopicOrRoutingKey;

    public RabbitMQConsumeEdgeTagCacheKey(string topicOrRoutingKey)
    {
        TopicOrRoutingKey = topicOrRoutingKey;
    }

    public bool Equals(RabbitMQConsumeEdgeTagCacheKey other)
        => TopicOrRoutingKey == other.TopicOrRoutingKey;

    public override bool Equals(object? obj)
        => obj is RabbitMQConsumeEdgeTagCacheKey other && Equals(other);

    public override int GetHashCode()
        => TopicOrRoutingKey?.GetHashCode() ?? 0;
}
