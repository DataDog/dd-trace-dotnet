using System;
using System.Threading;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    internal static class ElasticsearchNetCommon
    {
        public const string OperationName = "elasticsearch.query";
        public const string ServiceName = "elasticsearch";
        public const string SpanType = "elasticsearch";
        public const string ComponentValue = "elasticsearch-net";
        public const string ElasticsearchActionKey = "elasticsearch.action";
        public const string ElasticsearchMethodKey = "elasticsearch.method";
        public const string ElasticsearchUrlKey = "elasticsearch.url";

        public static readonly Type CancellationTokenType = typeof(CancellationToken);

        private static readonly ILog Log = LogProvider.GetLogger(typeof(ElasticsearchNetCommon));

        public static Scope CreateScope(Tracer tracer, string integrationName, object pipeline, dynamic requestData)
        {
            if (!tracer.Settings.IsIntegrationEnabled(integrationName))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            string requestName = null;
            try
            {
                var requestParameters = Emit.DynamicMethodBuilder<Func<object, object>>
                                            .GetOrCreateMethodCallDelegate(
                                                 pipeline.GetType(),
                                                 "get_RequestParameters")(pipeline);

                requestName = requestParameters?.GetType().Name.Replace("RequestParameters", string.Empty);
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

            var serviceName = string.Join("-", tracer.DefaultServiceName, ServiceName);

            Scope scope = null;

            try
            {
                scope = tracer.StartActive(OperationName, serviceName: serviceName);
                var span = scope.Span;
                span.ResourceName = requestName ?? pathAndQuery ?? string.Empty;
                span.Type = SpanType;
                span.SetTag(Tags.InstrumentationName, ComponentValue);
                span.SetTag(Tags.SpanKind, SpanKinds.Client);
                span.SetTag(ElasticsearchActionKey, requestName);
                span.SetTag(ElasticsearchMethodKey, method);
                span.SetTag(ElasticsearchUrlKey, url);

                // set analytics sample rate if enabled
                var analyticsSampleRate = tracer.Settings.GetIntegrationAnalyticsSampleRate(integrationName, enabledWithGlobalSetting: false);
                span.SetMetric(Tags.Analytics, analyticsSampleRate);
            }
            catch (Exception ex)
            {
                Log.ErrorException("Error creating or populating scope.", ex);
            }

            return scope;
        }
    }
}
