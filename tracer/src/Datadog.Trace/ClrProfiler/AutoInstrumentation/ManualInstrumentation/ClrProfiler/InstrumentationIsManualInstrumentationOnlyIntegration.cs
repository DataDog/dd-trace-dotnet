// <copyright file="InstrumentationIsManualInstrumentationOnlyIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.ClrProfiler;

/// <summary>
/// System.Boolean Datadog.Trace.ClrProfiler.Instrumentation::IsManualInstrumentationOnly() calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.Manual",
    TypeName = "Datadog.Trace.ClrProfiler.Instrumentation",
    MethodName = "IsManualInstrumentationOnly",
    ReturnTypeName = ClrNames.Bool,
    ParameterTypeNames = [],
    MinimumVersion = ManualInstrumentationConstants.MinVersion,
    MaximumVersion = ManualInstrumentationConstants.MaxVersion,
    IntegrationName = ManualInstrumentationConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class InstrumentationIsManualInstrumentationOnlyIntegration
{
    internal static CallTargetReturn<bool> OnMethodEnd<TTarget>(bool returnValue, Exception? exception, in CallTargetState state)
    {
        return new CallTargetReturn<bool>(true);
    }
}
