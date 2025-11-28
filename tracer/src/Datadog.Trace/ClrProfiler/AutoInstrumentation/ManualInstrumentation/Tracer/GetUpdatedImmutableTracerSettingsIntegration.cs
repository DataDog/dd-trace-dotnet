// <copyright file="GetUpdatedImmutableTracerSettingsIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Tracer;

/// <summary>
/// Datadog.Trace.Tracer::GetUpdatedImmutableTracerSettings() calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.Manual",
    TypeName = "Datadog.Trace.Tracer",
    MethodName = "GetUpdatedImmutableTracerSettings",
    ReturnTypeName = "System.Collections.Generic.IDictionary`2[System.String,System.Object]",
    ParameterTypeNames = [ClrNames.Object, "System.Object&"],
    MinimumVersion = "3.7.0", // added in 3.7.0
    MaximumVersion = ManualInstrumentationConstants.MaxVersion,
    IntegrationName = ManualInstrumentationConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class GetUpdatedImmutableTracerSettingsIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, ref object? automaticTracer, ref object? automaticSettings)
    {
        // In previous versions of Datadog.Trace (<3.30.0), we would replace the entire TracerSettings
        // object whenever the settings changed, and use that to track whether we need to update the manual
        // side. Now, TracerSettings is immutable for the lifetime of the application, and we instead
        // update the MutableSettings and ExporterSettings whenever something changes. To keep roughly the same
        // compatible behaviour here, we now store the current MutableSettings in `automaticSettings` to track
        // whether things need to update. Note that this isn't _strictly_ correct, because if the customer updates
        // only the exporter settings, we won't track that it's changed here. However, in PopulateSettings we _also_
        // don't populate the latest exporter settings there, so that's ok! Setting the exporter settings in code is
        // deprecated (as it's problematic for a bunch of reasons), but it's still possible, so this is a half-way
        // house way to handle it.
        if (automaticTracer is Datadog.Trace.Tracer tracer
         && (automaticSettings is null || !ReferenceEquals(tracer.CurrentTraceSettings.Settings, automaticSettings)))
        {
            automaticSettings = tracer.CurrentTraceSettings.Settings;
            var dict = new Dictionary<string, object?>();
            CtorIntegration.PopulateSettings(dict, tracer);
            return new CallTargetState(scope: null, state: dict);
        }

        return CallTargetState.GetDefault();
    }

    internal static CallTargetReturn<IDictionary<string, object?>?> OnMethodEnd<TTarget>(TTarget instance, IDictionary<string, object?>? returnValue, Exception? exception, in CallTargetState state)
    {
        return new CallTargetReturn<IDictionary<string, object?>?>(state.State as Dictionary<string, object?>);
    }
}
