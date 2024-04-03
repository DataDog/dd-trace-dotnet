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
    private readonly int _retries;

    public CIVisibilityTestCommand(ITestCommand innerCommand, int retries)
    {
        _innerCommand = innerCommand;
        _retries = retries;
    }

    [DuckReverseMethod]
    public object Execute(object contextObject)
    {
        var context = contextObject.TryDuckCast<ITestExecutionContextWithRepeatCount>(out var contextWithRepeatCount) ?
                          contextWithRepeatCount :
                          contextObject.DuckCast<ITestExecutionContext>();

        var executionNumber = 0;
        var result = ExecuteTest(context, executionNumber++);
        if (result.ResultState.Status != TestStatus.Skipped &&
            result.ResultState.Status != TestStatus.Inconclusive)
        {
            // Retries
            ClearResultForRetry();
            var remainingRetries = _retries;
            while (remainingRetries-- > 0)
            {
                var retryResult = ExecuteTest(context, executionNumber++);
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

                // Clear result for retry
                if (remainingRetries > 0)
                {
                    ClearResultForRetry();
                }
            }
        }

        context.CurrentResult = result;
        return result.Instance!;

        void ClearResultForRetry()
        {
            context.CurrentResult = context.CurrentTest.MakeTestResult();
            if (context is ITestExecutionContextWithRepeatCount tmpContextWithRepeatCount)
            {
                // increment Retry count for next iteration. will only happen if we are guaranteed another iteration
                tmpContextWithRepeatCount.CurrentRepeatCount++;
            }
        }
    }

    private ITestResult ExecuteTest(ITestExecutionContext context, int executionNumber)
    {
        var test = NUnitIntegration.GetOrCreateTest(context.CurrentTest, executionNumber);
        ITestResult? testResult = null;
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

        if (test is not null)
        {
            NUnitIntegration.FinishTest(test, testResult);
        }

        return testResult;
    }
}
