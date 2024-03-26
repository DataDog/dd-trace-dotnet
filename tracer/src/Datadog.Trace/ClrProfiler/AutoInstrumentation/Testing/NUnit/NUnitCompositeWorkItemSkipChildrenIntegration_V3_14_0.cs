// <copyright file="NUnitCompositeWorkItemSkipChildrenIntegration_V3_14_0.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit;

/// <summary>
/// NUnit.Framework.Internal.Execution.CompositeWorkItem.SkipChildren() calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "nunit.framework",
    TypeName = "NUnit.Framework.Internal.Execution.CompositeWorkItem",
    MethodName = "SkipChildren",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = new[] { ClrNames.Ignore, "NUnit.Framework.Interfaces.ResultState", ClrNames.String, ClrNames.String },
    MinimumVersion = "3.14.0",
    MaximumVersion = "4.*.*",
    IntegrationName = NUnitIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class NUnitCompositeWorkItemSkipChildrenIntegration_V3_14_0
{
    internal static CallTargetState OnMethodBegin<TTarget, TSuite, TResultState>(TTarget instance, TSuite testSuite, TResultState resultState, string message, string? stackTrace)
        => NUnitCompositeWorkItemSkipChildrenIntegration.OnMethodBegin(instance, testSuite, resultState, message);

    internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception exception, in CallTargetState state)
        => NUnitCompositeWorkItemSkipChildrenIntegration.OnMethodEnd(instance, exception, in state);
}
