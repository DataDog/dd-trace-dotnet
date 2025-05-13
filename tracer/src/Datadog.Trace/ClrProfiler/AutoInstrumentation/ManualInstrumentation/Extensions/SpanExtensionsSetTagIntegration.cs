// <copyright file="SpanExtensionsSetTagIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.ComponentModel;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Proxies;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Extensions;

/// <summary>
/// Datadog.Trace.ISpan Datadog.Trace.SpanExtensions::SetTag(Datadog.Trace.ISpan,System.String,System.Nullable`1[System.Double]) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.Manual",
    TypeName = "Datadog.Trace.SpanExtensions",
    MethodName = "SetTag",
    ReturnTypeName = "Datadog.Trace.ISpan",
    ParameterTypeNames = new[] { "Datadog.Trace.ISpan", ClrNames.String, "System.Nullable`1[System.Double]" },
    MinimumVersion = ManualInstrumentationConstants.MinVersion,
    MaximumVersion = ManualInstrumentationConstants.MaxVersion,
    IntegrationName = ManualInstrumentationConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class SpanExtensionsSetTagIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TSpan>(ref TSpan span, ref string key, ref double? value)
    {
        TelemetryFactory.Metrics.Record(PublicApiUsage.SpanExtensions_SetTag);

        // Annoyingly, this takes an ISpan, so we have to do some duckTyping to make it work
        // it's most likely to be a duck-typed Span, so try that first
        if (span is IDuckType { Instance: Span s })
        {
            // this is the "typical" scenario
            s.SetTagInternal(key, value);
        }
        else if (span is Span autoSpan)
        {
            autoSpan.SetTagInternal(key, value);
        }
        else if (span is null)
        {
            // bad usage, but catering to it just in case
        }
        else
        {
            // This is a worst case, should basically never be necessary
            // Only required if customers create a custom ISpan
            span.DuckCast<ISpanSetTagProxy>()!.SetTag(key, value?.ToString());
        }

        // The default implementation returns the span
        return CallTargetState.GetDefault();
    }
}
