// <copyright file="XUnitRetryCoordinator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Ci;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit;

internal static class XUnitRetryCoordinator
{
    private static int _totalRetries = -1;

    internal static void InitializeRetryBudget(ITestOptimization testOptimization)
    {
        Interlocked.CompareExchange(
            ref _totalRetries,
            testOptimization.FlakyRetryFeature?.TotalFlakyRetryCount ?? TestOptimizationFlakyRetryFeature.TotalFlakyRetryCountDefault,
            -1);
    }

    internal static int GetRemainingAtrBudget()
        => Interlocked.CompareExchange(ref _totalRetries, 0, 0);

    internal static XUnitRetryExecutionDecision GetOrCreateRetryExecutionDecision(
        TestCaseMetadata testCaseMetadata,
        bool hasFailures,
        bool hasNotRun)
        => XUnitIntegration.GetOrCreateRetryExecutionDecision(testCaseMetadata, hasFailures, hasNotRun, ref _totalRetries);

#if NETCOREAPP3_1_OR_GREATER
    internal static async ValueTask<XUnitRunSummary> ProcessResultAsync<TRetryRunner>(
#else
    internal static async Task<XUnitRunSummary> ProcessResultAsync<TRetryRunner>(
#endif
        RetryMessageBus messageBus,
        TestCaseMetadata testCaseMetadata,
        string? testCaseDisplayName,
        XUnitRunSummary runSummary,
        TRetryRunner retryRunner)
        where TRetryRunner : struct, IXUnitRetryRunner
    {
        var testOptimization = TestOptimization.Instance;

        switch (testCaseMetadata)
        {
            case { SelectedRetryMode: not TestRetryMode.None, AbortByThreshold: false }:
            {
                var isFirstExecution = testCaseMetadata.ExecutionIndex == 0;
                if (isFirstExecution)
                {
                    XUnitIntegration.InitializeTotalExecutions(
                        testOptimization,
                        testCaseMetadata,
                        () => Common.GetNumberOfExecutionsForDuration(System.TimeSpan.FromSeconds((double)runSummary.Time)));
                }

                if (testCaseMetadata.CountDownExecutionNumber > 0)
                {
                    var retryDecision = GetOrCreateRetryExecutionDecision(
                        testCaseMetadata,
                        hasFailures: runSummary.Failed > 0,
                        hasNotRun: runSummary.NotRun > 0);

                    if (retryDecision == XUnitRetryExecutionDecision.Retry)
                    {
                        if (XUnitIntegration.ShouldWaitForExceptionInstrumentation(testOptimization, testCaseMetadata))
                        {
                            Common.Log.Debug("XUnit retry: Waiting for exception instrumentation before retrying {TestCaseDisplayName}.", testCaseDisplayName);
                            await testOptimization.DynamicInstrumentationFeature!
                                                  .WaitForExceptionInstrumentation(TestOptimizationDynamicInstrumentationFeature.DefaultExceptionHandlerTimeout)
                                                  .ConfigureAwait(false);
                        }

                        testCaseMetadata.PrepareForRetry();
                        if (await retryRunner.RunAsync().ConfigureAwait(false) is { } retrySummary)
                        {
                            runSummary.Aggregate(retrySummary);
                        }
                        else
                        {
                            Common.Log.Error("XUnit retry: Unable to read retry RunSummary for {TestCaseDisplayName}.", testCaseDisplayName);
                        }
                    }
                }

                if (isFirstExecution)
                {
                    if (testCaseMetadata is { IsQuarantinedTest: true } or { IsDisabledTest: true })
                    {
                        runSummary.HideQuarantinedOrDisabledResult();
                    }
                    else
                    {
                        runSummary.NormalizeFrameworkResult();
                    }

                    messageBus.FlushMessages(testCaseMetadata.UniqueID, runSummary.GetFrameworkResult());
                }

                break;
            }

            case { IsQuarantinedTest: true } or { IsDisabledTest: true }:
                runSummary.ReportQuarantinedOrDisabledResultAsSkipped();
                messageBus.FlushMessages(testCaseMetadata.UniqueID, runSummary.GetFrameworkResult());
                break;

            default:
                messageBus.FlushMessages(testCaseMetadata.UniqueID, runSummary.GetFrameworkResult());
                break;
        }

        return runSummary;
    }
}
