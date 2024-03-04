// <copyright file="GlobalSettingsSetDebugEnabledIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Configuration;

/// <summary>
/// System.Void Datadog.Trace.Configuration.GlobalSettings::SetDebugEnabled(System.Boolean) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.Manual",
    TypeName = "Datadog.Trace.Configuration.GlobalSettings",
    MethodName = "SetDebugEnabled",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = new[] { ClrNames.Bool },
    MinimumVersion = ManualInstrumentationConstants.MinVersion,
    MaximumVersion = ManualInstrumentationConstants.MaxVersion,
    IntegrationName = ManualInstrumentationConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class GlobalSettingsSetDebugEnabledIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget>(bool enabled)
    {
        TelemetryFactory.Metrics.Record(PublicApiUsage.GlobalSettings_SetDebugEnabled);
        GlobalSettings.SetDebugEnabledInternal(enabled);
        return CallTargetState.GetDefault();
    }
}
