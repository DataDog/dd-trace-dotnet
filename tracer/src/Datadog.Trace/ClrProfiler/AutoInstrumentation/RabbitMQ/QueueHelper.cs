// <copyright file="QueueHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Runtime.CompilerServices;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.RabbitMQ;

internal class QueueHelper
{
    // A map between RabbitMQ Consumer<TKey,TValue> and the corresponding queue
    private static readonly ConditionalWeakTable<string, string> ConsumerToQueueMap = new();

    public static void SetQueue(string consumerTag, string queue)
    {
#if NETCOREAPP3_1_OR_GREATER
        ConsumerToQueueMap.AddOrUpdate(consumerTag, queue);
#else
        ConsumerToQueueMap.GetValue(consumerTag, x => queue);
#endif
    }

    public static bool TryGetQueue(string consumerTag, out string? queue)
    {
        queue = null;

        return ConsumerToQueueMap.TryGetValue(consumerTag, out queue);
    }

    public static void RemoveQueue(string consumerTag)
    {
        ConsumerToQueueMap.Remove(consumerTag);
    }
}
