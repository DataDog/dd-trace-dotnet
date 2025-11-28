// <copyright file="QueueDeclareAsyncIntegration.cs" company="Datadog">
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
/// System.Threading.Tasks.Task`1[RabbitMQ.Client.QueueDeclareOk] RabbitMQ.Client.Impl.Channel::QueueDeclareAsync(System.String,System.Boolean,System.Boolean,System.Boolean,System.Collections.Generic.IDictionary`2[System.String,System.Object],System.Boolean,System.Boolean,System.Threading.CancellationToken) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "RabbitMQ.Client",
    TypeName = "RabbitMQ.Client.Impl.Channel",
    MethodName = "QueueDeclareAsync",
    ReturnTypeName = "System.Threading.Tasks.Task`1[RabbitMQ.Client.QueueDeclareOk]",
    ParameterTypeNames = [ClrNames.String, ClrNames.Bool, ClrNames.Bool, ClrNames.Bool, RabbitMQConstants.IDictionaryArgumentsTypeName, ClrNames.Bool, ClrNames.Bool, ClrNames.CancellationToken],
    MinimumVersion = "7.0.0",
    MaximumVersion = "7.*.*",
    IntegrationName = RabbitMQConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class QueueDeclareAsyncIntegration
{
    private const string Command = "queue.declare";

    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, string? queue, bool durable, bool exclusive, bool autoDelete, IDictionary<string, object>? arguments, bool passive, bool noWait, in CancellationToken cancellationToken)
        where TTarget : IModelBase
    {
        return new CallTargetState(RabbitMQIntegration.CreateScope(Tracer.Instance, out _, Command, SpanKinds.Client, queue: queue, host: instance?.Session?.Connection?.Endpoint?.HostName));
    }

    internal static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception? exception, in CallTargetState state)
    {
        state.Scope.DisposeWithException(exception);
        return returnValue;
    }
}
