// <copyright file="PopulateBasicPropertiesHeadersWithTracingOptionsIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.RabbitMQ;

/// <summary>
/// RabbitMQ.Client.BasicProperties RabbitMQ.Client.Impl.Channel::PopulateBasicPropertiesHeaders[TProperties](TProperties,RabbitMQ.Client.ResolvedTracingOptions,System.Diagnostics.Activity,System.UInt64) calltarget instrumentation
/// for RabbitMQ.Client 7.3.0+, which added the <c>ResolvedTracingOptions</c> parameter.
/// See <see cref="PopulateBasicPropertiesHeadersIntegration"/> for the earlier 7.x signature.
/// </summary>
[InstrumentMethod(
    AssemblyName = "RabbitMQ.Client",
    TypeName = "RabbitMQ.Client.Impl.Channel",
    MethodName = "PopulateBasicPropertiesHeaders",
    ReturnTypeName = "RabbitMQ.Client.BasicProperties",
    ParameterTypeNames = ["!!0", "RabbitMQ.Client.ResolvedTracingOptions", ClrNames.Activity, ClrNames.UInt64],
    MinimumVersion = "7.0.0",
    MaximumVersion = "7.*.*",
    IntegrationName = RabbitMQConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class PopulateBasicPropertiesHeadersWithTracingOptionsIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TBasicProperties, TTracingOptions, TActivity>(TTarget instance, TBasicProperties basicProperties, TTracingOptions tracing, TActivity sendActivity, ulong publishSequenceNumber)
        => new(scope: null, basicProperties);

    internal static CallTargetReturn<TReturn?> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception? exception, in CallTargetState state)
        => PopulateBasicPropertiesHeadersIntegration.OnMethodEnd(instance, returnValue, exception, in state);
}
