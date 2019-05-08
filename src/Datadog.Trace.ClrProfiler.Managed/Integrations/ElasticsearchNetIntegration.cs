using System;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// Traces an Elasticsearch pipeline
    /// </summary>
    public static class ElasticsearchNetIntegration
    {
        private const string IntegrationName = "ElasticsearchNet";
        private const string TargetVersion = "6";

        private static readonly ILog Log = LogProvider.GetLogger(typeof(ElasticsearchNetIntegration));

        private static Type _requestDataType;

        /// <summary>
        /// Traces a synchronous call to Elasticsearch.
        /// </summary>
        /// <typeparam name="TResponse">The type of the response</typeparam>
        /// <param name="pipeline">The pipeline for the original method</param>
        /// <param name="requestData">The request data</param>
        /// <returns>The original result</returns>
        [InterceptMethod(
            CallerAssembly = "Elasticsearch.Net",
            TargetAssembly = "Elasticsearch.Net",
            TargetType = "Elasticsearch.Net.IRequestPipeline",
            TargetMinimumVersion = TargetVersion,
            TargetMaximumVersion = TargetVersion)]
        public static object CallElasticsearch<TResponse>(object pipeline, object requestData)
        {
            // TResponse CallElasticsearch<TResponse>(RequestData requestData) where TResponse : class, IElasticsearchResponse, new();
            var originalMethod = Emit.DynamicMethodBuilder<Func<object, object, TResponse>>
                                     .GetOrCreateMethodCallDelegate(
                                          pipeline.GetType(),
                                          "CallElasticsearch",
                                          methodGenericArguments: new[] { typeof(TResponse) });

            using (var scope = ElasticsearchNetCommon.CreateScope(Tracer.Instance, IntegrationName, pipeline, requestData))
            {
                try
                {
                    return originalMethod(pipeline, requestData);
                }
                catch (Exception ex) when (scope?.Span.SetExceptionForFilter(ex) ?? false)
                {
                    // unreachable code
                    throw;
                }
            }
        }

        /// <summary>
        /// Traces an asynchronous call to Elasticsearch.
        /// </summary>
        /// <typeparam name="TResponse">Type type of the response</typeparam>
        /// <param name="pipeline">The pipeline for the original method</param>
        /// <param name="requestData">The request data</param>
        /// <param name="cancellationTokenSource">A cancellation token</param>
        /// <returns>The original result</returns>
        [InterceptMethod(
            CallerAssembly = "Elasticsearch.Net",
            TargetAssembly = "Elasticsearch.Net",
            TargetType = "Elasticsearch.Net.IRequestPipeline",
            TargetMinimumVersion = TargetVersion,
            TargetMaximumVersion = TargetVersion)]
        public static object CallElasticsearchAsync<TResponse>(object pipeline, object requestData, object cancellationTokenSource)
        {
            // Task<TResponse> CallElasticsearchAsync<TResponse>(RequestData requestData, CancellationToken cancellationToken) where TResponse : class, IElasticsearchResponse, new();
            var cancellationToken = ((CancellationTokenSource)cancellationTokenSource)?.Token ?? CancellationToken.None;
            return CallElasticsearchAsyncInternal<TResponse>(pipeline, requestData, cancellationToken);
        }

        /// <summary>
        /// Traces an asynchronous call to Elasticsearch.
        /// </summary>
        /// <typeparam name="TResponse">Type type of the response</typeparam>
        /// <param name="pipeline">The pipeline for the original method</param>
        /// <param name="requestData">The request data</param>
        /// <param name="cancellationToken">A cancellation token</param>
        /// <returns>The original result</returns>
        private static async Task<TResponse> CallElasticsearchAsyncInternal<TResponse>(object pipeline, object requestData, CancellationToken cancellationToken)
        {
            if (_requestDataType == null)
            {
                _requestDataType = requestData.GetType();
            }

            var originalMethod = Emit.DynamicMethodBuilder<Func<object, object, CancellationToken, Task<TResponse>>>
                                     .GetOrCreateMethodCallDelegate(
                                          pipeline.GetType(),
                                          "CallElasticsearchAsync",
                                          methodParameterTypes: new[] { _requestDataType, ElasticsearchNetCommon.CancellationTokenType },
                                          methodGenericArguments: new[] { typeof(TResponse) });

            using (var scope = ElasticsearchNetCommon.CreateScope(Tracer.Instance, IntegrationName, pipeline, requestData))
            {
                try
                {
                    return await originalMethod(pipeline, requestData, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    scope.Span.SetException(ex);
                    throw;
                }
            }
        }
    }
}
