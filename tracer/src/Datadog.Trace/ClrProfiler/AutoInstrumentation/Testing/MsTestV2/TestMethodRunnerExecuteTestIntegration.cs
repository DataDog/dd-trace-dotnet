// <copyright file="TestMethodRunnerExecuteTestIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.ComponentModel;
using Datadog.Trace.Ci.Tags;
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
    ParameterTypeNames = ["Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestMethodInfo"],
    MinimumVersion = "14.0.0",
    MaximumVersion = "14.*.*",
    IntegrationName = MsTestIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class TestMethodRunnerExecuteTestIntegration
{
    private static SkipTestMethodExecutor? _itrSkipTestMethodExecutor;
    private static SkipTestMethodExecutor? _disabledSkipTestMethodExecutor;

    internal static CallTargetState OnMethodBegin<TTarget, TTestMethod>(TTarget instance, TTestMethod testMethod)
        where TTarget : ITestMethodRunner
        where TTestMethod : ITestMethod
    {
        // In order to skip a test we change the Executor to one that returns a valid outcome without calling
        // the MethodInfo of the test
        if (MsTestIntegration.IsEnabled &&
            instance.TestMethodInfo is { TestMethodOptions: { Executor: { } executor } } testMethodInfo)
        {
            SkipTestMethodExecutor? newExecutor = null;

            if (MsTestIntegration.ShouldSkip(testMethod, out _, out _))
            {
                _itrSkipTestMethodExecutor ??= new SkipTestMethodExecutor.SyncImpl(executor.GetType().Assembly, IntelligentTestRunnerTags.SkippedByReason);
                newExecutor = _itrSkipTestMethodExecutor;
            }
            else if (MsTestIntegration.GetTestProperties(testMethod) is { Disabled: true, AttemptToFix: false })
            {
                _disabledSkipTestMethodExecutor ??= new SkipTestMethodExecutor.SyncImpl(executor.GetType().Assembly, "Flaky test is disabled by Datadog.");
                newExecutor = _disabledSkipTestMethodExecutor;
            }

            if (newExecutor is not null)
            {
                testMethodInfo.TestMethodOptions.Executor = DuckType.CreateReverse(executor.GetType(), newExecutor);
            }
        }

        return CallTargetState.GetDefault();
    }
}

/// <summary>
/// Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestMethodRunner.ExecuteTest calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyNames = ["Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter", "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices"],
    TypeName = "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestMethodRunner",
    MethodName = "ExecuteTestAsync",
    ReturnTypeName = "System.Threading.Tasks.Task`1[Microsoft.VisualStudio.TestTools.UnitTesting.TestResult[]]",
    ParameterTypeNames = ["Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestMethodInfo"],
    MinimumVersion = "14.0.0",
    MaximumVersion = "14.*.*",
    IntegrationName = MsTestIntegration.IntegrationName)]
[InstrumentMethod(
    AssemblyNames = ["MSTestAdapter.PlatformServices"],
    TypeName = "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestMethodRunner",
    MethodName = "ExecuteTestAsync",
    ReturnTypeName = "System.Threading.Tasks.Task`1[Microsoft.VisualStudio.TestTools.UnitTesting.TestResult[]]",
    ParameterTypeNames = ["Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestMethodInfo"],
    MinimumVersion = "4.0.0",
    MaximumVersion = "4.*.*",
    IntegrationName = MsTestIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
#pragma warning disable SA1402
public static class TestMethodRunnerExecuteTestIntegrationV3_9
#pragma warning restore SA1402
{
    private static SkipTestMethodExecutor? _itrSkipTestMethodExecutor;
    private static SkipTestMethodExecutor? _disabledSkipTestMethodExecutor;

    internal static CallTargetState OnMethodBegin<TTarget, TTestMethod>(TTarget instance, TTestMethod testMethod)
        where TTarget : ITestMethodRunnerV3_9
        where TTestMethod : ITestMethod
    {
        // In order to skip a test we change the Executor to one that returns a valid outcome without calling
        // the MethodInfo of the test
        if (MsTestIntegration.IsEnabled &&
            instance.TestMethodInfo is { Executor: { } executor } testMethodInfo)
        {
            SkipTestMethodExecutor? newExecutor = null;

            if (MsTestIntegration.ShouldSkip(testMethod, out _, out _))
            {
                _itrSkipTestMethodExecutor ??= new SkipTestMethodExecutor.AsyncImpl(executor.GetType().Assembly, IntelligentTestRunnerTags.SkippedByReason);
                newExecutor = _itrSkipTestMethodExecutor;
            }
            else if (MsTestIntegration.GetTestProperties(testMethod) is { Disabled: true, AttemptToFix: false })
            {
                _disabledSkipTestMethodExecutor ??= new SkipTestMethodExecutor.AsyncImpl(executor.GetType().Assembly, "Flaky test is disabled by Datadog.");
                newExecutor = _disabledSkipTestMethodExecutor;
            }

            if (newExecutor is not null)
            {
                testMethodInfo.Executor = DuckType.CreateReverse(executor.GetType(), newExecutor);
            }
        }

        return CallTargetState.GetDefault();
    }
}
