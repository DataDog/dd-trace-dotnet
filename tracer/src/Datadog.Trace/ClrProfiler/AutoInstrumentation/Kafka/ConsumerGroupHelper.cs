// <copyright file="ConsumerGroupHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Runtime.CompilerServices;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka;

internal class ConsumerGroupHelper
{
    // A map between Kafka Consumer<TKey,TValue> and the corresponding consumer group
    private static readonly ConditionalWeakTable<object, string> ConsumerToGroupIdMap = new();
    private static readonly ConditionalWeakTable<object, string> ConsumerToBootstrapServersMap = new();

    public static void SetConsumerGroup(object consumer, string groupId, string bootstrapServers)
    {
#if NETCOREAPP3_1_OR_GREATER
        ConsumerToGroupIdMap.AddOrUpdate(consumer, groupId);
        ConsumerToBootstrapServersMap.AddOrUpdate(consumer, bootstrapServers);
#else
        ConsumerToGroupIdMap.GetValue(consumer, x => groupId);
        ConsumerToBootstrapServersMap.GetValue(consumer, x => bootstrapServers);
#endif
    }

    public static bool TryGetConsumerGroup(object consumer, out string? groupId, out string? bootstrapServers)
    {
        groupId = null;
        bootstrapServers = null;

        return ConsumerToGroupIdMap.TryGetValue(consumer, out groupId) && ConsumerToBootstrapServersMap.TryGetValue(consumer, out bootstrapServers);
    }

    public static void RemoveConsumerGroup(object consumer)
    {
        ConsumerToGroupIdMap.Remove(consumer);
        ConsumerToBootstrapServersMap.Remove(consumer);
    }
}
