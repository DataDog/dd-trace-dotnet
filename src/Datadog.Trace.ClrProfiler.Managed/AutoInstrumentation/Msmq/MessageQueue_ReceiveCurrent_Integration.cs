using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Msmq
{
    /// <summary>
    /// Msmq calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
    AssemblyName = "System.Messaging",
    TypeName = "System.Messaging.MessageQueue",
    MethodName = "ReceiveCurrent",
    ReturnTypeName = ClrNames.MsmqMessage,
    ParameterTypeNames = new[] { ClrNames.TimeSpan, ClrNames.Int32, ClrNames.CursorHandle, ClrNames.MsmqMessagePropertyFilter, ClrNames.MsmqMessageQueueTransaction, ClrNames.MsmqMessageQueueTransactionType },
    MinimumVersion = "4.0.0",
    MaximumVersion = "4.*.*",
    IntegrationName = IntegrationName)]
    public class MessageQueue_ReceiveCurrent_Integration
    {
        private const string IntegrationName = nameof(IntegrationIds.Msmq);
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(MessageQueue_ReceiveCurrent_Integration));

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="timeout">obj</param>
        /// <param name="action">args</param>
        /// <param name="cursorHandle">cursorHandle</param>
        /// <param name="messagePropertyFilter">args1</param>
        /// <param name="messageQueueTransaction">filter</param>
        /// <param name="messageQueueTransactionType">internalTransaction</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget>(TTarget instance, TimeSpan timeout, int action, object cursorHandle, object messagePropertyFilter, object messageQueueTransaction, object messageQueueTransactionType)
        {
            Log.Information("message queue receive current integration");
            return CallTargetState.GetDefault();
        }

        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TReturn">Type of the return</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>CallTargetReturn</returns>
        public static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, Exception exception, CallTargetState state)
        {
            state.Scope.DisposeWithException(exception);
            return CallTargetReturn<TReturn>.GetDefault();
        }
    }
}
