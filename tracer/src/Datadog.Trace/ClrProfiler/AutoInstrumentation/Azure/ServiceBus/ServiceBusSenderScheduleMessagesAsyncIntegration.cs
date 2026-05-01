// <copyright file="ServiceBusSenderScheduleMessagesAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Shared;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.ServiceBus;

/// <summary>
/// System.Threading.Tasks.Task`1[System.Collections.Generic.IReadOnlyList`1[System.Int64]] Azure.Messaging.ServiceBus.ServiceBusSender::ScheduleMessagesAsync(System.Collections.Generic.IEnumerable`1[Azure.Messaging.ServiceBus.ServiceBusMessage],System.DateTimeOffset,System.Threading.CancellationToken) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Azure.Messaging.ServiceBus",
    TypeName = "Azure.Messaging.ServiceBus.ServiceBusSender",
    MethodName = "ScheduleMessagesAsync",
    ReturnTypeName = "System.Threading.Tasks.Task`1[System.Collections.Generic.IReadOnlyList`1[System.Int64]]",
    ParameterTypeNames = ["System.Collections.Generic.IEnumerable`1[Azure.Messaging.ServiceBus.ServiceBusMessage]", "System.DateTimeOffset", ClrNames.CancellationToken],
    MinimumVersion = "7.14.0",
    MaximumVersion = "7.*.*",
    IntegrationName = nameof(IntegrationId.AzureServiceBus))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ServiceBusSenderScheduleMessagesAsyncIntegration
{
    private const string OperationName = "send";
    private static readonly string[] DefaultProduceEdgeTags = ["direction:out", "type:servicebus"];

    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, ref IEnumerable messages, ref DateTimeOffset scheduledEnqueueTime, ref CancellationToken cancellationToken)
        where TTarget : IServiceBusSender, IDuckType
    {
        var state = AzureServiceBusCommon.CreateSenderSpan(instance, OperationName, messages);

        var tracer = Tracer.Instance;
        var dataStreamsManager = tracer.TracerManager.DataStreamsManager;

        if (dataStreamsManager.IsEnabled
            && state.Scope?.Span is Span span
            && messages is ICollection materializedMessages)
        {
            var entityPath = instance.EntityPath;
            var edgeTags = string.IsNullOrEmpty(entityPath)
                ? DefaultProduceEdgeTags
                : dataStreamsManager.GetOrCreateEdgeTags(
                    new ServiceBusEdgeTagCacheKey(entityPath!, IsConsume: false),
                    static k => ["direction:out", $"topic:{k.EntityPath}", "type:servicebus"]);
            span.SetDataStreamsCheckpoint(dataStreamsManager, CheckpointKind.Produce, edgeTags, 0, 0);
            foreach (var msgObj in materializedMessages)
            {
                if (msgObj?.DuckCast<IServiceBusMessage>() is { ApplicationProperties: IDictionary<string, object> props }
                    && !props.ContainsKey(DataStreamsPropagationHeaders.PropagationKeyBase64))
                {
                    dataStreamsManager.InjectPathwayContextAsBase64String(
                        span.Context.PathwayContext,
                        new AzureHeadersCollectionAdapter(props));
                }
            }
        }

        return state;
    }

    internal static TReturn? OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception exception, in CallTargetState state)
    {
        state.Scope?.DisposeWithException(exception);
        return returnValue;
    }
}
