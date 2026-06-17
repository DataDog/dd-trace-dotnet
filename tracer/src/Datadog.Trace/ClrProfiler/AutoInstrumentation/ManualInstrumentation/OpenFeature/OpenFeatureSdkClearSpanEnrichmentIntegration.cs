// <copyright file="OpenFeatureSdkClearSpanEnrichmentIntegration.cs" company="Datadog">
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
/// System.Void Datadog.FeatureFlags.OpenFeature.FeatureFlagsSdk::ClearSpanEnrichment() calltarget
/// instrumentation. Bridges provider-close cleanup into <see cref="SpanEnrichmentStore.Clear"/>.
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.FeatureFlags.OpenFeature",
    TypeName = "Datadog.FeatureFlags.OpenFeature.FeatureFlagsSdk",
    MethodName = "ClearSpanEnrichment",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = [],
    MinimumVersion = "2.0.0",
    MaximumVersion = "2.*.*",
    IntegrationName = nameof(IntegrationId.OpenFeature))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class OpenFeatureSdkClearSpanEnrichmentIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget>()
    {
        SpanEnrichmentStore.Clear();
        return CallTargetState.GetDefault();
    }
}
