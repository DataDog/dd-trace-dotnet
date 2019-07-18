using System;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// Traces an Elasticsearch pipeline
    /// </summary>
    public static class ElasticsearchNet6Integration
    {
        // NOTE: keep this name without the 6 to avoid breaking changes
        private const string IntegrationName = "ElasticsearchNet";
        private const string Version6 = "6";

        private static readonly ILog Log = LogProvider.GetLogger(typeof(ElasticsearchNet6Integration));

        /// <summary>
        /// Traces a synchronous call to Elasticsearch.
        /// </summary>
        /// <typeparam name="TResponse">The type of the response</typeparam>
        /// <param name="pipeline">The pipeline for the original method</param>
        /// <param name="requestData">The request data</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <returns>The original result</returns>
        [InterceptMethod(
            CallerAssembly = "Elasticsearch.Net",
            TargetAssembly = "Elasticsearch.Net",
            TargetType = "Elasticsearch.Net.IRequestPipeline",
            TargetSignatureTypes = new[] { "T", "Elasticsearch.Net.RequestData" },
            TargetMinimumVersion = Version6,
            TargetMaximumVersion = Version6)]
        public static object CallElasticsearch<TResponse>(object pipeline, object requestData, int opCode, int mdToken)
        {
            // TResponse CallElasticsearch<TResponse>(RequestData requestData) where TResponse : class, IElasticsearchResponse, new();
            var originalMethod = Emit.DynamicMethodBuilder<Func<object, object, TResponse>>
                                     .GetOrCreateMethodCallDelegate(
                                          ElasticsearchNetCommon.RequestPipelineType,
                                          "CallElasticsearch",
                                          (OpCodeValue)opCode,
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
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <returns>The original result</returns>
        [InterceptMethod(
            CallerAssembly = "Elasticsearch.Net",
            TargetAssembly = "Elasticsearch.Net",
            TargetType = "Elasticsearch.Net.IRequestPipeline",
            TargetSignatureTypes = new[] { "System.Threading.Tasks.Task`1<T>", "Elasticsearch.Net.RequestData", ClrNames.CancellationToken },
            TargetMinimumVersion = Version6,
            TargetMaximumVersion = Version6)]
        public static object CallElasticsearchAsync<TResponse>(object pipeline, object requestData, object cancellationTokenSource, int opCode, int mdToken)
        {
            // Task<TResponse> CallElasticsearchAsync<TResponse>(RequestData requestData, CancellationToken cancellationToken) where TResponse : class, IElasticsearchResponse, new();
            var tokenSource = cancellationTokenSource as CancellationTokenSource;
            var cancellationToken = tokenSource?.Token ?? CancellationToken.None;
            var callOpCode = (OpCodeValue)opCode;
            return CallElasticsearchAsyncInternal<TResponse>(pipeline, requestData, cancellationToken, callOpCode);
        }

        /// <summary>
        /// Traces an asynchronous call to Elasticsearch.
        /// </summary>
        /// <typeparam name="TResponse">Type type of the response</typeparam>
        /// <param name="pipeline">The pipeline for the original method</param>
        /// <param name="requestData">The request data</param>
        /// <param name="cancellationToken">A cancellation token</param>
        /// <param name="callOpCode">The <see cref="OpCodeValue"/> used in the original method call.</param>
        /// <returns>The original result</returns>
        private static async Task<TResponse> CallElasticsearchAsyncInternal<TResponse>(object pipeline, object requestData, CancellationToken cancellationToken, OpCodeValue callOpCode)
        {
            var originalMethod = Emit.DynamicMethodBuilder<Func<object, object, CancellationToken, Task<TResponse>>>
                                     .GetOrCreateMethodCallDelegate(
                                          ElasticsearchNetCommon.RequestPipelineType,
                                          "CallElasticsearchAsync",
                                          callOpCode,
                                          methodParameterTypes: new[] { ElasticsearchNetCommon.RequestDataType, ElasticsearchNetCommon.CancellationTokenType },
                                          methodGenericArguments: new[] { typeof(TResponse) });

            using (var scope = ElasticsearchNetCommon.CreateScope(Tracer.Instance, IntegrationName, pipeline, requestData))
            {
                try
                {
                    return await originalMethod(pipeline, requestData, cancellationToken).ConfigureAwait(false);
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
