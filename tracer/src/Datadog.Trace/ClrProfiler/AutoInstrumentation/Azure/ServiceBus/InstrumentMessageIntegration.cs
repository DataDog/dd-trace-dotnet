// <copyright file="InstrumentMessageIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.ServiceBus
{
    /// <summary>
    /// Azure.Messaging.ServiceBus.Core.TransportSender.SendAsync calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Azure.Messaging.ServiceBus",
        TypeName = "Azure.Core.Shared.MessagingClientDiagnostics",
        MethodName = "InstrumentMessage",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { "System.Collections.Generic.IDictionary`2[System.String,System.Object]", ClrNames.String, "System.String&", "System.String&" },
        MinimumVersion = "7.14.0",
        MaximumVersion = "7.*.*",
        IntegrationName = nameof(IntegrationId.AzureServiceBus))]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class InstrumentMessageIntegration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="properties">The application message properties.</param>
        /// <param name="activityName">The activity name.</param>
        /// <param name="traceparent">The resulting traceparent string.</param>
        /// <param name="tracestate">The resulting tracestate string.</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, IDictionary<string, object> properties, string activityName, ref string traceparent, ref string tracestate)
        {
            if (Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationId.AzureServiceBus)
                && Tracer.Instance.TracerManager.DataStreamsManager.IsEnabled)
            {
                // Adding DSM to the send operation of IReadOnlyCollection<ServiceBusMessage>|ServiceBusMessageBatch - Step Two:
                // In between the OnMethodBegin and OnMethodEnd, a new Activity will be created to represent
                // the Azure Service Bus message. To access the active message that is being instrumented,
                // save the active message properties object to an AsyncLocal field. This will limit
                // our lookup to one AsyncLocal field and one static field
                AzureServiceBusCommon.ActiveMessageProperties.Value = properties;
            }

            return CallTargetState.GetDefault();
        }

        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value, in an async scenario will be T of Task of T</returns>
        internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception exception, in CallTargetState state)
        {
            AzureServiceBusCommon.ActiveMessageProperties.Value = null;
            return CallTargetReturn.GetDefault();
        }
    }
}
