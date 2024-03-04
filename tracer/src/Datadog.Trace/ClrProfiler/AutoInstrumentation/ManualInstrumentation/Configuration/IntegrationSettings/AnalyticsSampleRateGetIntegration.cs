// <copyright file="AnalyticsSampleRateGetIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Configuration.IntegrationSettings;

/// <summary>
/// System.Double Datadog.Trace.Configuration.IntegrationSettings::get_AnalyticsSampleRate() calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.Manual",
    TypeName = "Datadog.Trace.Configuration.IntegrationSettings",
    MethodName = "get_AnalyticsSampleRate",
    ReturnTypeName = "System.Double",
    ParameterTypeNames = [],
    MinimumVersion = ManualInstrumentationConstants.MinVersion,
    MaximumVersion = ManualInstrumentationConstants.MaxVersion,
    IntegrationName = ManualInstrumentationConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class AnalyticsSampleRateGetIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
    {
        TelemetryFactory.Metrics.Record(PublicApiUsage.IntegrationSettings_AnalyticsSampleRate_Get);
        return CallTargetState.GetDefault();
    }
}
