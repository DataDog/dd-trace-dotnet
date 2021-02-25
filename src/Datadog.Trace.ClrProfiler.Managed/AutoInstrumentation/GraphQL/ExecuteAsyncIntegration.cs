using System;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL
{
    /// <summary>
    /// GraphQL.Execution.ExecutionStrategy calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "GraphQL",
        TypeName = "GraphQL.Execution.ExecutionStrategy",
        MethodName = "ExecuteAsync",
        ReturnTypeName = "System.Threading.Tasks.Task`1<GraphQL.ExecutionResult>",
        ParameterTypeNames = new[] { "GraphQL.Execution.ExecutionContext" },
        MinimumVersion = "2.3.0",
        MaximumVersion = "2.*.*",
        IntegrationName = IntegrationName)]
    [InstrumentMethod(
        AssemblyName = "GraphQL",
        TypeName = "GraphQL.Execution.SubscriptionExecutionStrategy",
        MethodName = "ExecuteAsync",
        ReturnTypeName = "System.Threading.Tasks.Task`1<GraphQL.ExecutionResult>",
        ParameterTypeNames = new[] { "GraphQL.Execution.ExecutionContext" },
        MinimumVersion = "2.3.0",
        MaximumVersion = "2.*.*",
        IntegrationName = IntegrationName)]
    public class ExecuteAsyncIntegration
    {
        private const string IntegrationName = nameof(IntegrationIds.GraphQL);
        private const string ErrorType = "GraphQL.ExecutionError";

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TContext">Type of the execution context</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="context">The execution context of the GraphQL operation.</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget, TContext>(TTarget instance, TContext context)
            where TContext : IExecutionContext
        {
            return new CallTargetState(scope: GraphQLCommon.CreateScopeFromExecuteAsync(Tracer.Instance, context), state: context);
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
        public static TExecutionResult OnAsyncMethodEnd<TTarget, TExecutionResult>(TTarget instance, TExecutionResult executionResult, Exception exception, CallTargetState state)
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
                else
                {
                    if (state.State.TryDuckCast<IExecutionContext>(out var context))
                    {
                        GraphQLCommon.RecordExecutionErrorsIfPresent(scope.Span, ErrorType, context.Errors);
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
