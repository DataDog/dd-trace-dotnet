// <copyright file="KafkaConsumerConstructorIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Util.Delegates;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka;

/// <summary>
/// Confluent.Kafka Consumer() calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Confluent.Kafka",
    TypeName = "Confluent.Kafka.Consumer`2",
    MethodName = ".ctor",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = new[] { "Confluent.Kafka.ConsumerBuilder`2[!0,!1]" },
    MinimumVersion = "1.4.0",
    MaximumVersion = "2.*.*",
    IntegrationName = KafkaConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class KafkaConsumerConstructorIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TConsumerBuilder>(TTarget instance, TConsumerBuilder consumer)
        where TConsumerBuilder : IConsumerBuilder
    {
        var tracer = Tracer.Instance;
        if (tracer.CurrentTraceSettings.Settings.IsIntegrationEnabled(KafkaConstants.IntegrationId))
        {
            string groupId = null;
            string bootstrapServers = null;

            foreach (var kvp in consumer.Config)
            {
                if (string.Equals(kvp.Key, KafkaHelper.GroupIdKey, StringComparison.Ordinal))
                {
                    if (!string.IsNullOrEmpty(kvp.Value))
                    {
                        groupId = kvp.Value;
                    }
                }
                else if (string.Equals(kvp.Key, KafkaHelper.BootstrapServersKey, StringComparison.Ordinal))
                {
                    if (!string.IsNullOrEmpty(kvp.Value))
                    {
                        bootstrapServers = kvp.Value;
                    }
                }
            }

            if (tracer.TracerManager.DataStreamsManager.IsEnabled)
            {
                // add handler to track committed offsets
                consumer.OffsetsCommittedHandler = consumer.OffsetsCommittedHandler.Instrument(new OffsetsCommittedCallbacks(groupId));
            }

            // Only config setting "group.id" is required, so assert that the value is non-null before adding to the ConsumerGroup cache
            if (groupId is not null)
            {
                return new CallTargetState(scope: null, state: new string[] { groupId, bootstrapServers });
            }
        }

        return CallTargetState.GetDefault();
    }

    internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception exception, in CallTargetState state)
    {
        if (exception is null && state is { State: string[] { Length: 2 } config })
        {
            var groupId = config[0];
            var bootstrapServers = config[1];
            var clusterId = KafkaHelper.GetClusterId(bootstrapServers, instance) ?? string.Empty;
            ConsumerCache.SetConsumerGroup(instance, groupId, bootstrapServers, clusterId);
        }

        return CallTargetReturn.GetDefault();
    }
}
