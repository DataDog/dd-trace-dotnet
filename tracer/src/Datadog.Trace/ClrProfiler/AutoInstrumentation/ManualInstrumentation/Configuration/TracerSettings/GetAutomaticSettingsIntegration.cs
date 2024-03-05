// <copyright file="GetAutomaticSettingsIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Configuration.TracerSettings;

/// <summary>
/// Datadog.Trace.Configuration.ITracerSettings Datadog.Trace.Configuration.TracerSettings::GetAutomaticSettings(System.Boolean) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.Manual",
    TypeName = "Datadog.Trace.Configuration.TracerSettings",
    MethodName = "GetAutomaticSettings",
    ReturnTypeName = "Datadog.Trace.Configuration.ITracerSettings",
    ParameterTypeNames = [ClrNames.Bool],
    MinimumVersion = ManualInstrumentationConstants.MinVersion,
    MaximumVersion = ManualInstrumentationConstants.MaxVersion,
    IntegrationName = ManualInstrumentationConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class GetAutomaticSettingsIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget>(bool useDefaultSources)
    {
        var settings = useDefaultSources
                           ? Trace.Configuration.TracerSettings.FromDefaultSourcesInternal()
                           : new Trace.Configuration.TracerSettings(null, new ConfigurationTelemetry());
        return new CallTargetState(scope: null, state: settings);
    }

    internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TReturn returnValue, Exception exception, in CallTargetState state)
    {
        // replace the null settings object with the duck typed TracerSettings
        return new CallTargetReturn<TReturn>(state.State.DuckCast<TReturn>());
    }
}
