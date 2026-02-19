// <copyright file="TestOptimizationTestCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Net;
using Datadog.Trace.Ci.Tagging;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit;

internal sealed class TestOptimizationTestCommand
{
    private readonly ITestCommand _innerCommand;

    public TestOptimizationTestCommand(ITestCommand innerCommand)
    {
        _innerCommand = innerCommand;
    }

    [DuckReverseMethod]
    public object? Execute(object contextObject)
    {
        var testOptimization = TestOptimization.Instance;
        var context = contextObject.TryDuckCast<ITestExecutionContextWithRepeatCount>(out var contextWithRepeatCount) ? contextWithRepeatCount : contextObject.DuckCast<ITestExecutionContext>();
        var result = context.CurrentResult;

        // Getting test management properties
        TestOptimizationClient.TestManagementResponseTestPropertiesAttributes? testManagementProperties = null;
        if (testOptimization.TestManagementFeature?.Enabled == true)
        {
            if (NUnitIntegration.GetTestModuleFrom(context.CurrentTest) is { } module &&
                NUnitIntegration.GetTestSuiteFrom(context.CurrentTest) is { } suite &&
                context.CurrentTest.Method?.MethodInfo?.Name is { } testMethodName)
            {
                testManagementProperties = testOptimization.TestManagementFeature?.GetTestProperties(module.Name, suite.Name, testMethodName);
            }
        }

        // If test is disabled we mark it as skipped and don't run it
        if (testManagementProperties is { Disabled: true, AttemptToFix: false })
        {
            Common.Log.Debug("TestOptimizationTestCommand: Test is disabled by Datadog.");
            SetSkippedResult(result, "Flaky test is disabled by Datadog.");
            if (NUnitIntegration.GetOrCreateTest(context.CurrentTest, 0) is { } test)
            {
                NUnitIntegration.FinishTest(test, result);
            }

            context.CurrentResult = result;
            return result.Instance;
        }

        // Execute test
        var retryState = new RetryState();
        result = ExecuteTest(context, 0, ref retryState, out var testTags, out var duration);
        var resultStatus = result.ResultState.Status;

        // If test is quarantined we mark it as skipped after the first run so we hide the actual test status to the testing framework
        if (testManagementProperties is { Quarantined: true, AttemptToFix: false })
        {
            Common.Log.Debug("TestOptimizationTestCommand: Test is quarantined by Datadog.");
            SetSkippedResult(result, "Flaky test is quarantined by Datadog.");
        }

        // Determine if retries will happen (used for final_status decision)
        var isSkippedOrInconclusive = resultStatus is TestStatus.Skipped or TestStatus.Inconclusive;
        var efdWillRetry = testOptimization.EarlyFlakeDetectionFeature?.Enabled == true && testTags?.TestIsNew == "true";
        var atrRemainingBudget = testOptimization.FlakyRetryFeature?.Enabled == true ? FlakyRetryBehavior.GetRemainingBudget() : 0;
        // -1 = uninitialized → assume retries possible; 0 = exhausted → no retries
        var atrHasBudget = atrRemainingBudget != 0;
        var atrWillRetry = resultStatus == TestStatus.Failed &&
                           testOptimization.FlakyRetryFeature?.Enabled == true &&
                           atrHasBudget;
        var atfWillRetry = testManagementProperties is { AttemptToFix: true };

        // Early bailout for skip/inconclusive
        if (isSkippedOrInconclusive)
        {
            context.CurrentResult = result;
            return result.Instance;
        }

        // Apply retries
        if (efdWillRetry)
        {
            result = DoRetries(new EarlyFlakeDetectionRetryBehavior(duration), context, result);
        }
        else if (atrWillRetry)
        {
            // check if is the first execution and the dynamic instrumentation feature is enabled
            if (testOptimization.DynamicInstrumentationFeature?.Enabled == true)
            {
                // let's wait for the instrumentation of an exception has been done
                Common.Log.Debug("TestOptimizationTestCommand: First execution with an exception detected. Waiting for the exception instrumentation.");
                testOptimization.DynamicInstrumentationFeature.WaitForExceptionInstrumentation(TestOptimizationDynamicInstrumentationFeature.DefaultExceptionHandlerTimeout).SafeWait();
                Common.Log.Debug("TestOptimizationTestCommand: Exception instrumentation was set or timed out.");
            }

            result = DoRetries(new FlakyRetryBehavior(testOptimization), context, result);
        }
        else if (atfWillRetry)
        {
            // testManagementProperties is validated non-null by the atfWillRetry pattern match above
            result = DoRetries(new AttemptToFixRetryBehavior(testOptimization, testManagementProperties!), context, result);
        }

        context.CurrentResult = result;
        return result.Instance;
    }

    private static void AgregateResults(ITestResult result, ITestResult retryResult)
    {
        result.Duration += retryResult.Duration;
        result.StartTime = result.StartTime < retryResult.StartTime ? result.StartTime : retryResult.StartTime;
        result.EndTime = result.EndTime > retryResult.EndTime ? result.EndTime : retryResult.EndTime;

        var message = result.Message;
        if (!string.IsNullOrEmpty(retryResult.Message))
        {
            message += Environment.NewLine + retryResult.Message;
        }

        var stackTrace = result.StackTrace;
        if (!string.IsNullOrEmpty(retryResult.StackTrace))
        {
            stackTrace += Environment.NewLine + retryResult.StackTrace;
        }

        result.SetResult(
            result.ResultState?.Status != TestStatus.Passed ? retryResult.ResultState : result.ResultState,
            message,
            stackTrace);

        if (retryResult.Output is { } testResultOutput && testResultOutput != "\r\n" && testResultOutput != "\n")
        {
            result.OutWriter?.WriteLine(testResultOutput);
        }

        if (retryResult.AssertionResults?.Count > 0 && result.AssertionResults is { } assertionResults)
        {
            foreach (var assertionResult in retryResult.AssertionResults)
            {
                assertionResults.Add(assertionResult);
            }
        }
    }

    private static void SetSkippedResult(ITestResult result, string message)
    {
        result.SetResult(result.ResultState.StaticIgnored, message, string.Empty);
    }

    private void ClearResultForRetry(ITestExecutionContext context)
    {
        context.CurrentResult = context.CurrentTest.MakeTestResult();
        if (context is ITestExecutionContextWithRepeatCount tmpContextWithRepeatCount)
        {
            // increment Retry count for next iteration. will only happen if we are guaranteed another iteration
            tmpContextWithRepeatCount.CurrentRepeatCount++;
        }
    }

    private ITestResult ExecuteTest(ITestExecutionContext context, int executionNumber, ref RetryState retryState, out TestSpanTags? testTags, out TimeSpan duration)
    {
        ITestResult? testResult = null;
        var test = NUnitIntegration.GetOrCreateTest(context.CurrentTest, executionNumber);
        var clock = TraceClock.Instance;
        var startTime = clock.UtcNow;
        try
        {
            testResult = _innerCommand.Execute(context);
        }
        catch (Exception ex)
        {
            // Commands are supposed to catch exceptions, but some don't,
            // and we want to look at restructuring the API in the future.
            testResult ??= context.CurrentTest.MakeTestResult();
            testResult.RecordException(ex);
        }
        finally
        {
            duration = clock.UtcNow - startTime;
            testTags = test?.GetTags();
        }

        var attemptToFixRetryBehavior = retryState.BehaviorType == typeof(AttemptToFixRetryBehavior);
        var resultStatus = testResult.ResultState.Status;

        if (retryState.IsARetry)
        {
            // Track if any retry passed (for final_status calculation)
            // CRITICAL: Only PASS counts as passed, not SKIP!
            if (resultStatus == TestStatus.Passed)
            {
                retryState.AnyRetryPassed = true;
            }

            // PRESERVED: AllRetriesFailed clears on pass OR skip (existing behavior for has_failed_all_retries)
            if (resultStatus != TestStatus.Failed)
            {
                retryState.AllRetriesFailed = false;
            }

            // ATF: AllAttemptsPassed clears only on actual failure (not skip)
            // Per ATF semantics: "failed" means actual failure, not skip/inconclusive
            if (attemptToFixRetryBehavior && resultStatus == TestStatus.Failed)
            {
                retryState.AllAttemptsPassed = false;
            }
        }

        if (test is not null && testTags is not null)
        {
            // Quarantined tests are masked to skip for the testing framework.
            // Keep retry-only semantics for ATR/EFD/ATF, but expose final_status=skip
            // for quarantined tests when no retry will happen.
            if (!retryState.IsARetry && testTags.IsQuarantined == "true" && testTags.FinalStatus is null)
            {
                var isSkippedOrInconclusive = resultStatus is TestStatus.Skipped or TestStatus.Inconclusive;
                var efdWillRetry = TestOptimization.Instance.EarlyFlakeDetectionFeature?.Enabled == true && testTags.TestIsNew == "true";
                var atrRemainingBudget = TestOptimization.Instance.FlakyRetryFeature?.Enabled == true ? FlakyRetryBehavior.GetRemainingBudget() : 0;
                var atrWillRetry = resultStatus == TestStatus.Failed &&
                                   TestOptimization.Instance.FlakyRetryFeature?.Enabled == true &&
                                   atrRemainingBudget != 0;
                var atfWillRetry = testTags.IsAttemptToFix == "true";
                var willRetry = !isSkippedOrInconclusive && (efdWillRetry || atrWillRetry || atfWillRetry);

                if (!willRetry)
                {
                    var anyExecutionPassed = resultStatus == TestStatus.Passed;
                    var anyExecutionFailed = resultStatus == TestStatus.Failed;
                    testTags.FinalStatus = Common.CalculateFinalStatus(anyExecutionPassed, anyExecutionFailed, isSkippedOrInconclusive, testTags);
                }
            }

            if (duration.TotalMinutes >= 5 &&
                TestOptimization.Instance.EarlyFlakeDetectionFeature?.Enabled == true &&
                testTags.TestIsNew == "true")
            {
                testTags.EarlyFlakeDetectionTestAbortReason = "slow";
            }

            // Determine if this is the final execution
            var isAtrBehavior = retryState.BehaviorType == typeof(FlakyRetryBehavior);
            var isAtrEarlyExit = isAtrBehavior &&
                                 resultStatus == TestStatus.Passed &&
                                 !retryState.IsLastRetry;

            // Pre-compute ATR budget exhaustion
            var isAtrBudgetExhausted = false;
            if (isAtrBehavior && resultStatus == TestStatus.Failed && !retryState.IsLastRetry)
            {
                var remainingBudget = FlakyRetryBehavior.GetRemainingBudget();
                // This pre-close check runs before ShouldRetry() decrements the ATR budget.
                // If budget is 1 now, the next decrement reaches 0, so no further retry will run.
                isAtrBudgetExhausted = remainingBudget <= 1;
            }

            var isFinalExecution = retryState.IsLastRetry || isAtrEarlyExit || isAtrBudgetExhausted;

            if (isFinalExecution)
            {
                if (retryState.AllRetriesFailed)
                {
                    testTags.HasFailedAllRetries = "true";
                }

                // Set final_status on the final retry
                var anyExecutionPassed = retryState.InitialExecutionPassed || retryState.AnyRetryPassed;
                // For ATF: any actual failure (initial or retry) means the fix didn't work (test is still flaky)
                // Note: skip/inconclusive does NOT count as failure per ATF semantics
                var anyExecutionFailed = retryState.InitialExecutionFailed || !retryState.AllAttemptsPassed;
                var isSkippedOrInconclusive = resultStatus is TestStatus.Skipped or TestStatus.Inconclusive;
                testTags.FinalStatus = Common.CalculateFinalStatus(anyExecutionPassed, anyExecutionFailed, isSkippedOrInconclusive, testTags);

                // ATF: AttemptToFixPassed should be consistent with final_status
                // If any execution failed, the fix didn't work
                if (attemptToFixRetryBehavior)
                {
                    testTags.AttemptToFixPassed = anyExecutionFailed ? "false" : "true";
                }
            }

            NUnitIntegration.FinishTest(test, testResult);
        }

        return testResult;
    }

    private ITestResult DoRetries<TBehavior>(in TBehavior behavior, ITestExecutionContext context, ITestResult result)
        where TBehavior : struct, IRetryBehavior
    {
        var executionNumber = 1;
        var remainingRetries = behavior.RemainingRetries;
        var retryNumber = 0;
        var totalRetries = remainingRetries;
        // Initialize InitialExecutionPassed/Failed from the initial execution result
        // CRITICAL: Only PASS counts as passed, only FAIL counts as failed (not skip)
        var initialStatus = result.ResultState.Status;
        var initialPassed = initialStatus == TestStatus.Passed;
        var initialFailed = initialStatus == TestStatus.Failed;
        var retryState = new RetryState
        {
            IsARetry = true,
            BehaviorType = typeof(TBehavior),
            InitialExecutionPassed = initialPassed,
            InitialExecutionFailed = initialFailed
        };
        while (remainingRetries-- > 0)
        {
            retryNumber++;
            Common.Log.Debug<string?, int, int>("TestOptimizationTestCommand: {Mode}: [Retry {Num}] Running retry of {TotalRetries}.", behavior.RetryMode, retryNumber, totalRetries);
            ClearResultForRetry(context);

            retryState.IsLastRetry = remainingRetries == 0;
            var retryResult = ExecuteTest(context, executionNumber++, ref retryState, out _, out _);
            Common.Log.Debug<string?, int>("TestOptimizationTestCommand: {Mode}: [Retry {Num}] Aggregating results.", behavior.RetryMode, retryNumber);
            AgregateResults(result, retryResult);
            if (!behavior.ShouldRetry(result))
            {
                Common.Log.Debug<string?, int>("TestOptimizationTestCommand: {Mode}: [Retry {Num}] Retry ended by the feature.", behavior.RetryMode, retryNumber);
                break;
            }
        }

        if (retryNumber > 0)
        {
            Common.Log.Debug("TestOptimizationTestCommand: {Mode}: All retries were executed.", behavior.RetryMode);
        }

        return behavior.ResultChanges(result);
    }

    private ref struct RetryState
    {
        public bool IsARetry;
        public bool IsLastRetry;
        public bool AllAttemptsPassed;
        public bool AllRetriesFailed;
        public bool InitialExecutionPassed;
        public bool InitialExecutionFailed;
        public bool AnyRetryPassed;
        public Type? BehaviorType;

        public RetryState()
        {
            IsARetry = false;
            IsLastRetry = false;
            AllAttemptsPassed = true;
            AllRetriesFailed = true;
            InitialExecutionPassed = false;
            InitialExecutionFailed = false;
            AnyRetryPassed = false;
            BehaviorType = null;
        }
    }
}
