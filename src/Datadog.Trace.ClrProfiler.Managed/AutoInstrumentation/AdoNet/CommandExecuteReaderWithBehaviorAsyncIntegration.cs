using System;
using System.Data.Common;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet
{
    /// <summary>
    /// CallTarget instrumentation for:
    /// Task[*DataReader] [Command].ExecuteReaderAsync(CommandBehavior, CancellationToken)
    /// Task[DbDataReader] [Command].ExecuteDbDataReaderAsync(CommandBehavior, CancellationToken)
    /// </summary>
    public class CommandExecuteReaderWithBehaviorAsyncIntegration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TBehavior">Command Behavior type</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="commandBehavior">Command behavior</param>
        /// <param name="cancellationToken">CancellationToken value</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget, TBehavior>(TTarget instance, TBehavior commandBehavior, CancellationToken cancellationToken)
        {
            return new CallTargetState(ScopeFactory.CreateDbCommandScope(Tracer.Instance, instance as DbCommand));
        }

        /// <summary>
        /// OnAsyncMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TReturn">Type of the return value, in an async scenario will be T of Task of T</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="returnValue">Return value instance</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value, in an async scenario will be T of Task of T</returns>
        public static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
        {
            state.Scope.DisposeWithException(exception);
            return returnValue;
        }
    }
}
