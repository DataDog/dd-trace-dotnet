using System;
using System.Threading;
using System.Threading.Tasks;

namespace Datadog.Trace.ClrProfiler.Integrations.Elasticsearch.Net
{
    /// <summary>
    /// Traces an Elasticsearch pipeline
    /// </summary>
    public static class Pipeline
    {
        private const string OperationName = "elasticsearch.query";
        private const string ServiceName = "elasticsearch";
        private const string SpanType = "elasticsearch";
        private const string ComponentKey = "component";
        private const string ComponentValue = "elasticsearch-net";
        private const string SpanKindKey = "span.kind";
        private const string SpanKindValue = "client";
        private const string ElasticsearchActionKey = "elasticsearch.action";
        private const string ElasticsearchMethodKey = "elasticsearch.method";
        private const string ElasticsearchUrlKey = "elasticsearch.url";
        private const string ElasticsearchParamsKey = "elasticsearch.params";

        private static Type _requestDataType;
        private static Type _cancellationTokenType = typeof(CancellationToken);

        /// <summary>
        /// CallElasticsearch traces a call to Elasticsearch
        /// </summary>
        /// <typeparam name="TResponse">The type of the response</typeparam>
        /// <param name="pipeline">The pipeline for the original method</param>
        /// <param name="requestData">The request data</param>
        /// <returns>The original result</returns>
        public static object CallElasticsearch<TResponse>(object pipeline, object requestData)
        {
            var originalMethod = DynamicMethodBuilder<Func<object, object, TResponse>>.GetOrCreateMethodCallDelegate(
                pipeline.GetType(),
                "CallElasticsearch",
                methodGenericArguments: new Type[] { typeof(TResponse) });
            return CreateScope(pipeline, requestData).Span.Trace(() => originalMethod(pipeline, requestData));
        }

        /// <summary>
        /// CallElasticsearchAsync traces an asynchronous call to Elasticsearch
        /// </summary>
        /// <typeparam name="TResponse">Type type of the response</typeparam>
        /// <param name="pipeline">The pipeline for the original method</param>
        /// <param name="requestData">The request data</param>
        /// <param name="cancellationTokenSource">A cancellation token</param>
        /// <returns>The original result</returns>
        public static object CallElasticsearchAsync<TResponse>(object pipeline, object requestData, object cancellationTokenSource)
        {
            if (_requestDataType == null)
            {
                _requestDataType = pipeline.GetType().Assembly.GetType("Elasticsearch.Net.RequestData");
            }

            var cancellationToken = (cancellationTokenSource as CancellationTokenSource)?.Token ?? CancellationToken.None;

            var originalMethod = DynamicMethodBuilder<Func<object, object, CancellationToken, TResponse>>
               .GetOrCreateMethodCallDelegate(
                    pipeline.GetType(),
                    "CallElasticsearchAsync",
                    methodParameterTypes: new[] { _requestDataType, _cancellationTokenType },
                    methodGenericArguments: new[] { typeof(TResponse) });

            return CreateScope(pipeline, requestData).Span.Trace(() => originalMethod(pipeline, requestData, cancellationToken));
        }

        private static Scope CreateScope(dynamic pipeline, dynamic requestData)
        {
            string requestName = null;
            try
            {
                var requestParameters = DynamicMethodBuilder<Func<object, dynamic>>.GetOrCreateMethodCallDelegate(
                    pipeline.GetType(),
                    "get_RequestParameters")(pipeline);
                requestName = requestParameters?.GetType()?.Name;
                requestName = requestName?.Replace("RequestParameters", string.Empty);
            }
            catch
            {
            }

            string pathAndQuery = null;
            try
            {
                pathAndQuery = requestData?.PathAndQuery;
            }
            catch
            {
            }

            string method = null;
            try
            {
                method = requestData?.Method?.ToString();
            }
            catch
            {
            }

            string url = null;
            try
            {
                url = requestData?.Uri.ToString();
            }
            catch
            {
            }

            var serviceName = string.Join("-", Tracer.Instance.DefaultServiceName, ServiceName);

            var scope = Tracer.Instance.StartActive(OperationName, serviceName: serviceName, finishOnClose: false);
            scope.Span.ResourceName = requestName ?? pathAndQuery ?? string.Empty;
            scope.Span.Type = SpanType;
            scope.Span.SetTag(ComponentKey, ComponentValue);
            scope.Span.SetTag(SpanKindKey, SpanKindValue);
            scope.Span.SetTag(ElasticsearchActionKey, requestName);
            scope.Span.SetTag(ElasticsearchMethodKey, method);
            scope.Span.SetTag(ElasticsearchUrlKey, url);

            return scope;
        }
    }
}
