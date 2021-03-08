using System;
using System.Collections.Generic;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.ClrProfiler.Integrations;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.RabbitMQ
{
    /// <summary>
    /// RabbitMQ.Client QueueDeclare calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "RabbitMQ.Client",
        TypeName = "RabbitMQ.Client.Framing.Impl.Model",
        MethodName = "_Private_QueueDeclare",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { ClrNames.String, ClrNames.Bool, ClrNames.Bool, ClrNames.Bool, ClrNames.Bool, ClrNames.Bool, RabbitMQConstants.IDictionaryArgumentsTypeName },
        MinimumVersion = "3.6.9",
        MaximumVersion = "6.*.*",
        IntegrationName = RabbitMQConstants.IntegrationName)]
    public class QueueDeclareIntegration
    {
        private const string Command = "queue.declare";

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="queue">Name of the queue.</param>
        /// <param name="passive">The original passive setting</param>
        /// <param name="durable">The original durable setting</param>
        /// <param name="exclusive">The original exclusive settings</param>
        /// <param name="autoDelete">The original autoDelete setting</param>
        /// <param name="nowait">The original nowait setting</param>
        /// <param name="arguments">The original arguments setting</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget>(TTarget instance, string queue, bool passive, bool durable, bool exclusive, bool autoDelete, bool nowait, IDictionary<string, object> arguments)
        {
            return new CallTargetState(RabbitMQIntegration.CreateScope(Tracer.Instance, out _, Command, SpanKinds.Client, queue: queue));
        }

        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A default CallTargetReturn to satisfy the CallTarget contract</returns>
        public static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception exception, CallTargetState state)
        {
            state.Scope.DisposeWithException(exception);
            return CallTargetReturn.GetDefault();
        }
    }
}
