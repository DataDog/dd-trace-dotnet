// <copyright file="ExecuteAsyncIntegrationExtraV13.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.HotChocolate
{
    /// <summary>
    /// HotChocolate.Execution.Processing.WorkScheduler calltarget instrumentation to retrieve OperationType
    /// </summary>
    [InstrumentMethod(
        IntegrationName = HotChocolateCommon.IntegrationName,
        MethodName = "ExecuteAsync",
        ReturnTypeName = "System.Threading.Tasks.Task`1<HotChocolate.Execution.IQueryResult>",
        ParameterTypeNames = new string[] { "HotChocolate.Execution.Processing.OperationContext" },
        AssemblyName = "HotChocolate.Execution",
        TypeName = "HotChocolate.Execution.Processing.QueryExecutor",
        MinimumVersion = "13",
        MaximumVersion = "13.*.*")]
    [InstrumentMethod(
        IntegrationName = HotChocolateCommon.IntegrationName,
        MethodName = "ExecuteAsync",
        ReturnTypeName = "System.Threading.Tasks.Task`1<HotChocolate.Execution.IQueryResult>",
        ParameterTypeNames = new string[] { "HotChocolate.Execution.Processing.OperationContext" },
        AssemblyName = "HotChocolate.Execution",
        TypeName = "HotChocolate.Execution.Processing.MutationExecutor",
        MinimumVersion = "13",
        MaximumVersion = "13.*.*")]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class ExecuteAsyncIntegrationExtraV13
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TOperationContext">Type of the first parameter</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="operationContext">Operation context</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TOperationContext>(TTarget instance, TOperationContext operationContext)
            where TOperationContext : IOperationContextV13
        {
            HotChocolateCommon.UpdateScopeFromExecuteAsyncV13(Tracer.Instance, operationContext);
            return CallTargetState.GetDefault();
        }

        internal static TQueryResult OnMethodEnd<TTarget, TQueryResult>(TTarget instance, TQueryResult queryResult, Exception exception, in CallTargetState state)
            where TQueryResult : IQueryResult
        {
            Scope scope = state.Scope;
            if (scope is null)
            {
                return queryResult;
            }

            try
            {
                if (exception != null)
                {
                    scope.Span?.SetException(exception);
                }
                else if (queryResult != null && queryResult.Errors != null)
                {
                    HotChocolateCommon.RecordExecutionErrorsIfPresent(scope.Span, HotChocolateCommon.ErrorType, queryResult.Errors);
                }
            }
            finally
            {
                scope.Dispose();
            }

            return queryResult;
        }
    }
}
