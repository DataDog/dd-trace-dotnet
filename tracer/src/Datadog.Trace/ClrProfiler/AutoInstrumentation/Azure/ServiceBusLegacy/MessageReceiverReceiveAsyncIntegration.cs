// <copyright file="MessageReceiverReceiveAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.ServiceBusLegacy;

/// <summary>
/// Microsoft.Azure.ServiceBus.Core.MessageReceiver::ReceiveAsync calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = LegacyServiceBusCommon.AssemblyName,
    TypeName = "Microsoft.Azure.ServiceBus.Core.MessageReceiver",
    MethodName = "ReceiveAsync",
    ReturnTypeName = "System.Threading.Tasks.Task`1[System.Collections.Generic.IList`1[" + LegacyServiceBusCommon.MessageTypeName + "]]",
    ParameterTypeNames = [ClrNames.Int32, "System.TimeSpan"],
    MinimumVersion = LegacyServiceBusCommon.MinimumVersion,
    MaximumVersion = LegacyServiceBusCommon.MaximumVersion,
    IntegrationName = nameof(IntegrationId.AzureServiceBus))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class MessageReceiverReceiveAsyncIntegration
{
    internal static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception? exception, in CallTargetState state)
        where TTarget : ILegacyClientEntity
    {
        if (exception is null && returnValue is IEnumerable messages)
        {
            LegacyServiceBusCommon.OnConsume(instance, messages);
        }

        return returnValue;
    }
}
