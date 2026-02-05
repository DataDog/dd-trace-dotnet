// <copyright file="OTelBaggage_GetCurrentIntegration.cs" company="Datadog">
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
    /// OpenTelemetry.Api Baggage.get_Current calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "OpenTelemetry.Api",
        TypeName = "OpenTelemetry.Baggage",
        MethodName = "get_Current",
        ReturnTypeName = "OpenTelemetry.Baggage",
        ParameterTypeNames = new string[0],
        MinimumVersion = "1.0.0",
        MaximumVersion = "1.0.0",
        IntegrationName = IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class OTelBaggage_GetCurrentIntegration
    {
        internal const string IntegrationName = nameof(Configuration.IntegrationId.OpenTelemetry);
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.OpenTelemetry;
        private static readonly Type? OTelBaggageType = Type.GetType("OpenTelemetry.Baggage, OpenTelemetry.Api", throwOnError: false);

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TInstance">Type of the instance (null for this static method)</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TInstance>(TInstance instance)
        {
            if (Tracer.Instance.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId)
                && OTelBaggageType is not null)
            {
                DuckType.CreateTypeResult proxyResult = DuckType.GetOrCreateProxyType(typeof(IApiBaggage), OTelBaggageType);
                if (proxyResult.Success)
                {
                    var apiBaggage = proxyResult.CreateInstance<IApiBaggage>(Activator.CreateInstance(OTelBaggageType));

                    // Since Datadog.Trace.Baggage.Current may have been updated since the last time OpenTelemetry.Baggage.Current was accessed,
                    // we must update the underlying OpenTelemetry.Baggage.Current store with the latest Datadog.Trace.Baggage.Current items.
                    // Note: When the user sets OpenTelemetry.Baggage.Current, those changes will override the contents of Datadog.Trace.Baggage.Current,
                    // so we can always consider Datadog.Trace.Baggage.Current as being up-to-date.
                    var baggageHolder = apiBaggage.EnsureBaggageHolder();
                    baggageHolder.Baggage = apiBaggage.Create(Baggage.Current.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
                }
            }

            return CallTargetState.GetDefault();
        }
    }
}
