// <copyright file="OpenFeatureSdkIsSpanEnrichmentEnabledIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.OpenFeature;

/// <summary>
/// System.Boolean Datadog.FeatureFlags.OpenFeature.FeatureFlagsSdk::IsSpanEnrichmentEnabled()
/// calltarget instrumentation. Returns the real <c>TracerSettings.IsSpanEnrichmentEnabled</c> so the
/// provider only constructs the span-enrichment hook when the gate is on.
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.FeatureFlags.OpenFeature",
    TypeName = "Datadog.FeatureFlags.OpenFeature.FeatureFlagsSdk",
    MethodName = "IsSpanEnrichmentEnabled",
    ReturnTypeName = ClrNames.Bool,
    ParameterTypeNames = [],
    MinimumVersion = "2.0.0",
    MaximumVersion = "2.*.*",
    IntegrationName = nameof(IntegrationId.OpenFeature))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class OpenFeatureSdkIsSpanEnrichmentEnabledIntegration
{
    internal static CallTargetReturn<bool> OnMethodEnd<TTarget>(bool returnValue, Exception? exception, in CallTargetState state)
    {
        var enabled = Datadog.Trace.Tracer.Instance.Settings.IsSpanEnrichmentEnabled;
        return new CallTargetReturn<bool>(enabled);
    }
}
