// <copyright file="DiagnosticSourceEnabledSetIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Configuration.TracerSettings;

/// <summary>
/// System.Void Datadog.Trace.Configuration.TracerSettings::set_DiagnosticSourceEnabled(System.Boolean) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.Manual",
    TypeName = "Datadog.Trace.Configuration.TracerSettings",
    MethodName = "set_DiagnosticSourceEnabled",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = new[] { ClrNames.Bool },
    MinimumVersion = ManualInstrumentationConstants.MinVersion,
    MaximumVersion = ManualInstrumentationConstants.MaxVersion,
    IntegrationName = ManualInstrumentationConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class DiagnosticSourceEnabledSetIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, ref bool value)
    {
        TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_DiagnosticSourceEnabled_Set);
        return CallTargetState.GetDefault();
    }
}
