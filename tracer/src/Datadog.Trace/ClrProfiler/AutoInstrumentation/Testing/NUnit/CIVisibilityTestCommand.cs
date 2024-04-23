// <copyright file="CIVisibilityTestCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit;

internal class CIVisibilityTestCommand
{
    private readonly ITestCommand _innerCommand;

    public CIVisibilityTestCommand(ITestCommand innerCommand)
    {
        _innerCommand = innerCommand;
    }

    [DuckReverseMethod]
    public object? Execute(object contextObject)
    {
        var context = contextObject.TryDuckCast<ITestExecutionContextWithRepeatCount>(out var contextWithRepeatCount) ?
                          contextWithRepeatCount :
                          contextObject.DuckCast<ITestExecutionContext>();
        var executionNumber = 0;
        var result = ExecuteTest(context, executionNumber++, out var isTestNew, out var duration);
        if (result.ResultState.Status != TestStatus.Skipped &&
            result.ResultState.Status != TestStatus.Inconclusive &&
            isTestNew)
        {
            // Get retries number
            var remainingRetries = Common.GetNumberOfExecutionsForDuration(duration) - 1;

            // Retries
            var retryNumber = 0;
            var totalRetries = remainingRetries;
            while (remainingRetries-- > 0)
            {
                retryNumber++;
                Common.Log.Debug<int, int>("EFD: [Retry {Num}] Running retry of {TotalRetries}.", retryNumber, totalRetries);
                ClearResultForRetry(context);
                var retryResult = ExecuteTest(context, executionNumber++, out _, out _);
                Common.Log.Debug<int>("EFD: [Retry {Num}] Aggregating results.", retryNumber);
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
                    result.ResultState.Status != TestStatus.Passed ? retryResult.ResultState : result.ResultState,
                    message,
                    stackTrace);

                if (retryResult.Output is { } testResultOutput && testResultOutput != "\r\n" && testResultOutput != "\n")
                {
                    result.OutWriter.WriteLine(testResultOutput);
                }

                if (retryResult.AssertionResults.Count > 0)
                {
                    foreach (var assertionResult in retryResult.AssertionResults)
                    {
                        result.AssertionResults.Add(assertionResult);
                    }
                }
            }

            if (retryNumber > 0)
            {
                Common.Log.Debug("EFD: All retries were executed.");
            }
        }

        context.CurrentResult = result;
        return result.Instance!;
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

    private ITestResult ExecuteTest(ITestExecutionContext context, int executionNumber, out bool isTestNew, out TimeSpan duration)
    {
        ITestResult? testResult = null;
        duration = TimeSpan.Zero;
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
        }

        isTestNew = false;
        if (test is not null)
        {
            if (test.GetTags() is { } testTags)
            {
                isTestNew = testTags.EarlyFlakeDetectionTestIsNew == "true";
                if (isTestNew && duration.TotalMinutes >= 5)
                {
                    testTags.EarlyFlakeDetectionTestAbortReason = "slow";
                }
            }

            NUnitIntegration.FinishTest(test, testResult);
        }

        return testResult;
    }
}
