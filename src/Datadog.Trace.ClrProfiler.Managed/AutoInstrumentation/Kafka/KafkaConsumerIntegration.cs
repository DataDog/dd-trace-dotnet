using System;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka
{
    /// <summary>
    /// Confluent.Kafka Producer.Produce calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Confluent.Kafka",
        TypeName = "Confluent.Kafka.Consumer`2",
        MethodName = "Consume",
        ReturnTypeName = KafkaConstants.ConsumeResultTypeName,
        ParameterTypeNames = new[] { ClrNames.Int32 },
        MinimumVersion = "1.4.0",
        MaximumVersion = "1.*.*",
        IntegrationName = KafkaConstants.IntegrationName)]
    public class KafkaConsumerIntegration
    {
        private static readonly IDatadogLogger Logger = DatadogLogging.GetLoggerFor<KafkaConsumerIntegration>();

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="millisecondsTimeout">The maximum period of time the call may block.</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget>(TTarget instance, int millisecondsTimeout)
        {
            // No guarantee the Consumer.Consume() method will actually fetch a message
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
        public static CallTargetReturn<TResponse> OnMethodEnd<TTarget, TResponse>(TTarget instance, TResponse response, Exception exception, CallTargetState state)
        {
            IConsumeResult consumeResult = null;
            if (response is not null)
            {
                // Have to do the cast here, not in the method signature, as otherwise response is never null
                consumeResult = response.DuckAs<IConsumeResult>();
            }

            if (exception is not null && exception.TryDuckCast<IConsumeException>(out var consumeException))
            {
                consumeResult = consumeException.ConsumerRecord;
            }

            if (consumeResult is not null)
            {
                Scope scope = KafkaHelper.CreateConsumerScope(
                    Tracer.Instance,
                    consumeResult.Topic,
                    consumeResult.Partition,
                    consumeResult.Offset,
                    consumeResult.Message);
                scope?.DisposeWithException(exception);
            }

            return new CallTargetReturn<TResponse>(response);
        }
    }
}
