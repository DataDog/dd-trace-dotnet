// <copyright file="ProducerCache.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.CompilerServices;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Util.Delegates;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka;

internal class ProducerCache
{
    // A map between Kafka Producer<TKey,TValue> and the corresponding producer bootstrap servers
    private static readonly ConditionalWeakTable<object, string> ProducerToBootstrapServersMap = new();
    private static readonly ConditionalWeakTable<object, Delegate> ProducerDefaultDeliveryHandlerMap = new();

    public static void AddProducer(object producer, string bootstrapServers)
    {
#if NETCOREAPP3_1_OR_GREATER
        ProducerToBootstrapServersMap.AddOrUpdate(producer, bootstrapServers);
#else
        ProducerToBootstrapServersMap.GetValue(producer, x => bootstrapServers);
#endif
    }

    public static void CreateDefaultDeliveryHandler<TProducer>(TProducer producer)
    {
        if (producer == null)
        {
            return;
        }

        var producerType = producer.GetType();
        var deliveryReportType = producerType.Assembly.GetType("Confluent.Kafka.DeliveryReport`2");
        var genericArgs = producerType.GetGenericArguments();

        if (deliveryReportType != null)
        {
            var genericDeliveryReportType = deliveryReportType.MakeGenericType(genericArgs);
            var delegateType = typeof(Action<>).MakeGenericType(genericDeliveryReportType);

            var handler = DelegateInstrumentation.Wrap(
                null,
                delegateType,
                new ProduceDeliveryCallbacks());
#if NETCOREAPP3_1_OR_GREATER
            ProducerDefaultDeliveryHandlerMap.AddOrUpdate(producer, handler);
#else
            ProducerDefaultDeliveryHandlerMap.GetValue(producer, _ => handler);
#endif
        }
    }

    public static bool TryGetDefaultDeliveryHandler(object producer, out Delegate? handler)
        => ProducerDefaultDeliveryHandlerMap.TryGetValue(producer, out handler);

    public static bool TryGetProducer(object producer, out string? bootstrapServers)
        => ProducerToBootstrapServersMap.TryGetValue(producer, out bootstrapServers);

    public static void RemoveProducer(object producer)
    {
        ProducerToBootstrapServersMap.Remove(producer);
        ProducerDefaultDeliveryHandlerMap.Remove(producer);
    }
}
