// <copyright file="SendServiceBusMessagesIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.ServiceBus
{
    /// <summary>
    /// Azure.Messaging.ServiceBus.ServiceBusSender calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Azure.Messaging.ServiceBus",
        TypeName = "Azure.Messaging.ServiceBus.ServiceBusSender",
        MethodName = "CreateDiagnosticScope",
        ReturnTypeName = "Azure.Core.Pipeline.DiagnosticScope",
        ParameterTypeNames = new[] { "System.Collections.Generic.IReadOnlyCollection`1[Azure.Messaging.ServiceBus.ServiceBusMessage]", ClrNames.String, "Azure.Core.Shared.MessagingDiagnosticOperation" },
        MinimumVersion = "7.14.0",
        MaximumVersion = "7.*.*",
        IntegrationName = nameof(IntegrationId.AzureServiceBus))]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class SendServiceBusMessagesIntegration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TOperation">Type of the diagnostic operation</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="messages">Collection instance of messages</param>
        /// <param name="activityName">Name of the activity</param>
        /// <param name="operation">Type of the operation</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TOperation>(TTarget instance, IEnumerable messages, string activityName, TOperation operation)
            where TTarget : ITransportSender, IDuckType
        {
            return CallTargetState.GetDefault();
        }
    }
}
