// <copyright file="AwsSqsCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SQS
{
    internal static class AwsSqsCommon
    {
        private const string DatadogAwsSqsServiceName = "aws-sqs";
        private const string SqsRequestOperationName = "sqs.request";
        private const string SqsServiceName = "SQS";
        private const string SqsOperationName = "aws.sqs";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AwsSqsCommon));

        internal const string IntegrationName = nameof(Configuration.IntegrationId.AwsSqs);
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.AwsSqs;

        public static Scope? CreateScope(Tracer tracer, string operation, out AwsSqsTags? tags, ISpanContext? parentContext = null, string spanKind = SpanKinds.Client)
        {
            tags = null;

            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId) || !tracer.Settings.IsIntegrationEnabled(AwsConstants.IntegrationId))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            Scope? scope = null;

            try
            {
                tags = tracer.CurrentTraceSettings.Schema.Messaging.CreateAwsSqsTags(spanKind);
                string serviceName = tracer.CurrentTraceSettings.GetServiceName(tracer, DatadogAwsSqsServiceName);
                string operationName = GetOperationName(tracer, spanKind);
                scope = tracer.StartActiveInternal(operationName, parent: parentContext, tags: tags, serviceName: serviceName);
                var span = scope.Span;

                span.Type = SpanTypes.Http;
                span.ResourceName = $"{SqsServiceName}.{operation}";

                tags.Service = SqsServiceName;
                tags.Operation = operation;
                tags.SetAnalyticsSampleRate(IntegrationId, tracer.Settings, enabledWithGlobalSetting: false);
                tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            // always returns the scope, even if it's null because we couldn't create it,
            // or we couldn't populate it completely (some tags is better than no tags)
            return scope;
        }

        [return: NotNullIfNotNull(nameof(queueUrl))]
        public static string? GetQueueName(string? queueUrl)
        {
            if (string.IsNullOrEmpty(queueUrl))
            {
                return queueUrl;
            }

            var lastSeparationIndex = queueUrl!.LastIndexOf('/') + 1;
            return queueUrl.Substring(lastSeparationIndex);
        }

        internal static string GetOperationName(Tracer tracer, string spanKind)
        {
            if (tracer.CurrentTraceSettings.Schema.Version == SchemaVersion.V0)
            {
                return SqsRequestOperationName;
            }

            return spanKind switch
            {
                SpanKinds.Consumer => tracer.CurrentTraceSettings.Schema.Messaging.GetInboundOperationName(SqsOperationName),
                SpanKinds.Producer => tracer.CurrentTraceSettings.Schema.Messaging.GetOutboundOperationName(SqsOperationName),
                _ => $"{SqsOperationName}.request"
            };
        }
    }
}
