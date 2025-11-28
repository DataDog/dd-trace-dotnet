// <copyright file="BasicDeliverAsyncAsyncIntegration.cs" company="Datadog">
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
/// System.Threading.Tasks.Task RabbitMQ.Client.IAsyncBasicConsumer::HandleBasicDeliverAsync(System.String,System.UInt64,System.Boolean,System.String,System.String,RabbitMQ.Client.IReadOnlyBasicProperties,System.ReadOnlyMemory`1[System.Byte],System.Threading.CancellationToken) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "RabbitMQ.Client",
    TypeName = "RabbitMQ.Client.IAsyncBasicConsumer",
    MethodName = "HandleBasicDeliverAsync",
    ReturnTypeName = ClrNames.Task,
    ParameterTypeNames = [ClrNames.String, ClrNames.UInt64, ClrNames.Bool, ClrNames.String, ClrNames.String, RabbitMQConstants.IReadOnlyBasicPropertiesTypeName, "System.ReadOnlyMemory`1[System.Byte]", ClrNames.CancellationToken],
    MinimumVersion = "7.0.0",
    MaximumVersion = "7.*.*",
    IntegrationName = RabbitMQConstants.IntegrationName,
    CallTargetIntegrationKind = CallTargetKind.Interface)]
[InstrumentMethod(
    AssemblyName = "RabbitMQ.Client",
    TypeName = "RabbitMQ.Client.AsyncDefaultBasicConsumer",
    MethodName = "HandleBasicDeliverAsync",
    ReturnTypeName = ClrNames.Task,
    ParameterTypeNames = [ClrNames.String, ClrNames.UInt64, ClrNames.Bool, ClrNames.String, ClrNames.String, RabbitMQConstants.IReadOnlyBasicPropertiesTypeName, "System.ReadOnlyMemory`1[System.Byte]", ClrNames.CancellationToken],
    MinimumVersion = "7.0.0",
    MaximumVersion = "7.*.*",
    IntegrationName = RabbitMQConstants.IntegrationName,
    CallTargetIntegrationKind = CallTargetKind.Derived)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class BasicDeliverAsyncAsyncIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TProperties, TBody>(TTarget instance, ref string? consumerTag, ulong deliveryTag, bool redelivered, string? exchange, string? routingKey, TProperties properties, TBody body, in CancellationToken cancellationToken)
        where TProperties : IReadOnlyBasicProperties
        where TBody : IBody, IDuckType
    {
        return RabbitMQIntegration.BasicDeliver_OnMethodBegin(instance, redelivered, exchange, routingKey, properties, body);
    }

    internal static TReturn? OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception exception, in CallTargetState state)
    {
        state.Scope.DisposeWithException(exception);
        return returnValue;
    }
}
