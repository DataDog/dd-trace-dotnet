// <copyright file="MessageReceivePumpDispatchIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.ServiceBusLegacy;

/// <summary>
/// Microsoft.Azure.ServiceBus.MessageReceivePump::MessageDispatchTask calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = LegacyServiceBusCommon.AssemblyName,
    TypeName = "Microsoft.Azure.ServiceBus.MessageReceivePump",
    MethodName = "MessageDispatchTask",
    ReturnTypeName = ClrNames.Task,
    ParameterTypeNames = [LegacyServiceBusCommon.MessageTypeName],
    MinimumVersion = LegacyServiceBusCommon.MinimumVersion,
    MaximumVersion = LegacyServiceBusCommon.MaximumVersion,
    IntegrationName = nameof(IntegrationId.AzureServiceBus))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class MessageReceivePumpDispatchIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TMessage>(TTarget instance, TMessage message)
        where TMessage : class
    {
        if (message is not null)
        {
            LegacyServiceBusCommon.OnDispatch(message);
        }

        return CallTargetState.GetDefault();
    }
}
