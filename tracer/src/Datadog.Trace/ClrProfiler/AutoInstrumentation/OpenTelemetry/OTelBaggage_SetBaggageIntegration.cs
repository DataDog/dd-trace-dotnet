// <copyright file="OTelBaggage_SetBaggageIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using System.Linq;
using Datadog.Trace;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.OpenTelemetry
{
    /// <summary>
    /// OpenTelemetry.Api Baggage.SetBaggage calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "OpenTelemetry.Api",
        TypeName = "OpenTelemetry.Baggage",
        MethodName = "SetBaggage",
        ReturnTypeName = "OpenTelemetry.Baggage",
        ParameterTypeNames = new[] { ClrNames.String, ClrNames.String, "OpenTelemetry.Baggage" },
        MinimumVersion = "1.0.0",
        MaximumVersion = "1.0.0",
        IntegrationName = IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class OTelBaggage_SetBaggageIntegration
    {
        internal const string IntegrationName = nameof(Configuration.IntegrationId.OpenTelemetry);
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.OpenTelemetry;

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TInstance">Type of the instance (null for this static method)</typeparam>
        /// <typeparam name="TBaggage">Type of the baggage</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="key">Key to be added to baggage.</param>
        /// <param name="value">Value to be added to baggage.</param>
        /// <param name="baggage">Optional baggage, or default if not specified.</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TInstance, TBaggage>(TInstance instance, string key, string value, TBaggage baggage)
            where TBaggage : IApiBaggage
        {
            if (Tracer.Instance.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId)
                && baggage.TryDuckCast<IApiBaggage>(out var apiBaggage))
            {
                // Update the underlying OpenTelemetry.Baggage.Current store to the latest Datadog.Trace.Baggage.Current items, which will be used if the user does not provide a custom baggage instance.
                // If the user does provide, a custom baggage instance, then this operation will have been useless as it will be overridden.
                // TODO: Once the behavior is validated to be correct, optimize this away.
                var baggageHolder = apiBaggage.EnsureBaggageHolder();
                baggageHolder.Baggage = apiBaggage.Create(Baggage.Current.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
            }

            return CallTargetState.GetDefault();
        }

        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TReturn">Type of the return value</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="returnValue">Return value</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value, in an async scenario will be T of Task of T</returns>
        internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        {
            if (Tracer.Instance.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId)
                && returnValue.TryDuckCast<IApiBaggage>(out var apiBaggage))
            {
                // Important: Before returning to the caller, the static OpenTelemetry.Baggage APIs set the to-be-returned baggage object as OpenTelemetry.Baggage.Current.
                // However, this is not done through the public setter (which we instrument), but through the backing field, so we must manually update Datadog.Trace.Baggage.Current
                // so it remains in-sync.
                //
                // Additional notes:
                // - Since the Datadog Baggage model is mutable (allowing the user to get the Datadog.Trace.Baggage.Current once and continue to mutate that reference),
                //   we must clear then add the new baggage items.
                // - The API can be invoked with an arbitrary OpenTelemetry.Baggage object passed via the parameter, so we must replace all baggage items every time
                //   we perform this instrumentation.
                Baggage.Current.Clear();
                var newBaggage = new Baggage(apiBaggage.GetBaggage());
                newBaggage.MergeInto(Baggage.Current);
            }

            return new CallTargetReturn<TReturn>(returnValue);
        }
    }
}
