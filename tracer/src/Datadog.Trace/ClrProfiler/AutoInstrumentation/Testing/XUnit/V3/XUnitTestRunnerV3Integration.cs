// <copyright file="XUnitTestRunnerV3Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit.V3;

/// <summary>
/// Xunit.v3.TestRunner`2.Run calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "xunit.v3.core",
    TypeName = "Xunit.v3.TestRunner`2",
    MethodName = "RunTest",
    ParameterTypeNames = ["_"],
    ReturnTypeName = "System.Threading.Tasks.ValueTask`1[System.TimeSpan]",
    MinimumVersion = "1.0.0",
    MaximumVersion = "1.*.*",
    IntegrationName = XUnitIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class XUnitTestRunnerV3Integration
{
    internal static CallTargetState OnMethodBegin<TTarget, TContext>(TTarget instance, TContext context)
    {
        if (!XUnitIntegration.IsEnabled || instance is null)
        {
            return CallTargetState.GetDefault();
        }

        Common.Log.Warning("XUnitTestRunnerV3Integration.OnMethodBegin, instance: {0}, context: {1}", instance, context);
        return CallTargetState.GetDefault();
    }

    internal static CallTargetReturn<TResult> OnMethodEnd<TTarget, TResult>(TTarget instance, TResult returnValue, Exception exception, in CallTargetState state)
    {
        Common.Log.Warning("XUnitTestRunnerV3Integration.OnMethodEnd, instance: {0}, context: {1}", instance, returnValue);
        return new CallTargetReturn<TResult>(returnValue);
    }

    internal static async Task<TReturn> OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
    {
        await Task.Yield();
        Common.Log.Warning("XUnitTestRunnerV3Integration.OnAsyncMethodEnd, instance: {0}, context: {1}", instance, returnValue);
        return returnValue;
    }
}
