// <copyright file="RabbitMQProduceEdgeTagCacheKey.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.RabbitMQ;

/// <summary>
/// Value-type cache key for RabbitMQ produce edge tags. Using a named struct avoids boxing and
/// is compatible with all supported target frameworks.
/// When <see cref="Exchange"/> is empty the array uses <see cref="TopicOrRoutingKey"/> as the topic;
/// otherwise it uses <see cref="Exchange"/> and <see cref="HasRoutingKey"/>.
/// </summary>
internal readonly record struct RabbitMQProduceEdgeTagCacheKey(string Exchange, string TopicOrRoutingKey, bool HasRoutingKey);
