// <copyright file="TracerMetricsEnabledGetIntegration.cs" company="Datadog">
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
/// System.Boolean Datadog.Trace.Configuration.TracerSettings::get_TracerMetricsEnabled() calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.Manual",
    TypeName = "Datadog.Trace.Configuration.TracerSettings",
    MethodName = "get_TracerMetricsEnabled",
    ReturnTypeName = ClrNames.Bool,
    ParameterTypeNames = [],
    MinimumVersion = ManualInstrumentationConstants.MinVersion,
    MaximumVersion = ManualInstrumentationConstants.MaxVersion,
    IntegrationName = ManualInstrumentationConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class TracerMetricsEnabledGetIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
    {
        TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_TracerMetricsEnabled_Get);
        return CallTargetState.GetDefault();
    }
}
