// <copyright file="EventHubProducerClientSendEnumerableAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.EventHubs;

/// <summary>
/// Azure.Messaging.EventHubs.Producer.EventHubProducerClient.SendAsync calltarget instrumentation for IEnumerable&lt;EventData&gt;
/// </summary>
[InstrumentMethod(
    AssemblyName = "Azure.Messaging.EventHubs",
    TypeName = "Azure.Messaging.EventHubs.Producer.EventHubProducerClient",
    MethodName = "SendAsync",
    ReturnTypeName = ClrNames.Task,
    ParameterTypeNames = new[] { "System.Collections.Generic.IEnumerable`1[Azure.Messaging.EventHubs.EventData]", ClrNames.CancellationToken },
    MinimumVersion = "5.9.2",
    MaximumVersion = "5.*.*",
    IntegrationName = nameof(IntegrationId.AzureEventHubs))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class EventHubProducerClientSendEnumerableAsyncIntegration
{
    private const string OperationName = "send";

    internal static CallTargetState OnMethodBegin<TTarget, TEventDataEnumerable>(
        TTarget instance,
        TEventDataEnumerable eventBatch,
        CancellationToken cancellationToken)
        where TTarget : IEventHubProducerClient, IDuckType
    {
        return EventHubsCommon.CreateSenderSpan(
            instance,
            OperationName,
            messages: eventBatch as IEnumerable,
            messageCount: null,
            spanLinks: null);
    }

    internal static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception? exception, in CallTargetState state)
    {
        state.Scope?.DisposeWithException(exception);
        return returnValue;
    }
}
