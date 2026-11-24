// <copyright file="OpenFeatureSdkCanEnqueueEVPWithFeaturesIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.ClrProfiler.CallTarget.Handlers;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.OpenFeature;

/// <summary>
/// Tells the provider that this tracer accepts EnqueueEVPWithFeatures. With an older tracer the stub
/// keeps returning false, and the provider falls back to EnqueueEVP so the evaluation is still counted.
/// It also returns false once CallTarget has disabled the EnqueueEVPWithFeatures integration, so later
/// evaluations are still counted, without their features. Both stubs are on the same type.
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.FeatureFlags.OpenFeature",
    TypeName = "Datadog.FeatureFlags.OpenFeature.FeatureFlagsSdk",
    MethodName = "CanEnqueueEVPWithFeatures",
    ReturnTypeName = ClrNames.Bool,
    ParameterTypeNames = [],
    MinimumVersion = "2.0.0",
    MaximumVersion = "2.*.*",
    IntegrationName = nameof(IntegrationId.OpenFeature))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class OpenFeatureSdkCanEnqueueEVPWithFeaturesIntegration
{
    internal static CallTargetReturn<bool> OnMethodEnd<TTarget>(bool returnValue, Exception? exception, in CallTargetState state)
        => new(exception is null && IntegrationOptions<OpenFeatureSdkEnqueueEVPWithFeaturesIntegration, TTarget>.IsIntegrationEnabled);
}
