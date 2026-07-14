// <copyright file="EventGridSenderClientSendEventsAsyncIntegration.cs" company="Datadog">
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
/// Azure.Messaging.EventGrid.Namespaces.EventGridSenderClient.SendAsync calltarget instrumentation for IEnumerable&lt;CloudEvent&gt;
/// </summary>
[InstrumentMethod(
    AssemblyName = "Azure.Messaging.EventGrid.Namespaces",
    TypeName = "Azure.Messaging.EventGrid.Namespaces.EventGridSenderClient",
    MethodName = "SendAsync",
    ReturnTypeName = "System.Threading.Tasks.Task`1[Azure.Core.Response]",
    ParameterTypeNames = ["System.Collections.Generic.IEnumerable`1[Azure.Messaging.CloudEvent]", ClrNames.CancellationToken],
    MinimumVersion = "1.0.0",
    MaximumVersion = "1.*.*",
    IntegrationName = nameof(IntegrationId.AzureEventGrid))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class EventGridSenderClientSendEventsAsyncIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TCloudEvents>(TTarget instance, TCloudEvents cloudEvents, CancellationToken cancellationToken)
        where TTarget : IEventGridSenderClient, IDuckType
    {
        return EventGridCommon.CreateNamespaceProducerSpanForEvents(instance, cloudEvents as IEnumerable);
    }

    internal static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception? exception, in CallTargetState state)
    {
        state.Scope?.DisposeWithException(exception);
        return returnValue;
    }
}
