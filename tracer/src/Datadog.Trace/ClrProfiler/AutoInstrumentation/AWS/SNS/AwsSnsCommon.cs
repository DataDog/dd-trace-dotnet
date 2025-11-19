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
using Datadog.Trace.Util;

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

        public static Scope? CreateScope(Tracer tracer, string operation, string spanKind, IAmazonSNSRequestWithTopicArn request, out AwsSnsTags? tags, ISpanContext? parentContext = null)
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
                tags = perTraceSettings.Schema.Messaging.CreateAwsSnsTags(spanKind);
                var serviceName = perTraceSettings.GetServiceName(DatadogAwsSnsServiceName);
                var operationName = GetOperationName(tracer, spanKind);
                scope = tracer.StartActiveInternal(operationName, parent: parentContext, tags: tags, serviceName: serviceName);
                var span = scope.Span;

                span.Type = SpanTypes.Http;
                span.ResourceName = $"{SnsServiceName}.{operation}";

                tags.Service = SnsServiceName;
                tags.Operation = operation;
                tags.TopicArn = request.TopicArn;
                tags.TopicName = GetTopicName(tags.TopicArn);
                bool isOutbound = (spanKind == SpanKinds.Client) || (spanKind == SpanKinds.Producer);
                bool isServerless = EnvironmentHelpers.IsAwsLambda();
                Console.WriteLine($"spanKind: {spanKind}");
                Console.WriteLine($"isOutbound: {isOutbound}");
                Console.WriteLine($"isServerless: {isServerless}");
                Console.WriteLine($"tags.Region: {tags.Region}");
                if (isServerless && isOutbound && tags.Region != null)
                {
                    tags.PeerService = "sns." + tags.Region + ".amazonaws.com";
                    tags.PeerServiceSource = "peer.service";
                    Console.WriteLine($"peer service tag for serverless: {tags.PeerService}");
                }
                else if (!isServerless && isOutbound)
                {
                    tags.PeerService = tags.TopicName;
                    tags.PeerServiceSource = Trace.Tags.TopicName;
                    Console.WriteLine($"peer service tag for serverfull: {tags.PeerService}");
                }

                perTraceSettings.Schema.RemapPeerService(tags);
                tags.SetAnalyticsSampleRate(IntegrationId, perTraceSettings.Settings, enabledWithGlobalSetting: false);
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
            if (StringUtil.IsNullOrEmpty(topicArn))
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
