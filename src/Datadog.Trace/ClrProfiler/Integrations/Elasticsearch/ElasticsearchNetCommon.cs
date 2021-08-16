// <copyright file="ElasticsearchNetCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Elasticsearch;
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

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ElasticsearchNetCommon));

        public static Scope CreateScope<T>(Tracer tracer, IntegrationInfo integrationId, RequestPipelineStruct pipeline, T requestData)
            where T : IRequestData
        {
            if (!tracer.Settings.IsIntegrationEnabled(integrationId))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            var requestParameters = pipeline.RequestParameters;
            string requestName = requestParameters?.GetType().Name.Replace("RequestParameters", string.Empty);

            var pathAndQuery = requestData.Path;

            string method = requestData.Method;
            var url = requestData.Uri?.ToString();

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
