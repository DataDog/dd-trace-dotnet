// <copyright file="KafkaProduceSyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka
{
    /// <summary>
    /// Confluent.Kafka Producer.Produce calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Confluent.Kafka",
        TypeName = "Confluent.Kafka.Producer`2",
        MethodName = "Produce",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { KafkaConstants.TopicPartitionTypeName, KafkaConstants.MessageTypeName, KafkaConstants.ActionOfDeliveryReportTypeName },
        MinimumVersion = "1.4.0",
        MaximumVersion = "2.*.*",
        IntegrationName = KafkaConstants.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class KafkaProduceSyncIntegration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TTopicPartition">Type of the TopicPartition</typeparam>
        /// <typeparam name="TMessage">Type of the message</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="topicPartition">TopicPartition instance</param>
        /// <param name="message">Message instance</param>
        /// <param name="deliveryHandler">Delivery Handler instance</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TTopicPartition, TMessage>(TTarget instance, TTopicPartition topicPartition, TMessage message, ref Delegate deliveryHandler)
            where TMessage : IMessage
        {
            // We should not use a delivery handler if deliveryReports are disabled (enabled by default),
            // since it will result in InvalidOperationException
            // https://github.com/confluentinc/confluent-kafka-dotnet/blob/65362199f13bdad8a0831541f53d92e1e95a8a37/src/Confluent.Kafka/Producer.cs#L869
            if (
                deliveryHandler == null &&
                Tracer.Instance.TracerManager.DataStreamsManager.IsEnabled &&
                ProducerCache.TryGetDefaultDeliveryHandler(instance, out var handler))
            {
                deliveryHandler = handler;
            }

            // manually doing duck cast here so we have access to the _original_ TopicPartition type
            // as a generic parameter, for injecting headers
            var partition = topicPartition.DuckCast<ITopicPartition>();
            Scope scope = KafkaHelper.CreateProducerScope(
                Tracer.Instance,
                instance,
                partition,
                isTombstone: message.Value is null,
                finishOnClose: deliveryHandler is null);

            if (scope is not null)
            {
                KafkaHelper.TryInjectHeaders<TTopicPartition, TMessage>(
                    scope.Span,
                    Tracer.Instance.TracerManager.DataStreamsManager,
                    partition?.Topic,
                    message);
                return new CallTargetState(scope);
            }

            return CallTargetState.GetDefault();
        }

        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value, in an async scenario will be T of Task of T</returns>
        internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception exception, in CallTargetState state)
        {
            state.Scope.DisposeWithException(exception);
            return CallTargetReturn.GetDefault();
        }
    }
}
