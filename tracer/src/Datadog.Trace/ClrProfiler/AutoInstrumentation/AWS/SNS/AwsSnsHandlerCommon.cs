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
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SNS;

internal static class AwsSnsHandlerCommon
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AwsSnsHandlerCommon));

    public static CallTargetState BeforePublish<TPublishRequest>(TPublishRequest request, SendType sendType)
    {
        if (request is null)
        {
            Console.WriteLine("SNS BeforePublish: Request is null, skipping");
            return CallTargetState.GetDefault();
        }

        Console.WriteLine("SNS BeforePublish: Starting SNS publish operation. SendType: {0}", sendType.OperationName);

        // we can't use generic constraints for this duck typing, because we need the original type for CachedMessageHeadersHelper
        var requestProxy = request.DuckCast<IAmazonSNSRequestWithTopicArn>();

        var scope = AwsSnsCommon.CreateScope(Tracer.Instance, sendType.OperationName, SpanKinds.Producer, out var tags);

        var topicName = AwsSnsCommon.GetTopicName(requestProxy.TopicArn);
        Console.WriteLine("SNS BeforePublish: Topic ARN: {0}, Topic Name: {1}", requestProxy.TopicArn, topicName);

        if (tags is not null && requestProxy.TopicArn is not null)
        {
            tags.TopicArn = requestProxy.TopicArn;
            tags.TopicName = topicName;
        }

        if (scope?.Span.Context is { } context && !string.IsNullOrEmpty(topicName))
        {
            var dataStreamsManager = Tracer.Instance.TracerManager.DataStreamsManager;
            Console.WriteLine("SNS BeforePublish: DataStreamsManager enabled: {0}", dataStreamsManager?.IsEnabled);

            // avoid allocation if edgeTags are not going to be used
            var edgeTags = dataStreamsManager is { IsEnabled: true } ? ["direction:out", $"topic:{topicName}", "type:sns"] : Array.Empty<string>();
            Console.WriteLine("SNS BeforePublish: Edge tags: [{0}]", string.Join(", ", edgeTags));

            if (sendType == SendType.SingleMessage)
            {
                Console.WriteLine("SNS BeforePublish: Processing single message");
                scope.Span.SetDataStreamsCheckpoint(dataStreamsManager, CheckpointKind.Produce, edgeTags, payloadSizeBytes: 0, timeInQueueMs: 0);
                Console.WriteLine("SNS BeforePublish: Set DataStreams checkpoint for single message");

                var messageAttributes = request.DuckCast<IContainsMessageAttributes>();
                Console.WriteLine("SNS BeforePublish: Message attributes count: {0}", messageAttributes?.MessageAttributes?.Count ?? 0);

                if (messageAttributes != null)
                {
                    Console.WriteLine("SNS BeforePublish: Injecting headers into single message");
                    try
                    {
                        ContextPropagation.InjectHeadersIntoMessage(messageAttributes, context, dataStreamsManager, CachedMessageHeadersHelper<TPublishRequest>.Instance);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error injecting headers into SNS message");
                    }

                    Console.WriteLine("SNS BeforePublish: Injected headers into single message");
                }
                else
                {
                    Console.WriteLine("SNS BeforePublish: Message attributes are null, skipping");
                }
            }
            else if (sendType == SendType.Batch)
            {
                Console.WriteLine("SNS BeforePublish: Processing batch message");
                var batchRequestProxy = request.DuckCast<IPublishBatchRequest>();
                // Skip adding Trace Context if entries don't exist or empty.
                if (batchRequestProxy.PublishBatchRequestEntries is { Count: > 0 })
                {
                    Console.WriteLine("SNS BeforePublish: Processing {0} batch entries", batchRequestProxy.PublishBatchRequestEntries.Count);
                    foreach (var t in batchRequestProxy.PublishBatchRequestEntries)
                    {
                        var entry = t?.DuckCast<IContainsMessageAttributes>();

                        if (entry != null)
                        {
                            Console.WriteLine("SNS BeforePublish: Setting checkpoint for batch entry");
                            scope.Span.SetDataStreamsCheckpoint(dataStreamsManager, CheckpointKind.Produce, edgeTags, payloadSizeBytes: 0, timeInQueueMs: 0);
                            Console.WriteLine("SNS BeforePublish: Injected headers into batch entry");
                            ContextPropagation.InjectHeadersIntoMessage(entry, context, dataStreamsManager, CachedMessageHeadersHelper<TPublishRequest>.Instance);
                        }
                        else
                        {
                            Console.WriteLine("SNS BeforePublish: Batch entry is null, skipping");
                        }
                    }
                }
                else
                {
                    Console.WriteLine("SNS BeforePublish: No batch entries found or entries are empty");
                }
            }
        }
        else
        {
            Console.WriteLine(
                "SNS BeforePublish: Skipping DataStreams processing - Scope: {0}, Context: {1}, TopicName: {2}",
                scope != null,
                scope?.Span.Context != null,
                topicName);
        }

        Console.WriteLine("SNS BeforePublish: Completed SNS publish operation");
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
