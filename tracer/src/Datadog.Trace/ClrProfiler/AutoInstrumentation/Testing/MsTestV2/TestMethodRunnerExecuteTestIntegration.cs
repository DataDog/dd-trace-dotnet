// <copyright file="TestMethodRunnerExecuteTestIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.Ci;
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
            SkippableTest? skippableTest = null;
            var testManagementProperties = MsTestIntegration.GetTestProperties(testMethod);

            if (Common.IsDisabledByTestManagement(testManagementProperties))
            {
                _disabledSkipTestMethodExecutor ??= new SkipTestMethodExecutor.SyncImpl(executor.GetType().Assembly, "Flaky test is disabled by Datadog.");
                newExecutor = _disabledSkipTestMethodExecutor;
            }
            else if (Common.CanApplyItrSkip(testManagementProperties) &&
                     MsTestIntegration.ShouldSkip(testMethod, out _, out _, out skippableTest))
            {
                newExecutor = new SkipTestMethodExecutor.SyncImpl(
                    executor.GetType().Assembly,
                    IntelligentTestRunnerTags.SkippedByReason,
                    recordCoverageBackfillSkip: true,
                    skippableTest: skippableTest);
            }

            if (newExecutor is not null)
            {
                var replacementExecutor = DuckType.CreateReverse(executor.GetType(), newExecutor);
                testMethodInfo.TestMethodOptions.Executor = replacementExecutor;
                return TestMethodExecutorRestore.Create(testMethodInfo.TestMethodOptions, executor, replacementExecutor);
            }
        }

        return CallTargetState.GetDefault();
    }

    internal static CallTargetReturn<TReturn?> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception? exception, in CallTargetState state)
    {
        TestMethodExecutorRestore.Restore(state);
        return new CallTargetReturn<TReturn?>(returnValue);
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
    MaximumVersion = "4.2.*",
    IntegrationName = MsTestIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
#pragma warning disable SA1402
public static class TestMethodRunnerExecuteTestIntegrationV3_9
#pragma warning restore SA1402
{
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
            SkippableTest? skippableTest = null;
            var testManagementProperties = MsTestIntegration.GetTestProperties(testMethod);

            if (Common.IsDisabledByTestManagement(testManagementProperties))
            {
                _disabledSkipTestMethodExecutor ??= new SkipTestMethodExecutor.AsyncImpl(executor.GetType().Assembly, "Flaky test is disabled by Datadog.");
                newExecutor = _disabledSkipTestMethodExecutor;
            }
            else if (Common.CanApplyItrSkip(testManagementProperties) &&
                     MsTestIntegration.ShouldSkip(testMethod, out _, out _, out skippableTest))
            {
                newExecutor = new SkipTestMethodExecutor.AsyncImpl(
                    executor.GetType().Assembly,
                    IntelligentTestRunnerTags.SkippedByReason,
                    recordCoverageBackfillSkip: true,
                    skippableTest: skippableTest);
            }

            if (newExecutor is not null)
            {
                var replacementExecutor = DuckType.CreateReverse(executor.GetType(), newExecutor);
                testMethodInfo.Executor = replacementExecutor;
                return TestMethodExecutorRestore.Create(testMethodInfo, executor, replacementExecutor);
            }
        }

        return CallTargetState.GetDefault();
    }

    internal static CallTargetReturn<TReturn?> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception? exception, in CallTargetState state)
    {
        if (exception is not null)
        {
            TestMethodExecutorRestore.Restore(state);
        }

        return new CallTargetReturn<TReturn?>(returnValue);
    }

    internal static TReturn? OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception? exception, in CallTargetState state)
    {
        TestMethodExecutorRestore.Restore(state);
        return returnValue;
    }
}

/// <summary>
/// Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestMethodRunner.ExecuteTestAsync calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyNames = ["MSTestAdapter.PlatformServices"],
    TypeName = "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestMethodRunner",
    MethodName = "ExecuteTestAsync",
    ReturnTypeName = "System.Threading.Tasks.Task`1[Microsoft.VisualStudio.TestTools.UnitTesting.TestResult[]]",
    ParameterTypeNames = ["Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ITestContext", "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestMethodInfo"],
    MinimumVersion = "4.3.0",
    MaximumVersion = "4.*.*",
    IntegrationName = MsTestIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
#pragma warning disable SA1402
public static class TestMethodRunnerExecuteTestIntegrationV4_3
#pragma warning restore SA1402
{
    internal static CallTargetState OnMethodBegin<TTarget, TTestContext, TTestMethod>(TTarget instance, TTestContext testContext, TTestMethod testMethod)
        where TTarget : ITestMethodRunnerV3_9
        where TTestMethod : ITestMethod
        => TestMethodRunnerExecuteTestIntegrationV3_9.OnMethodBegin(instance, testMethod);

    internal static CallTargetReturn<TReturn?> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception? exception, in CallTargetState state)
        => TestMethodRunnerExecuteTestIntegrationV3_9.OnMethodEnd(instance, returnValue, exception, in state);

    internal static TReturn? OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception? exception, in CallTargetState state)
        => TestMethodRunnerExecuteTestIntegrationV3_9.OnAsyncMethodEnd(instance, returnValue, exception, in state);
}
