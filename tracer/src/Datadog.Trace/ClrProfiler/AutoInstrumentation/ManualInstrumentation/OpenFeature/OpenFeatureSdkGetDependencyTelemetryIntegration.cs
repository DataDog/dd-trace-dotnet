// <copyright file="OpenFeatureSdkGetDependencyTelemetryIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.FeatureFlags;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.OpenFeature;

/// <summary>Returns the detached prerequisite telemetry carried by a deferred evaluation.</summary>
[InstrumentMethod(
    AssemblyName = "Datadog.FeatureFlags.OpenFeature",
    TypeName = "Datadog.FeatureFlags.OpenFeature.FeatureFlagsSdk",
    MethodName = "GetDependencyTelemetry",
    ReturnTypeName = "System.Collections.Generic.IReadOnlyList`1[System.Collections.Generic.IDictionary`2[System.String,System.Object]]",
    ParameterTypeNames = ["System.Collections.Generic.IDictionary`2[System.String,System.String]"],
    MinimumVersion = "2.0.0",
    MaximumVersion = "2.*.*",
    IntegrationName = nameof(IntegrationId.OpenFeature))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class OpenFeatureSdkGetDependencyTelemetryIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget>(ref IDictionary<string, string>? evaluationMetadata)
        => new(null, evaluationMetadata);

    internal static CallTargetReturn<IReadOnlyList<IDictionary<string, object?>>?> OnMethodEnd<TTarget>(
        IReadOnlyList<IDictionary<string, object?>>? returnValue,
        Exception? exception,
        in CallTargetState state)
        => new((state.State as DeferredExposureMetadata)?.DependencyTelemetry);
}
