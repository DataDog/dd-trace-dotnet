// <copyright file="OpenFeatureSdkSetFlagSelectionIntegration.cs" company="Datadog">
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
/// Hands the flag selection a provider was constructed with to the tracer, which claims it for the
/// process. With an older tracer the stub keeps returning false and the full configuration loads.
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.FeatureFlags.OpenFeature",
    TypeName = "Datadog.FeatureFlags.OpenFeature.FeatureFlagsSdk",
    MethodName = "SetFlagSelection",
    ReturnTypeName = ClrNames.Bool,
    ParameterTypeNames = ["System.String[]", "System.String[]"],
    MinimumVersion = "2.0.0",
    MaximumVersion = "2.*.*",
    IntegrationName = nameof(IntegrationId.OpenFeature))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class OpenFeatureSdkSetFlagSelectionIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget>(ref string?[]? tags, ref string?[]? keyPatterns)
    {
        TracerManager.Instance.FeatureFlags?.SetFlagSelection(tags, keyPatterns);
        return CallTargetState.GetDefault();
    }

    internal static CallTargetReturn<bool> OnMethodEnd<TTarget>(bool returnValue, Exception? exception, in CallTargetState state)
        => new(exception is null);
}
