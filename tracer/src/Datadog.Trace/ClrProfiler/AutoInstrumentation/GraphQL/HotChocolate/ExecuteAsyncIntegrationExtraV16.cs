// <copyright file="ExecuteAsyncIntegrationExtraV16.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.HotChocolate
{
    /// <summary>
    /// HotChocolate.Execution.Processing.QueryExecutor calltarget instrumentation to retrieve OperationType
    /// In v16, QueryExecutor and OperationContext moved from HotChocolate.Execution to HotChocolate.Types,
    /// and Operation.Type was renamed to Operation.Kind.
    /// </summary>
    [InstrumentMethod(
        IntegrationName = HotChocolateCommon.IntegrationName,
        MethodName = "ExecuteAsync",
        ReturnTypeName = "System.Threading.Tasks.Task`1[HotChocolate.Execution.IExecutionResult]",
        ParameterTypeNames = ["HotChocolate.Execution.Processing.OperationContext"],
        AssemblyName = "HotChocolate.Types",
        TypeName = "HotChocolate.Execution.Processing.QueryExecutor",
        MinimumVersion = "16.0.0",
        MaximumVersion = "16.*.*")]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class ExecuteAsyncIntegrationExtraV16
    {
        internal static CallTargetState OnMethodBegin<TTarget, TOperationContext>(TTarget instance, TOperationContext operationContext)
            where TOperationContext : IOperationContextV16
        {
            if (operationContext.Instance != null && operationContext.Operation.HasValue)
            {
                var operation = operationContext.Operation.Value;
                var operationType = HotChocolateCommon.GetOperation(operation.Kind);
                var operationName = operation.Name;

                HotChocolateCommon.UpdateScopeFromExecuteAsync(Tracer.Instance, operationType, operationName);
            }

            return CallTargetState.GetDefault();
        }
    }
}
