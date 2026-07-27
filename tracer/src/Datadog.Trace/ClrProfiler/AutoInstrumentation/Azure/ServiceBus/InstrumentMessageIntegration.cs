// <copyright file="InstrumentMessageIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Shared;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Propagators;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.ServiceBus
{
    /// <summary>
    /// Azure.Core.Shared.MessagingClientDiagnostics::InstrumentMessage calltarget instrumentation
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
    public sealed class InstrumentMessageIntegration
    {
        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, IDictionary<string, object> properties, string activityName, ref string traceparent, ref string tracestate)
        {
            var tracer = Tracer.Instance;
            if (tracer.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId.AzureServiceBus))
            {
                var activeScope = tracer.InternalActiveScope;
                if (activeScope?.Span?.Context is SpanContext && properties != null)
                {
                    AzureMessagingCommon.InjectContext(properties, activeScope);
                }
            }

            return CallTargetState.GetDefault();
        }
    }
}
