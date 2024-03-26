// <copyright file="TestMethodRunnerExecuteTestIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2;

/// <summary>
/// Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestMethodRunner.ExecuteTest calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter",
    TypeName = "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestMethodRunner",
    MethodName = "ExecuteTest",
    ReturnTypeName = "Microsoft.VisualStudio.TestTools.UnitTesting.TestResult[]",
    ParameterTypeNames = new[] { "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestMethodInfo" },
    MinimumVersion = "14.0.0",
    MaximumVersion = "14.*.*",
    IntegrationName = MsTestIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class TestMethodRunnerExecuteTestIntegration
{
    private static ItrSkipTestMethodExecutor _skipTestMethodExecutor;

    /// <summary>
    /// OnMethodBegin callback
    /// </summary>
    /// <typeparam name="TTarget">Type of the target</typeparam>
    /// <typeparam name="TTestMethod">Type of the ITestMethod</typeparam>
    /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
    /// <param name="testMethod">Test method instance</param>
    /// <returns>Calltarget state value</returns>
    internal static CallTargetState OnMethodBegin<TTarget, TTestMethod>(TTarget instance, TTestMethod testMethod)
        where TTarget : ITestMethodRunner
        where TTestMethod : ITestMethod
    {
        // Check if the test should be skipped by ITR
        if (MsTestIntegration.IsEnabled && MsTestIntegration.ShouldSkip(testMethod, out _, out _))
        {
            // In order to skip a test we change the Executor to one that returns a valid outcome without calling
            // the MethodInfo of the test
            var executor = instance.TestMethodInfo.TestMethodOptions.Executor;
            _skipTestMethodExecutor ??= new ItrSkipTestMethodExecutor(executor.GetType().Assembly);
            instance.TestMethodInfo.TestMethodOptions.Executor = DuckType.CreateReverse(executor.GetType(), _skipTestMethodExecutor);
        }

        return CallTargetState.GetDefault();
    }
}
