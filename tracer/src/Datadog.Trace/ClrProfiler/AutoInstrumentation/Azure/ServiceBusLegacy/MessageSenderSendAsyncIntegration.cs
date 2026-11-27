// <copyright file="MessageSenderSendAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.ServiceBusLegacy;

/// <summary>
/// Microsoft.Azure.ServiceBus.Core.MessageSender::SendAsync calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = LegacyServiceBusCommon.AssemblyName,
    TypeName = "Microsoft.Azure.ServiceBus.Core.MessageSender",
    MethodName = "SendAsync",
    ReturnTypeName = ClrNames.Task,
    ParameterTypeNames = ["System.Collections.Generic.IList`1[" + LegacyServiceBusCommon.MessageTypeName + "]"],
    MinimumVersion = LegacyServiceBusCommon.MinimumVersion,
    MaximumVersion = LegacyServiceBusCommon.MaximumVersion,
    IntegrationName = nameof(IntegrationId.AzureServiceBus))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class MessageSenderSendAsyncIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, IEnumerable? messageList)
        where TTarget : ILegacyClientEntity
    {
        LegacyServiceBusCommon.OnProduce(instance, messageList);
        return CallTargetState.GetDefault();
    }
}
