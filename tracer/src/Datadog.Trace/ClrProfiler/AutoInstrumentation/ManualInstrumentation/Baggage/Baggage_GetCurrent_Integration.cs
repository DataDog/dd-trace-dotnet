// <copyright file="Baggage_GetCurrent_Integration.cs" company="Datadog">
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
/// System.Collections.Generic.IDictionary`2[System.String,System.String] Datadog.Trace.Baggage::get_Current() calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.Manual",
    TypeName = "Datadog.Trace.Baggage",
    MethodName = "get_Current",
    ReturnTypeName = "System.Collections.Generic.IDictionary`2[System.String,System.String]",
    ParameterTypeNames = [],
    MinimumVersion = ManualInstrumentationConstants.MinVersion,
    MaximumVersion = ManualInstrumentationConstants.MaxVersion,
    IntegrationName = ManualInstrumentationConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class Baggage_GetCurrent_Integration
{
    internal static CallTargetReturn<IDictionary<string, string?>?> OnMethodEnd<TTarget>(
        IDictionary<string, string>? returnValue,
        Exception? exception,
        in CallTargetState state)
    {
        return new CallTargetReturn<IDictionary<string, string?>?>(Trace.Baggage.Current);
    }
}
