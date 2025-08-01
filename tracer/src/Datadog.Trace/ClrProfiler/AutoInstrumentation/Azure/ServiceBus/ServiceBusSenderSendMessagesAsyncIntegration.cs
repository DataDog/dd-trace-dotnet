// <copyright file="ServiceBusSenderSendMessagesAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure_Messaging_ServiceBus;

/// <summary>
/// System.Threading.Tasks.Task Azure.Messaging.ServiceBus.ServiceBusSender::SendMessagesAsync(System.Collections.Generic.IEnumerable`1[Azure.Messaging.ServiceBus.ServiceBusMessage],System.Threading.CancellationToken) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Azure.Messaging.ServiceBus",
    TypeName = "Azure.Messaging.ServiceBus.ServiceBusSender",
    MethodName = "SendMessagesAsync",
    ReturnTypeName = ClrNames.Task,
    ParameterTypeNames = ["System.Collections.Generic.IEnumerable`1[Azure.Messaging.ServiceBus.ServiceBusMessage]", ClrNames.CancellationToken],
    MinimumVersion = "7.20.1",
    MaximumVersion = "7.*.*",
    IntegrationName = nameof(IntegrationId.AzureMessagingServiceBus))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class ServiceBusSenderSendMessagesAsyncIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TMessages>(TTarget instance, ref TMessages? messages, ref CancellationToken cancellationToken)
    {
        var tracer = Tracer.Instance;
        if (tracer.Settings.IsIntegrationEnabled(IntegrationId.AzureServiceBus))
        {
            var scope = tracer.StartActiveInternal(OperationName);
            var span = scope.Span;
            span.SetTag(Tags.SpanKind, SpanKinds.Producer);
            span.SetTag("azure.servicebus.entity_path", "entity_path");
            span.SetTag("azure.servicebus.namespace", "namespace");
            span.SetTag("azure.servicebus.operation", "send_batch");
            return new CallTargetState(scope);
        }

        return CallTargetState.GetDefault();
    }

    internal static TReturn? OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception exception, in CallTargetState state)
    {
        return returnValue;
    }
}
