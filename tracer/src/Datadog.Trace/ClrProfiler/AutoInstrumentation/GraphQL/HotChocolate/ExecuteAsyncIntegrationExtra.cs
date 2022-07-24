// <copyright file="ExecuteAsyncIntegrationExtra.cs" company="Datadog">
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
    [InstrumentMethodAttribute(
        IntegrationName = HotChocolateCommon.IntegrationName,
        MethodName = "ExecuteAsync",
        ReturnTypeName = "System.Threading.Tasks.Task`1<HotChocolate.Execution.IExecutionResult>",
        ParameterTypeNames = new string[] { "HotChocolate.Execution.Processing.IOperationContext" },
        AssemblyName = "HotChocolate.Execution",
        TypeName = "HotChocolate.Execution.Processing.QueryExecutor",
        MinimumVersion = "11",
        MaximumVersion = "12.*.*")]
    [InstrumentMethodAttribute(
        IntegrationName = HotChocolateCommon.IntegrationName,
        MethodName = "ExecuteAsync",
        ReturnTypeName = "System.Threading.Tasks.Task`1<HotChocolate.Execution.IExecutionResult>",
        ParameterTypeNames = new string[] { "HotChocolate.Execution.Processing.IOperationContext" },
        AssemblyName = "HotChocolate.Execution",
        TypeName = "HotChocolate.Execution.Processing.MutationExecutor",
        MinimumVersion = "11",
        MaximumVersion = "11.*.*")]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class ExecuteAsyncIntegrationExtra
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
            where TOperationContext : IOperationContext
        {
            return new CallTargetState(scope: HotChocolateCommon.UpdateScopeFromExecuteAsync(Tracer.Instance, operationContext));
        }
    }
}
