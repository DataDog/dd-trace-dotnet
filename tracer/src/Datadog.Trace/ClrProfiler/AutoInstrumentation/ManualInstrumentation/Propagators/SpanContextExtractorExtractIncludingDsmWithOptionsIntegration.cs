// <copyright file="SpanContextExtractorExtractIncludingDsmWithOptionsIntegration.cs" company="Datadog">
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
/// Instrumentation for <c>SpanContextExtractor.ExtractIncludingDsmWithOptions{TCarrier}</c>,
/// called by the public <c>ExtractIncludingDsm</c> overload that takes <see cref="DataStreamsCheckpointOptions"/>.
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.Manual",
    TypeName = "Datadog.Trace.SpanContextExtractor",
    MethodName = "ExtractIncludingDsmWithOptions",
    ReturnTypeName = "Datadog.Trace.ISpanContext",
    ParameterTypeNames = ["!!0", "System.Func`3[!!0,System.String,System.Collections.Generic.IEnumerable`1[System.String]]", ClrNames.String, ClrNames.String, ClrNames.Int64, ClrNames.Int64, ClrNames.Bool],
    MinimumVersion = ManualInstrumentationConstants.MinVersion,
    MaximumVersion = ManualInstrumentationConstants.MaxVersion,
    IntegrationName = ManualInstrumentationConstants.IntegrationName)]
[Browsable(browsable: false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class SpanContextExtractorExtractIncludingDsmWithOptionsIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TCarrier, TAction>(TTarget instance, in TCarrier carrier, in TAction getter, string messageType, string source, long timestampMs, long sourceTimestampMs, bool chainFromPrevious)
    {
        TelemetryFactory.Metrics.Record(PublicApiUsage.SpanContextExtractor_ExtractIncludingDsm);

        // we can't trust that the customer honours the nullable reference types here,
        // so we wrap the method in a try/catch and ensure we always return non-null
        var extract = (Func<TCarrier, string, IEnumerable<string?>>)(object)getter!;
        var extractor = new SafeExtractor<TCarrier>(extract);

        var tracer = Datadog.Trace.Tracer.Instance;
        var extracted = SpanContextExtractor.ExtractWithCheckpointOptions(tracer, carrier, extractor.SafeExtract, messageType, source, timestampMs, sourceTimestampMs, chainFromPrevious);
        return new CallTargetState(scope: null, extracted);
    }

    internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
    {
        return new CallTargetReturn<TReturn>(state.State.DuckCast<TReturn>());
    }
}
