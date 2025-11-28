// <copyright file="ExecuteAsyncIntegrationV13.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.HotChocolate
{
    /// <summary>
    /// HotChocolate.Execution.RequestExecutor calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        IntegrationName = HotChocolateCommon.IntegrationName,
        MethodName = "ExecuteAsync",
        ReturnTypeName = "System.Threading.Tasks.Task`1[HotChocolate.Execution.IExecutionResult]",
        ParameterTypeNames = new[] { "HotChocolate.Execution.IQueryRequest", ClrNames.CancellationToken },
        AssemblyName = "HotChocolate.Execution",
        TypeName = "HotChocolate.Execution.RequestExecutor",
        MinimumVersion = "13",
        MaximumVersion = "13.*.*")]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class ExecuteAsyncIntegrationV13
    {
        internal static CallTargetState OnMethodBegin<TTarget, TQueryRequest>(TTarget instance, TQueryRequest request, in CancellationToken token)
            where TQueryRequest : IQueryRequest
        {
            if (request.Instance is not null)
            {
                return new CallTargetState(scope: HotChocolateCommon.CreateScopeFromQueryRequest(Tracer.Instance, request));
            }

            return CallTargetState.GetDefault();
        }

        internal static TExecutionResult OnAsyncMethodEnd<TTarget, TExecutionResult>(TTarget instance, TExecutionResult executionResult, Exception? exception, in CallTargetState state)
        {
            var scope = state.Scope;
            if (scope is null)
            {
                return executionResult;
            }

            try
            {
                if (exception != null)
                {
                    scope.Span?.SetException(exception);
                }
                else if (executionResult.TryDuckCast<IQueryResult>(out var result))
                {
                    if (result.Errors != null)
                    {
                        HotChocolateCommon.RecordExecutionErrorsIfPresent(scope.Span, HotChocolateCommon.ErrorType, result.Errors);
                    }
                }
            }
            finally
            {
                scope.Dispose();
            }

            return executionResult;
        }
    }
}
