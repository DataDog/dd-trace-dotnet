// <copyright file="MessageQueue_ReceiveCurrent_Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Msmq
{
    /// <summary>
    /// Msmq calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
    AssemblyName = "System.Messaging",
    TypeName = "System.Messaging.MessageQueue",
    MethodName = "ReceiveCurrent",
    ReturnTypeName = MsmqConstants.MsmqMessage,
    ParameterTypeNames = new[] { ClrNames.TimeSpan, ClrNames.Int32, MsmqConstants.MsmqCursorHandle, MsmqConstants.MsmqMessagePropertyFilter, MsmqConstants.MsmqMessageQueueTransaction, MsmqConstants.MsmqMessageQueueTransactionType },
    MinimumVersion = "4.0.0",
    MaximumVersion = "4.*.*",
    IntegrationName = MsmqConstants.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class MessageQueue_ReceiveCurrent_Integration
    {
        private const string CommandPeek = MsmqConstants.MsmqPeekCommand;
        private const string CommandReceive = MsmqConstants.MsmqReceiveCommand;

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TMessageQueue">Generic TMessageQueue</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method, the message queue</param>
        /// <param name="timeout">A System.TimeSpan that indicates the time to wait until a new message is available for inspection.</param>
        /// <param name="action">If action is 0, it's a peek (message remains in the queue), otherwise it's a receive</param>
        /// <param name="cursorHandle">A System.Messaging.Cursor that maintains a specific position in the message queue.</param>
        /// <param name="messagePropertyFilter"> Controls and selects the properties that are retrieved when peeking or receiving messages from a message queue.</param>2
        /// <param name="messageQueueTransaction">transaction</param>
        /// <param name="messageQueueTransactionType">type of transaction</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TMessageQueue>(TMessageQueue instance, TimeSpan timeout, int action, object cursorHandle, object messagePropertyFilter, object messageQueueTransaction, MessageQueueTransactionType messageQueueTransactionType)
            where TMessageQueue : IMessageQueue
        {
            // Given this is an instance method, it's safe to assume instance.Instance is not null
            var scope = MsmqCommon.CreateScope(Tracer.Instance, action != 0 ? CommandPeek : CommandReceive, SpanKinds.Consumer, instance);
            return new CallTargetState(scope);
        }

        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TResult">Type of the result</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="messageResult">message result</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>CallTargetReturn</returns>
        internal static CallTargetReturn<TResult> OnMethodEnd<TTarget, TResult>(TTarget instance, TResult messageResult, Exception exception, in CallTargetState state)
        {
            state.Scope.DisposeWithException(exception);
            return new CallTargetReturn<TResult>(messageResult);
        }
    }
}
