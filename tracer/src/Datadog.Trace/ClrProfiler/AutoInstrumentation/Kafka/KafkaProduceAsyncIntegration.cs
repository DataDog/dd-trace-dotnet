// <copyright file="KafkaProduceAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka
{
    /// <summary>
    /// Confluent.Kafka Producer.Produce calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Confluent.Kafka",
        TypeName = "Confluent.Kafka.Producer`2",
        MethodName = "ProduceAsync",
        ReturnTypeName = KafkaConstants.TaskDeliveryReportTypeName,
        ParameterTypeNames = new[] { KafkaConstants.TopicPartitionTypeName, KafkaConstants.MessageTypeName, ClrNames.CancellationToken },
        MinimumVersion = "1.4.0",
        MaximumVersion = "2.*.*",
        IntegrationName = KafkaConstants.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class KafkaProduceAsyncIntegration
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
        /// <param name="cancellationToken">CancellationToken instance</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TTopicPartition, TMessage>(TTarget instance, TTopicPartition topicPartition, TMessage message, CancellationToken cancellationToken)
            where TMessage : IMessage
        {
            var partition = topicPartition.DuckCast<ITopicPartition>();
            Scope scope = KafkaHelper.CreateProducerScope(
                Tracer.Instance,
                instance,
                partition,
                isTombstone: message.Value is null,
                finishOnClose: true);

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
        /// OnAsyncMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TResponse">Type of the response, in an async scenario will be T of Task of T</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="response">Response instance</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value, in an async scenario will be T of Task of T</returns>
        internal static TResponse OnAsyncMethodEnd<TTarget, TResponse>(TTarget instance, TResponse response, Exception exception, in CallTargetState state)
            where TResponse : IDeliveryResult
        {
            if (state.Scope?.Span?.Tags is KafkaTags tags)
            {
                IDeliveryResult deliveryResult = null;
                if (exception is not null)
                {
                    var produceException = exception.DuckAs<IProduceException>();
                    if (produceException is not null)
                    {
                        deliveryResult = produceException.DeliveryResult;
                    }
                }
                else if (response is not null)
                {
                    deliveryResult = response;
                }

                if (deliveryResult is not null)
                {
                    tags.Partition = deliveryResult.Partition.ToString();
                    tags.Offset = deliveryResult.Offset.ToString();

                    var dataStreams = Tracer.Instance.TracerManager.DataStreamsManager;
                    if (dataStreams.IsEnabled)
                    {
                        dataStreams.TrackBacklog(
                            $"partition:{deliveryResult.Partition.Value},topic:{deliveryResult.Topic},type:kafka_produce",
                            deliveryResult.Offset.Value);
                    }
                }
            }

            state.Scope.DisposeWithException(exception);
            return response;
        }
    }
}
