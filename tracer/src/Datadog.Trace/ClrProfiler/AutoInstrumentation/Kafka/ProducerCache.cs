// <copyright file="ProducerCache.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Datadog.Trace.Util.Delegates;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka;

internal static class ProducerCache
{
    // A map between Kafka Producer<TKey,TValue> and the corresponding producer bootstrap servers
    private static readonly ConditionalWeakTable<object, string> ProducerToBootstrapServersMap = new();
    private static readonly ConditionalWeakTable<object, string> ProducerToClusterIdMap = new();
    private static readonly ConditionalWeakTable<object, Delegate?> ProducerToDefaultDeliveryHandlerMap = new();

    public static void AddBootstrapServers(object producer, string bootstrapServers, string clusterId)
    {
#if NETCOREAPP3_1_OR_GREATER
        ProducerToBootstrapServersMap.AddOrUpdate(producer, bootstrapServers);
        ProducerToClusterIdMap.AddOrUpdate(producer, clusterId);
#else
        ProducerToBootstrapServersMap.GetValue(producer, x => bootstrapServers);
        ProducerToClusterIdMap.GetValue(producer, x => clusterId);
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

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The NativeAOT build keeps the types looked up by constant names, with all their members (ReflectionRootDescriptor).")]
    [UnconditionalSuppressMessage("Trimming", "IL2055", Justification = "DeliveryReport<TKey, TValue> is a class: its instantiations over reference types share their canonical code, and the NativeAOT build keeps the ones over value types of the producers the application creates (KafkaDeliveryReportDirectives).")]
    [UnconditionalSuppressMessage("AotAnalysis", "IL3050", Justification = "DeliveryReport<TKey, TValue> is a class: its instantiations over reference types share their canonical code, and the NativeAOT build keeps the ones over value types of the producers the application creates (KafkaDeliveryReportDirectives).")]
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

    public static bool TryGetProducer(object producer, out string? bootstrapServers, out string? clusterId)
    {
        bootstrapServers = null;
        clusterId = null;

        return ProducerToBootstrapServersMap.TryGetValue(producer, out bootstrapServers)
            && ProducerToClusterIdMap.TryGetValue(producer, out clusterId);
    }

    public static bool TryGetDefaultDeliveryHandler(object producer, out Delegate? handler)
    {
        return ProducerToDefaultDeliveryHandlerMap.TryGetValue(producer, out handler);
    }

    public static void RemoveProducer(object producer)
    {
        ProducerToBootstrapServersMap.Remove(producer);
        ProducerToClusterIdMap.Remove(producer);
        ProducerToDefaultDeliveryHandlerMap.Remove(producer);
    }
}
