// <copyright file="FeatureFlagsSdkRegisterOnNewConfigEventHandlerIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.FeatureFlags;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Datadog_Trace_Manual;

/// <summary>
/// System.Void Datadog.Trace.FeatureFlags.FeatureFlagsSdk::RegisterOnNewConfigEventHandler(System.Action) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.Manual",
    TypeName = "Datadog.Trace.FeatureFlags.FeatureFlagsSdk",
    MethodName = "RegisterOnNewConfigEventHandler",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = ["System.Action"],
    MinimumVersion = "3.31.0",
    MaximumVersion = "3.*.*",
    IntegrationName = nameof(IntegrationId.DatadogTraceManual))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class FeatureFlagsSdkRegisterOnNewConfigEventHandlerIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget>(ref Action? onNewConfig)
    {
        TracerManager.Instance.FeatureFlags?.RegisterOnNewConfigEventHandler(onNewConfig);
        return CallTargetState.GetDefault();
    }
}
