using System;
using System.Data.Common;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.MySql
{
    /// <summary>
    /// CallTarget instrumentation for:
    /// MySqlDataReader MySql.Data.MySqlClient.MySqlCommand.ExecuteReader(CommandBehavior)
    /// DbDataReader MySql.Data.MySqlClient.MySqlCommand.ExecuteDbDataReader(CommandBehavior)
    /// MySqlDataReader MySqlConnector.MySqlCommand.ExecuteReader(CommandBehavior)
    /// DbDataReader MySqlConnector.MySqlCommand.ExecuteDbDataReader(CommandBehavior)
    /// </summary>
    [MySqlClientConstants.MySqlData.InstrumentSqlCommand(
        Method = AdoNetConstants.MethodNames.ExecuteReader,
        ReturnTypeName = MySqlClientConstants.MySqlData.SqlDataReaderType,
        ParametersTypesNames = new[] { AdoNetConstants.TypeNames.CommandBehavior })]
    [MySqlClientConstants.MySqlData.InstrumentSqlCommand(
        Method = AdoNetConstants.MethodNames.ExecuteDbDataReader,
        ReturnTypeName = AdoNetConstants.TypeNames.DbDataReaderType,
        ParametersTypesNames = new[] { AdoNetConstants.TypeNames.CommandBehavior })]
    [MySqlClientConstants.MySqlConnector.InstrumentSqlCommand(
        Method = AdoNetConstants.MethodNames.ExecuteReader,
        ReturnTypeName = MySqlClientConstants.MySqlConnector.SqlDataReaderType,
        ParametersTypesNames = new[] { AdoNetConstants.TypeNames.CommandBehavior })]
    [MySqlClientConstants.MySqlConnector.InstrumentSqlCommand(
        Method = AdoNetConstants.MethodNames.ExecuteDbDataReader,
        ReturnTypeName = AdoNetConstants.TypeNames.DbDataReaderType,
        ParametersTypesNames = new[] { AdoNetConstants.TypeNames.CommandBehavior })]
    public class MySqlCommandExecuteReaderWithBehaviorIntegration
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
