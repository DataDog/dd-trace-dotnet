// <copyright file="UnitTestRunnerRunSingleTestIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections;
using System.ComponentModel;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2;

/// <summary>
/// Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.UnitTestRunner.RunSingleTest calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyNames = ["Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter", "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices"],
    TypeName = "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.UnitTestRunner",
    MethodName = "RunSingleTest",
    ReturnTypeName = "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestResult[]",
    ParameterTypeNames = ["Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.TestMethod", "System.Collections.Generic.IDictionary`2[System.String,System.Object]"],
    MinimumVersion = "14.0.0",
    MaximumVersion = "14.*.*",
    IntegrationName = MsTestIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class UnitTestRunnerRunSingleTestIntegration
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
                    unitTestResultObject.TryDuckCast<UnitTestResultStruct>(out var unitTestResult) &&
                    methodInfoCacheItem.TestMethodInfo.TryDuckCast<ITestMethod>(out var testMethod))
                {
                    Common.Log.Debug("[UnitTestRunnerRunSingleTestIntegration] UnitTestRunner.RunSingleTest() call target interception: {Class}.{Name} | {Outcome}", testMethod.TestClassName, testMethod.TestMethodName, unitTestResult.Outcome);

                    if (unitTestResult.Outcome is UnitTestResultOutcome.Inconclusive or UnitTestResultOutcome.NotRunnable or UnitTestResultOutcome.Ignored)
                    {
                        var skipHandled =
                            MsTestIntegration.ShouldSkip(testMethod, out _, out _) ||
                            MsTestIntegration.GetTestProperties(testMethod) is { Quarantined: true } or { Disabled: true };
                        if (!skipHandled)
                        {
                            // This instrumentation catches all tests being ignored
                            var test = MsTestIntegration.OnMethodBegin(testMethod, instance.GetType(), isRetry: false);
                            if (test is not null)
                            {
                                // Set final_status = skip for ignored/inconclusive tests
                                if (test.GetTags() is { } testTags)
                                {
                                    testTags.FinalStatus = TestTags.StatusSkip;
                                }

                                test.Close(TestStatus.Skip, TimeSpan.Zero, unitTestResult.ErrorMessage);
                            }
                        }
                    }
                    else if (unitTestResult.Outcome is UnitTestResultOutcome.Error or UnitTestResultOutcome.Failed)
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

                                    // Set final_status = fail for class initialization failures
                                    if (test.GetTags() is { } testTags)
                                    {
                                        testTags.FinalStatus = TestTags.StatusFail;
                                    }

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
                                    MsTestIntegration.OnMethodBegin(testMethodInfo, instance.GetType(), isRetry: false) is { } asmTest)
                                {
                                    asmTest.SetErrorInfo(assemblyInitializationException);

                                    // Set final_status = fail for assembly initialization failures
                                    if (asmTest.GetTags() is { } asmTestTags)
                                    {
                                        asmTestTags.FinalStatus = TestTags.StatusFail;
                                    }

                                    asmTest.Close(TestStatus.Fail);
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
