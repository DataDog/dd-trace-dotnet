// <copyright file="AwsSnsHandlerCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Shared;
using Datadog.Trace.ClrProfiler.CallTarget;
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

        var scope = AwsSnsCommon.CreateScope(Tracer.Instance, sendType.OperationName, SpanKinds.Producer, out var tags);
        if (tags is not null && requestProxy.TopicArn is not null)
        {
            tags.TopicArn = requestProxy.TopicArn;
            tags.TopicName = AwsSnsCommon.GetTopicName(requestProxy.TopicArn);
        }

        if (scope?.Span.Context is { } context)
        {
            if (sendType == SendType.SingleMessage)
            {
                ContextPropagation.InjectHeadersIntoMessage(request.DuckCast<IContainsMessageAttributes>(), context, dataStreamsManager: null, CachedMessageHeadersHelper<TPublishRequest>.Instance);
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
                            ContextPropagation.InjectHeadersIntoMessage(entry, context, dataStreamsManager: null, CachedMessageHeadersHelper<TPublishRequest>.Instance);
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
