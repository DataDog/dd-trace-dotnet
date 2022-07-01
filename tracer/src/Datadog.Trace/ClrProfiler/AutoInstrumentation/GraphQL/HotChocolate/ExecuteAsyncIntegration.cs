// <copyright file="ExecuteAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.HotChocolate
{
    /// <summary>
    /// GraphQL.Execution.ExecutionStrategy calltarget instrumentation
    /// </summary>
    [InstrumentMethodAttribute(
        IntegrationName = HotChocolateCommon.IntegrationName,
        MethodName = HotChocolateCommon.ExecuteAsyncMethodName,
        ReturnTypeName = HotChocolateCommon.ReturnTypeName,
        ParameterTypeNames = new string[] { },
        AssemblyName = HotChocolateCommon.HotChocolateAssembly,
        TypeName = "HotChocolate.Execution.Processing.WorkScheduler",
        MinimumVersion = HotChocolateCommon.Major1,
        MaximumVersion = "*")]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class ExecuteAsyncIntegration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
            where TTarget : IWorkScheduler
        {
            return new CallTargetState(scope: HotChocolateCommon.CreateScopeFromExecuteAsync(Tracer.Instance, instance), state: instance);
        }

        /// <summary>
        /// OnAsyncMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        internal static void OnAsyncMethodEnd<TTarget>(TTarget instance, Exception exception, in CallTargetState state)
        {
            Scope scope = state.Scope;
            if (state.Scope is null)
            {
                return;
            }

            try
            {
                if (exception != null)
                {
                    scope.Span?.SetException(exception);
                }
                else if (state.State is IWorkScheduler scheduler && scheduler.Context != null && scheduler.Context.Result != null)
                {
                    HotChocolateCommon.RecordExecutionErrorsIfPresent(scope.Span, GraphQLCommon.ExecuteErrorType, scheduler.Context.Result.Errors);
                }
            }
            finally
            {
                scope.Dispose();
            }
        }
    }
}
