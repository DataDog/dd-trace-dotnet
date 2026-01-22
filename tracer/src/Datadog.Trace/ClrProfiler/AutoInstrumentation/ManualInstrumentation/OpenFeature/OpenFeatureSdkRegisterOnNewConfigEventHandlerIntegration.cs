// <copyright file="OpenFeatureSdkRegisterOnNewConfigEventHandlerIntegration.cs" company="Datadog">
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
/// System.Void Datadog.Trace.FeatureFlags.FeatureFlagsSdk::RegisterOnNewConfigEventHandler(System.Action) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.FeatureFlags.OpenFeature",
    TypeName = "Datadog.FeatureFlags.OpenFeature.FeatureFlagsSdk",
    MethodName = "RegisterOnNewConfigEventHandler",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = ["System.Action"],
    MinimumVersion = "2.0.0",
    MaximumVersion = "2.*.*",
    IntegrationName = nameof(IntegrationId.OpenFeature))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class OpenFeatureSdkRegisterOnNewConfigEventHandlerIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget>(ref Action? onNewConfig)
    {
        TracerManager.Instance.FeatureFlags?.RegisterOnNewConfigEventHandler(onNewConfig);
        return CallTargetState.GetDefault();
    }
}
