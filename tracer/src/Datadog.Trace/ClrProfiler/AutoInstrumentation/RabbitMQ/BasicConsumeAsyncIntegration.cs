// <copyright file="BasicConsumeAsyncIntegration.cs" company="Datadog">
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
/// RabbitMQ.Client BasicConsumeAsync calltarget instrumentation for v7+
/// </summary>
[InstrumentMethod(
    AssemblyName = "RabbitMQ.Client",
    TypeName = "RabbitMQ.Client.Impl.Channel",
    MethodName = "BasicConsumeAsync",
    ReturnTypeName = ClrNames.StringTask,
    ParameterTypeNames = [ClrNames.String, ClrNames.Bool, ClrNames.String, ClrNames.Bool, ClrNames.Bool, RabbitMQConstants.IDictionaryArgumentsTypeName, "RabbitMQ.Client.IAsyncBasicConsumer", ClrNames.CancellationToken],
    MinimumVersion = "7.0.0",
    MaximumVersion = "7.*.*",
    IntegrationName = RabbitMQConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class BasicConsumeAsyncIntegration
{
    private const string Command = "basic.consume";

    internal static CallTargetState OnMethodBegin<TTarget, TConsumer>(TTarget instance, string? queue, bool autoAck, string? consumerTag, bool noLocal, bool exclusive, IDictionary<string, object>? arguments, TConsumer consumer, in CancellationToken cancellationToken)
    {
        return BasicConsumeIntegration.OnMethodBegin(instance, queue, autoAck, consumerTag, noLocal, exclusive, arguments, consumer);
    }

    internal static string? OnAsyncMethodEnd<TTarget>(TTarget instance, string? returnValue, Exception? exception, in CallTargetState state)
    {
        state.Scope.DisposeWithException(exception);
        return returnValue;
    }
}
