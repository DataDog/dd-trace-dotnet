// <copyright file="EventGridSenderClientSendIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.EventGrid;

/// <summary>
/// Azure.Messaging.EventGrid.Namespaces.EventGridSenderClient.Send/SendAsync calltarget instrumentation for CloudEvent
/// </summary>
[InstrumentMethod(
    AssemblyName = "Azure.Messaging.EventGrid.Namespaces",
    TypeName = "Azure.Messaging.EventGrid.Namespaces.EventGridSenderClient",
    MethodName = "Send",
    ReturnTypeName = "Azure.Core.Response",
    ParameterTypeNames = ["Azure.Messaging.CloudEvent", ClrNames.CancellationToken],
    MinimumVersion = "1.0.0",
    MaximumVersion = "1.*.*",
    IntegrationName = nameof(IntegrationId.AzureEventGrid))]
[InstrumentMethod(
    AssemblyName = "Azure.Messaging.EventGrid.Namespaces",
    TypeName = "Azure.Messaging.EventGrid.Namespaces.EventGridSenderClient",
    MethodName = "SendAsync",
    ReturnTypeName = "System.Threading.Tasks.Task`1[Azure.Core.Response]",
    ParameterTypeNames = ["Azure.Messaging.CloudEvent", ClrNames.CancellationToken],
    MinimumVersion = "1.0.0",
    MaximumVersion = "1.*.*",
    IntegrationName = nameof(IntegrationId.AzureEventGrid))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class EventGridSenderClientSendIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TCloudEvent>(TTarget instance, TCloudEvent cloudEvent, CancellationToken cancellationToken)
        where TTarget : IEventGridSenderClient
    {
        return EventGridCommon.CreateNamespaceProducerSpanForEvent(instance, cloudEvent);
    }

    internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception? exception, in CallTargetState state)
    {
        state.Scope?.DisposeWithException(exception);
        return new(returnValue);
    }

    internal static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception? exception, in CallTargetState state)
    {
        state.Scope?.DisposeWithException(exception);
        return returnValue;
    }
}
