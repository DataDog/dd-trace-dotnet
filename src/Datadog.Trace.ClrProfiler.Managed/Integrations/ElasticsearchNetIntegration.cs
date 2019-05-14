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
        // NOTE: keep this name without the 6 to avoid breaking changes
        private const string IntegrationName = "ElasticsearchNet";
        private const string Version6 = "6";
        private const string Version5 = "5";

        private static readonly ILog Log = LogProvider.GetLogger(typeof(ElasticsearchNetIntegration));
        private static readonly InterceptedMethodAccess<Func<object, object, CancellationToken, object>> CallElasticsearchAsyncAccess = new InterceptedMethodAccess<Func<object, object, CancellationToken, object>>();
        private static readonly GenericAsyncTargetAccess AsyncTargetAccess = new GenericAsyncTargetAccess();

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
            TargetMinimumVersion = Version5,
            TargetMaximumVersion = Version6)]
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
            TargetMethod = "CallElasticsearchAsync",
            TargetMinimumVersion = Version6,
            TargetMaximumVersion = Version6)]
        public static object CallElasticsearch6Async<TResponse>(object pipeline, object requestData, object cancellationTokenSource)
        {
            // Task<TResponse> CallElasticsearchAsync<TResponse>(RequestData requestData, CancellationToken cancellationToken) where TResponse : class, IElasticsearchResponse, new();
            var tokenSource = cancellationTokenSource as CancellationTokenSource;
            var cancellationToken = tokenSource?.Token ?? CancellationToken.None;
            return CallElasticsearchAsync6Internal<TResponse>(pipeline, requestData, cancellationToken);
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
            TargetMethod = "CallElasticsearchAsync",
            TargetMinimumVersion = Version5,
            TargetMaximumVersion = Version5)]
        public static object CallElasticsearch5Async<TResponse>(object pipeline, object requestData, object cancellationTokenSource)
        {
            // Task<ElasticsearchResponse<TReturn>> CallElasticsearchAsync<TReturn>(RequestData requestData, CancellationToken cancellationToken) where TReturn : class;
            var tokenSource = cancellationTokenSource as CancellationTokenSource;
            var cancellationToken = tokenSource?.Token ?? CancellationToken.None;

            var pipelineType = pipeline.GetType();
            var responseType = pipelineType.Assembly.GetType("Elasticsearch.Net.ElasticsearchResponse`1", throwOnError: false);
            var genericResponseType = responseType.MakeGenericType(typeof(TResponse));

            return AsyncTargetAccess.InvokeGenericTaskDelegate(
                owningType: pipelineType,
                taskResultType: genericResponseType,
                nameOfIntegrationMethod: nameof(CallElasticsearch5AsyncInternal),
                integrationType: typeof(ElasticsearchNet5Integration),
                pipeline,
                requestData,
                cancellationToken);
        }

        /// <summary>
        /// Traces an asynchronous call to Elasticsearch.
        /// </summary>
        /// <typeparam name="TResponse">Type type of the response</typeparam>
        /// <param name="pipeline">The pipeline for the original method</param>
        /// <param name="requestData">The request data</param>
        /// <param name="cancellationToken">A cancellation token</param>
        /// <returns>The original result</returns>
        private static async Task<TResponse> CallElasticsearchAsync6Internal<TResponse>(object pipeline, object requestData, CancellationToken cancellationToken)
        {
            var originalMethod = Emit.DynamicMethodBuilder<Func<object, object, CancellationToken, Task<TResponse>>>
                                     .GetOrCreateMethodCallDelegate(
                                          pipeline.GetType(),
                                          "CallElasticsearchAsync",
                                          methodParameterTypes: new[] { requestData.GetType(), ElasticsearchNetCommon.CancellationTokenType },
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

        /// <summary>
        /// Traces an asynchronous call to Elasticsearch.
        /// </summary>
        /// <typeparam name="T">Type type of the Task</typeparam>
        /// <param name="pipeline">The pipeline for the original method</param>
        /// <param name="requestData">The request data</param>
        /// <param name="cancellationToken">A cancellation token</param>
        /// <returns>The original result</returns>
        private static async Task<T> CallElasticsearch5AsyncInternal<T>(object pipeline, object requestData, CancellationToken cancellationToken)
        {
            var executeAsync = CallElasticsearchAsyncAccess.GetInterceptedMethod(
                owningType: pipeline.GetType(),
                returnType: typeof(Task<T>),
                methodName: nameof(CallElasticsearch5Async),
                generics: Interception.NullTypeArray,
                parameters: Interception.ParamsToTypes(requestData, cancellationToken));

            using (var scope = ElasticsearchNetCommon.CreateScope(Tracer.Instance, IntegrationName, pipeline, requestData))
            {
                try
                {
                    var task = (Task<T>)executeAsync(pipeline, requestData, cancellationToken);
                    return await task.ConfigureAwait(false);
                }
                catch (Exception ex) when (scope?.Span.SetExceptionForFilter(ex) ?? false)
                {
                    // unreachable code
                    throw;
                }
            }
        }
    }
}
