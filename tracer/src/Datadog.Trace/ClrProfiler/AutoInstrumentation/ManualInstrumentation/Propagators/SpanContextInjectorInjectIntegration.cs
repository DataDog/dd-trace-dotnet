// <copyright file="SpanContextInjectorInjectIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Propagators;

/// <summary>
/// System.Void Datadog.Trace.SpanContextInjector::Inject[TCarrier](TCarrier,System.Action`3[TCarrier,System.String,System.String],Datadog.Trace.ISpanContext) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.Manual",
    TypeName = "Datadog.Trace.SpanContextInjector",
    MethodName = "Inject",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = ["!!0", "System.Action`3[!!0,System.String,System.String]", "Datadog.Trace.ISpanContext"],
    MinimumVersion = ManualInstrumentationConstants.MinVersion,
    MaximumVersion = ManualInstrumentationConstants.MaxVersion,
    IntegrationName = ManualInstrumentationConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class SpanContextInjectorInjectIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TCarrier, TAction, TSpanContext>(TTarget instance, in TCarrier carrier, in TAction setter, TSpanContext context)
    {
        // The Injector.Inject method currently _only_ works with SpanContext objects
        // Therefore, there's no point calling inject unless we can remap it to a SpanContext
        TelemetryFactory.Metrics.Record(PublicApiUsage.SpanContextInjector_Inject);
        var inject = (Action<TCarrier, string, string>)(object)setter!;

        if (SpanContextHelper.GetContext(context) is { } spanContext)
        {
            SpanContextInjector.InjectInternal(carrier, inject, spanContext);
        }

        return CallTargetState.GetDefault();
    }
}
