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
using System.Threading.Tasks;
using Datadog.Trace.Ci;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.ClrProfiler.CallTarget.Handlers;
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
    internal static CallTargetState OnMethodBegin<TTarget, TTestMethod>(TTarget instance, TTestMethod testMethod)
        => TestMethodAttributeExecuteAsyncIntegration.OnMethodBegin(instance, testMethod);

    internal static CallTargetReturn<TReturn?> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception? exception, in CallTargetState state)
    {
        returnValue = TestMethodAttributeExecuteAsyncIntegration.OnAsyncMethodEnd(instance, returnValue, exception, state).SafeGetResult();
        IntegrationOptions.RestoreScopeFromAsyncExecution(in state);
        return new CallTargetReturn<TReturn?>(returnValue);
    }
}

/// <summary>
/// Microsoft.VisualStudio.TestPlatform.TestFramework.Execute calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Microsoft.VisualStudio.TestPlatform.TestFramework",
    TypeName = "Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute",
    MethodName = "ExecuteAsync",
    ReturnTypeName = "System.Threading.Tasks.Task`1[Microsoft.VisualStudio.TestTools.UnitTesting.TestResult[]]",
    ParameterTypeNames = ["Microsoft.VisualStudio.TestTools.UnitTesting.ITestMethod"],
    MinimumVersion = "14.0.0",
    MaximumVersion = "14.*.*",
    IntegrationName = MsTestIntegration.IntegrationName)]
[InstrumentMethod(
    AssemblyName = "Microsoft.VisualStudio.TestPlatform.TestFramework",
    TypeName = "Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute",
    MethodName = "ExecuteAsync",
    ReturnTypeName = "System.Threading.Tasks.Task`1[Microsoft.VisualStudio.TestTools.UnitTesting.TestResult[]]",
    ParameterTypeNames = ["Microsoft.VisualStudio.TestTools.UnitTesting.ITestMethod"],
    MinimumVersion = "14.0.0",
    MaximumVersion = "14.*.*",
    IntegrationName = MsTestIntegration.IntegrationName,
    CallTargetIntegrationKind = CallTargetKind.Derived)]
[InstrumentMethod(
    AssemblyNames = ["MSTest.TestFramework"],
    TypeName = "Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute",
    MethodName = "ExecuteAsync",
    ReturnTypeName = "System.Threading.Tasks.Task`1[Microsoft.VisualStudio.TestTools.UnitTesting.TestResult[]]",
    ParameterTypeNames = ["Microsoft.VisualStudio.TestTools.UnitTesting.ITestMethod"],
    MinimumVersion = "4.0.0",
    MaximumVersion = "4.*.*",
    IntegrationName = MsTestIntegration.IntegrationName)]
[InstrumentMethod(
    AssemblyNames = ["MSTest.TestFramework"],
    TypeName = "Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute",
    MethodName = "ExecuteAsync",
    ReturnTypeName = "System.Threading.Tasks.Task`1[Microsoft.VisualStudio.TestTools.UnitTesting.TestResult[]]",
    ParameterTypeNames = ["Microsoft.VisualStudio.TestTools.UnitTesting.ITestMethod"],
    MinimumVersion = "4.0.0",
    MaximumVersion = "4.*.*",
    IntegrationName = MsTestIntegration.IntegrationName,
    CallTargetIntegrationKind = CallTargetKind.Derived)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
#pragma warning disable SA1402
public static class TestMethodAttributeExecuteAsyncIntegration
#pragma warning restore SA1402
{
    private static int _totalRetries = -1;

    internal static CallTargetState OnMethodBegin<TTarget, TTestMethod>(TTarget instance, TTestMethod testMethod)
    {
        if (!MsTestIntegration.IsEnabled || instance is SkipTestMethodExecutor)
        {
            return CallTargetState.GetDefault();
        }

        if (Tracer.Instance.InternalActiveScope is { Span.Type: SpanTypes.Test } scope)
        {
            // Avoid a test inside another test
            Common.Log.Warning("Avoid a test inside another test: {Span}.", scope.Span.ResourceName);
            return CallTargetState.GetDefault();
        }

        var testMethodProxy = (ITestMethod?)testMethod.DuckAs<ITestMethodV4>() ?? testMethod.DuckAs<ITestMethodV3>();
        if (testMethodProxy is null)
        {
            DuckTypeException.Throw("Failed to duck type the test method instance to ITestMethodV3 or ITestMethodV4.");
        }

        var testRunnerState = new TestRunnerState(testMethodProxy, MsTestIntegration.OnMethodBegin(testMethodProxy, testMethodProxy.Type, isRetry: false));
        return new CallTargetState(Tracer.Instance.InternalActiveScope, testRunnerState);
    }

    internal static CallTargetReturn<TReturn?> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception? exception, in CallTargetState state)
    {
        return new CallTargetReturn<TReturn?>(returnValue);
    }

    internal static async Task<TReturn?> OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception? exception, CallTargetState state)
    {
        var testOptimization = TestOptimization.Instance;
        if (state.State is TestRunnerState { Test: not null } testMethodState)
        {
            var duration = testMethodState.Elapsed;
            var testMethod = testMethodState.TestMethod;
            var isEfdTest = false;
            var isAttemptToFix = false;
            var allowRetries = false;
            var resultStatus = TestStatus.Skip;

            if (!(returnValue is IList { Count: > 0 } returnValueList))
            {
                Common.Log.Warning("TestMethodAttributeExecuteIntegration: Failed to extract TestResult from return value");
                testMethodState.Test.Close(TestStatus.Fail);
                return returnValue;
            }

            MsTestIntegration.AddTotalTestCases(returnValueList.Count - 1);
            for (var i = 0; i < returnValueList.Count; i++)
            {
                var test = i == 0 ? testMethodState.Test : MsTestIntegration.OnMethodBegin(testMethodState.TestMethod, testMethodState.TestMethod.Type, isRetry: false, testMethodState.Test.StartTime);
                if (test?.GetTags() is { } testTags)
                {
                    if (testOptimization.EarlyFlakeDetectionFeature?.Enabled == true)
                    {
                        isEfdTest = isEfdTest || testTags.TestIsNew == "true";
                        if (isEfdTest && duration.TotalMinutes >= 5)
                        {
                            testTags.EarlyFlakeDetectionTestAbortReason = "slow";
                        }
                    }

                    if (testOptimization.TestManagementFeature?.Enabled == true)
                    {
                        isAttemptToFix = isAttemptToFix || testTags.IsAttemptToFix == "true";
                    }

                    if (returnValueList[i].TryDuckCast<ITestResult>(out var testResult))
                    {
                        var retryState = new RetryState();
                        resultStatus = HandleTestResult(test, testMethod, testResult, exception, retryState);
                        allowRetries = allowRetries || resultStatus != TestStatus.Skip;
                    }
                    else
                    {
                        Common.Log.Warning("TestMethodAttributeExecuteIntegration: Failed to cast {TestResultObject} to TestResultStruct", returnValueList[i]);
                        test.Close(TestStatus.Fail);
                    }
                }
            }

            if ((isEfdTest || isAttemptToFix) && allowRetries)
            {
                var remainingRetries = 0;
                var retryReason = string.Empty;

                // Get retries number and reason
                if (isEfdTest)
                {
                    remainingRetries = Common.GetNumberOfExecutionsForDuration(duration) - 1;
                    retryReason = "Early flake detection";
                }
                else if (isAttemptToFix)
                {
                    remainingRetries = testOptimization.TestManagementFeature?.TestManagementAttemptToFixRetryCount - 1 ?? TestOptimizationTestManagementFeature.TestManagementAttemptToFixRetryCountDefault;
                    retryReason = "Attempt to fix";
                }

                if (remainingRetries > 0)
                {
                    var retryState = new RetryState
                    {
                        IsARetry = true,
                        IsAttemptToFix = isAttemptToFix,
                    };

                    // Handle retries
                    List<IList> results = [returnValueList];
                    Common.Log.Debug<string?, int>("TestMethodAttributeExecuteIntegration: {Mode}: We need to retry {Times} times", retryReason, remainingRetries);
                    for (var i = 0; i < remainingRetries; i++)
                    {
                        retryState.IsLastRetry = i == remainingRetries - 1;
                        Common.Log.Debug<string?, int>("TestMethodAttributeExecuteIntegration: {Mode}: Retry number: {RetryNumber}", retryReason, i);
                        await RunRetryAsync(testMethod, testMethodState, retryState, results).ConfigureAwait(false);
                    }

                    // Calculate final results
                    returnValue = (TReturn?)GetFinalResults(results);
                }
            }
            else if (testOptimization.FlakyRetryFeature?.Enabled == true && resultStatus == TestStatus.Fail)
            {
                // check if is the first execution and the dynamic instrumentation feature is enabled
                if (testOptimization.DynamicInstrumentationFeature?.Enabled == true)
                {
                    // let's wait for the instrumentation of an exception has been done
                    Common.Log.Debug("TestMethodAttributeExecuteIntegration: First execution with an exception detected. Waiting for the exception instrumentation.");
                    testOptimization.DynamicInstrumentationFeature.WaitForExceptionInstrumentation(TestOptimizationDynamicInstrumentationFeature.DefaultExceptionHandlerTimeout).SafeWait();
                    Common.Log.Debug("TestMethodAttributeExecuteIntegration: Exception instrumentation was set or timed out.");
                }

                // Flaky retry is enabled and the test failed
                var retryState = new RetryState
                {
                    IsARetry = true,
                    IsAttemptToFix = false,
                };
                Interlocked.CompareExchange(ref _totalRetries, testOptimization.FlakyRetryFeature?.TotalFlakyRetryCount ?? TestOptimizationFlakyRetryFeature.TotalFlakyRetryCountDefault, -1);
                var remainingRetries = testOptimization.FlakyRetryFeature?.FlakyRetryCount ?? TestOptimizationFlakyRetryFeature.FlakyRetryCountDefault;
                if (remainingRetries > 0)
                {
                    // Handle retries
                    var results = new List<IList> { returnValueList };
                    for (var i = 0; i < remainingRetries; i++)
                    {
                        if (Interlocked.Decrement(ref _totalRetries) <= 0)
                        {
                            Common.Log.Debug("TestMethodAttributeExecuteIntegration: FlakyRetry: Exceeded number of total retries. [{Number}]", testOptimization.FlakyRetryFeature?.TotalFlakyRetryCount);
                            break;
                        }

                        Common.Log.Debug<int>("TestMethodAttributeExecuteIntegration: FlakyRetry: [Retry {Num}] Running retry...", i + 1);
                        var failedResult = await RunRetryAsync(testMethod, testMethodState, retryState, results).ConfigureAwait(false);

                        // If the retried test passed, we can stop the retries
                        if (!failedResult)
                        {
                            Common.Log.Debug<int>("TestMethodAttributeExecuteIntegration: FlakyRetry: [Retry {Num}] Test passed in retry.", i + 1);
                            break;
                        }
                    }

                    // Calculate final results
                    returnValue = (TReturn)GetFinalResults(results);
                }
            }
        }

        return returnValue;

        static async Task<bool> RunRetryAsync(ITestMethod testMethod, TestRunnerState testMethodState, RetryState retryState, List<IList> resultsCollection)
        {
            var retryTest = MsTestIntegration.OnMethodBegin(testMethod, testMethod.Type, isRetry: true);
            object? retryTestResult = null;
            Exception? retryException = null;
            var hasFailed = false;
            try
            {
                if (testMethodState.TestMethod is ITestMethodV4 testMethodV4)
                {
                    retryTestResult = await testMethodV4.InvokeAsync(null);
                }
                else if (testMethodState.TestMethod is ITestMethodV3 testMethodV3)
                {
                    retryTestResult = testMethodV3.Invoke(null);
                }
                else
                {
                    Common.Log.Warning("TestMethodAttributeExecuteIntegration: Unknown ITestMethod type {TestMethodType} for retry execution", testMethodState.TestMethod.GetType().FullName);
                }
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
                        var ciRetryTest = j == 0 ? retryTest : MsTestIntegration.OnMethodBegin(testMethod, testMethod.Type, isRetry: true, retryTest?.StartTime);
                        if (ciRetryTest is null)
                        {
                            continue;
                        }

                        if (HandleTestResult(ciRetryTest, testMethod, retryTestResultList[j].DuckCast<ITestResult>()!, retryException, retryState) == TestStatus.Fail)
                        {
                            hasFailed = true;
                        }
                    }

                    resultsCollection.Add(retryTestResultList);
                }
                else
                {
                    if (retryTest is not null && HandleTestResult(retryTest, testMethod, retryTestResult.DuckCast<ITestResult>()!, retryException, retryState) == TestStatus.Fail)
                    {
                        hasFailed = true;
                    }

                    resultsCollection.Add(new List<object?> { retryTestResult });
                }
            }

            return hasFailed;
        }
    }

    private static TestStatus HandleTestResult<TTestMethod, TTestResult>(Test test, TTestMethod testMethod, TTestResult testResult, Exception? exception, RetryState retryState)
        where TTestResult : ITestResult
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
            var testMethodProxy = (ITestMethod?)testMethod.DuckAs<ITestMethodV4>() ?? testMethod.DuckAs<ITestMethodV3>();
            if (testMethodProxy is null)
            {
                DuckTypeException.Throw("Failed to duck type the test method instance to ITestMethodV3 or ITestMethodV4.");
            }

            MsTestIntegration.UpdateTestParameters(test, testMethodProxy, testResult.DisplayName);
        }

        try
        {
            if (exception is not null)
            {
                test.Close(TestStatus.Fail);
                return TestStatus.Fail;
            }

            var testStatus = GetStatusFromOutcome(testResult.Outcome);
            if (retryState.IsARetry)
            {
                if (testStatus != TestStatus.Fail)
                {
                    retryState.AllRetriesFailed = false;
                }
                else if (retryState.IsAttemptToFix)
                {
                    retryState.AllAttemptsPassed = false;
                }

                if (retryState.IsLastRetry && test.GetTags() is { } testTags)
                {
                    if (retryState.IsAttemptToFix)
                    {
                        testTags.AttemptToFixPassed = retryState.AllAttemptsPassed ? "true" : "false";
                    }

                    if (retryState.AllRetriesFailed)
                    {
                        testTags.HasFailedAllRetries = "true";
                    }
                }
            }

            switch (testStatus)
            {
                case TestStatus.Fail:
                    test.Close(TestStatus.Fail);
                    return TestStatus.Fail;
                case TestStatus.Skip:
                    test.Close(TestStatus.Skip, TimeSpan.Zero, testException?.Message ?? string.Empty);
                    return TestStatus.Skip;
                case TestStatus.Pass:
                    test.Close(TestStatus.Pass);
                    return TestStatus.Pass;
                default:
                    test.Close(TestStatus.Fail);
                    return TestStatus.Fail;
            }
        }
        finally
        {
            if (TestOptimization.Instance.TestManagementFeature?.Enabled == true)
            {
                var testTags = test.GetTags();
                var isQuarantined = testTags.IsQuarantined == "true";
                var isAttemptToFix = testTags.IsAttemptToFix == "true";
                if (isQuarantined || isAttemptToFix)
                {
                    Common.Log.Debug("TestMethodAttributeExecuteIntegration: Test is quarantined or is an attempt to fix. Skipping test.");
                    testResult.Outcome = UnitTestOutcome.Ignored;
                    testResult.TestFailureException = null;
                }
            }
        }
    }

    private static TestStatus GetStatusFromOutcome(UnitTestOutcome outcome)
    {
        return outcome switch
        {
            UnitTestOutcome.Error or UnitTestOutcome.Failed or UnitTestOutcome.Timeout => TestStatus.Fail,
            UnitTestOutcome.Inconclusive or UnitTestOutcome.NotRunnable => TestStatus.Skip,
            UnitTestOutcome.Passed => TestStatus.Pass,
            _ => Unknown(outcome)
        };

        static TestStatus Unknown(UnitTestOutcome outcome)
        {
            Common.Log.Warning("TestMethodAttributeExecuteIntegration: Failed to handle the test status: {Outcome}", outcome);
            return TestStatus.Fail;
        }
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
        public readonly Test? Test;
        public readonly DateTimeOffset StartTime;

        public TestRunnerState(ITestMethod testMethod, Test? test)
        {
            TestMethod = testMethod;
            Test = test;
            _clock = TraceClock.Instance;
            StartTime = _clock.UtcNow;
        }

        public TimeSpan Elapsed => _clock.UtcNow - StartTime;
    }

    private class RetryState
    {
        public bool IsARetry { get; set; } = false;

        public bool IsLastRetry { get; set; } = false;

        public bool AllAttemptsPassed { get; set; } = true;

        public bool AllRetriesFailed { get; set; } = true;

        public bool IsAttemptToFix { get; set; } = false;
    }
}
