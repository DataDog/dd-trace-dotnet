// <copyright file="OpenFeatureSdkIsEvaluationEventsEnabledIntegration.cs" company="Datadog">
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
/// Reads the flagevaluation startup setting without depending on lazy writer activation.
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.FeatureFlags.OpenFeature",
    TypeName = "Datadog.FeatureFlags.OpenFeature.FeatureFlagsSdk",
    MethodName = "IsEvaluationEventsEnabled",
    ReturnTypeName = ClrNames.Bool,
    ParameterTypeNames = [],
    MinimumVersion = "2.0.0",
    MaximumVersion = "2.*.*",
    IntegrationName = nameof(IntegrationId.OpenFeature))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class OpenFeatureSdkIsEvaluationEventsEnabledIntegration
{
    internal static CallTargetReturn<bool> OnMethodEnd<TTarget>(bool returnValue, Exception? exception, in CallTargetState state)
        => new(exception is null && TracerManager.Instance.FeatureFlags?.EvaluationEventsEnabled == true);
}
