// <copyright file="InstrumentMessageIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Shared;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.EventHubs;

/// <summary>
/// Azure.Core.Shared.MessagingClientDiagnostics.InstrumentMessage calltarget instrumentation for EventHubs
/// </summary>
[InstrumentMethod(
    AssemblyName = "Azure.Messaging.EventHubs",
    TypeName = "Azure.Core.Shared.MessagingClientDiagnostics",
    MethodName = "InstrumentMessage",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = new[] { "System.Collections.Generic.IDictionary`2[System.String,System.Object]", ClrNames.String, "System.String&", "System.String&" },
    MinimumVersion = "5.9.2",
    MaximumVersion = "5.*.*",
    IntegrationName = nameof(IntegrationId.AzureEventHubs))]
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
        if (Tracer.Instance.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId.AzureEventHubs))
        {
            var activeScope = Tracer.Instance.ActiveScope;
            if (activeScope?.Span?.Context is SpanContext spanContext && properties != null)
            {
                AzureMessagingCommon.InjectContext(properties, activeScope as Scope);
            }
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
        return CallTargetReturn.GetDefault();
    }
}
