using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

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
        private const string Command = "msmq.receive";
        private const string IntegrationName = nameof(IntegrationIds.Msmq);
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(MessageQueue_ReceiveCurrent_Integration));

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TMessageQueue">Generic tmessage queue</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method, the message queue</param>
        /// <param name="timeout">   A System.TimeSpan that indicates the time to wait until a new message is available for inspection.</param>
        /// <param name="action">args</param>
        /// <param name="cursorHandle">A System.Messaging.Cursor that maintains a specific position in the message queue.</param>
        /// <param name="messagePropertyFilter"> Controls and selects the properties that are retrieved when peeking or receiving messages from a message queue.</param>2
        /// <param name="messageQueueTransaction">transaction</param>
        /// <param name="messageQueueTransactionType">type of transaction</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TMessageQueue>(TMessageQueue instance, TimeSpan timeout, int action, object cursorHandle, object messagePropertyFilter, object messageQueueTransaction, object messageQueueTransactionType)
            where TMessageQueue : IMessageQueue
        {
            Log.Information("receive on method begin");
            Console.WriteLine("in receive");
            Console.ReadLine();

            var scope = MsmqCommon.CreateScope(Tracer.Instance, Command, SpanKinds.Producer, instance.QueueName, instance.FormatName, instance.Label, instance.LastModifyTime, messageQueueTransaction != null, messageQueueTransactionType.ToString(), instance.Transactional, out MsmqTags tags);
            return new CallTargetState(scope);
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
