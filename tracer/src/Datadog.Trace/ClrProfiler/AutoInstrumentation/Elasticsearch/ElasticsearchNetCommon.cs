// <copyright file="ElasticsearchNetCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Threading;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Util.Http;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Elasticsearch
{
    internal static class ElasticsearchNetCommon
    {
        public const string DatabaseType = "elasticsearch";
        public const string ComponentValue = "elasticsearch-net";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ElasticsearchNetCommon));

        public static Scope? CreateScope<T>(Tracer tracer, IntegrationId integrationId, RequestPipelineStruct pipeline, T requestData)
            where T : IRequestData
        {
            if (!tracer.Settings.IsIntegrationEnabled(integrationId))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            string method = requestData.Method.ToString();
            var url = requestData.Uri?.ToString();

            var scope = CreateScope(tracer, integrationId, method, pipeline.RequestParameters, out var tags);
            if (tags is not null)
            {
                tags.Url = url;
                tags.Host = HttpRequestUtils.GetNormalizedHost(requestData.Uri?.Host);
            }

            return scope;
        }

        public static Scope? CreateScope(Tracer tracer, IntegrationId integrationId, string? method, object? requestParameters, out ElasticsearchTags? tags)
        {
            if (!tracer.Settings.IsIntegrationEnabled(integrationId))
            {
                // integration disabled, don't create a scope, skip this trace
                tags = null;
                return null;
            }

            var requestName = requestParameters?.GetType().Name.Replace("RequestParameters", string.Empty);

            var operationName = tracer.CurrentTraceSettings.Schema.Database.GetOperationName(DatabaseType);
            var serviceName = tracer.CurrentTraceSettings.Schema.Database.GetServiceName(DatabaseType);
            tags = tracer.CurrentTraceSettings.Schema.Database.CreateElasticsearchTags();

            Scope? scope = null;

            try
            {
                scope = tracer.StartActiveInternal(operationName, serviceName: serviceName, tags: tags);
                var span = scope.Span;
                span.ResourceName = requestName ?? string.Empty;
                span.Type = DatabaseType;
                tags.Action = requestName;
                tags.Method = method;

                tags.SetAnalyticsSampleRate(integrationId, tracer.Settings, enabledWithGlobalSetting: false);
                tracer.CurrentTraceSettings.Schema.RemapPeerService(tags);

                tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(integrationId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            return scope;
        }
    }
}
