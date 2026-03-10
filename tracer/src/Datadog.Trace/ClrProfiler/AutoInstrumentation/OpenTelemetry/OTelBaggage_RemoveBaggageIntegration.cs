// <copyright file="OTelBaggage_RemoveBaggageIntegration.cs" company="Datadog">
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
    /// OpenTelemetry.Api Baggage.RemoveBaggage calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "OpenTelemetry.Api",
        TypeName = "OpenTelemetry.Baggage",
        MethodName = "RemoveBaggage",
        ReturnTypeName = "OpenTelemetry.Baggage",
        ParameterTypeNames = new[] { ClrNames.String, "OpenTelemetry.Baggage" },
        MinimumVersion = "1.0.0",
        MaximumVersion = "1.0.0",
        IntegrationName = nameof(Configuration.IntegrationId.OpenTelemetry))]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class OTelBaggage_RemoveBaggageIntegration
    {
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.OpenTelemetry;

        internal static CallTargetState OnMethodBegin<TInstance, TBaggage>(TInstance instance, string key, TBaggage apiBaggage)
            where TBaggage : IApiBaggage
        {
            // If the user provides the default baggage instance, then OpenTelemetry.Baggage.Current will be used as the baggage source,
            // so we must update the underlying OpenTelemetry.Baggage.Current store to the latest Datadog.Trace.Baggage.Current items.
            if (Tracer.Instance.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId)
                && apiBaggage.Baggage is null)
            {
                // Since Datadog.Trace.Baggage.Current may have been updated since the last time OpenTelemetry.Baggage.Current was accessed,
                // we must update the underlying OpenTelemetry.Baggage.Current store with the latest Datadog.Trace.Baggage.Current items.
                // Note: When the user sets OpenTelemetry.Baggage.Current, those changes will override the contents of Datadog.Trace.Baggage.Current,
                // so we can always consider Datadog.Trace.Baggage.Current as being up-to-date.
                var baggageHolder = apiBaggage.EnsureBaggageHolder();
                baggageHolder.Baggage = apiBaggage.Create(Baggage.Current.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
            }

            return CallTargetState.GetDefault();
        }

        internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        {
            if (Tracer.Instance.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId)
                && exception is null
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
