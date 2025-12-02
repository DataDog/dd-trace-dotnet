// <copyright file="BasicGetAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.RabbitMQ;

/// <summary>
/// System.Threading.Tasks.Task`1[RabbitMQ.Client.BasicGetResult] RabbitMQ.Client.Impl.Channel::BasicGetAsync(System.String,System.Boolean,System.Threading.CancellationToken) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "RabbitMQ.Client",
    TypeName = "RabbitMQ.Client.Impl.Channel",
    MethodName = "BasicGetAsync",
    ReturnTypeName = "System.Threading.Tasks.Task`1[RabbitMQ.Client.BasicGetResult]",
    ParameterTypeNames = [ClrNames.String, ClrNames.Bool, ClrNames.CancellationToken],
    MinimumVersion = "7.0.0",
    MaximumVersion = "7.*.*",
    IntegrationName = RabbitMQConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class BasicGetAsyncIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, string? queue, bool autoAck, in CancellationToken cancellationToken)
    {
        return new CallTargetState(scope: null, state: queue, startTime: DateTimeOffset.UtcNow);
    }

    internal static TResult OnAsyncMethodEnd<TTarget, TResult>(TTarget instance, TResult returnValue, Exception exception, in CallTargetState state)
        where TResult : IBasicGetResult, IDuckType
    {
        return BasicGetIntegration.OnMethodEnd(instance, returnValue, exception, in state).GetReturnValue()!;
    }
}
