// <copyright file="GetIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.IbmMq
{
    /// <summary>
    /// IBM MQ Put calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = IbmMqConstants.IbmMqAssemblyName,
        TypeName = IbmMqConstants.MqDestinationTypeName,
        MethodName = "Get",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = [IbmMqConstants.MqMessageTypeName, IbmMqConstants.MqMessageGetOptionsTypeName, ClrNames.Int32],
        MinimumVersion = "9.0.0",
        MaximumVersion = "9.*.*",
        IntegrationName = IbmMqConstants.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class GetIntegration
    {
        internal static CallTargetState OnMethodBegin<TTarget, TMessage, TOptions>(TTarget instance, TMessage msg, TOptions options, int maxSize)
        {
            return new CallTargetState(null, msg, DateTimeOffset.UtcNow);
        }

        internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception? exception, in CallTargetState state)
            where TTarget : IMqQueue, IDuckType
        {
            if (instance.Instance != null)
            {
                if (state.State != null && state.State.TryDuckCast<IMqMessage>(out var msg))
                {
                    var scope = IbmMqHelper.CreateConsumerScope(Tracer.Instance, state.StartTime, instance, msg);
                    scope.DisposeWithException(exception);
                }
            }

            return CallTargetReturn.GetDefault();
        }
    }
}
