// <copyright file="ModelBasicConsumeIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.RabbitMQ
{
    /// <summary>
    /// RabbitMQ.Client BasicConsume calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "RabbitMQ.Client",
        TypeName = "RabbitMQ.Client.Framing.Impl.Model",
        MethodName = "_Private_BasicConsume",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { ClrNames.String, ClrNames.String, ClrNames.Bool, ClrNames.Bool, ClrNames.Bool, ClrNames.Bool, RabbitMQConstants.IDictionaryArgumentsTypeName },
        MinimumVersion = "3.6.9",
        MaximumVersion = "6.*.*",
        IntegrationName = RabbitMQConstants.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class ModelBasicConsumeIntegration
    {
        private const string Command = "basic.consume";

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="queue">Name of the queue.</param>
        /// <param name="consumerTag">The original consumerTag setting</param>
        /// <param name="noLocal">The original noLocal setting</param>
        /// <param name="autoAck">The original autoAck setting</param>
        /// <param name="exclusive">The original exclusive setting</param>
        /// <param name="nowait">The original nowait setting</param>
        /// <param name="arguments">The original arguments setting</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, string queue, string consumerTag, bool noLocal, bool autoAck, bool exclusive, bool nowait, IDictionary<string, object> arguments)
        {
            Console.WriteLine("BasicConsume start");
            QueueHelper.SetQueue(consumerTag, queue);
            Console.WriteLine("Setting queue! consumerTag: " + consumerTag + ", queue: " + queue);
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
        internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception exception, in CallTargetState state)
        {
            state.Scope.DisposeWithException(exception);
            return CallTargetReturn.GetDefault();
        }
    }
}
