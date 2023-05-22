// <copyright file="ProducerCache.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Runtime.CompilerServices;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka;

internal class ProducerCache
{
    // A map between Kafka Producer<TKey,TValue> and the corresponding producer bootstrap servers
    private static readonly ConditionalWeakTable<object, string> ProducerToBootstrapServersMap = new();

    public static void AddProducer(object producer, string bootstrapServers)
    {
#if NETCOREAPP3_1_OR_GREATER
        ProducerToBootstrapServersMap.AddOrUpdate(producer, bootstrapServers);
#else
        ProducerToBootstrapServersMap.GetValue(producer, x => bootstrapServers);
#endif
    }

    public static bool TryGetProducer(object producer, out string? bootstrapServers)
        => ProducerToBootstrapServersMap.TryGetValue(producer, out bootstrapServers);

    public static void RemoveProducer(object producer)
    {
        ProducerToBootstrapServersMap.Remove(producer);
    }
}
