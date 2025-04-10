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
    internal static CallTargetState OnMethodBegin<TTarget, TProducerBuilder>(TTarget instance, TProducerBuilder builder)
        where TProducerBuilder : IProducerBuilder
    {
        if (!Tracer.Instance.Settings.IsIntegrationEnabled(KafkaConstants.IntegrationId)
            || builder.Instance is null
            || builder.Config is null)
        {
            return CallTargetState.GetDefault();
        }

        var bootstrapServers = string.Empty;
        var deliveryReportsEnabled = true;
        foreach (var kvp in builder.Config)
        {
            if (string.Equals(kvp.Key, KafkaHelper.BootstrapServersKey, StringComparison.Ordinal))
            {
                if (!string.IsNullOrEmpty(kvp.Value))
                {
                    bootstrapServers = kvp.Value;
                }
            }

            if (string.Equals(kvp.Key, KafkaHelper.EnableDeliveryReportsField, StringComparison.Ordinal))
            {
                if (!string.IsNullOrEmpty(kvp.Value))
                {
                    deliveryReportsEnabled = bool.Parse(kvp.Value);
                }
            }
        }

        if (deliveryReportsEnabled)
        {
            // TODO unsure about this - if bootstrapServers is empty we return a default calltargetstate
            // but then we remove this instance from the producer cache as the state isn't set, but won't remove if no exception
            ProducerCache.AddDefaultDeliveryHandler(instance);
        }

        if (!string.IsNullOrEmpty(bootstrapServers))
        {
            // Save the map between this builder and its bootstrap server config
            ProducerCache.AddBootstrapServers(instance, bootstrapServers);
            return new CallTargetState(scope: null, state: instance);
        }

        return CallTargetState.GetDefault();
    }

    internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception exception, in CallTargetState state)
    {
        // This method is called in the Producer constructor, so if we have an exception
        // the builder won't be created, so no point recording it.
        if (exception is not null && state is { State: { } producer })
        {
            ProducerCache.RemoveProducer(producer);
        }

        return CallTargetReturn.GetDefault();
    }
}
