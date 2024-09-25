// <copyright file="UnitTestRunnerRunSingleTestIntegration.cs" company="Datadog">
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
    ReturnTypeName = "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestResult[]",
    ParameterTypeNames = ["Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.TestMethod", "System.Collections.Generic.IDictionary`2[System.String,System.Object]"],
    MinimumVersion = "14.0.0",
    MaximumVersion = "14.*.*",
    IntegrationName = MsTestIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class UnitTestRunnerRunSingleTestIntegration
{
    /// <summary>
    /// OnMethodEnd callback
    /// </summary>
    /// <typeparam name="TTarget">Type of the target</typeparam>
    /// <typeparam name="TReturn">Type of the return value</typeparam>
    /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
    /// <param name="returnValue">Return value</param>
    /// <param name="exception">Exception instance in case the original code threw an exception.</param>
    /// <param name="state">Calltarget state value</param>
    /// <returns>A response value, in an async scenario will be T of Task of T</returns>
    internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
    {
        if (instance is null || !MsTestIntegration.IsEnabled)
        {
            return new CallTargetReturn<TReturn>(returnValue);
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
                    Common.Log.Debug("UnitTestRunner.RunSingleTest() call target interception: {Class}.{Name}", testMethod.TestClassName, testMethod.TestMethodName);

                    if (unitTestResult.Outcome is UnitTestResultOutcome.Inconclusive or UnitTestResultOutcome.NotRunnable or UnitTestResultOutcome.Ignored)
                    {
                        if (!MsTestIntegration.ShouldSkip(testMethod, out _, out _))
                        {
                            // This instrumentation catches all tests being ignored
                            MsTestIntegration.OnMethodBegin(testMethod, instance.GetType(), isRetry: false)?
                               .Close(TestStatus.Skip, TimeSpan.Zero, unitTestResult.ErrorMessage);
                        }
                    }
                    else if (unitTestResult.Outcome is UnitTestResultOutcome.Error or UnitTestResultOutcome.Failed)
                    {
                        if (methodInfoCacheItem.TestMethodInfo.TryDuckCast<ITestMethodInfo>(out var testMethodInfo))
                        {
                            // We need to check if the test is failing because a Class initialization error
                            if (testMethodInfo.Parent.Instance.TryDuckCast<ClassInfoInitializationExceptionStruct>(out var classInfoInitializationExceptionStruct))
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
                            if (testMethodInfo.Parent.Instance.TryDuckCast<ClassInfoCleanupExceptionsStruct>(out var classInfoCleanupExceptionsStruct))
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
                            if (testMethodInfo.Parent.Parent.Instance.TryDuckCast<AssemblyInfoExceptionsStruct>(out var assemblyInfoExceptionsStruct))
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

        return new CallTargetReturn<TReturn>(returnValue);
    }

    [DuckCopy]
    internal struct ClassInfoInitializationExceptionStruct
    {
        public Exception? ClassInitializationException;
    }

    [DuckCopy]
    internal struct ClassInfoCleanupExceptionsStruct
    {
        public Exception? ClassCleanupException;
    }

    [DuckCopy]
    internal struct AssemblyInfoExceptionsStruct
    {
        public Exception? AssemblyInitializationException;
    }
}
