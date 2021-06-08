// <copyright file="AwsSqsCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SQS
{
    internal static class AwsSqsCommon
    {
        private const string DatadogAwsSqsServiceName = "aws-sqs";
        private const string SqsOperationName = "sqs.request";
        private const string SqsServiceName = "SQS";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AwsSqsCommon));

        internal const string IntegrationName = nameof(IntegrationIds.AwsSqs);
        internal static readonly IntegrationInfo IntegrationId = IntegrationRegistry.GetIntegrationInfo(IntegrationName);

        public static Scope CreateScope(Tracer tracer, string operation, out AwsSqsTags tags, ISpanContext parentContext = null)
        {
            tags = null;

            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId) || !tracer.Settings.IsIntegrationEnabled(AwsConstants.IntegrationId))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            Scope scope = null;

            try
            {
                Span parent = tracer.ActiveScope?.Span;

                tags = new AwsSqsTags();
                string serviceName = tracer.Settings.GetServiceName(tracer, DatadogAwsSqsServiceName);
                scope = tracer.StartActiveWithTags(SqsOperationName, parent: parentContext, tags: tags, serviceName: serviceName);
                var span = scope.Span;

                span.Type = SpanTypes.Http;
                span.ResourceName = $"{SqsServiceName}.{operation}";

                tags.Service = SqsServiceName;
                tags.Operation = operation;
                tags.SetAnalyticsSampleRate(IntegrationId, tracer.Settings, enabledWithGlobalSetting: false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            // always returns the scope, even if it's null because we couldn't create it,
            // or we couldn't populate it completely (some tags is better than no tags)
            return scope;
        }
    }
}
