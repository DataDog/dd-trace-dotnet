// <copyright file="SetCurrentIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Baggage;

/// <summary>
/// System.Void Datadog.Trace.Baggage::set_Current(System.Collections.Generic.IDictionary`2[System.String,System.String]) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.Manual",
    TypeName = "Datadog.Trace.Baggage",
    MethodName = "set_Current",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = ["System.Collections.Generic.IDictionary`2[System.String,System.String]"],
    MinimumVersion = ManualInstrumentationConstants.MinVersion,
    MaximumVersion = ManualInstrumentationConstants.MaxVersion,
    IntegrationName = ManualInstrumentationConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class SetCurrentIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget>(ref IDictionary<string, string> value)
    {
        if (value == null!)
        {
            Trace.Baggage.Current = new Trace.Baggage();
        }

        Trace.Baggage.Current = new Trace.Baggage(value);
        return CallTargetState.GetDefault();
    }

}