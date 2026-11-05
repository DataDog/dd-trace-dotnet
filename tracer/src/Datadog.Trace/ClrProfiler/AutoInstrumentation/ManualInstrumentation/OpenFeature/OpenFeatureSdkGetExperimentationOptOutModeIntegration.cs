// <copyright file="OpenFeatureSdkGetExperimentationOptOutModeIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.OpenFeature;

/// <summary>Reads the configured experimentation opt-out mode at provider construction.</summary>
[InstrumentMethod(
    AssemblyName = "Datadog.FeatureFlags.OpenFeature",
    TypeName = "Datadog.FeatureFlags.OpenFeature.FeatureFlagsSdk",
    MethodName = "GetExperimentationOptOutMode",
    ReturnTypeName = ClrNames.String,
    ParameterTypeNames = [],
    MinimumVersion = "2.0.0",
    MaximumVersion = "2.*.*",
    IntegrationName = nameof(IntegrationId.OpenFeature))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class OpenFeatureSdkGetExperimentationOptOutModeIntegration
{
    internal static CallTargetReturn<string?> OnMethodEnd<TTarget>(string? returnValue, Exception? exception, in CallTargetState state)
        => new(global::Datadog.Trace.Tracer.Instance.Settings.FeatureFlags.ExperimentationOptOutMode);
}
