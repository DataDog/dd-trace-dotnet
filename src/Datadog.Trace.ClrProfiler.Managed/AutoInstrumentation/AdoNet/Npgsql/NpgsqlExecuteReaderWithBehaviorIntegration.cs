using System;
using System.Data.Common;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.Npgsql
{
    /// <summary>
    /// CallTarget instrumentation for:
    /// NpgsqlDataReader Npgsql.NpgsqlCommand.ExecuteReader(CommandBehavior)
    /// DbDataReader Npgsql.NpgsqlCommand.ExecuteDbDataReader(CommandBehavior)
    /// </summary>
    [NpgsqlConstants.Npgsql.InstrumentSqlCommand(
        Method = AdoNetConstants.MethodNames.ExecuteReader,
        ReturnTypeName = NpgsqlConstants.Npgsql.SqlDataReaderType,
        ParametersTypesNames = new[] { AdoNetConstants.TypeNames.CommandBehavior })]
    [NpgsqlConstants.Npgsql.InstrumentSqlCommand(
        Method = AdoNetConstants.MethodNames.ExecuteDbDataReader,
        ReturnTypeName = AdoNetConstants.TypeNames.DbDataReaderType,
        ParametersTypesNames = new[] { AdoNetConstants.TypeNames.CommandBehavior })]
    public class NpgsqlExecuteReaderWithBehaviorIntegration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TBehavior">Command Behavior type</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="commandBehavior">Command behavior</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget, TBehavior>(TTarget instance, TBehavior commandBehavior)
        {
            return new CallTargetState(ScopeFactory.CreateDbCommandScope(Tracer.Instance, instance as DbCommand));
        }

        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TReturn">Type of the return value</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="returnValue">Task of HttpResponse message instance</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value, in an async scenario will be T of Task of T</returns>
        public static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
        {
            state.Scope.DisposeWithException(exception);
            return new CallTargetReturn<TReturn>(returnValue);
        }
    }
}
