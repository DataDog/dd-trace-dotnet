// <copyright file="KafkaProduceSyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
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
        MaximumVersion = "1.*.*",
        IntegrationName = KafkaConstants.IntegrationName)]
    public class KafkaProduceSyncIntegration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TTopicPartition">Type of the TopicPartition</typeparam>
        /// <typeparam name="TMessage">Type of the message</typeparam>
        /// <typeparam name="TDeliveryHandler">Type of the delivery handler action</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="topicPartition">TopicPartition instance</param>
        /// <param name="message">Message instance</param>
        /// <param name="deliveryHandler">Delivery Handler instance</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget, TTopicPartition, TMessage, TDeliveryHandler>(TTarget instance, TTopicPartition topicPartition, TMessage message, TDeliveryHandler deliveryHandler)
            where TMessage : IMessage
        {
            // manually doing duck cast here so we have access to the _original_ TopicPartition type
            // as a generic parameter, for injecting headers
            Scope scope = KafkaHelper.CreateProducerScope(
                Tracer.Instance,
                topicPartition.DuckCast<ITopicPartition>(),
                isTombstone: message.Value is null,
                finishOnClose: deliveryHandler is null);

            if (scope is not null)
            {
                KafkaHelper.TryInjectHeaders<TTopicPartition, TMessage>(scope.Span.Context, message);
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
        public static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception exception, CallTargetState state)
        {
            state.Scope.DisposeWithException(exception);
            return CallTargetReturn.GetDefault();
        }
    }
}
