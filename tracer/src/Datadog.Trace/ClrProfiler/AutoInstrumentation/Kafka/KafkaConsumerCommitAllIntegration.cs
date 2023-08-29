// <copyright file="KafkaConsumerCommitAllIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka;

/// <summary>
/// Confluent.Kafka Consumer.Commit calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Confluent.Kafka",
    TypeName = "Confluent.Kafka.Consumer`2",
    MethodName = "Commit",
    ReturnTypeName = KafkaConstants.TopicPartitionOffsetListTypeName,
    MinimumVersion = "1.4.0",
    MaximumVersion = "2.*.*",
    IntegrationName = KafkaConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class KafkaConsumerCommitAllIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
    {
        return CallTargetState.GetDefault();
    }

    internal static CallTargetReturn<TResponse> OnMethodEnd<TTarget, TResponse>(TTarget instance, TResponse response, Exception exception, in CallTargetState state)
        where TResponse : ITopicPartitionOffsets, IDuckType
    {
        if (exception is null && response.Instance is not null)
        {
            ConsumerGroupHelper.TryGetConsumerGroup(instance, out var groupId, out var _);
            var dataStreams = Tracer.Instance.TracerManager.DataStreamsManager;

            for (var i = 0; i < response.Count; i++)
            {
                var item = response[i];
                dataStreams.TrackBacklog(
                    $"consumer_group:{groupId},partition:{item.Partition.Value},topic:{item.Topic},type:kafka_commit",
                    item.Offset.Value);
            }
        }

        return new CallTargetReturn<TResponse>(response);
    }
}
