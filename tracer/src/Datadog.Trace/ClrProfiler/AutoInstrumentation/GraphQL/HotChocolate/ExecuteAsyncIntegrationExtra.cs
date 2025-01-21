// <copyright file="ExecuteAsyncIntegrationExtra.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

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
        ReturnTypeName = "System.Threading.Tasks.Task`1[HotChocolate.Execution.IExecutionResult]",
        ParameterTypeNames = new[] { "HotChocolate.Execution.Processing.IOperationContext" },
        AssemblyName = "HotChocolate.Execution",
        TypeName = "HotChocolate.Execution.Processing.QueryExecutor",
        MinimumVersion = "11",
        MaximumVersion = "12.*.*")]
    [InstrumentMethod(
        IntegrationName = HotChocolateCommon.IntegrationName,
        MethodName = "ExecuteAsync",
        ReturnTypeName = "System.Threading.Tasks.Task`1[HotChocolate.Execution.IExecutionResult]",
        ParameterTypeNames = new[] { "HotChocolate.Execution.Processing.IOperationContext" },
        AssemblyName = "HotChocolate.Execution",
        TypeName = "HotChocolate.Execution.Processing.MutationExecutor",
        MinimumVersion = "11",
        MaximumVersion = "11.*.*")]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class ExecuteAsyncIntegrationExtra
    {
        internal static CallTargetState OnMethodBegin<TTarget, TOperationContext>(TTarget instance, TOperationContext operationContext)
            where TOperationContext : IOperationContext
        {
            var operation = operationContext.Operation;
            var operationType = HotChocolateCommon.GetOperation(operation.OperationType);
            var operationName = operation.Name?.Value;

            HotChocolateCommon.UpdateScopeFromExecuteAsync(Tracer.Instance, operationType, operationName);
            return CallTargetState.GetDefault();
        }
    }
}
