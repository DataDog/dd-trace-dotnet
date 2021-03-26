using System;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.ClrProfiler.Integrations;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Elasticsearch.V5
{
    /// <summary>
    /// Elasticsearch.Net.RequestPipeline.CallElasticsearch&lt;T&gt; calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = ElasticsearchV5Constants.ElasticsearchAssemblyName,
        TypeName = ElasticsearchV5Constants.RequestPipelineTypeName,
        MethodName = "CallElasticsearch",
        ReturnTypeName = "Elasticsearch.Net.ElasticsearchResponse`1<T>",
        ParameterTypeNames = new[] { "Elasticsearch.Net.RequestData" },
        MinimumVersion = ElasticsearchV5Constants.Version5,
        MaximumVersion = ElasticsearchV5Constants.Version5,
        IntegrationName = ElasticsearchV5Constants.IntegrationName)]
    // ReSharper disable once InconsistentNaming
    public class RequestPipeline_CallElasticsearch_Integration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TRequestData">Type of the request</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="requestData">The request data</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget, TRequestData>(TTarget instance, TRequestData requestData)
        {
            var scope = ElasticsearchNetCommon.CreateScope(Tracer.Instance, ElasticsearchV5Constants.IntegrationId, instance.DuckCast<RequestPipelineStruct>(), new RequestDataV5(requestData));

            return new CallTargetState(scope);
        }

        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TResponse">Type of the response</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="response">Response instance</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value, in an async scenario will be T of Task of T</returns>
        public static CallTargetReturn<TResponse> OnMethodEnd<TTarget, TResponse>(TTarget instance, TResponse response, Exception exception, CallTargetState state)
        {
            state.Scope?.DisposeWithException(exception);
            return new CallTargetReturn<TResponse>(response);
        }
    }
}
