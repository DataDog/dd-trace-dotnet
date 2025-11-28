// <copyright file="ExecuteAsyncIntegrationExtraV13.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

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
        ReturnTypeName = "System.Threading.Tasks.Task`1[HotChocolate.Execution.IQueryResult]",
        ParameterTypeNames = new[] { "HotChocolate.Execution.Processing.OperationContext" },
        AssemblyName = "HotChocolate.Execution",
        TypeName = "HotChocolate.Execution.Processing.QueryExecutor",
        MinimumVersion = "13",
        MaximumVersion = "13.*.*")]
    [InstrumentMethod(
        IntegrationName = HotChocolateCommon.IntegrationName,
        MethodName = "ExecuteAsync",
        // v14 has a different return type (but since we don't use it we can have the same instrumentation)
        ReturnTypeName = "System.Threading.Tasks.Task`1[HotChocolate.Execution.IOperationResult]",
        ParameterTypeNames = new[] { "HotChocolate.Execution.Processing.OperationContext" },
        AssemblyName = "HotChocolate.Execution",
        TypeName = "HotChocolate.Execution.Processing.QueryExecutor",
        MinimumVersion = "14",
        MaximumVersion = "15.*.*")]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class ExecuteAsyncIntegrationExtraV13
    {
        internal static CallTargetState OnMethodBegin<TTarget, TOperationContext>(TTarget instance, TOperationContext operationContext)
            where TOperationContext : IOperationContextV13
        {
            if (operationContext.Instance != null && operationContext.Operation.HasValue)
            {
                var operation = operationContext.Operation.Value;
                var operationType = HotChocolateCommon.GetOperation(operation.OperationType);
                var operationName = operation.Name;

                HotChocolateCommon.UpdateScopeFromExecuteAsync(Tracer.Instance, operationType, operationName);
            }

            return CallTargetState.GetDefault();
        }
    }
}
