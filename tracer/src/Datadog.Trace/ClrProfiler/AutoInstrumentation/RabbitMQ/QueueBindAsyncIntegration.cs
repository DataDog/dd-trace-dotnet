// <copyright file="QueueBindAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.RabbitMQ;

/// <summary>
/// RabbitMQ.Client QueueBind calltarget instrumentation
/// </summary>
/// <summary>
/// System.Threading.Tasks.Task RabbitMQ.Client.Impl.Channel::QueueBindAsync(System.String,System.String,System.String,System.Collections.Generic.IDictionary`2[System.String,System.Object],System.Boolean,System.Threading.CancellationToken) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "RabbitMQ.Client",
    TypeName = "RabbitMQ.Client.Impl.Channel",
    MethodName = "QueueBindAsync",
    ReturnTypeName = ClrNames.Task,
    ParameterTypeNames = [ClrNames.String, ClrNames.String, ClrNames.String, RabbitMQConstants.IDictionaryArgumentsTypeName, ClrNames.Bool, ClrNames.CancellationToken],
    MinimumVersion = "7.0.0",
    MaximumVersion = "7.*.*",
    IntegrationName = RabbitMQConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class QueueBindAsyncIntegration
{
    private const string Command = "queue.bind";

    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, string? queue, string? exchange, string? routingKey, IDictionary<string, object>? arguments, bool noWait, in CancellationToken cancellationToken)
        where TTarget : IModelBase
    {
        return new CallTargetState(RabbitMQIntegration.CreateScope(Tracer.Instance, out _, Command, SpanKinds.Client, queue: queue, exchange: exchange, routingKey: routingKey, host: instance?.Session?.Connection?.Endpoint?.HostName));
    }

    internal static TReturn? OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception exception, in CallTargetState state)
    {
        state.Scope.DisposeWithException(exception);
        return returnValue;
    }
}
