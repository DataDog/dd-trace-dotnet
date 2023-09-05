// <copyright file="ProducerCache.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.CompilerServices;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka;

internal class ProducerCache
{
    // A map between Kafka Producer<TKey,TValue> and the corresponding producer bootstrap servers
    private static readonly ConditionalWeakTable<object, string> ProducerToBootstrapServersMap = new();
    private static readonly ConditionalWeakTable<object, Action<object>> ProducerDefaultDeliveryHandlerMap = new();
    private static readonly Action<object> DefaultDeliveryHandler = _ => { };

    public static void AddProducer(object producer, string bootstrapServers)
    {
#if NETCOREAPP3_1_OR_GREATER
        ProducerToBootstrapServersMap.AddOrUpdate(producer, bootstrapServers);
#else
        ProducerToBootstrapServersMap.GetValue(producer, x => bootstrapServers);
#endif
    }

    public static void CreateDefaultDeliveryHandler(object producer)
    {
#if NETCOREAPP3_1_OR_GREATER
        ProducerDefaultDeliveryHandlerMap.AddOrUpdate(producer, DefaultDeliveryHandler);
#else
        ProducerDefaultDeliveryHandlerMap.GetValue(producer, x => DefaultDeliveryHandler);
#endif
    }

    public static bool TryGetDefaultDeliveryHandler(object producer, out Action<object>? handler)
        => ProducerDefaultDeliveryHandlerMap.TryGetValue(producer, out handler);

    public static bool TryGetProducer(object producer, out string? bootstrapServers)
        => ProducerToBootstrapServersMap.TryGetValue(producer, out bootstrapServers);

    public static void RemoveProducer(object producer)
    {
        ProducerToBootstrapServersMap.Remove(producer);
    }
}
