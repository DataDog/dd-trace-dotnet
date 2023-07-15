// <copyright file="QueueHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.CompilerServices;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.RabbitMQ;

internal class QueueHelper
{
    // A map between RabbitMQ Consumer<TKey,TValue> and the corresponding queue
    private static readonly ConditionalWeakTable<object, string> ConsumerToQueueMap = new();

    public static void SetQueue(object consumer, string queue)
    {
#if NETCOREAPP3_1_OR_GREATER
        ConsumerToQueueMap.AddOrUpdate(consumer, queue);
#else
        ConsumerToQueueMap.GetValue(consumer, x => queue);
#endif
        Console.WriteLine("Done setting queue: " + consumer + ", " + queue);

        if (ConsumerToQueueMap.TryGetValue(consumer, out var readQueue))
        {
            Console.WriteLine("Able to immediately read queue: " + consumer + ", " + readQueue);
            Console.WriteLine(ConsumerToQueueMap);
        }
        else
        {
            Console.WriteLine("UNABLE to immediately read queue: " + consumer);
        }
    }

    public static bool TryGetQueue(object consumer, out string? queue)
    {
        Console.WriteLine("TryGetQueue: " + consumer);
        Console.WriteLine(ConsumerToQueueMap);
        queue = null;

        return ConsumerToQueueMap.TryGetValue(consumer, out queue);
    }

    public static void RemoveQueue(object consumer)
    {
        Console.WriteLine("RemoveQueue: " + consumer);
        ConsumerToQueueMap.Remove(consumer);
    }
}
