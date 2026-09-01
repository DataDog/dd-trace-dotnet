// <copyright file="XUnitTestAssemblyRunnerRunV4Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit.V3;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit.V4;

/// <summary>
/// Instruments xUnit 4 test assembly execution.
/// </summary>
[InstrumentMethod(
    AssemblyName = "xunit.v3.core",
    TypeName = "Xunit.v3.TestAssemblyRunner`4",
    MethodName = "Run",
    ParameterTypeNames = ["!0"],
    ReturnTypeName = "System.Threading.Tasks.ValueTask`1[Xunit.v3.RunSummary]",
    MinimumVersion = "4.0.0",
    MaximumVersion = "4.*.*",
    IntegrationName = XUnitIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class XUnitTestAssemblyRunnerRunV4Integration
{
    internal static CallTargetState OnMethodBegin<TTarget, TContext>(TTarget instance, TContext context)
        where TContext : ITestAssemblyRunnerContextV3, IDuckType
        => XUnitTestAssemblyRunnerRunV3Integration.OnMethodBegin(instance, context);

    internal static CallTargetReturn<TResult> OnMethodEnd<TTarget, TResult>(TTarget instance, TResult returnValue, Exception exception, in CallTargetState state)
        => XUnitTestAssemblyRunnerRunV3Integration.OnMethodEnd(instance, returnValue, exception, in state);

    internal static Task<TReturn> OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
        => XUnitTestAssemblyRunnerRunV3Integration.OnAsyncMethodEnd(instance, returnValue, exception, state);
}
