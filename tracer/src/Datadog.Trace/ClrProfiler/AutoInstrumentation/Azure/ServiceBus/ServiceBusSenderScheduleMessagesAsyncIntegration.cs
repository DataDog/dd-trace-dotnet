// <copyright file="ServiceBusSenderScheduleMessagesAsyncIntegration.cs" company="Datadog">
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
public static class ServiceBusSenderScheduleMessagesAsyncIntegration
{
    private const string OperationName = "send";

    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, ref IEnumerable messages, ref DateTimeOffset scheduledEnqueueTime, ref CancellationToken cancellationToken)
        where TTarget : IServiceBusSender, IDuckType
    {
        return AzureServiceBusCommon.CreateSenderSpan(instance, OperationName, messages);
    }

    internal static TReturn? OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception exception, in CallTargetState state)
    {
        state.Scope?.DisposeWithException(exception);
        return returnValue;
    }
}
