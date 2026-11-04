// <copyright file="OpenFeatureSdkCommitExposureIntegration.cs" company="Datadog">
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

/// <summary>
/// Records and sends the exposure that EvaluateDeferred left pending, and returns the verdict.
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.FeatureFlags.OpenFeature",
    TypeName = "Datadog.FeatureFlags.OpenFeature.FeatureFlagsSdk",
    MethodName = "CommitExposure",
    ReturnTypeName = ClrNames.Int32,
    ParameterTypeNames = ["System.Collections.Generic.IDictionary`2[System.String,System.String]"],
    MinimumVersion = "2.0.0",
    MaximumVersion = "2.*.*",
    IntegrationName = nameof(IntegrationId.OpenFeature))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class OpenFeatureSdkCommitExposureIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget>(ref IDictionary<string, string>? evaluationMetadata)
        => new(scope: null, evaluationMetadata);

    internal static CallTargetReturn<int> OnMethodEnd<TTarget>(int returnValue, Exception? exception, in CallTargetState state)
        => new(TracerManager.Instance.FeatureFlags?.CommitExposure(state.State as IDictionary<string, string>) ?? ExposureCommitResult.NothingPending);
}
