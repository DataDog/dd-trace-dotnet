using System;
using System.Threading;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    internal static class ElasticsearchNetCommon
    {
        public const string OperationName = "elasticsearch.query";
        public const string ServiceName = "elasticsearch";
        public const string SpanType = "elasticsearch";
        public const string ComponentValue = "elasticsearch-net";

        public static readonly Type CancellationTokenType = typeof(CancellationToken);
        public static readonly Type RequestPipelineType = Type.GetType("Elasticsearch.Net.IRequestPipeline, Elasticsearch.Net");
        public static readonly Type RequestDataType = Type.GetType("Elasticsearch.Net.RequestData, Elasticsearch.Net");

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ElasticsearchNetCommon));

        public static Scope CreateScope(Tracer tracer, IntegrationInfo integrationId, object pipeline, object requestData)
        {
            if (!tracer.Settings.IsIntegrationEnabled(integrationId))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            string requestName = pipeline.GetProperty("RequestParameters")
                                         .GetValueOrDefault()
                                        ?.GetType()
                                         .Name
                                         .Replace("RequestParameters", string.Empty);

            var pathAndQuery = requestData.GetProperty<string>("PathAndQuery").GetValueOrDefault() ??
                               requestData.GetProperty<string>("Path").GetValueOrDefault();

            string method = requestData.GetProperty("Method").GetValueOrDefault()?.ToString();
            var url = requestData.GetProperty("Uri").GetValueOrDefault()?.ToString();

            string serviceName = tracer.Settings.GetServiceName(tracer, ServiceName);

            Scope scope = null;

            try
            {
                var tags = new ElasticsearchTags();
                scope = tracer.StartActiveWithTags(OperationName, serviceName: serviceName, tags: tags);
                var span = scope.Span;
                span.ResourceName = requestName ?? pathAndQuery ?? string.Empty;
                span.Type = SpanType;
                tags.Action = requestName;
                tags.Method = method;
                tags.Url = url;

                tags.SetAnalyticsSampleRate(integrationId, tracer.Settings, enabledWithGlobalSetting: false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            return scope;
        }
    }
}
