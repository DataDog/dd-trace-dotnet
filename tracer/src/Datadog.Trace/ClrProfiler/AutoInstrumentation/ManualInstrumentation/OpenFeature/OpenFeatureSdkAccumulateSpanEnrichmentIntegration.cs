// <copyright file="OpenFeatureSdkAccumulateSpanEnrichmentIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.FeatureFlags;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.OpenFeature;

/// <summary>
/// System.Void Datadog.FeatureFlags.OpenFeature.FeatureFlagsSdk::AccumulateSpanEnrichment(System.Nullable`1[System.Int64],System.Boolean,System.String,System.Boolean,System.String,System.Object) calltarget instrumentation.
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.FeatureFlags.OpenFeature",
    TypeName = "Datadog.FeatureFlags.OpenFeature.FeatureFlagsSdk",
    MethodName = "AccumulateSpanEnrichment",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = ["System.Nullable`1[System.Int64]", ClrNames.Bool, ClrNames.String, ClrNames.Bool, ClrNames.String, ClrNames.Object],
    MinimumVersion = "2.0.0",
    MaximumVersion = "2.*.*",
    IntegrationName = nameof(IntegrationId.OpenFeature))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class OpenFeatureSdkAccumulateSpanEnrichmentIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget>(ref long? serialId, ref bool doLog, ref string? targetingKey, ref bool hasVariant, ref string flagKey, ref object? value)
    {
        var tracer = Datadog.Trace.Tracer.Instance;
        var traceContext = tracer.InternalActiveScope?.Span?.Context.TraceContext;

        // Skip creating per-trace state for evaluations that would record nothing.
        if (traceContext is not null && SpanEnrichmentState.IsRecordable(serialId, hasVariant))
        {
            traceContext.GetOrCreateFeatureFlagEnrichment()
                        ?.Accumulate(serialId, doLog, targetingKey, hasVariant, flagKey, value);
        }

        return CallTargetState.GetDefault();
    }
}
