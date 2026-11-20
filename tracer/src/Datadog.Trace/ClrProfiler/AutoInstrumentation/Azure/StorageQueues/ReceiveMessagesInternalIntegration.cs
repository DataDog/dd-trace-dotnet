// <copyright file="ReceiveMessagesInternalIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using System.Text;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.StorageQueues;

/// <summary>
/// Azure.Storage.Queues QueueClient.ReceiveMessagesInternal calltarget instrumentation.
/// Every public ReceiveMessage/ReceiveMessages overload funnels into this method.
/// </summary>
[InstrumentMethod(
    AssemblyName = "Azure.Storage.Queues",
    TypeName = "Azure.Storage.Queues.QueueClient",
    MethodName = "ReceiveMessagesInternal",
    ReturnTypeName = "System.Threading.Tasks.Task`1[Azure.Response`1[Azure.Storage.Queues.Models.QueueMessage[]]]",
    ParameterTypeNames = new[] { "System.Nullable`1[System.Int32]", "System.Nullable`1[System.TimeSpan]", ClrNames.String, ClrNames.Bool, ClrNames.CancellationToken },
    MinimumVersion = "12.5.0",
    MaximumVersion = "12.*.*",
    IntegrationName = AzureStorageQueuesCommon.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ReceiveMessagesInternalIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget>(
        TTarget instance,
        int? maxMessages,
        TimeSpan? visibilityTimeout,
        string? operationName,
        bool async,
        CancellationToken cancellationToken)
        where TTarget : IQueueClient, IDuckType
    {
        var tracer = Tracer.Instance;
        var dataStreamsManager = tracer.TracerManager.DataStreamsManager;
        if (!dataStreamsManager.IsEnabled)
        {
            return CallTargetState.GetDefault();
        }

        return new CallTargetState(
            AzureStorageQueuesCommon.CreateScope(tracer, SpanKinds.Consumer, instance.Name).Scope,
            instance.Name);
    }

    internal static TResponse OnAsyncMethodEnd<TTarget, TResponse>(TTarget instance, TResponse response, Exception? exception, in CallTargetState state)
        where TResponse : IQueueMessagesResponse, IDuckType
    {
        try
        {
            if (exception is null
                && response?.Instance is not null
                && state is { Scope.Span: { } span, State: string queueName }
                && response.Value is { Count: > 0 } messages)
            {
                var tracer = Tracer.Instance;
                var dataStreamsManager = tracer.TracerManager.DataStreamsManager;
                if (dataStreamsManager.IsEnabled)
                {
                    var edgeTags = AzureStorageQueuesCommon.GetEdgeTags(dataStreamsManager, queueName, isConsume: true);
                    var consumeTime = span.StartTime.UtcDateTime;

                    foreach (var messageObj in messages)
                    {
                        if (messageObj?.DuckCast<IQueueMessage>() is not { } message)
                        {
                            continue;
                        }

                        var pathwayContext = AzureStorageQueuesCommon.ExtractPathwayContextFromMessageText(dataStreamsManager, message.MessageText);

                        var timeInQueueMs = 0L;
                        if (message.InsertedOn is { } insertedOn)
                        {
                            timeInQueueMs = Math.Max(0, (long)(consumeTime - insertedOn.UtcDateTime).TotalMilliseconds);
                        }

                        var payloadSizeBytes = dataStreamsManager.IsInDefaultState
                            ? 0
                            : message.MessageText is { } messageText ? Encoding.UTF8.GetByteCount(messageText) : 0;

                        span.SetDataStreamsCheckpoint(dataStreamsManager, CheckpointKind.Consume, edgeTags, payloadSizeBytes, timeInQueueMs, pathwayContext);
                    }
                }
            }
        }
        finally
        {
            state.Scope?.DisposeWithException(exception);
        }

        return response;
    }
}
