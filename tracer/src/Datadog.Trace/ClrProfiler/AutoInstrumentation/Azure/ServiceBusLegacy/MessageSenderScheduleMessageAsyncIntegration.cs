// <copyright file="MessageSenderScheduleMessageAsyncIntegration.cs" company="Datadog">
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
/// Microsoft.Azure.ServiceBus.Core.MessageSender::ScheduleMessageAsync calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = LegacyServiceBusCommon.AssemblyName,
    TypeName = "Microsoft.Azure.ServiceBus.Core.MessageSender",
    MethodName = "ScheduleMessageAsync",
    ReturnTypeName = "System.Threading.Tasks.Task`1[System.Int64]",
    ParameterTypeNames = [LegacyServiceBusCommon.MessageTypeName, "System.DateTimeOffset"],
    MinimumVersion = LegacyServiceBusCommon.MinimumVersion,
    MaximumVersion = LegacyServiceBusCommon.MaximumVersion,
    IntegrationName = nameof(IntegrationId.AzureServiceBus))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class MessageSenderScheduleMessageAsyncIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TMessage>(TTarget instance, TMessage message, DateTimeOffset scheduleEnqueueTimeUtc)
        where TTarget : ILegacyClientEntity
    {
        if (message is not null)
        {
            LegacyServiceBusCommon.OnProduce(instance, new object[] { message });
        }

        return CallTargetState.GetDefault();
    }
}
