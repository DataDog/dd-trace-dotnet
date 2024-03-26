// <copyright file="AwsSnsCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SNS
{
    internal static class AwsSnsCommon
    {
        private const string DatadogAwsSnsServiceName = "aws-sns";
        private const string SnsRequestOperationName = "sns.request";
        private const string SnsServiceName = "SNS";
        private const string SnsOperationName = "aws.sns";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AwsSnsCommon));

        internal const string IntegrationName = nameof(Configuration.IntegrationId.AwsSns);
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.AwsSns;

        public static Scope? CreateScope(Tracer tracer, string operation, string spanKind, out AwsSnsTags? tags, ISpanContext? parentContext = null)
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
                tags = tracer.CurrentTraceSettings.Schema.Messaging.CreateAwsSnsTags(spanKind);
                var serviceName = tracer.CurrentTraceSettings.GetServiceName(tracer, DatadogAwsSnsServiceName);
                var operationName = GetOperationName(tracer, spanKind);
                scope = tracer.StartActiveInternal(operationName, parent: parentContext, tags: tags, serviceName: serviceName);
                var span = scope.Span;

                span.Type = SpanTypes.Http;
                span.ResourceName = $"{SnsServiceName}.{operation}";

                tags.Service = SnsServiceName;
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

        [return: NotNullIfNotNull(nameof(topicArn))]
        public static string? GetTopicName(string? topicArn)
        {
            if (topicArn is null)
            {
                return topicArn;
            }

            var lastSeparationIndex = topicArn.LastIndexOf(':') + 1;
            return topicArn.Substring(lastSeparationIndex);
        }

        internal static string GetOperationName(Tracer tracer, string spanKind)
        {
            if (tracer.CurrentTraceSettings.Schema.Version == SchemaVersion.V0)
            {
                return SnsRequestOperationName;
            }

            return spanKind switch
            {
                SpanKinds.Consumer => tracer.CurrentTraceSettings.Schema.Messaging.GetInboundOperationName(SnsOperationName),
                SpanKinds.Producer => tracer.CurrentTraceSettings.Schema.Messaging.GetOutboundOperationName(SnsOperationName),
                _ => $"{SnsOperationName}.request"
            };
        }
    }
}
