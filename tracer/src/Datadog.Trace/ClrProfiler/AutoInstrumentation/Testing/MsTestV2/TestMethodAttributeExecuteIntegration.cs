// <copyright file="TestMethodAttributeExecuteIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.ClrProfiler.CallTarget.Handlers;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Util;

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
public sealed class TestMethodAttributeExecuteIntegration
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
public sealed class TestMethodAttributeExecuteAsyncIntegration
#pragma warning restore SA1402
{
    // Per-row cache for parameterized test execution results, keyed by test identifier (DisplayName)
    // This survives across initial and retry executions for the same test method
    // Use ConcurrentDictionary for thread safety - MSTest can run parameterized rows in parallel
    // ConditionalWeakTable allows garbage collection of testMethod without manual cleanup
    private static readonly ConditionalWeakTable<object, ConcurrentDictionary<string, bool>> InitialExecutionPassedCache = new();
    private static readonly ConditionalWeakTable<object, ConcurrentDictionary<string, bool>> InitialExecutionFailedCache = new();
    private static readonly ConditionalWeakTable<object, ConcurrentDictionary<string, bool>> AnyRetryPassedCache = new();
    private static readonly ConditionalWeakTable<object, ConcurrentDictionary<string, bool>> AllAttemptsPassedCache = new();

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
            var initialExecutionPassed = false;
            var initialExecutionFailed = false;
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
                        // Check if this test will have EFD/ATF retries (based on tags)
                        var testIsEfd = testTags.TestIsNew == "true";
                        var testIsAtf = testTags.IsAttemptToFix == "true";

                        var retryState = new RetryState
                        {
                            IsEfdOrAtfTest = testIsEfd || testIsAtf
                        };
                        resultStatus = HandleTestResult(test, testMethod, testResult, exception, retryState);
                        allowRetries = allowRetries || resultStatus != TestStatus.Skip;

                        // Track if initial execution passed/failed (for final_status) - both aggregate and per-row
                        var displayName = testResult.DisplayName ?? test.Name ?? string.Empty;
                        var cacheKey = GetCacheKey(displayName);
                        if (resultStatus == TestStatus.Pass)
                        {
                            initialExecutionPassed = true;
                            // Cache per-row initial execution result for parameterized tests
                            SetInitialExecutionPassed(testMethodState.TestMethod, cacheKey, true);
                        }
                        else if (resultStatus == TestStatus.Fail)
                        {
                            initialExecutionFailed = true;
                            // Cache per-row initial execution failure for ATF tracking
                            SetInitialExecutionFailed(testMethodState.TestMethod, cacheKey, true);
                        }
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
                        IsEfdOrAtfTest = true,
                        TotalExecutions = 1 + remainingRetries,
                        InitialExecutionPassed = initialExecutionPassed,
                        InitialExecutionFailed = initialExecutionFailed,
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
                Interlocked.CompareExchange(ref _totalRetries, testOptimization.FlakyRetryFeature?.TotalFlakyRetryCount ?? TestOptimizationFlakyRetryFeature.TotalFlakyRetryCountDefault, -1);
                var remainingRetries = testOptimization.FlakyRetryFeature?.FlakyRetryCount ?? TestOptimizationFlakyRetryFeature.FlakyRetryCountDefault;
                if (remainingRetries > 0)
                {
                    var retryState = new RetryState
                    {
                        IsARetry = true,
                        IsAttemptToFix = false,
                        IsEfdOrAtfTest = false,
                        TotalExecutions = 1 + remainingRetries,
                        InitialExecutionPassed = initialExecutionPassed,
                        InitialExecutionFailed = initialExecutionFailed,
                    };

                    // Handle retries
                    var results = new List<IList> { returnValueList };
                    for (var i = 0; i < remainingRetries; i++)
                    {
                        retryState.IsLastRetry = i == remainingRetries - 1;

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

        // Get display name for per-row caching - must be before exception branch
        var displayName = testResult.DisplayName ?? test.Name ?? string.Empty;
        var cacheKey = GetCacheKey(displayName);

        var shouldMaskOutcome = false;
        try
        {
            if (exception is not null)
            {
                // Track failure for ATF - both shared state and per-row cache
                if (retryState.IsAttemptToFix)
                {
                    retryState.AllAttemptsPassed = false;

                    // Cache per-row ATF failure for parameterized tests (retry path)
                    if (retryState.IsARetry && testMethod is not null)
                    {
                        SetAllAttemptsPassed(testMethod, cacheKey, false);
                    }
                }

                // Track initial execution failure - both shared state and per-row cache
                if (!retryState.IsARetry)
                {
                    retryState.InitialExecutionFailed = true;
                    if (testMethod is not null)
                    {
                        SetInitialExecutionFailed(testMethod, cacheKey, true);
                    }
                }

                // Set final_status before closing
                SetFinalStatusIfApplicable(test, testMethod, cacheKey, TestStatus.Fail, retryState);

                test.Close(TestStatus.Fail);
                return TestStatus.Fail;
            }

            var testStatus = GetStatusFromOutcome(testResult.Outcome);

            // Track pass status for final_status calculation
            if (retryState.IsARetry)
            {
                // Retry execution
                if (testStatus != TestStatus.Fail)
                {
                    retryState.AllRetriesFailed = false;
                }

                if (testStatus == TestStatus.Pass)
                {
                    retryState.AnyRetryPassed = true;

                    // Cache per-row retry pass result for parameterized tests
                    SetAnyRetryPassed(testMethod!, cacheKey, true);
                }
                else if (retryState.IsAttemptToFix && testStatus == TestStatus.Fail)
                {
                    retryState.AllAttemptsPassed = false;

                    // Cache per-row ATF failure for parameterized tests
                    SetAllAttemptsPassed(testMethod!, cacheKey, false);
                }
            }
            else
            {
                // Initial execution
                if (testStatus == TestStatus.Pass)
                {
                    retryState.InitialExecutionPassed = true;

                    // Cache per-row initial pass result for parameterized tests
                    SetInitialExecutionPassed(testMethod!, cacheKey, true);
                }
                else if (testStatus == TestStatus.Fail)
                {
                    retryState.InitialExecutionFailed = true;

                    // Cache per-row initial execution failure for ATF tracking
                    SetInitialExecutionFailed(testMethod!, cacheKey, true);
                }
            }

            // Set final_status before closing the test
            SetFinalStatusIfApplicable(test, testMethod, cacheKey, testStatus, retryState);

            // Determine if we should mask outcome (quarantined/ATF) - only on final execution
            var testTags = test.GetTags();
            if (TestOptimization.Instance.TestManagementFeature?.Enabled == true && testTags is not null)
            {
                var isQuarantined = testTags.IsQuarantined == "true";
                var isAttemptToFix = testTags.IsAttemptToFix == "true";
                var isDisabled = testTags.IsDisabled == "true";

                // Only mask outcome on final execution for ATF
                // Quarantined and disabled tests always mask outcome
                if (isQuarantined || isDisabled || (isAttemptToFix && (retryState.IsLastRetry || (!retryState.IsARetry && !retryState.IsEfdOrAtfTest))))
                {
                    shouldMaskOutcome = true;
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
            if (shouldMaskOutcome)
            {
                Common.Log.Debug("TestMethodAttributeExecuteIntegration: Test is quarantined, disabled, or is an attempt to fix (final). Masking outcome.");
                testResult.Outcome = UnitTestOutcome.Ignored;
                testResult.TestFailureException = null;
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

    private static void SetFinalStatusIfApplicable<TTestMethod>(Test test, TTestMethod testMethod, string cacheKey, TestStatus testStatus, RetryState retryState)
    {
        var testTags = test.GetTags();
        if (testTags == null)
        {
            return;
        }

        // Per-span guard to prevent duplicate setting
        if (testTags.FinalStatus is not null)
        {
            return;
        }

        // Determine if this is a "final execution" for final_status calculation
        bool isFinalExecution;

        if (retryState.IsARetry)
        {
            // For retries, check various conditions
            var isAtrRetry = retryState is { IsAttemptToFix: false, IsEfdOrAtfTest: false };

            // ATR early exit: test actually passed (Skip doesn't trigger early exit)
            var isAtrEarlyExit = isAtrRetry && testStatus == TestStatus.Pass;

            // ATR budget exhaustion: test failed and budget is about to run out
            var isAtrBudgetExhausted = isAtrRetry && testStatus == TestStatus.Fail && GetRemainingAtrBudget() <= 1;

            isFinalExecution = retryState.IsLastRetry || isAtrEarlyExit || isAtrBudgetExhausted;
        }
        else
        {
            // Initial execution - it's final if no retries will happen
            // For EFD/ATF, retries will always happen
            // For ATR, retries happen only if test fails and ATR is enabled
            var atrEnabled = TestOptimization.Instance.FlakyRetryFeature?.Enabled == true;
            var willHaveAtrRetries = atrEnabled && testStatus == TestStatus.Fail;
            isFinalExecution = !retryState.IsEfdOrAtfTest && !willHaveAtrRetries;
        }

        if (!isFinalExecution)
        {
            return;
        }

        // Only set retry-specific tags for tests with actual retries
        if (retryState.TotalExecutions > 1)
        {
            if (retryState.AllRetriesFailed)
            {
                testTags.HasFailedAllRetries = "true";
            }
        }

        // Calculate final_status using PER-ROW CACHE for parameterized tests
        // This ensures each row gets the correct final_status based on its own execution results,
        // not the shared RetryState which aggregates across all rows
        bool anyExecutionPassed;
        bool anyExecutionFailed;
        if (retryState.TotalExecutions == 1)
        {
            // Single execution: current status determines pass/fail
            anyExecutionPassed = testStatus == TestStatus.Pass;
            anyExecutionFailed = testStatus == TestStatus.Fail;
        }
        else if (testMethod is not null)
        {
            // Retry: use per-row cache for correct parameterized test handling
            var initialPassed = GetInitialExecutionPassed(testMethod, cacheKey);
            var initialFailed = GetInitialExecutionFailed(testMethod, cacheKey);
            var retryPassed = GetAnyRetryPassed(testMethod, cacheKey);
            var allAttemptsPassed = GetAllAttemptsPassed(testMethod, cacheKey);
            anyExecutionPassed = initialPassed || retryPassed;
            // For ATF: any actual failure (initial or retry) means the fix didn't work (test is still flaky)
            // Note: skip does NOT count as failure per ATF semantics
            anyExecutionFailed = initialFailed || !allAttemptsPassed;
        }
        else
        {
            // Fallback: use shared RetryState (shouldn't happen in normal flow)
            anyExecutionPassed = retryState.InitialExecutionPassed || retryState.AnyRetryPassed;
            // Use explicit InitialExecutionFailed - skip does NOT count as failure per ATF semantics
            anyExecutionFailed = retryState.InitialExecutionFailed || !retryState.AllAttemptsPassed;
        }

        var isSkippedOrInconclusive = testStatus == TestStatus.Skip;
        testTags.FinalStatus = Common.CalculateFinalStatus(anyExecutionPassed, anyExecutionFailed, isSkippedOrInconclusive, testTags);

        // ATF: AttemptToFixPassed should be consistent with final_status
        // If any execution failed, the fix didn't work
        if (retryState.TotalExecutions > 1 && retryState.IsAttemptToFix)
        {
            testTags.AttemptToFixPassed = anyExecutionFailed ? "false" : "true";
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

    /// <summary>
    /// Returns the remaining ATR budget. -1 means uninitialized, 0 means exhausted.
    /// </summary>
    internal static int GetRemainingAtrBudget()
        => Interlocked.CompareExchange(ref _totalRetries, 0, 0);

    /// <summary>
    /// Gets the cache key for per-row tracking. Uses displayName (includes parameter values for parameterized tests).
    /// Falls back to a default key if displayName is null/empty to avoid collision.
    /// </summary>
    private static string GetCacheKey(string? displayName)
    {
        // For parameterized tests: displayName includes parameter values (e.g., "TestMethod (1, 2)")
        // For non-parameterized tests: displayName may be empty, use a default fallback
        if (StringUtil.IsNullOrEmpty(displayName))
        {
            return "__default__";
        }

        return displayName;
    }

    /// <summary>
    /// Gets whether the initial execution passed for a specific parameterized row.
    /// </summary>
    private static bool GetInitialExecutionPassed(object testMethodKey, string cacheKey)
    {
        if (InitialExecutionPassedCache.TryGetValue(testMethodKey, out var cache) && cache.TryGetValue(cacheKey, out var passed))
        {
            return passed;
        }

        return false; // Default: assume initial failed if not cached
    }

    /// <summary>
    /// Sets whether the initial execution passed for a specific parameterized row.
    /// </summary>
    private static void SetInitialExecutionPassed(object testMethodKey, string cacheKey, bool passed)
    {
        var cache = InitialExecutionPassedCache.GetOrCreateValue(testMethodKey);
        cache[cacheKey] = passed;
    }

    /// <summary>
    /// Gets whether any retry execution passed for a specific parameterized row.
    /// </summary>
    private static bool GetAnyRetryPassed(object testMethodKey, string cacheKey)
    {
        if (AnyRetryPassedCache.TryGetValue(testMethodKey, out var cache) && cache.TryGetValue(cacheKey, out var passed))
        {
            return passed;
        }

        return false; // Default: no retry has passed yet
    }

    /// <summary>
    /// Sets whether any retry execution passed for a specific parameterized row.
    /// </summary>
    private static void SetAnyRetryPassed(object testMethodKey, string cacheKey, bool passed)
    {
        var cache = AnyRetryPassedCache.GetOrCreateValue(testMethodKey);
        cache[cacheKey] = passed;
    }

    /// <summary>
    /// Gets whether the initial execution failed for a specific parameterized row.
    /// </summary>
    private static bool GetInitialExecutionFailed(object testMethodKey, string cacheKey)
    {
        if (InitialExecutionFailedCache.TryGetValue(testMethodKey, out var cache) && cache.TryGetValue(cacheKey, out var failed))
        {
            return failed;
        }

        return false; // Default: assume initial didn't fail if not cached
    }

    /// <summary>
    /// Sets whether the initial execution failed for a specific parameterized row.
    /// </summary>
    private static void SetInitialExecutionFailed(object testMethodKey, string cacheKey, bool failed)
    {
        var cache = InitialExecutionFailedCache.GetOrCreateValue(testMethodKey);
        cache[cacheKey] = failed;
    }

    /// <summary>
    /// Gets whether all attempts passed for a specific parameterized row (ATF tracking).
    /// </summary>
    private static bool GetAllAttemptsPassed(object testMethodKey, string cacheKey)
    {
        if (AllAttemptsPassedCache.TryGetValue(testMethodKey, out var cache) && cache.TryGetValue(cacheKey, out var allPassed))
        {
            return allPassed;
        }

        return true; // Default: assume all passed until a failure is recorded
    }

    /// <summary>
    /// Sets whether all attempts passed for a specific parameterized row (ATF tracking).
    /// </summary>
    private static void SetAllAttemptsPassed(object testMethodKey, string cacheKey, bool allPassed)
    {
        var cache = AllAttemptsPassedCache.GetOrCreateValue(testMethodKey);
        cache[cacheKey] = allPassed;
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

    private sealed class RetryState
    {
        public bool IsARetry { get; set; } = false;

        public bool IsLastRetry { get; set; } = false;

        public bool AllAttemptsPassed { get; set; } = true;

        public bool AllRetriesFailed { get; set; } = true;

        public bool IsAttemptToFix { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether this test is an EFD or ATF test (retries will always happen).
        /// </summary>
        public bool IsEfdOrAtfTest { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether the initial execution passed. Only PASS counts as passed, not SKIP.
        /// </summary>
        public bool InitialExecutionPassed { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether the initial execution failed. Only FAIL counts as failed, not SKIP.
        /// Used for ATF final_status calculation.
        /// </summary>
        public bool InitialExecutionFailed { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether any retry execution passed. Only PASS counts as passed, not SKIP.
        /// Used for final_status calculation.
        /// </summary>
        public bool AnyRetryPassed { get; set; } = false;

        /// <summary>
        /// Gets or sets the total number of executions for this test (1 = single execution, >1 = has retries).
        /// </summary>
        public int TotalExecutions { get; set; } = 1;
    }
}
