// <copyright file="KafkaConsumerConstructorIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;

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
public class KafkaConsumerConstructorIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TConsumerBuilder>(TTarget instance, TConsumerBuilder consumer)
        where TConsumerBuilder : IConsumerBuilder
    {
        if (Tracer.Instance.Settings.IsIntegrationEnabled(KafkaConstants.IntegrationId))
        {
            foreach (var kvp in consumer.Config)
            {
                if (string.Equals(kvp.Key, "group.id", StringComparison.Ordinal))
                {
                    if (!string.IsNullOrEmpty(kvp.Value))
                    {
                        // Save the map between this consumer and a consumer group
                        return new CallTargetState(scope: null, state: kvp.Value);
                    }
                }
            }
        }

        return CallTargetState.GetDefault();
    }

    internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception exception, in CallTargetState state)
    {
        // This method is called in the Consumer constructor, so if we have an exception
        // the consumer won't be created, so no point recording it.
        if (exception is null && state.State is string s)
        {
            ConsumerGroupHelper.SetConsumerGroup(instance, s);
        }

        return CallTargetReturn.GetDefault();
    }
}
