// <copyright file="EventGridPublisherClientSendEventsAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.EventGrid;

/// <summary>
/// Azure.Messaging.EventGrid.EventGridPublisherClient.SendEventsAsync calltarget instrumentation for IEnumerable&lt;EventGridEvent&gt;
/// </summary>
[InstrumentMethod(
    AssemblyName = "Azure.Messaging.EventGrid",
    TypeName = "Azure.Messaging.EventGrid.EventGridPublisherClient",
    MethodName = "SendEventsAsync",
    ReturnTypeName = ClrNames.Task,
    ParameterTypeNames = ["System.Collections.Generic.IEnumerable`1[Azure.Messaging.EventGrid.EventGridEvent]", ClrNames.CancellationToken],
    MinimumVersion = "4.0.0",
    MaximumVersion = "5.*.*",
    IntegrationName = nameof(IntegrationId.AzureEventGrid))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class EventGridPublisherClientSendEventsAsyncIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TEvents>(TTarget instance, TEvents events, CancellationToken cancellationToken)
        where TTarget : IEventGridPublisherClient, IDuckType
    {
        return EventGridCommon.CreateProducerSpan(instance, events as IEnumerable);
    }

    internal static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception? exception, in CallTargetState state)
    {
        state.Scope?.DisposeWithException(exception);
        return returnValue;
    }
}
