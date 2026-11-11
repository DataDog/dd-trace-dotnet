// <copyright file="OpenFeatureSdkRecordEVPHookErrorIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.FeatureFlags.FlagEvaluation;
using Datadog.Trace.Telemetry;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.OpenFeature;

/// <summary>
/// Records unexpected provider hook failures without including customer input or affecting evaluations.
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.FeatureFlags.OpenFeature",
    TypeName = "Datadog.FeatureFlags.OpenFeature.FeatureFlagsSdk",
    MethodName = "RecordEVPHookError",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = [],
    MinimumVersion = "2.0.0",
    MaximumVersion = "2.*.*",
    IntegrationName = nameof(IntegrationId.OpenFeature))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class OpenFeatureSdkRecordEVPHookErrorIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget>()
    {
        RecordError();
        return CallTargetState.GetDefault();
    }

    internal static void RecordError(IMetricsTelemetryCollector? metrics = null)
        => new FlagEvaluationTelemetry(metrics ?? TelemetryFactory.Metrics).HookError();
}
