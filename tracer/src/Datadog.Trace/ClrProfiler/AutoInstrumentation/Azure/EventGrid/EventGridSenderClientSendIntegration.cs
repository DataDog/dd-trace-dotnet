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
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.EventGrid;

/// <summary>
/// Azure.Messaging.EventGrid.Namespaces.EventGridSenderClient.Send calltarget instrumentation for CloudEvent
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
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class EventGridSenderClientSendIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TCloudEvent>(TTarget instance, TCloudEvent cloudEvent, CancellationToken cancellationToken)
        where TTarget : IEventGridSenderClient, IDuckType
    {
        return EventGridCommon.CreateNamespaceProducerSpanForEvent(instance, cloudEvent);
    }

    internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception? exception, in CallTargetState state)
    {
        state.Scope?.DisposeWithException(exception);
        return new(returnValue);
    }
}
