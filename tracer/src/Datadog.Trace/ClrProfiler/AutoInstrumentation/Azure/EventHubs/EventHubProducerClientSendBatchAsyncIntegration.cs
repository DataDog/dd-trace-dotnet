// <copyright file="EventHubProducerClientSendBatchAsyncIntegration.cs" company="Datadog">
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
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.EventHubs;

/// <summary>
/// Azure.Messaging.EventHubs.Producer.EventHubProducerClient.SendAsync calltarget instrumentation for EventDataBatch
/// </summary>
[InstrumentMethod(
    AssemblyName = "Azure.Messaging.EventHubs",
    TypeName = "Azure.Messaging.EventHubs.Producer.EventHubProducerClient",
    MethodName = "SendAsync",
    ReturnTypeName = ClrNames.Task,
    ParameterTypeNames = new[] { "Azure.Messaging.EventHubs.Producer.EventDataBatch", ClrNames.CancellationToken },
    MinimumVersion = "5.9.2",
    MaximumVersion = "5.*.*",
    IntegrationName = nameof(IntegrationId.AzureEventHubs))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class EventHubProducerClientSendBatchAsyncIntegration
{
    private const string OperationName = "send";

    internal static CallTargetState OnMethodBegin<TTarget, TEventBatch>(
        TTarget instance,
        TEventBatch eventBatch,
        CancellationToken cancellationToken)
        where TTarget : IEventHubProducerClient, IDuckType
        where TEventBatch : IEventDataBatch, IDuckType
    {
        var spanLinks = BatchSpanContextStorage.ExtractSpanContexts(eventBatch?.Instance);
        var messageCount = eventBatch?.Instance != null ? eventBatch.Count : (int?)null;

        return EventHubsCommon.CreateSenderSpan(
            instance,
            OperationName,
            messages: null,
            messageCount: messageCount,
            spanLinks: spanLinks);
    }

    internal static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception? exception, in CallTargetState state)
    {
        state.Scope?.DisposeWithException(exception);
        return returnValue;
    }
}
