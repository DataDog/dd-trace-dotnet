// <copyright file="AwsSnsHandlerCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Shared;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SNS;

internal static class AwsSnsHandlerCommon
{
    public static CallTargetState BeforePublish<TPublishRequest>(TPublishRequest request, SendType sendType)
    {
        if (request is null)
        {
            return CallTargetState.GetDefault();
        }

        // we can't use generic constraints for this duck typing, because we need the original type for CachedMessageHeadersHelper
        var requestProxy = request.DuckCast<IAmazonSNSRequestWithTopicArn>();

        var tracer = Tracer.Instance;
        var scope = AwsSnsCommon.CreateScope(tracer, sendType.OperationName, SpanKinds.Producer, out var tags);

        var topicName = AwsSnsCommon.GetTopicName(requestProxy.TopicArn);
        if (tags is not null && requestProxy.TopicArn is not null)
        {
            tags.TopicArn = requestProxy.TopicArn;
            tags.TopicName = topicName;
        }

        if (scope?.Span.Context is { } context && !string.IsNullOrEmpty(topicName))
        {
            var dataStreamsManager = tracer.TracerManager.DataStreamsManager;
            // avoid allocation if edgeTags are not going to be used
            var edgeTags = dataStreamsManager is { IsEnabled: true } ? ["direction:out", $"topic:{topicName}", "type:sns"] : Array.Empty<string>();

            if (sendType == SendType.SingleMessage)
            {
                // scope.Span.SetDataStreamsCheckpoint(dataStreamsManager, CheckpointKind.Produce, edgeTags, payloadSizeBytes: 0, timeInQueueMs: 0);
                ContextPropagation.InjectHeadersIntoMessage(tracer, request.DuckCast<IContainsMessageAttributes>(), context, dataStreamsManager, CachedMessageHeadersHelper<TPublishRequest>.Instance);
            }
            else if (sendType == SendType.Batch)
            {
                var batchRequestProxy = request.DuckCast<IPublishBatchRequest>();
                // Skip adding Trace Context if entries don't exist or empty.
                if (batchRequestProxy.PublishBatchRequestEntries is { Count: > 0 })
                {
                    foreach (var t in batchRequestProxy.PublishBatchRequestEntries)
                    {
                        var entry = t?.DuckCast<IContainsMessageAttributes>();

                        if (entry != null)
                        {
                            // scope.Span.SetDataStreamsCheckpoint(dataStreamsManager, CheckpointKind.Produce, edgeTags, payloadSizeBytes: 0, timeInQueueMs: 0);
                            ContextPropagation.InjectHeadersIntoMessage(tracer, entry, context, dataStreamsManager, CachedMessageHeadersHelper<TPublishRequest>.Instance);
                        }
                    }
                }
            }
        }

        return new CallTargetState(scope);
    }

    public class SendType
    {
        public static readonly SendType SingleMessage = new("Publish");

        public static readonly SendType Batch = new("PublishBatch");

        private SendType(string value)
        {
            OperationName = value;
        }

        public string OperationName { get; }

        public override string ToString()
        {
            return OperationName;
        }
    }
}
