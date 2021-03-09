using System;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.ClrProfiler.Integrations;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.RabbitMQ
{
    /// <summary>
    /// RabbitMQ.Client BasicGet calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "RabbitMQ.Client",
        TypeName = "RabbitMQ.Client.Impl.ModelBase",
        MethodName = "BasicGet",
        ReturnTypeName = "RabbitMQ.Client.BasicGetResult",
        ParameterTypeNames = new[] { ClrNames.String, ClrNames.Bool },
        MinimumVersion = "3.6.9",
        MaximumVersion = "6.*.*",
        IntegrationName = RabbitMQConstants.IntegrationName)]
    public class BasicGetIntegration
    {
        private const string Command = "basic.get";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(BasicGetIntegration));

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="queue">The queue name of the message</param>
        /// <param name="autoAck">The original autoAck argument</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget>(TTarget instance, string queue, bool autoAck)
        {
            return new CallTargetState(scope: null, state: queue, startTime: DateTimeOffset.UtcNow);
        }

        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TResult">Type of the BasicGetResult</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="basicGetResult">BasicGetResult instance</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A default CallTargetReturn to satisfy the CallTarget contract</returns>
        public static CallTargetReturn<TResult> OnMethodEnd<TTarget, TResult>(TTarget instance, TResult basicGetResult, Exception exception, CallTargetState state)
            where TResult : IBasicGetResult, IDuckType
        {
            string queue = (string)state.State;
            DateTimeOffset? startTime = state.StartTime;

            SpanContext propagatedContext = null;
            string messageSize = null;

            if (basicGetResult.Instance != null)
            {
                messageSize = basicGetResult.Body?.Length.ToString();
                var basicPropertiesHeaders = basicGetResult.BasicProperties?.Headers;

                // try to extract propagated context values from headers
                if (basicPropertiesHeaders != null)
                {
                    try
                    {
                        propagatedContext = SpanContextPropagator.Instance.Extract(basicPropertiesHeaders, ContextPropagation.HeadersGetter);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error extracting propagated headers.");
                    }
                }
            }

            using (var scope = RabbitMQIntegration.CreateScope(Tracer.Instance, out RabbitMQTags tags, Command, parentContext: propagatedContext, spanKind: SpanKinds.Consumer, queue: queue, startTime: startTime))
            {
                if (scope != null)
                {
                    string queueDisplayName = string.IsNullOrEmpty(queue) || !queue.StartsWith("amq.gen-") ? queue : "<generated>";
                    scope.Span.ResourceName = $"{Command} {queueDisplayName}";

                    if (tags != null && messageSize != null)
                    {
                        tags.MessageSize = messageSize;
                    }

                    if (exception != null)
                    {
                        scope.Span.SetException(exception);
                    }
                }
            }

            return new CallTargetReturn<TResult>(basicGetResult);
        }
    }
}
