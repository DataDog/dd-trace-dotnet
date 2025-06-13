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
internal class GetUpdatedImmutableTracerSettingsIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, ref object? automaticTracer, ref object? automaticSettings)
    {
        if (automaticTracer is Datadog.Trace.Tracer tracer
         && (automaticSettings is null || !ReferenceEquals(tracer.Settings, automaticSettings)))
        {
            automaticSettings = tracer.Settings;
            var dict = new Dictionary<string, object?>();
            CtorIntegration.PopulateSettings(dict, tracer.Settings);
            return new CallTargetState(scope: null, state: dict);
        }

        return CallTargetState.GetDefault();
    }

    internal static CallTargetReturn<IDictionary<string, object?>?> OnMethodEnd<TTarget>(TTarget instance, IDictionary<string, object?>? returnValue, Exception? exception, in CallTargetState state)
    {
        return new CallTargetReturn<IDictionary<string, object?>?>(state.State as Dictionary<string, object?>);
    }
}
