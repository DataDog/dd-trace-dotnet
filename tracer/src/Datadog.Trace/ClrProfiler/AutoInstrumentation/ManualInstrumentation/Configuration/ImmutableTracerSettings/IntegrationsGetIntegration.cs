﻿// <copyright file="IntegrationsGetIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Configuration.ImmutableTracerSettings;

/// <summary>
/// Datadog.Trace.Configuration.ImmutableIntegrationSettingsCollection Datadog.Trace.Configuration.ImmutableTracerSettings::get_Integrations() calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.Manual",
    TypeName = "Datadog.Trace.Configuration.ImmutableTracerSettings",
    MethodName = "get_Integrations",
    ReturnTypeName = "Datadog.Trace.Configuration.ImmutableIntegrationSettingsCollection",
    ParameterTypeNames = [],
    MinimumVersion = ManualInstrumentationConstants.MinVersion,
    MaximumVersion = ManualInstrumentationConstants.MaxVersion,
    IntegrationName = ManualInstrumentationConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class IntegrationsGetIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
    {
        TelemetryFactory.Metrics.Record(PublicApiUsage.ImmutableTracerSettings_Integrations_Get);
        return CallTargetState.GetDefault();
    }
}
