// <copyright file="SpanExtensionsSetTraceSamplingPriorityIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Proxies;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Extensions;

/// <summary>
/// System.Void Datadog.Trace.ExtensionMethods.SpanExtensions::SetTraceSamplingPriority(Datadog.Trace.ISpan,Datadog.Trace.SamplingPriority) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.Manual",
    TypeName = "Datadog.Trace.ExtensionMethods.SpanExtensions",
    MethodName = "SetTraceSamplingPriority",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = new[] { "Datadog.Trace.ISpan", "Datadog.Trace.SamplingPriority" },
    MinimumVersion = ManualInstrumentationConstants.MinVersion,
    MaximumVersion = ManualInstrumentationConstants.MaxVersion,
    IntegrationName = ManualInstrumentationConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class SpanExtensionsSetTraceSamplingPriorityIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TSpan>(ref TSpan span, SamplingPriority samplingPriority)
    {
        TelemetryFactory.Metrics.Record(PublicApiUsage.SpanExtensions_SetTraceSamplingPriority);

        // Annoyingly, this takes an ISpan, so we have to do some duckTyping to make it work
        // it's most likely to be a duck-typed Span, so try that first
        if (span is IDuckType { Instance: Span s })
        {
            s.SetTraceSamplingPriorityInternal(samplingPriority);
        }
        else if (span is Span autoSpan)
        {
            autoSpan.SetTraceSamplingPriorityInternal(samplingPriority);
        }

        // If this isn't an automatic span, or the span is null, then this almost certainly won't work,
        // because SetTraceSamplingPriorityInternal tries to extract the "real" SpanContext from it
        return CallTargetState.GetDefault();
    }
}
