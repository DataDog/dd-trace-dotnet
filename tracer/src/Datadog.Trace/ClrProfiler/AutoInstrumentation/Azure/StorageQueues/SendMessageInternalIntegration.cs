// <copyright file="SendMessageInternalIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Shared;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.StorageQueues;

/// <summary>
/// Azure.Storage.Queues QueueClient.SendMessageInternal calltarget instrumentation.
/// Every public SendMessage/SendMessageAsync overload funnels into this method.
/// </summary>
[InstrumentMethod(
    AssemblyName = "Azure.Storage.Queues",
    TypeName = "Azure.Storage.Queues.QueueClient",
    MethodName = "SendMessageInternal",
    ReturnTypeName = "System.Threading.Tasks.Task`1[Azure.Response`1[Azure.Storage.Queues.Models.SendReceipt]]",
    ParameterTypeNames = new[] { "System.BinaryData", "System.Nullable`1[System.TimeSpan]", "System.Nullable`1[System.TimeSpan]", ClrNames.Bool, ClrNames.CancellationToken, ClrNames.String },
    MinimumVersion = "12.5.0",
    MaximumVersion = "12.*.*",
    IntegrationName = AzureStorageQueuesCommon.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class SendMessageInternalIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TMessage>(
        TTarget instance,
        TMessage message,
        TimeSpan? visibilityTimeout,
        TimeSpan? timeToLive,
        bool async,
        CancellationToken cancellationToken,
        string? operationName)
        where TTarget : IQueueClient, IDuckType
    {
        var tracer = Tracer.Instance;
        var dataStreamsManager = tracer.TracerManager.DataStreamsManager;
        if (!dataStreamsManager.IsEnabled)
        {
            return CallTargetState.GetDefault();
        }

        var state = AzureStorageQueuesCommon.CreateScope(tracer, SpanKinds.Producer, instance.Name);
        if (state.Scope is not { } scope || scope.Span is not { } span)
        {
            return CallTargetState.GetDefault();
        }

        var payloadSizeBytes = 0L;
        if (!dataStreamsManager.IsInDefaultState
            && message?.TryDuckCast<IBinaryData>(out var binaryData) == true)
        {
            payloadSizeBytes = binaryData.Length;
        }

        var edgeTags = AzureStorageQueuesCommon.GetEdgeTags(dataStreamsManager, instance.Name, isConsume: false);
        span.SetDataStreamsCheckpoint(dataStreamsManager, CheckpointKind.Produce, edgeTags, payloadSizeBytes, 0);

        var previousPathwayContext = AzureStorageQueuesCommon.CurrentPendingPathwayContext;
        AzureStorageQueuesCommon.SetPendingPathwayContext(span.Context.PathwayContext);

        return new CallTargetState(scope, previousPathwayContext);
    }

    internal static TResponse OnAsyncMethodEnd<TTarget, TResponse>(TTarget instance, TResponse response, Exception? exception, in CallTargetState state)
    {
        state.Scope?.DisposeWithException(exception);
        AzureStorageQueuesCommon.SetPendingPathwayContext((PathwayContext?)state.State);
        return response;
    }
}
