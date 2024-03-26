// <copyright file="KafkaConsumerConsumeIntegration.cs" company="Datadog">
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
    /// Confluent.Kafka Consumer.Consume calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Confluent.Kafka",
        TypeName = "Confluent.Kafka.Consumer`2",
        MethodName = "Consume",
        ReturnTypeName = KafkaConstants.ConsumeResultTypeName,
        ParameterTypeNames = new[] { ClrNames.Int32 },
        MinimumVersion = "1.4.0",
        MaximumVersion = "2.*.*",
        IntegrationName = KafkaConstants.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class KafkaConsumerConsumeIntegration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="millisecondsTimeout">The maximum period of time the call may block.</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, int millisecondsTimeout)
        {
            // If we are already in a consumer scope, close it, and start a new one on method exit.
            KafkaHelper.CloseConsumerScope(Tracer.Instance);
            return CallTargetState.GetDefault();
        }

        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TResponse">Type of the response</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="response">Response instance</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value, in an async scenario will be T of Task of T</returns>
        internal static CallTargetReturn<TResponse> OnMethodEnd<TTarget, TResponse>(TTarget instance, TResponse response, Exception exception, in CallTargetState state)
            where TResponse : IConsumeResult, IDuckType
        {
            IConsumeResult consumeResult = response.Instance is not null ? response : null;

            if (exception is not null && exception.TryDuckCast<IConsumeException>(out var consumeException))
            {
                consumeResult = consumeException.ConsumerRecord;
            }

            if (consumeResult is not null)
            {
                // This sets the span as active and either disposes it immediately
                // or disposes it on the next call to Consumer.Consume()
                var tracer = Tracer.Instance;
                Scope scope = KafkaHelper.CreateConsumerScope(
                    tracer,
                    tracer.TracerManager.DataStreamsManager,
                    instance,
                    consumeResult.Topic,
                    consumeResult.Partition,
                    consumeResult.Offset,
                    consumeResult.Message);

                if (!Tracer.Instance.Settings.KafkaCreateConsumerScopeEnabledInternal)
                {
                    // Close and dispose the scope immediately
                    scope.DisposeWithException(exception);
                }
                else if (exception is not null)
                {
                    scope?.Span?.SetException(exception);
                }
            }

            return new CallTargetReturn<TResponse>(response);
        }
    }
}
