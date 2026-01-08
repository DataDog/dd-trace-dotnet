// <copyright file="BasicPublishAsyncCachedStringsIntegration.cs" company="Datadog">
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
/// System.Threading.Tasks.ValueTask RabbitMQ.Client.Impl.Channel::BasicPublishAsync[TProperties](System.String,System.String,System.Boolean,TProperties,System.ReadOnlyMemory`1[System.Byte],System.Threading.CancellationToken) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "RabbitMQ.Client",
    TypeName = "RabbitMQ.Client.Impl.Channel",
    MethodName = "BasicPublishAsync",
    ReturnTypeName = "System.Threading.Tasks.ValueTask",
    ParameterTypeNames = ["RabbitMQ.Client.CachedString", "RabbitMQ.Client.CachedString", ClrNames.Bool, "!!0", "System.ReadOnlyMemory`1[System.Byte]", ClrNames.CancellationToken],
    MinimumVersion = "7.0.0",
    MaximumVersion = "7.*.*",
    IntegrationName = RabbitMQConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class BasicPublishAsyncCachedStringsIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TExchange, TRoutingKey, TBasicProperties, TBody>(TTarget instance, TExchange exchange, TRoutingKey routingKey, bool mandatory, TBasicProperties basicProperties, TBody body, in CancellationToken cancellationToken)
        where TBasicProperties : IReadOnlyBasicProperties, IDuckType
        where TBody : IBody, IDuckType
        where TExchange : ICachedStringProxy
        where TRoutingKey : ICachedStringProxy
        where TTarget : IModelBase
    {
        var exchangeString = exchange.Instance is not null ? exchange.Value : null;
        var routingKeyString = routingKey.Instance is not null ? routingKey.Value : null;
        return BasicPublishAsyncIntegration.OnMethodBegin(instance, exchangeString, routingKeyString, mandatory, basicProperties, body, default);
    }

    internal static TReturn? OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception? exception, in CallTargetState state)
    {
        state.Scope.DisposeWithException(exception);
        return returnValue;
    }
}

/// <summary>
/// DuckTyping interface for RabbitMQ.Client.CachedString
/// </summary>
#pragma warning disable SA1201 // An interface should not follow a class
internal interface ICachedStringProxy : IDuckType
{
    /// <summary>
    /// Gets the value of System.String
    /// </summary>
    [DuckField(Name = "Value")]
    string Value { get; }
}
