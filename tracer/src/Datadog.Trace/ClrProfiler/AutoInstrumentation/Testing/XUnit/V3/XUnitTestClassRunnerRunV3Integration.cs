// <copyright file="XUnitTestClassRunnerRunV3Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System;
using System.ComponentModel;
using Datadog.Trace.Ci;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit.V3;

/// <summary>
/// Xunit.v3.TestClassRunner`4.Run calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "xunit.v3.core",
    TypeName = "Xunit.v3.TestClassRunner`4",
    MethodName = "Run",
    ParameterTypeNames = ["!0"],
    ReturnTypeName = "System.Threading.Tasks.ValueTask`1[Xunit.v3.RunSummary]",
    MinimumVersion = "1.0.0",
    MaximumVersion = "2.*.*",
    IntegrationName = XUnitIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
internal static class XUnitTestClassRunnerRunV3Integration
{
    internal static CallTargetState OnMethodBegin<TTarget, TContext>(TTarget instance, TContext context)
        where TContext : IXunitTestClassRunnerContextV3
    {
        if (!XUnitIntegration.IsEnabled || instance is null)
        {
            return CallTargetState.GetDefault();
        }

        if (TestModule.Current is { } testModule)
        {
            return new CallTargetState(null, testModule.InternalGetOrCreateSuite(context.TestClass.TestClassName ?? string.Empty));
        }

        Common.Log.Warning("Test module cannot be found.");
        return CallTargetState.GetDefault();
    }

    internal static CallTargetReturn<TResult> OnMethodEnd<TTarget, TResult>(TTarget instance, TResult returnValue, Exception exception, in CallTargetState state)
    {
        if (state.State == TestSuite.Current)
        {
            // Restore the AsyncLocal set
            // This is used to mimic the ExecutionContext copy from the StateMachine
            // CallTarget integrations does this automatically when using a normal `Scope`
            // in this case we have to do it manually.
            TestSuite.Current = null;
        }

        return new CallTargetReturn<TResult>(returnValue);
    }

    internal static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
    {
        if (state.State is TestSuite testSuite)
        {
            testSuite.Close();
        }

        return returnValue;
    }
}
