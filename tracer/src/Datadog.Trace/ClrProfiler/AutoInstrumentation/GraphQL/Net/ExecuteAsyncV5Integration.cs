// <copyright file="ExecuteAsyncV5Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.Net
{
    /// <summary>
    /// GraphQL.Execution.ExecutionStrategy calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        IntegrationName = GraphQLCommon.IntegrationName,
        MethodName = GraphQLCommon.ExecuteAsyncMethodName,
        ReturnTypeName = GraphQLCommon.ReturnTypeName,
        ParameterTypeNames = new[] { GraphQLCommon.ExecutionContextTypeName },
        AssemblyName = GraphQLCommon.GraphQLAssembly,
        TypeName = "GraphQL.Execution.ExecutionStrategy",
        MinimumVersion = GraphQLCommon.Major5,
        MaximumVersion = GraphQLCommon.Major5)]
    [InstrumentMethod(
        IntegrationName = GraphQLCommon.IntegrationName,
        MethodName = GraphQLCommon.ExecuteAsyncMethodName,
        ReturnTypeName = GraphQLCommon.ReturnTypeName,
        ParameterTypeNames = new[] { GraphQLCommon.ExecutionContextTypeName },
        AssemblyName = GraphQLCommon.GraphQLAssembly,
        TypeName = "GraphQL.Execution.SubscriptionExecutionStrategy",
        MinimumVersion = GraphQLCommon.Major5,
        MaximumVersion = GraphQLCommon.Major5)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class ExecuteAsyncV5Integration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TContext">Type of the execution context</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="context">The execution context of the GraphQL operation.</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TContext>(TTarget instance, TContext context)
            where TContext : IExecutionContextV5
        {
            return new CallTargetState(scope: GraphQLCommon.CreateScopeFromExecuteAsyncV5(Tracer.Instance, context), state: context);
        }

        /// <summary>
        /// OnAsyncMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TExecutionResult">Type of the execution result value</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="executionResult">ExecutionResult instance</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value, in an async scenario will be T of Task of T</returns>
        internal static TExecutionResult OnAsyncMethodEnd<TTarget, TExecutionResult>(TTarget instance, TExecutionResult executionResult, Exception exception, in CallTargetState state)
        {
            Scope scope = state.Scope;
            if (state.Scope is null)
            {
                return executionResult;
            }

            try
            {
                if (exception != null)
                {
                    scope.Span?.SetException(exception);
                }
                else if (state.State is IExecutionContextV5 context)
                {
                    GraphQLCommon.RecordExecutionErrorsIfPresent(scope.Span, GraphQLCommon.ExecuteErrorType, context.Errors);
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
