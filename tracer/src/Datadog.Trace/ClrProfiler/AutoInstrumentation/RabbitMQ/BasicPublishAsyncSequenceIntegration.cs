// <copyright file="BasicPublishAsyncSequenceIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.RabbitMQ;

/// <summary>
/// System.Threading.Tasks.ValueTask RabbitMQ.Client.Impl.Channel::BasicPublishAsync[TProperties](System.String,System.String,System.Boolean,TProperties,System.Buffers.ReadOnlySequence`1[System.Byte],System.IDisposable,System.Threading.CancellationToken) calltarget instrumentation
/// (added in RabbitMQ.Client 7.3.0)
/// </summary>
[InstrumentMethod(
    AssemblyName = "RabbitMQ.Client",
    TypeName = "RabbitMQ.Client.Impl.Channel",
    MethodName = "BasicPublishAsync",
    ReturnTypeName = "System.Threading.Tasks.ValueTask",
    ParameterTypeNames = [ClrNames.String, ClrNames.String, ClrNames.Bool, "!!0", ClrNames.ReadOnlySequenceOfByteTypeName, ClrNames.IDisposableTypeName, ClrNames.CancellationToken],
    MinimumVersion = "7.0.0",
    MaximumVersion = "7.*.*",
    IntegrationName = RabbitMQConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class BasicPublishAsyncSequenceIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TBasicProperties, TBody>(TTarget instance, string? exchange, string? routingKey, bool mandatory, TBasicProperties basicProperties, TBody body, IDisposable? bodyOwner, in CancellationToken cancellationToken)
        where TBasicProperties : IReadOnlyBasicProperties, IDuckType
        where TBody : ISequenceBody, IDuckType
        where TTarget : IModelBase
        => BasicPublishAsyncIntegration.CreatePublishScope(instance, exchange, routingKey, basicProperties, body.GetSequenceLength());

    internal static TReturn? OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception? exception, in CallTargetState state)
    {
        state.Scope.DisposeWithException(exception);
        return returnValue;
    }
}
