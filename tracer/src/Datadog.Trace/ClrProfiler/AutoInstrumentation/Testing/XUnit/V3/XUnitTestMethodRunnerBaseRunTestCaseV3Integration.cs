// <copyright file="XUnitTestMethodRunnerBaseRunTestCaseV3Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Ci;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit.V3;

/// <summary>
/// Xunit.v3.TestCaseRunner`3.RunTest calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "xunit.v3.core",
    TypeName = "Xunit.v3.XunitTestMethodRunnerBase`3",
    MethodName = "RunTestCase",
    ParameterTypeNames = ["_", "_"],
    ReturnTypeName = "System.Threading.Tasks.ValueTask`1[Xunit.v3.RunSummary]",
    MinimumVersion = "1.0.0",
    MaximumVersion = "1.*.*",
    IntegrationName = XUnitIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class XUnitTestMethodRunnerBaseRunTestCaseV3Integration
{
    private static int _totalRetries = -1;

    internal static CallTargetState OnMethodBegin<TTarget, TContext, TTestCase>(TTarget instance, TContext context, TTestCase testcase)
        where TContext : IXunitTestMethodRunnerBaseContextV3
        where TTestCase : IXunitTestCaseV3
    {
        Common.Log.Warning("XUnitTestMethodRunnerBaseRunTestCaseV3Integration.OnMethodBegin, instance: {0}, context: {1}, testcase: {2}", instance, context, testcase);
        if (!XUnitIntegration.IsEnabled || instance is null)
        {
            return CallTargetState.GetDefault();
        }

        var instanceRunner = instance.DuckCast<IXunitTestMethodRunnerV3>();
        _ = instanceRunner;

        Interlocked.CompareExchange(ref _totalRetries, CIVisibility.Settings.TotalFlakyRetryCount, -1);

        return CallTargetState.GetDefault();
    }

    internal static CallTargetReturn<TResult> OnMethodEnd<TTarget, TResult>(TTarget instance, TResult returnValue, Exception exception, in CallTargetState state)
    {
        Common.Log.Warning("XUnitTestMethodRunnerBaseRunTestCaseV3Integration.OnMethodEnd, instance: {0}, context: {1}", instance, returnValue);
        return new CallTargetReturn<TResult>(returnValue);
    }

    internal static async Task<TReturn> OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
    {
        Common.Log.Warning("XUnitTestMethodRunnerBaseRunTestCaseV3Integration.OnAsyncMethodEnd, instance: {0}, context: {1}", instance, returnValue);
        await Task.Yield();
        return returnValue;
    }

#pragma warning disable SA1201
    internal interface IXunitTestMethodRunnerV3
    {
        IValueTaskOfTResultDuckType RunTestCase(object context, object testCase);
    }

    internal interface IValueTaskOfTResultDuckType : ITaskOfResultDuckType
    {
        Task AsTask();
    }

    internal interface ITaskOfResultDuckType
    {
        bool IsCompletedSuccessfully { get; }

        object? Result { get; }

        TaskAwaiter GetAwaiter();
    }
}
