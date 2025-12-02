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
    /// OpenTelemetry.Api Baggage.set_Current calltarget instrumentation
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
    public sealed class OTelBaggage_SetCurrentIntegration
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
                // We treat Datadog.Trace.Baggage.Current as the single source of truth for the baggage API interopability, so we completely replace its
                // contents with the items from the new OpenTelemetry.Baggage object. In order to keep the mutable model of Datadog.Trace.Baggage.Current,
                // we must clear the contents of Datadog.Trace.Baggage.Current and then add the new items.
                //
                // The typical use case is:
                // - Datadog web server instrumentations populate Datadog.Trace.Baggage.Current with baggage items from the current request headers.
                // - The user modifies the current baggage by calling OpenTelemetry.Baggage.get_Current() and calling other OpenTelemetry.Baggage APIs as needed.
                // - The user sets their completed baggage as OpenTelemetry.Baggage.Current either by using this OpenTelemetry.Baggage.set_Current() API or calling
                //   one of the static OpenTelemetry.Baggage APIs, which automatically updates OpenTelemetry.Baggage.Current with the returned Baggage object.
                Baggage.Current.Clear();
                var newBaggage = new Baggage(value.GetBaggage());
                newBaggage.MergeInto(Baggage.Current);
            }

            return CallTargetState.GetDefault();
        }
    }
}
