// <copyright file="ConsumerCache.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.CompilerServices;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka;

internal static class ConsumerCache
{
    // A map between Kafka Consumer<TKey,TValue> and the corresponding consumer group
    private static readonly ConditionalWeakTable<object, string> ConsumerToGroupIdMap = new();
    private static readonly ConditionalWeakTable<object, string> ConsumerToBootstrapServersMap = new();
    private static readonly ConditionalWeakTable<object, string> ConsumerToClusterIdMap = new();
    private static readonly ConditionalWeakTable<object, Type?> ConsumerToOffsetsCommittedHandlerMap = new();

    public static void SetConsumerGroup(object consumer, string groupId, string bootstrapServers, string clusterId)
    {
#if NETCOREAPP3_1_OR_GREATER
        ConsumerToGroupIdMap.AddOrUpdate(consumer, groupId);
        ConsumerToBootstrapServersMap.AddOrUpdate(consumer, bootstrapServers);
        ConsumerToClusterIdMap.AddOrUpdate(consumer, clusterId);
#else
        ConsumerToGroupIdMap.GetValue(consumer, _ => groupId);
        ConsumerToBootstrapServersMap.GetValue(consumer, _ => bootstrapServers);
        ConsumerToClusterIdMap.GetValue(consumer, _ => clusterId);
#endif
    }

    public static bool TryGetConsumerGroup(object consumer, out string? groupId, out string? bootstrapServers, out string? clusterId)
    {
        groupId = null;
        bootstrapServers = null;
        clusterId = null;

        return ConsumerToGroupIdMap.TryGetValue(consumer, out groupId)
            && ConsumerToBootstrapServersMap.TryGetValue(consumer, out bootstrapServers)
            && ConsumerToClusterIdMap.TryGetValue(consumer, out clusterId);
    }

    public static void RemoveConsumerGroup(object consumer)
    {
        ConsumerToGroupIdMap.Remove(consumer);
        ConsumerToBootstrapServersMap.Remove(consumer);
        ConsumerToClusterIdMap.Remove(consumer);
        ConsumerToOffsetsCommittedHandlerMap.Remove(consumer);
    }
}
