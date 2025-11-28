// <copyright file="BasicPublishAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Propagators;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.RabbitMQ;

/// <summary>
/// System.Threading.Tasks.ValueTask RabbitMQ.Client.Impl.Channel::BasicPublishAsync[TProperties](System.String,System.String,System.Boolean,TProperties,System.ReadOnlyMemory`1[System.Byte],System.Threading.CancellationToken) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "RabbitMQ.Client",
    TypeName = "RabbitMQ.Client.Impl.Channel",
    MethodName = "BasicPublishAsync",
    ReturnTypeName = "System.Threading.Tasks.ValueTask",
    ParameterTypeNames = [ClrNames.String, ClrNames.String, ClrNames.Bool, "!!0", "System.ReadOnlyMemory`1[System.Byte]", ClrNames.CancellationToken],
    MinimumVersion = "7.0.0",
    MaximumVersion = "7.*.*",
    IntegrationName = RabbitMQConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class BasicPublishAsyncIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TBasicProperties, TBody>(TTarget instance, string? exchange, string? routingKey, bool mandatory, TBasicProperties basicProperties, TBody body, in CancellationToken cancellationToken)
        where TBasicProperties : IReadOnlyBasicProperties, IDuckType
        where TBody : IBody, IDuckType
        where TTarget : IModelBase
    {
        var tracer = Tracer.Instance;
        var scope = RabbitMQIntegration.CreateScope(tracer, out var tags, BasicPublishIntegration.Command, spanKind: SpanKinds.Producer, exchange: exchange, routingKey: routingKey, host: instance?.Session?.Connection?.Endpoint?.HostName);

        // Tags is not null if span is not null, but keep analysis happy, as there's no attribute for that
        if (scope != null && tags is not null)
        {
            tracer.CurrentTraceSettings.Schema.RemapPeerService(tags);

            var exchangeDisplayName = string.IsNullOrEmpty(exchange) ? "<default>" : exchange;
            var routingKeyDisplayName = string.IsNullOrEmpty(routingKey) ? "<all>" : routingKey!.StartsWith("amq.gen-") ? "<generated>" : routingKey;
            scope.Span.ResourceName = $"{BasicPublishIntegration.Command} {exchangeDisplayName} -> {routingKeyDisplayName}";

            tags.MessageSize = body.Instance != null ? body.Length.ToString() : "0";
            if (basicProperties.Instance is not null)
            {
                if (basicProperties.IsDeliveryModePresent())
                {
                    tags.DeliveryMode = BasicPublishIntegration.DeliveryModeStrings[0x3 & basicProperties.DeliveryMode];
                }

                // We can't entirely reliably inject into the headers here, because
                // there's no guarantee that we can set the headers, as they're only
                // _required_ to be readonly. Instead we handle that in the
                // Channel.PopulateBasicPropertiesHeaders instrumentation which is called by BasicPublishAsync
            }
        }

        return new CallTargetState(scope);
    }

    // We don't support ValueTask in < .NET Core 3.1, which means this doesn't work and is never called
    internal static TReturn? OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception? exception, in CallTargetState state)
    {
        state.Scope.DisposeWithException(exception);
        return returnValue;
    }
}
