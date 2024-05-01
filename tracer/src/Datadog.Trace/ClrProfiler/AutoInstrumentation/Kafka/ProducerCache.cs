// <copyright file="ProducerCache.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Datadog.Trace.Util.Delegates;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka;

internal static class ProducerCache
{
    // A map between Kafka Producer<TKey,TValue> and the corresponding producer bootstrap servers
    private static readonly ConditionalWeakTable<object, string> ProducerToBootstrapServersMap = new();
    private static readonly ConditionalWeakTable<object, Delegate?> ProducerToDefaultDeliveryHandlerMap = new();

    public static void AddBootstrapServers(object producer, string bootstrapServers)
    {
#if NETCOREAPP3_1_OR_GREATER
        ProducerToBootstrapServersMap.AddOrUpdate(producer, bootstrapServers);
#else
        ProducerToBootstrapServersMap.GetValue(producer, x => bootstrapServers);
#endif
    }

    public static void AddDefaultDeliveryHandler(object producer)
    {
#if NETCOREAPP3_1_OR_GREATER
        ProducerToDefaultDeliveryHandlerMap.AddOrUpdate(producer, CreateDefaultDeliveryHandler(producer.GetType()));
#else
        ProducerToDefaultDeliveryHandlerMap.GetValue(producer, x => CreateDefaultDeliveryHandler(producer.GetType()));
#endif
    }

    private static Delegate? CreateDefaultDeliveryHandler(Type producerType)
    {
        var deliveryReportType = producerType.Assembly.GetType("Confluent.Kafka.DeliveryReport`2");
        var genericArgs = producerType.GetGenericArguments();

        if (deliveryReportType != null)
        {
            var genericDeliveryReportType = deliveryReportType.MakeGenericType(genericArgs);
            var delegateType = typeof(Action<>).MakeGenericType(genericDeliveryReportType);

            return DelegateInstrumentation.Wrap(
                null,
                delegateType,
                new ProduceDeliveryCallbacks());
        }

        return null;
    }

    public static bool TryGetProducer(object producer, out string? bootstrapServers)
        => ProducerToBootstrapServersMap.TryGetValue(producer, out bootstrapServers);

    public static bool TryGetDefaultDeliveryHandler(object producer, out Delegate? handler)
    {
        return ProducerToDefaultDeliveryHandlerMap.TryGetValue(producer, out handler);
    }

    public static void RemoveProducer(object producer)
    {
        ProducerToBootstrapServersMap.Remove(producer);
        ProducerToDefaultDeliveryHandlerMap.Remove(producer);
    }
}
