// <copyright file="UnitTestRunnerRunSingleTestAsyncIntegration3_8.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Datadog.Trace.Ci;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2;

/// <summary>
/// Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.UnitTestRunner.RunSingleTest calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyNames = ["Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter", "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices"],
    TypeName = "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.UnitTestRunner",
    MethodName = "RunSingleTestAsync",
    ReturnTypeName = "System.Threading.Tasks.Task`1[Microsoft.VisualStudio.TestTools.UnitTesting.TestResult[]]",
    ParameterTypeNames = ["Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.TestMethod", "System.Collections.Generic.IDictionary`2[System.String,System.Object]", "Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging.IMessageLogger"],
    MinimumVersion = "14.0.0",
    MaximumVersion = "14.*.*",
    IntegrationName = MsTestIntegration.IntegrationName)]
[InstrumentMethod(
    AssemblyNames = ["MSTestAdapter.PlatformServices"],
    TypeName = "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.UnitTestRunner",
    MethodName = "RunSingleTestAsync",
    ReturnTypeName = "System.Threading.Tasks.Task`1[Microsoft.VisualStudio.TestTools.UnitTesting.TestResult[]]",
    ParameterTypeNames = ["Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.TestMethod", "System.Collections.Generic.IDictionary`2[System.String,System.Object]", "Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging.IMessageLogger"],
    MinimumVersion = "4.0.0",
    MaximumVersion = "4.*.*",
    IntegrationName = MsTestIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
// ReSharper disable once InconsistentNaming
public static class UnitTestRunnerRunSingleTestAsyncIntegration3_8
{
    internal static CallTargetState OnMethodBegin<TTarget, TTestMethod, TMessageLogger>(TTarget instance, TTestMethod testMethod, IDictionary<string, object?> testContextProperties, TMessageLogger messageLogger)
    {
        return new CallTargetState(null, new StrongBox<MethodInfoCacheItem?>(MsTestIntegration.IsTestMethodRunnableThreadLocal.Value));
    }

    internal static CallTargetReturn<TReturn?> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception? exception, in CallTargetState state)
    {
        if (state.State is StrongBox<MethodInfoCacheItem?> box)
        {
            box.Value = MsTestIntegration.IsTestMethodRunnableThreadLocal.Value;
        }

        MsTestIntegration.IsTestMethodRunnableThreadLocal.Value = null;
        return new CallTargetReturn<TReturn?>(returnValue);
    }

    internal static TReturn? OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception? exception, in CallTargetState state)
    {
        if (instance is null || !MsTestIntegration.IsEnabled)
        {
            return returnValue;
        }

        MethodInfoCacheItem? methodInfoCacheItem;
        if (state.State is StrongBox<MethodInfoCacheItem?> { Value: { } value })
        {
            methodInfoCacheItem = value;
        }
        else
        {
            methodInfoCacheItem = MsTestIntegration.IsTestMethodRunnableThreadLocal.Value;
        }

        MsTestIntegration.IsTestMethodRunnableThreadLocal.Value = null;
        if (methodInfoCacheItem is not null && returnValue is IList { Count: > 0 } lstResults)
        {
            foreach (var unitTestResultObject in lstResults)
            {
                if (unitTestResultObject != null &&
                    unitTestResultObject.TryDuckCast<TestResultStruct3_8>(out var unitTestResult) &&
                    methodInfoCacheItem.TestMethodInfo.TryDuckCast<ITestMethod>(out var testMethod))
                {
                    Common.Log.Debug("[UnitTestRunnerRunSingleTestAsyncIntegration3_8] UnitTestRunner.RunSingleTest() call target interception: {Class}.{Name} | {Outcome}", testMethod.TestClassName, testMethod.TestMethodName, unitTestResult.Outcome);

                    if (unitTestResult.Outcome is UnitTestOutcome.Inconclusive or UnitTestOutcome.NotRunnable or UnitTestOutcome.Ignored)
                    {
                        var skipHandled =
                            MsTestIntegration.ShouldSkip(testMethod, out _, out _) ||
                            MsTestIntegration.GetTestProperties(testMethod) is { Quarantined: true } or { Disabled: true };
                        if (!skipHandled)
                        {
                            // This instrumentation catches all tests being ignored
                            MsTestIntegration.OnMethodBegin(testMethod, instance.GetType(), isRetry: false)?.Close(TestStatus.Skip, TimeSpan.Zero, unitTestResult.IgnoreReason);
                        }
                    }
                    else if (unitTestResult.Outcome is UnitTestOutcome.Error or UnitTestOutcome.Failed)
                    {
                        if (methodInfoCacheItem.TestMethodInfo.TryDuckCast<ITestMethodInfoWithParent>(out var testMethodInfo))
                        {
                            // We need to check if the test is failing because a Class initialization error
                            if (testMethodInfo.Parent?.Instance.TryDuckCast<ClassInfoInitializationExceptionStruct>(out var classInfoInitializationExceptionStruct) == true)
                            {
                                if (classInfoInitializationExceptionStruct.ClassInitializationException is { } classInitializationException &&
                                    MsTestIntegration.OnMethodBegin(testMethodInfo, instance.GetType(), isRetry: false) is { } test)
                                {
                                    test.SetErrorInfo(classInitializationException);
                                    test.Close(TestStatus.Fail);
                                }
                            }
                            else
                            {
                                Common.Log.Warning("Parent class cannot be duck casted to ClassInfoInitializationExceptionStruct.");
                            }

                            // We need to check if the test is failing because a Class cleanup error
                            if (testMethodInfo.Parent?.Instance.TryDuckCast<ClassInfoCleanupExceptionsStruct>(out var classInfoCleanupExceptionsStruct) == true)
                            {
                                if (classInfoCleanupExceptionsStruct.ClassCleanupException is { } classCleanupException &&
                                    MsTestIntegration.GetOrCreateTestSuiteFromTestClassInfo(testMethodInfo.Parent) is { } suite)
                                {
                                    suite.SetErrorInfo(classCleanupException);
                                }
                            }
                            else
                            {
                                Common.Log.Debug("Parent class cannot be duck casted to ClassInfoCleanupExceptionsStruct.");
                            }

                            // We need to check if the test is failing because a Assembly initialization error
                            if (testMethodInfo.Parent?.Parent?.Instance.TryDuckCast<AssemblyInfoExceptionsStruct>(out var assemblyInfoExceptionsStruct) == true)
                            {
                                if (assemblyInfoExceptionsStruct.AssemblyInitializationException is { } assemblyInitializationException &&
                                    MsTestIntegration.OnMethodBegin(testMethodInfo, instance.GetType(), isRetry: false) is { } test)
                                {
                                    test.SetErrorInfo(assemblyInitializationException);
                                    test.Close(TestStatus.Fail);
                                }
                            }
                            else
                            {
                                Common.Log.Warning("Parent assembly cannot be duck casted to AssemblyInfoExceptionsStruct.");
                            }
                        }
                    }
                }
            }
        }

        return returnValue;
    }
}
