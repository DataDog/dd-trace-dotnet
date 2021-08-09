// <copyright file="MessageQueue_SendInternal_Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Msmq
{
    /// <summary>
    /// Msmq calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
    AssemblyName = "System.Messaging",
    TypeName = "System.Messaging.MessageQueue",
    MethodName = "SendInternal",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = new[] { ClrNames.Object, MsmqConstants.MsmqMessageQueueTransaction, MsmqConstants.MsmqMessageQueueTransactionType },
    MinimumVersion = "4.0.0",
    MaximumVersion = "4.*.*",
    IntegrationName = MsmqConstants.IntegrationName)]
    public class MessageQueue_SendInternal_Integration
    {
        private const string Command = MsmqConstants.MsmqSendCommand;

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TMessageQueue">Message queue</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="message">Message itself, can be of any type</param>
        /// <param name="messageQueueTransaction">Message queue transaction can be null</param>
        /// <param name="messageQueueTransactionType">Message queue transaction type can be null</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TMessageQueue>(TMessageQueue instance, object message, object messageQueueTransaction, MessageQueueTransactionType messageQueueTransactionType)
            where TMessageQueue : IMessageQueue
        {
            var scope = MsmqCommon.CreateScope(Tracer.Instance, Command, SpanKinds.Producer, instance, messageQueueTransaction != null || messageQueueTransactionType != MessageQueueTransactionType.None);
            return new CallTargetState(scope);
        }

        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>CallTargetReturn</returns>
        public static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception exception, CallTargetState state)
        {
            state.Scope.DisposeWithException(exception);
            return CallTargetReturn.GetDefault();
        }
    }
}
