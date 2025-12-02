// <copyright file="OTelBaggage_SetCurrentIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.OpenTelemetry
{
    /// <summary>
    /// Msmq calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "OpenTelemetry.Api",
        TypeName = "OpenTelemetry.Baggage",
        MethodName = "set_Current",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { "OpenTelemetry.Baggage" },
        MinimumVersion = "1.0.0",
        MaximumVersion = "1.0.0",
        IntegrationName = IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class OTelBaggage_SetCurrentIntegration
    {
        internal const string IntegrationName = nameof(Configuration.IntegrationId.OpenTelemetry);
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.OpenTelemetry;

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TInstance">Type of the instance (null for this static method)</typeparam>
        /// <typeparam name="TBaggage">Type of the baggage</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="value">Baggage value of the setter operation.</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TInstance, TBaggage>(TInstance instance, TBaggage value)
            where TBaggage : IApiBaggage
        {
            if (Tracer.Instance.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId))
            {
                // Reminder: The Datadog baggage is the source of truth for the baggage API interopability. The typical use case is:
                // - Datadog automatic instrumentations may populate Datadog.Trace.Baggage.Current with baggage items from the current request
                // - When the user calls OpenTelemetry.Baggage.get_Current(), the instrumentation will return Datadog.Trace.Baggage.Current
                // - When the user calls the static OpenTelemetry.Baggage APIs (which updates OpenTelemetry.Baggage.Current), the instrumentation will update Datadog.Trace.Baggage.Current to match the new OpenTelemetry.Baggage.Current

                // The OpenTelemetry Baggage model creates immutable baggage instances, so users cannot hold a reference to a Baggage object and mutate it multiple times
                // On the other hand, the Datadog Baggage model is mutable, allowing the user to get the Datadog.Trace.Baggage.Current once and continue to mutate that reference.
                // To ensure that updates from OpenTelemetry are reflected in the Datadog Baggage model, we must clear then add the new baggage items.
                Baggage.Current.Clear();
                var newBaggage = new Baggage(value.GetBaggage());
                newBaggage.MergeInto(Baggage.Current);
            }

            return CallTargetState.GetDefault();
        }
    }
}
