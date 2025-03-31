// <copyright file="UnitTestRunnerRunSingleTestIntegration3_8.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System;
using System.Collections;
using System.ComponentModel;
using Datadog.Trace.Ci;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2;

/// <summary>
/// Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.UnitTestRunner.RunSingleTest calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter",
    TypeName = "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.UnitTestRunner",
    MethodName = "RunSingleTest",
    ReturnTypeName = "Microsoft.VisualStudio.TestTools.UnitTesting.TestResult[]",
    ParameterTypeNames = ["Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.TestMethod", "System.Collections.Generic.IDictionary`2[System.String,System.Object]", "Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging.IMessageLogger"],
    MinimumVersion = "14.0.0",
    MaximumVersion = "14.*.*",
    IntegrationName = MsTestIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
// ReSharper disable once InconsistentNaming
public static class UnitTestRunnerRunSingleTestIntegration3_8
{
    internal static CallTargetReturn<TReturn?> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception? exception, in CallTargetState state)
    {
        if (instance is null || !MsTestIntegration.IsEnabled)
        {
            return new CallTargetReturn<TReturn?>(returnValue);
        }

        var methodInfoCacheItem = MsTestIntegration.IsTestMethodRunnableThreadLocal.Value;
        MsTestIntegration.IsTestMethodRunnableThreadLocal.Value = null;
        if (methodInfoCacheItem is not null && returnValue is IList { Count: > 0 } lstResults)
        {
            foreach (var unitTestResultObject in lstResults)
            {
                if (unitTestResultObject != null &&
                    unitTestResultObject.TryDuckCast<TestResultStruct3_8>(out var unitTestResult) &&
                    methodInfoCacheItem.TestMethodInfo.TryDuckCast<ITestMethod>(out var testMethod))
                {
                    Common.Log.Debug("UnitTestRunner.RunSingleTest() call target interception: {Class}.{Name}", testMethod.TestClassName, testMethod.TestMethodName);

                    if (unitTestResult.Outcome is UnitTestOutcome.Inconclusive or UnitTestOutcome.NotRunnable or UnitTestOutcome.Unknown)
                    {
                        if (!MsTestIntegration.ShouldSkip(testMethod, out _, out _))
                        {
                            // This instrumentation catches all tests being ignored
                            MsTestIntegration.OnMethodBegin(testMethod, instance.GetType(), isRetry: false)?.Close(TestStatus.Skip, TimeSpan.Zero, unitTestResult.IgnoreReason);
                        }
                    }
                    else if (unitTestResult.Outcome is UnitTestOutcome.Error or UnitTestOutcome.Failed)
                    {
                        if (methodInfoCacheItem.TestMethodInfo.TryDuckCast<ITestMethodInfo>(out var testMethodInfo))
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

        return new CallTargetReturn<TReturn?>(returnValue);
    }
}
