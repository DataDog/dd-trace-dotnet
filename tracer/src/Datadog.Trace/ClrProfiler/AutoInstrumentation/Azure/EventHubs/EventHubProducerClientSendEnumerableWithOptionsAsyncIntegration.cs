// <copyright file="EventHubProducerClientSendEnumerableWithOptionsAsyncIntegration.cs" company="Datadog">
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
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.EventHubs;

/// <summary>
/// Azure.Messaging.EventHubs.Producer.EventHubProducerClient.SendAsync calltarget instrumentation for IEnumerable&lt;EventData&gt; with SendEventOptions,
/// which the overload without options also calls
/// </summary>
[InstrumentMethod(
    AssemblyName = "Azure.Messaging.EventHubs",
    TypeName = "Azure.Messaging.EventHubs.Producer.EventHubProducerClient",
    MethodName = "SendAsync",
    ReturnTypeName = ClrNames.Task,
    ParameterTypeNames = new[] { "System.Collections.Generic.IEnumerable`1[Azure.Messaging.EventHubs.EventData]", "Azure.Messaging.EventHubs.Producer.SendEventOptions", ClrNames.CancellationToken },
    MinimumVersion = "5.9.2",
    MaximumVersion = "5.*.*",
    IntegrationName = nameof(IntegrationId.AzureEventHubs))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class EventHubProducerClientSendEnumerableWithOptionsAsyncIntegration
{
    private const string OperationName = "send";

    internal static CallTargetState OnMethodBegin<TTarget, TEventDataEnumerable, TSendEventOptions>(
        TTarget instance,
        TEventDataEnumerable eventBatch,
        TSendEventOptions options,
        CancellationToken cancellationToken)
        where TTarget : IEventHubProducerClient, IDuckType
    {
        var tracer = Tracer.Instance;
        if (!tracer.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId.AzureEventHubs))
        {
            return CallTargetState.GetDefault();
        }

        var messages = eventBatch as IEnumerable;
        var state = IsSendSpanFor(tracer.InternalActiveScope?.Span, instance.EventHubName)
                        ? CallTargetState.GetDefault()
                        : EventHubsCommon.CreateSenderSpan(instance, OperationName, messages: messages);

        EventHubsCommon.SetProduceCheckpoints(tracer, messages, instance.EventHubName);

        return state;
    }

    internal static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception? exception, in CallTargetState state)
    {
        state.Scope?.DisposeWithException(exception);
        return returnValue;
    }

    private static bool IsSendSpanFor(Span? span, string? eventHubName)
        => span?.Tags is AzureEventHubsTags { SpanKind: SpanKinds.Producer, MessagingOperation: OperationName } tags
           && string.Equals(tags.MessagingDestinationName, eventHubName, StringComparison.Ordinal);
}
