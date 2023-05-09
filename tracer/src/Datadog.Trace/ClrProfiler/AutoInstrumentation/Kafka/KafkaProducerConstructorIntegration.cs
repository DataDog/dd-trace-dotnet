// <copyright file="KafkaProducerConstructorIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka;

/// <summary>
/// Confluent.Kafka Producer() calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Confluent.Kafka",
    TypeName = "Confluent.Kafka.Producer`2",
    MethodName = ".ctor",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = new[] { "Confluent.Kafka.ProducerBuilder`2[!0,!1]" },
    MinimumVersion = "1.4.0",
    MaximumVersion = "2.*.*",
    IntegrationName = KafkaConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class KafkaProducerConstructorIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TProducerBuilder>(TTarget instance, TProducerBuilder consumer)
        where TProducerBuilder : IProducerBuilder
    {
        if (Tracer.Instance.Settings.IsIntegrationEnabled(KafkaConstants.IntegrationId))
        {
            foreach (var kvp in consumer.Config)
            {
                if (string.Equals(kvp.Key, "bootstrap.servers", StringComparison.Ordinal))
                {
                    if (!string.IsNullOrEmpty(kvp.Value))
                    {
                        // Save the map between this consumer and a consumer group
                        ProducerCache.AddProducer(instance, kvp.Value);
                        return CallTargetState.GetDefault();
                    }
                }
            }
        }

        return CallTargetState.GetDefault();
    }

    internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception exception, in CallTargetState state)
    {
        // This method is called in the Producer constructor, so if we have an exception
        // the consumer won't be created, so no point recording it.
        if (exception is not null)
        {
            ProducerCache.RemoveProducer(instance);
        }

        return CallTargetReturn.GetDefault();
    }
}
