// <copyright file="OpenFeatureSdkIsReadyIntegration.cs" company="Datadog">
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
/// System.Boolean Datadog.Trace.FeatureFlags.FeatureFlagsSdk::IsReady() calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.FeatureFlags.OpenFeature",
    TypeName = "Datadog.FeatureFlags.OpenFeature.FeatureFlagsSdk",
    MethodName = "IsReady",
    ReturnTypeName = ClrNames.Bool,
    ParameterTypeNames = [],
    MinimumVersion = "2.0.0",
    MaximumVersion = "2.*.*",
    IntegrationName = nameof(IntegrationId.OpenFeature))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class OpenFeatureSdkIsReadyIntegration
{
    internal static CallTargetReturn<bool> OnMethodEnd<TTarget>(bool returnValue, Exception? exception, in CallTargetState state)
    {
        var featureFlags = TracerManager.Instance.FeatureFlags;
        return new CallTargetReturn<bool>(featureFlags is null || featureFlags.IsReady());
    }
}
