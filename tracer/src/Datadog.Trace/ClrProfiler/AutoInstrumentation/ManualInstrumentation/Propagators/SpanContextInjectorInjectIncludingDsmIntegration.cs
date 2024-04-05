// <copyright file="SpanContextInjectorInjectIncludingDsmIntegration.cs" company="Datadog">
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
/// Instrumentation for <see cref="Datadog.Trace.SpanContextInjector.InjectIncludingDsm{TCarrier}"/>
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.Manual",
    TypeName = "Datadog.Trace.SpanContextInjector",
    MethodName = "InjectIncludingDsm",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = ["!!0", "System.Action`3[!!0,System.String,System.String]", "Datadog.Trace.ISpanContext", "System.String", "System.String"],
    MinimumVersion = ManualInstrumentationConstants.MinVersion,
    MaximumVersion = ManualInstrumentationConstants.MaxVersion,
    IntegrationName = ManualInstrumentationConstants.IntegrationName)]
[Browsable(browsable: false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class SpanContextInjectorInjectIncludingDsmIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TCarrier, TAction, TSpanContext>(TTarget instance, in TCarrier carrier, in TAction setter, TSpanContext context, string messageType, string target)
    {
        // The Injector.Inject method currently _only_ works with SpanContext objects
        // Therefore, there's no point calling inject unless we can remap it to a SpanContext
        TelemetryFactory.Metrics.Record(PublicApiUsage.SpanContextInjector_Inject);
        var inject = (Action<TCarrier, string, string>)(object)setter!;

        if (SpanContextHelper.GetContext(context) is { } spanContext)
        {
            SpanContextInjector.InjectInternal(carrier, inject, spanContext, messageType, target);
        }

        return CallTargetState.GetDefault();
    }
}
