// <copyright file="TestMethodAttributeExecuteIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
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
[InstrumentMethod(
    AssemblyName = "Microsoft.VisualStudio.TestPlatform.TestFramework",
    TypeName = "Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute",
    MethodName = "Execute",
    ReturnTypeName = "Microsoft.VisualStudio.TestTools.UnitTesting.TestResult",
    ParameterTypeNames = ["Microsoft.VisualStudio.TestTools.UnitTesting.ITestMethod"],
    MinimumVersion = "14.0.0",
    MaximumVersion = "14.*.*",
    IntegrationName = MsTestIntegration.IntegrationName,
    CallTargetIntegrationKind = CallTargetKind.Derived)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class TestMethodAttributeExecuteIntegration
{
    private static int _totalRetries = -1;

    /// <summary>
    /// OnMethodBegin callback
    /// </summary>
    /// <typeparam name="TTarget">Type of the target</typeparam>
    /// <typeparam name="TTestMethod">Type of the ITestMethod</typeparam>
    /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
    /// <param name="testMethod">Test method instance</param>
    /// <returns>Calltarget state value</returns>
    internal static CallTargetState OnMethodBegin<TTarget, TTestMethod>(TTarget instance, TTestMethod testMethod)
        where TTestMethod : ITestMethod
    {
        if (!MsTestIntegration.IsEnabled || instance is ItrSkipTestMethodExecutor)
        {
            return CallTargetState.GetDefault();
        }

        if (Tracer.Instance.InternalActiveScope is { Span.Type: SpanTypes.Test })
        {
            // Avoid a test inside another test
            return CallTargetState.GetDefault();
        }

        return new CallTargetState(null, new TestRunnerState(testMethod, MsTestIntegration.OnMethodBegin(testMethod, testMethod.Type, isRetry: false)));
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
        if (state.State is TestRunnerState testMethodState)
        {
            var duration = testMethodState.Elapsed;
            var testMethod = testMethodState.TestMethod;
            var isTestNew = false;
            var allowRetries = false;
            var resultStatus = TestStatus.Skip;
            if (returnValue is IList { Count: > 0 } returnValueList)
            {
                MsTestIntegration.AddTotalTestCases(returnValueList.Count - 1);
                for (var i = 0; i < returnValueList.Count; i++)
                {
                    var test = i == 0 ? testMethodState.Test : MsTestIntegration.OnMethodBegin(testMethodState.TestMethod, testMethodState.TestMethod.Type, isRetry: false, testMethodState.Test.StartTime);
                    if (test.GetTags() is { } testTags)
                    {
                        isTestNew = isTestNew || testTags.EarlyFlakeDetectionTestIsNew == "true";
                        if (isTestNew && duration.TotalMinutes >= 5)
                        {
                            testTags.EarlyFlakeDetectionTestAbortReason = "slow";
                        }
                    }

                    resultStatus = HandleTestResult(test, testMethod, returnValueList[i], exception);
                    allowRetries = allowRetries || resultStatus != TestStatus.Skip;
                }
            }
            else
            {
                Common.Log.Warning("Failed to extract TestResult from return value");
                testMethodState.Test.Close(TestStatus.Fail);
                return new CallTargetReturn<TReturn>(returnValue);
            }

            if (isTestNew && allowRetries)
            {
                // Get retries number
                var remainingRetries = Common.GetNumberOfExecutionsForDuration(duration) - 1;
                if (remainingRetries > 0)
                {
                    // Handle retries
                    var results = new List<IList>();
                    results.Add(returnValueList);
                    Common.Log.Debug<int>("EFD: We need to retry {Times} times", remainingRetries);
                    for (var i = 0; i < remainingRetries; i++)
                    {
                        Common.Log.Debug<int>("EFD: Retry number: {RetryNumber}", i);
                        RunRetry(testMethod, testMethodState, results, out _);
                    }

                    // Calculate final results
                    returnValue = (TReturn)GetFinalResults(results);
                }
            }
            else if (CIVisibility.Settings.FlakyRetryEnabled == true && resultStatus == TestStatus.Fail)
            {
                // Flaky retry is enabled and the test failed
                Interlocked.CompareExchange(ref _totalRetries, CIVisibility.Settings.TotalFlakyRetryCount, -1);
                var remainingRetries = CIVisibility.Settings.FlakyRetryCount;
                if (remainingRetries > 0)
                {
                    // Handle retries
                    var results = new List<IList>();
                    results.Add(returnValueList);
                    for (var i = 0; i < remainingRetries; i++)
                    {
                        if (Interlocked.Decrement(ref _totalRetries) <= 0)
                        {
                            Common.Log.Debug<int>("FlakyRetry: Exceeded number of total retries. [{Number}]", CIVisibility.Settings.TotalFlakyRetryCount);
                            break;
                        }

                        Common.Log.Debug<int>("FlakyRetry: [Retry {Num}] Running retry...", i + 1);
                        RunRetry(testMethod, testMethodState, results, out var failedResult);

                        // If the retried test passed, we can stop the retries
                        if (!failedResult)
                        {
                            Common.Log.Debug<int>("FlakyRetry: [Retry {Num}] Test passed in retry.", i + 1);
                            break;
                        }
                    }

                    // Calculate final results
                    returnValue = (TReturn)GetFinalResults(results);
                }
            }
        }

        return new CallTargetReturn<TReturn>(returnValue);

        static void RunRetry(ITestMethod testMethod, TestRunnerState testMethodState, List<IList> resultsCollection, out bool hasFailed)
        {
            var retryTest = MsTestIntegration.OnMethodBegin(testMethod, testMethod.Type, isRetry: true);
            object? retryTestResult = null;
            Exception? retryException = null;
            hasFailed = false;
            try
            {
                retryTestResult = testMethodState.TestMethod.Invoke(null);
            }
            catch (Exception ex)
            {
                retryException = ex;
            }
            finally
            {
                if (retryTestResult is IList { Count: > 0 } retryTestResultList)
                {
                    for (var j = 0; j < retryTestResultList.Count; j++)
                    {
                        var ciRetryTest = j == 0 ? retryTest : MsTestIntegration.OnMethodBegin(testMethod, testMethod.Type, isRetry: true, retryTest.StartTime);
                        if (HandleTestResult(ciRetryTest, testMethod, retryTestResultList[j], retryException) == TestStatus.Fail)
                        {
                            hasFailed = true;
                        }
                    }

                    resultsCollection.Add(retryTestResultList);
                }
                else
                {
                    if (HandleTestResult(retryTest, testMethod, retryTestResult, retryException) == TestStatus.Fail)
                    {
                        hasFailed = true;
                    }

                    resultsCollection.Add(new List<object?> { retryTestResult });
                }
            }
        }
    }

    private static TestStatus HandleTestResult(Test test, ITestMethod testMethod, object? testResultObject, Exception? exception)
    {
        if (testResultObject.TryDuckCast<TestResultStruct>(out var testResult))
        {
            var testException = testResult.TestFailureException?.InnerException ??
                                testResult.TestFailureException ??
                                exception;
            if (testException != null)
            {
                var testExceptionType = testException.GetType();
                var testExceptionName = testExceptionType.Name;
                if (testExceptionName != "UnitTestAssertException" && testExceptionName != "AssertInconclusiveException")
                {
                    test.SetErrorInfo(testException);
                }
                else
                {
                    test.SetErrorInfo(testExceptionType.ToString(), testException.Message, testException.ToString());
                }
            }

            if (!string.IsNullOrEmpty(testResult.DisplayName) && test.Name != testResult.DisplayName)
            {
                test.SetName(testResult.DisplayName!);
                MsTestIntegration.UpdateTestParameters(test, testMethod, testResult.DisplayName);
            }

            if (exception is not null)
            {
                test.Close(TestStatus.Fail);
                return TestStatus.Fail;
            }

            switch (testResult.Outcome)
            {
                case UnitTestOutcome.Error or UnitTestOutcome.Failed or UnitTestOutcome.Timeout:
                    test.Close(TestStatus.Fail);
                    return TestStatus.Fail;
                case UnitTestOutcome.Inconclusive or UnitTestOutcome.NotRunnable:
                    test.Close(TestStatus.Skip, TimeSpan.Zero, testException?.Message ?? string.Empty);
                    return TestStatus.Skip;
                case UnitTestOutcome.Passed:
                    test.Close(TestStatus.Pass);
                    return TestStatus.Pass;
                default:
                    Common.Log.Warning("Failed to handle the test status: {Outcome}", testResult.Outcome);
                    test.Close(TestStatus.Fail);
                    return TestStatus.Fail;
            }
        }

        Common.Log.Warning("Failed to cast {TestResultObject} to TestResultStruct", testResultObject);
        test.Close(TestStatus.Fail);
        return TestStatus.Fail;
    }

    private static IList GetFinalResults(List<IList> executionStatuses)
    {
        var lstExceptions = new List<Exception>();
        var initialResults = executionStatuses[0];
        List<object?> lstResultsFromDifferentRuns = new();
        var finalResults = new object[initialResults.Count];
        for (var i = 0; i < initialResults.Count; i++)
        {
            foreach (var execution in executionStatuses)
            {
                if (i < execution.Count)
                {
                    lstResultsFromDifferentRuns.Add(execution[i]);
                }
            }

            finalResults[i] = GetResultFromRetries(lstResultsFromDifferentRuns);
            lstResultsFromDifferentRuns.Clear();
            lstExceptions.Clear();
        }

        for (var i = 0; i < initialResults.Count; i++)
        {
            initialResults[i] = finalResults[i];
        }

        return initialResults;

        object GetResultFromRetries(IList results)
        {
            ITestResult? finalResult = null;
            var resultsCount = 0;
            var duration = TimeSpan.Zero;
            foreach (var result in results)
            {
                if (result.TryDuckCast<ITestResult>(out var testResult))
                {
                    duration += testResult.Duration;
                    if (testResult.TestFailureException is { } testFailureException)
                    {
                        lstExceptions.Add(testFailureException);
                    }

                    if (resultsCount++ == 0 || testResult.Outcome == UnitTestOutcome.Passed)
                    {
                        finalResult = testResult;
                    }
                }
            }

            if (finalResult is null)
            {
                ThrowHelper.ThrowNullReferenceException("Failed to get the final result from the retries");
            }

            finalResult.Duration = duration;
            if (finalResult.Outcome != UnitTestOutcome.Passed && lstExceptions.Count > 0)
            {
                finalResult.TestFailureException = new AggregateException(lstExceptions);
            }

            finalResult.InnerResultsCount = resultsCount;
            return finalResult.Instance!;
        }
    }

    private readonly struct TestRunnerState
    {
        private readonly TraceClock _clock;
        public readonly ITestMethod TestMethod;
        public readonly Test Test;
        public readonly DateTimeOffset StartTime;

        public TestRunnerState(ITestMethod testMethod, Test test)
        {
            TestMethod = testMethod;
            Test = test;
            _clock = TraceClock.Instance;
            StartTime = _clock.UtcNow;
        }

        public TimeSpan Elapsed => _clock.UtcNow - StartTime;
    }
}
