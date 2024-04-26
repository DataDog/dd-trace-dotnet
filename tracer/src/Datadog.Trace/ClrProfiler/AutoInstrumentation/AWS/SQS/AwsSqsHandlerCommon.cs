// <copyright file="AwsSqsHandlerCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Vendors.Newtonsoft.Json.Utilities;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SQS;

/// <summary>
/// Contains the code that is shared between the integrations of sync/async and batch/single send and receive.
/// </summary>
internal static class AwsSqsHandlerCommon
{
    internal static CallTargetState BeforeSend<TSendMessageRequest>(TSendMessageRequest request, SendType sendType)
    {
        if (request is null)
        {
            return CallTargetState.GetDefault();
        }

        // we can't use generic constraints for this duck typing, because we need the original type
        // for the InjectHeadersIntoMessage<TSendMessageRequest> call below
        var requestProxy = request.DuckCast<IAmazonSQSRequestWithQueueUrl>();

        var scope = AwsSqsCommon.CreateScope(Tracer.Instance, sendType.OperationName, out var tags, spanKind: SpanKinds.Producer);

        var queueName = AwsSqsCommon.GetQueueName(requestProxy.QueueUrl);
        if (tags is not null && requestProxy.QueueUrl is not null)
        {
            tags.QueueUrl = requestProxy.QueueUrl;
            tags.QueueName = queueName;
        }

        if (scope?.Span.Context != null && !string.IsNullOrEmpty(queueName))
        {
            var dataStreamsManager = Tracer.Instance.TracerManager.DataStreamsManager;

            if (sendType == SendType.SingleMessage)
            {
                InjectForSingleMessage(dataStreamsManager, request, scope, queueName);
            }
            else if (sendType == SendType.Batch)
            {
                InjectForBatch(dataStreamsManager, request, scope, queueName);
            }
        }

        return new CallTargetState(scope);
    }

    private static void InjectForSingleMessage<TSendMessageRequest>(DataStreamsManager? dataStreamsManager, TSendMessageRequest request, Scope scope, string queueName)
    {
        var requestProxy = request.DuckCast<ISendMessageRequest>();
        if (requestProxy == null)
        {
            return;
        }

        if (dataStreamsManager != null && dataStreamsManager.IsEnabled)
        {
            var edgeTags = new[] { "direction:out", $"topic:{queueName}", "type:sqs" };
            scope.Span.SetDataStreamsCheckpoint(dataStreamsManager, CheckpointKind.Produce, edgeTags, payloadSizeBytes: 0, timeInQueueMs: 0);
        }

        ContextPropagation.InjectHeadersIntoMessage<TSendMessageRequest>(requestProxy, scope.Span.Context, dataStreamsManager);
    }

    private static void InjectForBatch<TSendMessageBatchRequest>(DataStreamsManager? dataStreamsManager, TSendMessageBatchRequest request, Scope scope, string queueName)
    {
        var requestProxy = request.DuckCast<ISendMessageBatchRequest>();
        if (requestProxy == null)
        {
            return;
        }

        var edgeTags = new[] { "direction:out", $"topic:{queueName}", "type:sqs" };
        foreach (var e in requestProxy.Entries)
        {
            var entry = e.DuckCast<IContainsMessageAttributes>();
            if (entry != null)
            {
                // this has no effect is DSM is disabled
                scope.Span.SetDataStreamsCheckpoint(dataStreamsManager, CheckpointKind.Produce, edgeTags, payloadSizeBytes: 0, timeInQueueMs: 0);
                // this needs to be done for context propagation even when DSM is disabled
                // (when DSM is enabled, it injects the pathway context on top of the trace context)
                ContextPropagation.InjectHeadersIntoMessage<TSendMessageBatchRequest>(entry, scope.Span.Context, dataStreamsManager);
            }
        }
    }

    internal static TResponse AfterSend<TResponse>(TResponse response, Exception? exception, in CallTargetState state)
    {
        state.Scope.DisposeWithException(exception);
        return response;
    }

    internal static CallTargetState BeforeReceive(IReceiveMessageRequest request)
    {
        if (request.Instance is null)
        {
            return CallTargetState.GetDefault();
        }

        var queueName = AwsSqsCommon.GetQueueName(request.QueueUrl);
        var scope = AwsSqsCommon.CreateScope(Tracer.Instance, "ReceiveMessage", out var tags, spanKind: SpanKinds.Consumer);
        if (tags is not null && request.QueueUrl is not null)
        {
            tags.QueueUrl = request.QueueUrl;
            tags.QueueName = queueName;
        }

        // request the message attributes that a datadog instrumentation might have set when sending
        if (request.MessageAttributeNames is null)
        {
            request.MessageAttributeNames = [ContextPropagation.SqsKey];
        }
        else
        {
            request.MessageAttributeNames.AddDistinct(ContextPropagation.SqsKey);
        }

        if (request.AttributeNames is null)
        {
            request.AttributeNames = ["SentTimestamp"];
        }
        else
        {
            request.AttributeNames.AddDistinct("SentTimestamp");
        }

        return new CallTargetState(scope, queueName);
    }

    internal static TResponse AfterReceive<TResponse>(TResponse response, Exception? exception, in CallTargetState state)
        where TResponse : IReceiveMessageResponse
    {
        if (response.Instance != null && response.Messages is { Count: > 0 } && state is { State: not null, Scope.Span: { } span })
        {
            var dataStreamsManager = Tracer.Instance.TracerManager.DataStreamsManager;
            if (dataStreamsManager is { IsEnabled: true })
            {
                var edgeTags = new[] { "direction:in", $"topic:{(string)state.State}", "type:sqs" };
                foreach (var o in response.Messages)
                {
                    var message = o.DuckCast<IMessage>();
                    if (message == null)
                    {
                        continue; // should not happen
                    }

                    var sentTime = 0;
                    if (message.Attributes != null && message.Attributes.TryGetValue("SentTimestamp", out var sentTimeStr) && sentTimeStr != null)
                    {
                        int.TryParse(sentTimeStr, out sentTime);
                    }

                    var adapter = AwsSqsHeadersAdapters.GetExtractionAdapter(message.MessageAttributes);
                    var parentPathway = dataStreamsManager.ExtractPathwayContext(adapter);
                    span.SetDataStreamsCheckpoint(dataStreamsManager, CheckpointKind.Consume, edgeTags, payloadSizeBytes: 0, sentTime, parentPathway);
                }
            }
        }

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
