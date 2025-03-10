// <copyright file="CIVisibilityTestCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Threading;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Net;
using Datadog.Trace.Ci.Tagging;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit;

internal class CIVisibilityTestCommand
{
    private static int _totalRetries = -1;
    private readonly ITestCommand _innerCommand;

    public CIVisibilityTestCommand(ITestCommand innerCommand)
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
            Common.Log.Debug("CIVisibilityTestCommand: Test is disabled by Datadog.");
            SetSkippedResult(result, "Flaky test is disabled by Datadog.");
            if (NUnitIntegration.GetOrCreateTest(context.CurrentTest, 0) is { } test)
            {
                NUnitIntegration.FinishTest(test, result);
            }

            context.CurrentResult = result;
            return result.Instance!;
        }

        // Execute test
        var executionNumber = 0;
        result = ExecuteTest(context, executionNumber++, out var testTags, out var duration);
        var resultStatus = result.ResultState.Status;

        // If test is quarantined we mark it as skipped after the first run so we hide the actual test status to the testing framework
        if (testManagementProperties is { Quarantined: true, AttemptToFix: false })
        {
            Common.Log.Debug("CIVisibilityTestCommand: Test is quarantined by Datadog.");
            SetSkippedResult(result, "Flaky test is quarantined by Datadog.");
        }

        // We bailout if the test was skipped or inconclusive
        if (resultStatus is TestStatus.Skipped or TestStatus.Inconclusive)
        {
            context.CurrentResult = result;
            return result.Instance!;
        }

        // Global retries locals
        var remainingRetries = 0;
        var retryNumber = 0;
        Func<ITestResult, bool> shouldRetry = static _ => true;
        string? retryMode = null;

        // Check the retries conditions
        if (testOptimization.EarlyFlakeDetectionFeature?.Enabled == true && testTags?.TestIsNew == "true")
        {
            // Early flake detection mode
            remainingRetries = Common.GetNumberOfExecutionsForDuration(duration) - 1;
            retryMode = "EFD";
        }
        else if (resultStatus == TestStatus.Failed && testOptimization.FlakyRetryFeature?.Enabled == true)
        {
            // Flaky retry mode
            Interlocked.CompareExchange(ref _totalRetries, testOptimization.FlakyRetryFeature.TotalFlakyRetryCount, -1);
            remainingRetries = testOptimization.FlakyRetryFeature.FlakyRetryCount;
            shouldRetry = static result => result.ResultState.Status == TestStatus.Failed && Interlocked.Decrement(ref _totalRetries) > 0;
            retryMode = "FlakyRetry";
        }

        if (retryMode != null)
        {
            var totalRetries = remainingRetries;
            while (remainingRetries-- > 0)
            {
                retryNumber++;
                Common.Log.Debug<string?, int, int>("CIVisibilityTestCommand: {Mode}: [Retry {Num}] Running retry of {TotalRetries}.", retryMode, retryNumber, totalRetries);
                ClearResultForRetry(context);
                var retryResult = ExecuteTest(context, executionNumber++, out _, out _);
                Common.Log.Debug<string?, int>("CIVisibilityTestCommand: {Mode}: [Retry {Num}] Aggregating results.", retryMode, retryNumber);
                AgregateResults(result, retryResult);
                if (!shouldRetry(result))
                {
                    Common.Log.Debug<string?, int>("CIVisibilityTestCommand: {Mode}: [Retry {Num}] Retry ended by the feature.", retryMode, retryNumber);
                    break;
                }
            }

            if (retryNumber > 0)
            {
                Common.Log.Debug("CIVisibilityTestCommand: {Mode}: All retries were executed.", retryMode);
            }
        }

        context.CurrentResult = result;
        return result.Instance!;
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

    private void SetSkippedResult(ITestResult result, string message)
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

    private ITestResult ExecuteTest(ITestExecutionContext context, int executionNumber, out TestSpanTags? testTags, out TimeSpan duration)
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

        if (test is not null)
        {
            if (duration.TotalMinutes >= 5 &&
                TestOptimization.Instance.EarlyFlakeDetectionFeature?.Enabled == true &&
                testTags!.TestIsNew == "true")
            {
                testTags.EarlyFlakeDetectionTestAbortReason = "slow";
            }

            NUnitIntegration.FinishTest(test, testResult);
        }

        return testResult;
    }
}
