// <copyright file="RabbitMQProduceEdgeTagCacheKey.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.RabbitMQ;

/// <summary>
/// Value-type cache key for RabbitMQ produce edge tags. Using a named struct avoids boxing and
/// is compatible with all supported target frameworks.
/// When <see cref="Exchange"/> is empty the array uses <see cref="TopicOrRoutingKey"/> as the topic;
/// otherwise it uses <see cref="Exchange"/> and <see cref="HasRoutingKey"/>.
/// </summary>
internal readonly struct RabbitMQProduceEdgeTagCacheKey : IEquatable<RabbitMQProduceEdgeTagCacheKey>
{
    public readonly string Exchange;
    public readonly string TopicOrRoutingKey;
    public readonly bool HasRoutingKey;

    public RabbitMQProduceEdgeTagCacheKey(string exchange, string topicOrRoutingKey, bool hasRoutingKey)
    {
        Exchange = exchange;
        TopicOrRoutingKey = topicOrRoutingKey;
        HasRoutingKey = hasRoutingKey;
    }

    public bool Equals(RabbitMQProduceEdgeTagCacheKey other)
        => Exchange == other.Exchange && TopicOrRoutingKey == other.TopicOrRoutingKey && HasRoutingKey == other.HasRoutingKey;

    public override bool Equals(object? obj)
        => obj is RabbitMQProduceEdgeTagCacheKey other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + (Exchange?.GetHashCode() ?? 0);
            hash = (hash * 31) + (TopicOrRoutingKey?.GetHashCode() ?? 0);
            hash = (hash * 31) + HasRoutingKey.GetHashCode();
            return hash;
        }
    }
}
