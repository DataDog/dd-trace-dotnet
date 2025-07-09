// <copyright file="AwsSqsHandlerCommon.cs" company="Datadog">
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
using Datadog.Trace.Vendors.Newtonsoft.Json.Utilities;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SQS;

/// <summary>
/// Contains the code that is shared between the integrations of sync/async and batch/single send and receive.
/// </summary>
internal static class AwsSqsHandlerCommon
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AwsSqsHandlerCommon));

    internal static CallTargetState BeforeSend<TSendMessageRequest>(TSendMessageRequest request, SendType sendType)
    {
        if (request is null)
        {
            Console.WriteLine("SQS BeforeSend: Request is null, skipping");
            return CallTargetState.GetDefault();
        }

        Console.WriteLine("SQS BeforeSend: Starting SQS send operation. SendType: {0}", sendType.OperationName);

        // we can't use generic constraints for this duck typing, because we need the original type
        // for the Inject call below
        var requestProxy = request.DuckCast<IAmazonSQSRequestWithQueueUrl>();

        var scope = AwsSqsCommon.CreateScope(Tracer.Instance, sendType.OperationName, out var tags, spanKind: SpanKinds.Producer);

        var queueName = AwsSqsCommon.GetQueueName(requestProxy.QueueUrl);
        Console.WriteLine("SQS BeforeSend: Queue URL: {0}, Queue Name: {1}", requestProxy.QueueUrl, queueName);

        if (tags is not null && requestProxy.QueueUrl is not null)
        {
            tags.QueueUrl = requestProxy.QueueUrl;
            tags.QueueName = queueName;
        }

        if (scope?.Span.Context != null && !string.IsNullOrEmpty(queueName))
        {
            var dataStreamsManager = Tracer.Instance.TracerManager.DataStreamsManager;
            Console.WriteLine("SQS BeforeSend: DataStreamsManager enabled: {0}", dataStreamsManager?.IsEnabled);

            if (sendType == SendType.SingleMessage)
            {
                Console.WriteLine("SQS BeforeSend: Processing single message");
                InjectForSingleMessage(dataStreamsManager, request, scope, queueName);
            }
            else if (sendType == SendType.Batch)
            {
                Console.WriteLine("SQS BeforeSend: Processing batch message");
                InjectForBatch(dataStreamsManager, request, scope, queueName);
            }
        }
        else
        {
            Console.WriteLine(
                "SQS BeforeSend: Skipping DataStreams processing - Scope: {0}, Context: {1}, QueueName: {2}",
                scope != null,
                scope?.Span.Context != null,
                queueName);
        }

        Console.WriteLine("SQS BeforeSend: Completed SQS send operation");
        return new CallTargetState(scope);
    }

    private static void InjectForSingleMessage<TSendMessageRequest>(DataStreamsManager? dataStreamsManager, TSendMessageRequest request, Scope scope, string queueName)
    {
        var requestProxy = request.DuckCast<IContainsMessageAttributes>();
        if (requestProxy == null)
        {
            Console.WriteLine("SQS InjectForSingleMessage: Request proxy is null, skipping");
            return;
        }

        Console.WriteLine("SQS InjectForSingleMessage: Message attributes count: {0}", requestProxy.MessageAttributes?.Count ?? 0);

        if (dataStreamsManager != null && dataStreamsManager.IsEnabled)
        {
            var edgeTags = new[] { "direction:out", $"topic:{queueName}", "type:sqs" };
            Console.WriteLine("SQS InjectForSingleMessage: Setting checkpoint with edge tags: [{0}]", string.Join(", ", edgeTags));
            scope.Span.SetDataStreamsCheckpoint(dataStreamsManager, CheckpointKind.Produce, edgeTags, payloadSizeBytes: 0, timeInQueueMs: 0);
            Console.WriteLine("SQS InjectForSingleMessage: Set DataStreams checkpoint for single message");
        }
        else
        {
            Console.WriteLine("SQS InjectForSingleMessage: DataStreamsManager is null or disabled, skipping checkpoint");
        }

        Console.WriteLine("SQS InjectForSingleMessage: Injecting headers into message");
        ContextPropagation.InjectHeadersIntoMessage(requestProxy, scope.Span.Context, dataStreamsManager, CachedMessageHeadersHelper<TSendMessageRequest>.Instance);
        Console.WriteLine("SQS InjectForSingleMessage: Injected headers into single message");
    }

    private static void InjectForBatch<TSendMessageBatchRequest>(DataStreamsManager? dataStreamsManager, TSendMessageBatchRequest request, Scope scope, string queueName)
    {
        var requestProxy = request.DuckCast<ISendMessageBatchRequest>();
        if (requestProxy == null || requestProxy.Entries == null)
        {
            Console.WriteLine("SQS InjectForBatch: Request proxy is null or entries are null, skipping");
            return;
        }

        Console.WriteLine("SQS InjectForBatch: Processing {0} batch entries", requestProxy.Entries.Count);
        var edgeTags = new[] { "direction:out", $"topic:{queueName}", "type:sqs" };
        foreach (var e in requestProxy.Entries)
        {
            var entry = e.DuckCast<IContainsMessageAttributes>();
            if (entry != null)
            {
                Console.WriteLine("SQS InjectForBatch: Processing batch entry with {0} message attributes", entry.MessageAttributes?.Count ?? 0);
                // this has no effect if DSM is disabled
                scope.Span.SetDataStreamsCheckpoint(dataStreamsManager, CheckpointKind.Produce, edgeTags, payloadSizeBytes: 0, timeInQueueMs: 0);
                Console.WriteLine("SQS InjectForBatch: Set DataStreams checkpoint for batch entry");
                // this needs to be done for context propagation even when DSM is disabled
                // (when DSM is enabled, it injects the pathway context on top of the trace context)
                ContextPropagation.InjectHeadersIntoMessage(entry, scope.Span.Context, dataStreamsManager, CachedMessageHeadersHelper<TSendMessageBatchRequest>.Instance);
                Console.WriteLine("SQS InjectForBatch: Injected headers into batch entry");
            }
            else
            {
                Console.WriteLine("SQS InjectForBatch: Batch entry is null, skipping");
            }
        }
    }

    internal static TResponse AfterSend<TResponse>(TResponse response, Exception? exception, in CallTargetState state)
    {
        Console.WriteLine("SQS AfterSend: Completing send operation. Exception: {0}", exception != null);
        state.Scope.DisposeWithException(exception);
        return response;
    }

    internal static CallTargetState BeforeReceive(IReceiveMessageRequest request)
    {
        if (request.Instance is null)
        {
            Console.WriteLine("SQS BeforeReceive: Request instance is null, skipping");
            return CallTargetState.GetDefault();
        }

        Console.WriteLine("SQS BeforeReceive: Starting SQS receive operation");

        var queueName = AwsSqsCommon.GetQueueName(request.QueueUrl);
        Console.WriteLine("SQS BeforeReceive: Queue URL: {0}, Queue Name: {1}", request.QueueUrl, queueName);

        var scope = AwsSqsCommon.CreateScope(Tracer.Instance, "ReceiveMessage", out var tags, spanKind: SpanKinds.Consumer);
        if (tags is not null && request.QueueUrl is not null)
        {
            tags.QueueUrl = request.QueueUrl;
            tags.QueueName = queueName;
        }

        // request the message attributes that a datadog instrumentation might have set when sending
        if (request.MessageAttributeNames is null)
        {
            Console.WriteLine("SQS BeforeReceive: Setting MessageAttributeNames to include {0}", ContextPropagation.InjectionKey);
            request.MessageAttributeNames = [ContextPropagation.InjectionKey];
        }
        else
        {
            Console.WriteLine("SQS BeforeReceive: Adding {0} to existing MessageAttributeNames", ContextPropagation.InjectionKey);
            request.MessageAttributeNames.AddDistinct(ContextPropagation.InjectionKey);
        }

        if (request.AttributeNames is null)
        {
            Console.WriteLine("SQS BeforeReceive: Setting AttributeNames to include SentTimestamp");
            request.AttributeNames = ["SentTimestamp"];
        }
        else
        {
            Console.WriteLine("SQS BeforeReceive: Adding SentTimestamp to existing AttributeNames");
            request.AttributeNames.AddDistinct("SentTimestamp");
        }

        Console.WriteLine("SQS BeforeReceive: Completed SQS receive operation setup");
        return new CallTargetState(scope, queueName);
    }

    internal static TResponse AfterReceive<TResponse>(TResponse response, Exception? exception, in CallTargetState state)
        where TResponse : IReceiveMessageResponse
    {
        Console.WriteLine("SQS AfterReceive: Processing receive response. Exception: {0}", exception != null);

        if (response.Instance != null && response.Messages is { Count: > 0 } && state is { State: not null, Scope.Span: { } span })
        {
            Console.WriteLine("SQS AfterReceive: Processing {0} messages", response.Messages.Count);
            var dataStreamsManager = Tracer.Instance.TracerManager.DataStreamsManager;
            Console.WriteLine("SQS AfterReceive: DataStreamsManager enabled: {0}", dataStreamsManager?.IsEnabled);

            if (dataStreamsManager is { IsEnabled: true })
            {
                var edgeTags = new[] { "direction:in", $"topic:{(string)state.State}", "type:sqs" };
                Console.WriteLine("SQS AfterReceive: Edge tags: [{0}]", string.Join(", ", edgeTags));

                foreach (var o in response.Messages)
                {
                    var message = o.DuckCast<IMessage>();
                    if (message == null)
                    {
                        Console.WriteLine("SQS AfterReceive: Message is null, skipping");
                        continue; // should not happen
                    }

                    Console.WriteLine("SQS AfterReceive: Processing message with {0} message attributes", message.MessageAttributes?.Count);

                    var sentTime = 0;
                    if (message.Attributes != null && message.Attributes.TryGetValue("SentTimestamp", out var sentTimeStr) && sentTimeStr != null)
                    {
                        int.TryParse(sentTimeStr, out sentTime);
                        Console.WriteLine("SQS AfterReceive: Message sent timestamp: {0}", sentTime);
                    }

                    var adapter = AwsMessageAttributesHeadersAdapters.GetExtractionAdapter(message.MessageAttributes);
                    Console.WriteLine("SQS AfterReceive: Created extraction adapter for message attributes");

                    var parentPathway = dataStreamsManager.ExtractPathwayContext(adapter);
                    Console.WriteLine("SQS AfterReceive: Extracted parent pathway: {0}", parentPathway != null);

                    span.SetDataStreamsCheckpoint(dataStreamsManager, CheckpointKind.Consume, edgeTags, payloadSizeBytes: 0, sentTime, parentPathway);
                    Console.WriteLine("SQS AfterReceive: Set DataStreams checkpoint for message with sentTime: {0}", sentTime);
                }
            }
            else
            {
                Console.WriteLine("SQS AfterReceive: DataStreamsManager is null or disabled, skipping checkpoints");
            }
        }
        else
        {
            Console.WriteLine(
                "SQS AfterReceive: No messages to process or invalid state - Response: {0}, Messages: {1}, State: {2}, Span: {3}",
                response.Instance != null,
                response.Messages?.Count ?? 0,
                state.State != null,
                state.Scope?.Span != null);
        }

        Console.WriteLine("SQS AfterReceive: Completing receive operation");
        state.Scope.DisposeWithException(exception);
        return response;
    }

    public class SendType
    {
        public static readonly SendType SingleMessage = new("SendMessage");

        public static readonly SendType Batch = new("SendMessageBatch");

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
