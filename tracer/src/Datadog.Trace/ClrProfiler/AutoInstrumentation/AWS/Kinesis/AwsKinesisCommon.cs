// <copyright file="AwsKinesisCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Schema;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Kinesis
{
    internal static class AwsKinesisCommon
    {
        private const string DatadogAwsKinesisServiceName = "aws-kinesis";
        private const string KinesisServiceName = "Kinesis";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AwsKinesisCommon));

        internal const string IntegrationName = nameof(IntegrationId.AwsKinesis);
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.AwsKinesis;

        public static string? StreamNameFromARN(string? arn)
        {
            if (StringUtil.IsNullOrEmpty(arn))
            {
                return null;
            }

            var arnComponents = arn.Split('/');
            if (arnComponents.Length != 2)
            {
                return null;
            }

            return arnComponents[1];
        }

        public static string? GetStreamName(IAmazonKinesisRequestWithStreamNameAndStreamArn request)
        {
            string? arnStreamName = StreamNameFromARN(request.StreamARN);
            return StringUtil.IsNullOrEmpty(arnStreamName) ? request.StreamName : arnStreamName;
        }

        public static Scope? CreateScope(Tracer tracer, string operation, string spanKind, ISpanContext? parentContext, out AwsKinesisTags? tags)
        {
            tags = null;

            var perTraceSettings = tracer.CurrentTraceSettings;
            if (!perTraceSettings.Settings.IsIntegrationEnabled(IntegrationId) || !perTraceSettings.Settings.IsIntegrationEnabled(AwsConstants.IntegrationId))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            Scope? scope = null;

            try
            {
                tags = perTraceSettings.Schema.Messaging.CreateAwsKinesisTags(spanKind);
                string serviceName = perTraceSettings.GetServiceName(DatadogAwsKinesisServiceName);
                string operationName = perTraceSettings.Schema.Messaging.GetOutboundOperationName(MessagingSchema.OperationType.AwsKinesis);
                scope = tracer.StartActiveInternal(operationName, parent: parentContext, tags: tags, serviceName: serviceName);
                var span = scope.Span;

                span.Type = SpanTypes.Http;
                span.ResourceName = $"{KinesisServiceName}.{operation}";

                tags.Service = KinesisServiceName;
                tags.Operation = operation;
                tags.SetAnalyticsSampleRate(IntegrationId, perTraceSettings.Settings, enabledWithGlobalSetting: false);
                tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            // Always returns the scope. Even if it's `null` because we couldn't create it,
            // or we couldn't populate it completely (some tags is better than no tags).
            return scope;
        }
    }
}
