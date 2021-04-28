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
    MethodName = "SendInternal",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = new[] { ClrNames.Object, ClrNames.MsmqMessageQueueTransaction, ClrNames.MsmqMessageQueueTransactionType },
    MinimumVersion = "4.0.0",
    MaximumVersion = "4.*.*",
    IntegrationName = IntegrationName)]
    public class MessageQueue_SendInternal_Integration
    {
        private const string IntegrationName = nameof(IntegrationIds.MessageQueue);
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(MessageQueue_SendInternal_Integration));

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="obj">obj</param>
        /// <param name="args">args</param>
        /// <param name="args2">args1</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget>(TTarget instance, object obj, object args, object args2)
            {
            Log.Information("message queue integration");
            return CallTargetState.GetDefault();
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
