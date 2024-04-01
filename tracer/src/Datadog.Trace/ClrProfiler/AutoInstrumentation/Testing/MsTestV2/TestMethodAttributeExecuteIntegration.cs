// <copyright file="TestMethodAttributeExecuteIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using Datadog.Trace.Ci;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2;

/// <summary>
/// Microsoft.VisualStudio.TestPlatform.TestFramework.Execute calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Microsoft.VisualStudio.TestPlatform.TestFramework",
    TypeName = "Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute",
    MethodName = "Execute",
    ReturnTypeName = "Microsoft.VisualStudio.TestTools.UnitTesting.TestResult",
    ParameterTypeNames = ["Microsoft.VisualStudio.TestTools.UnitTesting.ITestMethod"],
    MinimumVersion = "14.0.0",
    MaximumVersion = "14.*.*",
    IntegrationName = MsTestIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class TestMethodAttributeExecuteIntegration
{
    private static readonly int Retries = 0;

    /// <summary>
    /// OnMethodBegin callback
    /// </summary>
    /// <typeparam name="TTarget">Type of the target</typeparam>
    /// <typeparam name="TTestMethod">Type of the ITestMethod</typeparam>
    /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
    /// <param name="testMethod">Test method instance</param>
    /// <returns>Calltarget state value</returns>
    internal static CallTargetState OnMethodBegin<TTarget, TTestMethod>(TTarget instance, TTestMethod testMethod)
        where TTestMethod : ITestMethod, IDuckType
    {
        if (!MsTestIntegration.IsEnabled || instance is null)
        {
            return CallTargetState.GetDefault();
        }

        if (Retries == 0)
        {
            return new CallTargetState(null, MsTestIntegration.OnMethodBegin(testMethod, testMethod.Type));
        }

        return new CallTargetState(null, new TestRunnerState(testMethod, MsTestIntegration.OnMethodBegin(testMethod, testMethod.Type)));
    }

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
    internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception? exception, in CallTargetState state)
    {
        if (!MsTestIntegration.IsEnabled || instance is null)
        {
            return new CallTargetReturn<TReturn>(returnValue);
        }

        if (returnValue is Array { Length: 1 } returnValueArray &&
            returnValueArray.GetValue(0) is { } returnElement)
        {
            if (state.State is Test test)
            {
                HandleTestResult(test, returnElement, exception);
            }
            else if (state.State is TestRunnerState testRunnerState)
            {
                var retries = Retries;
                var initialTestStatus = HandleTestResult(testRunnerState.Test, returnElement, exception);
                if (initialTestStatus != TestStatus.Skip && retries > 0)
                {
                    var results = new object?[retries + 1];
                    results[0] = returnElement;
                    Common.Log.Debug<int>("We need to retry {Times} times", retries);
                    var testMethod = testRunnerState.TestMethod;
                    for (var i = 0; i < retries; i++)
                    {
                        Common.Log.Debug<int>("Retry number: {RetryNumber}", i);
                        var retryTest = MsTestIntegration.OnMethodBegin(testMethod, testMethod.Type);
                        object? retryTestResult = null;
                        Exception? retryException = null;
                        try
                        {
                            retryTestResult = testRunnerState.TestMethod.Invoke(null);
                        }
                        catch (Exception ex)
                        {
                            retryException = ex;
                        }
                        finally
                        {
                            HandleTestResult(retryTest, retryTestResult, retryException);
                            results[i + 1] = retryTestResult;
                        }
                    }

                    // Set return value
                    returnValueArray.SetValue(GetResultFromRetries(results), 0);
                }
            }
        }

        return new CallTargetReturn<TReturn>(returnValue);
    }

    private static TestStatus HandleTestResult(Test test, object? objTestResult, Exception? exception)
    {
        if (objTestResult is null || !objTestResult.TryDuckCast<TestResultStruct>(out var testResult))
        {
            test.SetErrorInfo("Exception", "Test result not found", null);
            test.Close(TestStatus.Fail);
            return TestStatus.Fail;
        }

        string? errorType = null;
        string? errorMessage = null;
        string? errorStackTrace = null;

        if (testResult.TestFailureException != null)
        {
            var testException = testResult.TestFailureException.InnerException ?? testResult.TestFailureException;
            var testExceptionType = testException.GetType();
            var testExceptionName = testExceptionType.Name;
            if (testExceptionName != "UnitTestAssertException" && testExceptionName != "AssertInconclusiveException")
            {
                test.SetErrorInfo(testException);
            }

            errorType = testExceptionType.FullName;
            errorMessage = testException.Message;
            errorStackTrace = testException.ToString();
        }

        switch (testResult.Outcome)
        {
            case UnitTestOutcome.Error:
            case UnitTestOutcome.Failed:
            case UnitTestOutcome.Timeout:
                test.SetErrorInfo(errorType ?? "Exception", errorMessage ?? testResult.Outcome.ToString(), errorStackTrace);
                test.Close(TestStatus.Fail);
                return TestStatus.Fail;
            case UnitTestOutcome.Inconclusive:
            case UnitTestOutcome.NotRunnable:
                if (exception is not null)
                {
                    test.SetErrorInfo(exception);
                    test.Close(TestStatus.Fail);
                    return TestStatus.Fail;
                }

                test.Close(TestStatus.Skip, TimeSpan.Zero, errorMessage);
                return TestStatus.Skip;
            case UnitTestOutcome.Passed:
                if (exception is not null)
                {
                    test.SetErrorInfo(exception);
                    test.Close(TestStatus.Fail);
                    return TestStatus.Fail;
                }

                test.Close(TestStatus.Pass);
                return TestStatus.Pass;
        }

        return TestStatus.Skip;
    }

    private static object GetResultFromRetries(object?[] results)
    {
        var lstResults = new List<ITestResult>();
        var lstExceptions = new List<Exception>();
        ITestResult? finalResult = null;
        var duration = TimeSpan.Zero;
        foreach (var result in results)
        {
            if (result is not null && result.TryDuckCast<ITestResult>(out var testResult))
            {
                lstResults.Add(testResult);
                duration += testResult.Duration;
                if (testResult.TestFailureException is { } testFailureException)
                {
                    lstExceptions.Add(testFailureException);
                }

                if (testResult.Outcome == UnitTestOutcome.Passed)
                {
                    finalResult = testResult;
                }
            }
        }

        if (finalResult is null)
        {
            finalResult = lstResults[0];
        }

        finalResult.Duration = duration;
        if (finalResult.Outcome != UnitTestOutcome.Passed && lstExceptions.Count > 0)
        {
            finalResult.TestFailureException = new AggregateException(lstExceptions);
        }

        finalResult.InnerResultsCount = lstResults.Count;
        return finalResult.Instance!;
    }

    private readonly struct TestRunnerState
    {
        public readonly ITestMethod TestMethod;
        public readonly Test Test;

        public TestRunnerState(ITestMethod testMethod, Test test)
        {
            TestMethod = testMethod;
            Test = test;
        }
    }
}
