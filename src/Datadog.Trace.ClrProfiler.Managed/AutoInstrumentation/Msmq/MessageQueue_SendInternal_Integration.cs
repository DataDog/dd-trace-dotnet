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
    MethodName = "SendInternal",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = new[] { ClrNames.Object, ClrNames.MsmqMessageQueueTransaction, ClrNames.MsmqMessageQueueTransactionType },
    MinimumVersion = "4.0.0",
    MaximumVersion = "4.*.*",
    IntegrationName = IntegrationName)]
    public class MessageQueue_SendInternal_Integration
    {
        private const string IntegrationName = nameof(IntegrationIds.Msmq);
        private const string Command = "msmq.send";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(MessageQueue_SendInternal_Integration));

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
            Log.Information("send internal integration");
            var scope = MsmqCommon.CreateScope(Tracer.Instance, Command, SpanKinds.Producer, instance.QueueName, instance.FormatName, instance.Label, instance.LastModifyTime, messageQueueTransaction != null, messageQueueTransactionType.ToString(), instance.Transactional, out _);
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
