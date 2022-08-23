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

    public static void SetConsumerGroup(object consumer, string groupId)
    {
#if NETCOREAPP3_1_OR_GREATER
        ConsumerToGroupIdMap.AddOrUpdate(consumer, groupId);
#else
        ConsumerToGroupIdMap.GetValue(consumer, x => groupId);
#endif
    }

    public static bool TryGetConsumerGroup(object consumer, out string? groupId)
        => ConsumerToGroupIdMap.TryGetValue(consumer, out groupId);

    public static bool RemoveConsumerGroup(object consumer)
        => ConsumerToGroupIdMap.Remove(consumer);
}
