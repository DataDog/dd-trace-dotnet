// <copyright file="SpanContextExtractorExtractIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Propagators;

/// <summary>
/// Datadog.Trace.ISpanContext Datadog.Trace.SpanContextExtractor::Extract[TCarrier](TCarrier,System.Func`3[TCarrier,System.String,System.Collections.Generic.IEnumerable`1[System.String]]) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.Manual",
    TypeName = "Datadog.Trace.SpanContextExtractor",
    MethodName = "Extract",
    ReturnTypeName = "Datadog.Trace.ISpanContext",
    ParameterTypeNames = ["!!0", "System.Func`3[!!0,System.String,System.Collections.Generic.IEnumerable`1[System.String]]"],
    MinimumVersion = ManualInstrumentationConstants.MinVersion,
    MaximumVersion = ManualInstrumentationConstants.MaxVersion,
    IntegrationName = ManualInstrumentationConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class SpanContextExtractorExtractIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TCarrier, TAction>(TTarget instance, in TCarrier carrier, in TAction getter)
    {
        TelemetryFactory.Metrics.Record(PublicApiUsage.SpanContextExtractor_Extract);

        // we can't trust that the customer honours the nullable reference types here,
        // so we wrap the method in a try/catch and ensure we always return non-null
        var extract = (Func<TCarrier, string, IEnumerable<string?>>)(object)getter!;
        var extractor = new SafeExtractor<TCarrier>(extract);

        var tracer = Datadog.Trace.Tracer.Instance;
        var extracted = SpanContextExtractor.Extract(tracer, carrier, extractor.SafeExtract);
        return new CallTargetState(scope: null, state: extracted);
    }

    internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
    {
        return new CallTargetReturn<TReturn>(state.State.DuckCast<TReturn>());
    }
}
