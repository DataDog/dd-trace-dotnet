// <copyright file="RequestPipeline_CallElasticsearchAsync_Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Elasticsearch.V6
{
    /// <summary>
    /// Elasticsearch.Net.RequestPipeline.CallElasticsearchAsync&lt;T&gt; calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = ElasticsearchV6Constants.ElasticsearchAssemblyName,
        TypeName = ElasticsearchV6Constants.RequestPipelineTypeName,
        MethodName = "CallElasticsearchAsync",
        ReturnTypeName = ClrNames.GenericTaskWithGenericClassParameter,
        ParameterTypeNames = new[] { "Elasticsearch.Net.RequestData", ClrNames.CancellationToken },
        MinimumVersion = ElasticsearchV6Constants.Version6,
        MaximumVersion = ElasticsearchV6Constants.Version6,
        IntegrationName = ElasticsearchV6Constants.IntegrationName)]
    // ReSharper disable once InconsistentNaming
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class RequestPipeline_CallElasticsearchAsync_Integration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TRequestData">Type of the request</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="requestData">The request data</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TRequestData>(TTarget instance, TRequestData requestData, CancellationToken cancellationToken)
            where TRequestData : IRequestData
        {
            var scope = ElasticsearchNetCommon.CreateScope(Tracer.Instance, ElasticsearchV6Constants.IntegrationId, instance.DuckCast<RequestPipelineStruct>(), requestData);

            return new CallTargetState(scope);
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
            state.Scope.DisposeWithException(exception);

            return executionResult;
        }
    }
}
