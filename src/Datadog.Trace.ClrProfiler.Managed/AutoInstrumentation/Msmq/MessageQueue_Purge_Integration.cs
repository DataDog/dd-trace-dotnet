using System;
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
    MethodName = "Purge",
    ReturnTypeName = ClrNames.Void,
    MinimumVersion = "4.0.0",
    MaximumVersion = "4.*.*",
    IntegrationName = MsmqConstants.IntegrationName)]
    public class MessageQueue_Purge_Integration
    {
        private const string Command = MsmqConstants.MsmqPurgeCommand;

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TMessageQueue">Message queue</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TMessageQueue>(TMessageQueue instance)
            where TMessageQueue : IMessageQueue
        {
            var scope = MsmqCommon.CreateScope(Tracer.Instance, Command, SpanKinds.Client, instance);
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
