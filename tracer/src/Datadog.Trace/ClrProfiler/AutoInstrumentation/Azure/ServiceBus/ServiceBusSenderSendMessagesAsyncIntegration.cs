// <copyright file="ServiceBusSenderSendMessagesAsyncIntegration.cs" company="Datadog">
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
/// System.Threading.Tasks.Task Azure.Messaging.ServiceBus.ServiceBusSender::SendMessagesAsync(System.Collections.Generic.IEnumerable`1[Azure.Messaging.ServiceBus.ServiceBusMessage],System.Threading.CancellationToken) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Azure.Messaging.ServiceBus",
    TypeName = "Azure.Messaging.ServiceBus.ServiceBusSender",
    MethodName = "SendMessagesAsync",
    ReturnTypeName = ClrNames.Task,
    ParameterTypeNames = ["System.Collections.Generic.IEnumerable`1[Azure.Messaging.ServiceBus.ServiceBusMessage]", ClrNames.CancellationToken],
    MinimumVersion = "7.14.0",
    MaximumVersion = "7.*.*",
    IntegrationName = nameof(IntegrationId.AzureServiceBus))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ServiceBusSenderSendMessagesAsyncIntegration
{
    private const string OperationName = "send";

    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, ref IEnumerable messages, ref CancellationToken cancellationToken)
        where TTarget : IServiceBusSender, IDuckType
    {
        var state = AzureServiceBusCommon.CreateSenderSpan(instance, OperationName, messages);

        var tracer = Tracer.Instance;
        var dataStreamsManager = tracer.TracerManager.DataStreamsManager;
        if (tracer.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId.AzureServiceBus)
            && dataStreamsManager.IsEnabled
            && messages is not null)
        {
            var entityPath = instance.EntityPath;
            var edgeTags = string.IsNullOrEmpty(entityPath)
                ? new[] { "direction:out", "type:servicebus" }
                : new[] { "direction:out", $"topic:{entityPath}", "type:servicebus" };

            foreach (var message in messages)
            {
                if (message.TryDuckCast<IServiceBusMessage>(out var serviceBusMessage)
                    && serviceBusMessage.ApplicationProperties is IDictionary<string, object> applicationProperties)
                {
                    var msgSize = dataStreamsManager.IsInDefaultState ? 0 : AzureServiceBusCommon.GetMessageSize(serviceBusMessage);

                    if (state.Scope?.Span is Span span)
                    {
                        span.SetDataStreamsCheckpoint(
                            dataStreamsManager,
                            CheckpointKind.Produce,
                            edgeTags,
                            msgSize,
                            0);

                        dataStreamsManager.InjectPathwayContextAsBase64String(
                            span.Context.PathwayContext,
                            new AzureHeadersCollectionAdapter(applicationProperties));
                    }
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
