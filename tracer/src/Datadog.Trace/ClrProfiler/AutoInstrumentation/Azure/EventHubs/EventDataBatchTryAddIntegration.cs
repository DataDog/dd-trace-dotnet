// <copyright file="EventDataBatchTryAddIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Shared;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.EventHubs;

/// <summary>
/// Azure.Messaging.EventHubs.Producer.EventDataBatch.TryAdd calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Azure.Messaging.EventHubs",
    TypeName = "Azure.Messaging.EventHubs.Producer.EventDataBatch",
    MethodName = "TryAdd",
    ReturnTypeName = ClrNames.Bool,
    ParameterTypeNames = new[] { "Azure.Messaging.EventHubs.EventData" },
    MinimumVersion = "5.9.2",
    MaximumVersion = "5.*.*",
    IntegrationName = nameof(IntegrationId.AzureEventHubs))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class EventDataBatchTryAddIntegration
{
    private const string OperationName = "create";

    internal static CallTargetState OnMethodBegin<TTarget, TEventData>(
        TTarget instance,
        TEventData eventData)
        where TTarget : IEventDataBatch, IDuckType
        where TEventData : IEventData, IDuckType
    {
        var tracer = Tracer.Instance;
        if (!tracer.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId.AzureEventHubs))
        {
            return CallTargetState.GetDefault();
        }

        if (eventData.Instance is null)
        {
            return CallTargetState.GetDefault();
        }

        var state = tracer.Settings.AzureEventHubsBatchLinksEnabled
                        ? EventHubsCommon.CreateSenderSpan(
                            instance,
                            OperationName,
                            messages: new[] { eventData.Instance },
                            messageCount: 1,
                            spanLinks: null)
                        : CallTargetState.GetDefault();

        // The batch snapshots the event's properties as part of TryAdd, so the pathway must be
        // injected into them here rather than at the later SendAsync(EventDataBatch) call.
        EventHubsCommon.SetProduceCheckpoint(tracer, eventData, instance.EventHubName);

        return state;
    }

    internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(
        TTarget instance,
        TReturn returnValue,
        Exception? exception,
        in CallTargetState state)
    {
        if (state.Scope != null)
        {
            if (exception == null && returnValue is bool success && success && instance != null)
            {
                BatchSpanContextStorage.AddSpanContext(instance, state.Scope.Span.Context);
            }

            state.Scope.DisposeWithException(exception);
        }

        return new CallTargetReturn<TReturn>(returnValue);
    }
}
