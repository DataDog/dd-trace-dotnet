// <copyright file="XUnitTestRunnerRunAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit;

/// <summary>
/// Xunit.Sdk.TestRunner`1.RunAsync calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyNames = ["xunit.execution.dotnet", "xunit.execution.desktop"],
    TypeName = "Xunit.Sdk.TestRunner`1",
    MethodName = "RunAsync",
    ReturnTypeName = "System.Threading.Tasks.Task`1[Xunit.Sdk.RunSummary]",
    MinimumVersion = "2.2.0",
    MaximumVersion = "2.*.*",
    IntegrationName = XUnitIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class XUnitTestRunnerRunAsyncIntegration
{
    private static int _totalRetries = -1;
    private static Type? _messageBusInterfaceType;

    /// <summary>
    /// OnMethodBegin callback
    /// </summary>
    /// <typeparam name="TTarget">Type of the target</typeparam>
    /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
    /// <returns>Calltarget state value</returns>
    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
    {
        if (!XUnitIntegration.IsEnabled || instance is null)
        {
            return CallTargetState.GetDefault();
        }

        var testOptimization = TestOptimization.Instance;
        var runnerInstance = instance.DuckCast<TestRunnerStruct>();

        // Skip the whole logic if the test has a skip reason
        if (runnerInstance.SkipReason is not null)
        {
            // Skip test support
            Common.Log.Debug("XUnitTestRunnerRunAsyncIntegration: Skipping test: {Class}.{Name} Reason: {Reason}", runnerInstance.TestClass?.ToString() ?? string.Empty, runnerInstance.TestMethod?.Name ?? string.Empty, runnerInstance.SkipReason);
            XUnitIntegration.CreateTest(ref runnerInstance);
            return CallTargetState.GetDefault();
        }

        // Try to ducktype the current instance to ITestClassRunner
        if (!instance.TryDuckCast<ITestRunner>(out var testRunnerInstance))
        {
            Common.Log.Error("XUnitTestRunnerRunAsyncIntegration: EFD/Retry: Current test runner instance cannot be ducktyped.");
            return CallTargetState.GetDefault();
        }

        // Check if the test should be skipped by the ITR
        if (XUnitIntegration.ShouldSkip(ref runnerInstance, out _, out _))
        {
            Common.Log.Debug("XUnitTestRunnerRunAsyncIntegration: Test skipped by test skipping feature: {Class}.{Name}", runnerInstance.TestClass?.ToString() ?? string.Empty, runnerInstance.TestMethod?.Name ?? string.Empty);
            // Refresh values after skip reason change, and create Skip by ITR span.
            runnerInstance.SkipReason = IntelligentTestRunnerTags.SkippedByReason;
            testRunnerInstance.SkipReason = runnerInstance.SkipReason;
            XUnitIntegration.CreateTest(ref runnerInstance);
            return CallTargetState.GetDefault();
        }

        var isEarlyFlakeDetectionEnabled = testOptimization.EarlyFlakeDetectionFeature?.Enabled == true;
        var isFlakyRetryEnabled = testOptimization.FlakyRetryFeature?.Enabled == true;
        var isTestManagementEnabled = testOptimization.TestManagementFeature?.Enabled == true;

        // If there's no...
        // - EarlyFlakeDetectionFeature enabled
        // - FlakyRetryFeature enabled
        // - TestManagementFeature enabled
        // then we don't need to handle any retry, so we just skip the remaining logic.
        if (!isEarlyFlakeDetectionEnabled && !isFlakyRetryEnabled && !isTestManagementEnabled)
        {
            return CallTargetState.GetDefault();
        }

        // If the flaky retry feature is enabled, we need to set the total retries to the total flaky retry count
        if (isFlakyRetryEnabled)
        {
            Interlocked.CompareExchange(ref _totalRetries, testOptimization.FlakyRetryFeature?.TotalFlakyRetryCount ?? TestOptimizationFlakyRetryFeature.TotalFlakyRetryCountDefault, -1);
        }

        // Let's check if the current message bus is our own implementation.
        RetryMessageBus retryMessageBus;
        TestCaseMetadata testCaseMetadata;
        if (testRunnerInstance.MessageBus is IDuckType { Instance: { } } ducktypedMessageBus)
        {
            Common.Log.Debug("XUnitTestRunnerRunAsyncIntegration: EFD/Retry: Current message bus is a duck type, retrieving RetryMessageBus instance");
            retryMessageBus = (RetryMessageBus)ducktypedMessageBus.Instance;
            testCaseMetadata = retryMessageBus.GetMetadata(runnerInstance.TestCase.UniqueID);
        }
        else if (testRunnerInstance.MessageBus is { } messageBus)
        {
            // Let's replace the IMessageBus with our own implementation to process all results before sending them to the original bus
            Common.Log.Debug("XUnitTestRunnerRunAsyncIntegration: EFD/Retry: Current message bus is not a duck type, creating new RetryMessageBus");
            _messageBusInterfaceType ??= messageBus.GetType().GetInterface("IMessageBus")!;
            var duckMessageBus = messageBus.DuckCast<IMessageBus>();
            retryMessageBus = new RetryMessageBus(duckMessageBus, 1, 1);
            // EFD is disabled but FlakeRetry is enabled
            testCaseMetadata = retryMessageBus.GetMetadata(runnerInstance.TestCase.UniqueID);
            testRunnerInstance.MessageBus = retryMessageBus.DuckImplement(_messageBusInterfaceType);
        }
        else
        {
            Common.Log.Error("XUnitTestRunnerRunAsyncIntegration: EFD/Retry: Message bus is null.");
            return CallTargetState.GetDefault();
        }

        // We skip the test if the tesk management property is set to Disabled and there's no attempt to fix
        if (XUnitIntegration.GetTestManagementProperties(ref runnerInstance) is { Disabled: true, AttemptToFix: false })
        {
            runnerInstance.SkipReason = "Flaky test is disabled by Datadog";
            testRunnerInstance.SkipReason = runnerInstance.SkipReason;
            testCaseMetadata.Skipped = true;
            Common.Log.Debug("XUnitTestRunnerRunAsyncIntegration: Skipping test: {Class}.{Name} Reason: {Reason}", runnerInstance.TestClass?.ToString() ?? string.Empty, runnerInstance.TestMethod?.Name ?? string.Empty, runnerInstance.SkipReason);
            XUnitIntegration.CreateTest(ref runnerInstance, testCaseMetadata);
        }

        // Decrement the execution number (the method body will do the execution)
        testCaseMetadata.CountDownExecutionNumber--;

        return new CallTargetState(null, new TestRunnerState(testRunnerInstance, retryMessageBus, testCaseMetadata));
    }

    /// <summary>
    /// OnAsyncMethodEnd callback
    /// </summary>
    /// <typeparam name="TTarget">Type of the target</typeparam>
    /// <typeparam name="TReturn">Type of the return value (Xunit.Sdk.RunSummary)</typeparam>
    /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
    /// <param name="returnValue">Instance of Xunit.Sdk.RunSummary</param>
    /// <param name="exception">Exception instance in case the original code threw an exception.</param>
    /// <param name="state">Calltarget state value</param>
    /// <returns>A response value, in an async scenario will be T of Task of T</returns>
    internal static async Task<TReturn> OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
    {
        var testOptimization = TestOptimization.Instance;

        // If the state is not a TestRunnerState, we just return the original value
        if (state.State is not TestRunnerState { MessageBus: { } messageBus, TestCaseMetadata: { } testCaseMetadata } testRunnerState)
        {
            return returnValue;
        }

        if (!returnValue.TryDuckCast<IRunSummary>(out var runSummary))
        {
            Common.Log.Debug("XUnitTestRunnerRunAsyncIntegration: TryGetEditableRunSummary failed. Flushing messages for: {DisplayName}", testRunnerState.TestRunner.DisplayName);
            messageBus.FlushMessages(testCaseMetadata.UniqueID);
            return returnValue;
        }

        switch (testCaseMetadata)
        {
            // We retry tests if:
            // - EarlyFlakeDetectionEnabled is true and AbortByThreshold is false, or
            // - FlakyRetryEnabled is true, or
            // - IsAttemptToFix is true
            case { EarlyFlakeDetectionEnabled: true, AbortByThreshold: false } or { FlakyRetryEnabled: true } or { IsAttemptToFix: true }:
            {
                var isFlakyRetryEnabled = testCaseMetadata.FlakyRetryEnabled;
                var isAttemptToFix = testCaseMetadata.IsAttemptToFix;
                var isFirstExecution = testCaseMetadata.ExecutionIndex == 0;

                // If it's the first execution then let's calculate the total executions
                if (isFirstExecution)
                {
                    // Let's make decisions regarding slow tests, retry failed test feature or an attempt to fix
                    if (isFlakyRetryEnabled)
                    {
                        testCaseMetadata.TotalExecutions = (testOptimization.FlakyRetryFeature?.FlakyRetryCount ?? TestOptimizationFlakyRetryFeature.FlakyRetryCountDefault) + 1;
                    }
                    else if (isAttemptToFix)
                    {
                        testCaseMetadata.TotalExecutions = testOptimization.TestManagementFeature?.TestManagementAttemptToFixRetryCount ?? TestOptimizationTestManagementFeature.TestManagementAttemptToFixRetryCountDefault;
                    }
                    else
                    {
                        var duration = TraceClock.Instance.UtcNow - testRunnerState.StartTime;
                        testCaseMetadata.TotalExecutions = Common.GetNumberOfExecutionsForDuration(duration);
                    }

                    testCaseMetadata.CountDownExecutionNumber = testCaseMetadata.TotalExecutions - 1;
                }

                if (testCaseMetadata.CountDownExecutionNumber > 0)
                {
                    // If we are not in the latest execution, we need to retry the test
                    var doRetry = true;
                    if (isFlakyRetryEnabled)
                    {
                        // For flaky retry feature, we need to check if the test has failed or if the total retries are exceeded
                        var remainingTotalRetries = Interlocked.Decrement(ref _totalRetries);
                        if (runSummary.Failed == 0)
                        {
                            Common.Log.Debug("XUnitTestRunnerRunAsyncIntegration: EFD/Retry: [FlakyRetryEnabled] A non failed test execution was detected, skipping the remaining executions.");
                            doRetry = false;
                        }
                        else if (remainingTotalRetries < 1)
                        {
                            Common.Log.Debug("XUnitTestRunnerRunAsyncIntegration: EFD/Retry: [FlakyRetryEnabled] Exceeded number of total retries. [{Number}]", testOptimization.FlakyRetryFeature?.TotalFlakyRetryCount);
                            doRetry = false;
                        }
                    }

                    if (doRetry)
                    {
                        // check if is the first execution and the dynamic instrumentation feature is enabled
                        if (isFlakyRetryEnabled && isFirstExecution && testCaseMetadata.HasAnException && testOptimization.DynamicInstrumentationFeature?.Enabled == true)
                        {
                            // let's wait for the instrumentation of an exception has been done
                            Common.Log.Debug("XUnitTestRunnerRunAsyncIntegration: First execution with an exception detected. Waiting for the exception instrumentation.");
                            await testOptimization.DynamicInstrumentationFeature.WaitForExceptionInstrumentation(TestOptimizationDynamicInstrumentationFeature.DefaultExceptionHandlerTimeout).ConfigureAwait(false);
                            Common.Log.Debug("XUnitTestRunnerRunAsyncIntegration: Exception instrumentation was set or timed out.");
                        }

                        // Let's execute the retry
                        var retryNumber = testCaseMetadata.ExecutionIndex + 1;

                        // Set the retry as a continuation of this execution. This will be executing recursively until the execution count is 0/
                        Common.Log.Debug<int, int>("XUnitTestRunnerRunAsyncIntegration: EFD/Retry: [Retry {Num}] Test class runner is duck casted, running a retry. [Current retry value is {Value}]", retryNumber, testCaseMetadata.CountDownExecutionNumber);
                        var innerReturnValue = await ((Task<TReturn>)testRunnerState.TestRunner.RunAsync()).ConfigureAwait(false);
                        if (innerReturnValue.TryDuckCast<IRunSummary>(out var innerRunSummary))
                        {
                            Common.Log.Debug<int>("XUnitTestRunnerRunAsyncIntegration: EFD/Retry: [Retry {Num}] Aggregating results.", retryNumber);
                            runSummary.Aggregate(innerRunSummary);
                        }
                        else
                        {
                            Common.Log.Error<int>("XUnitTestRunnerRunAsyncIntegration: EFD/Retry: [Retry {Num}] Unable to duck cast the return value to IRunSummary.", retryNumber);
                        }
                    }
                }
                else
                {
                    // If we are in the last execution, we write some debug logs
                    if (isFlakyRetryEnabled && runSummary.Failed == 0)
                    {
                        Common.Log.Debug("XUnitTestRunnerRunAsyncIntegration: EFD/Retry: [FlakyRetryEnabled] A non failed test execution was detected.");
                    }
                    else
                    {
                        Common.Log.Debug("XUnitTestRunnerRunAsyncIntegration: EFD/Retry: All retries were executed.");
                    }
                }

                if (isFirstExecution)
                {
                    // Let's clear the failed and skipped runs if we have at least one successful run
                    if (Common.Log.IsEnabled(LogEventLevel.Debug))
                    {
                        var debugMsg = $"EFD/Retry: Summary: {testRunnerState.TestRunner.DisplayName} [Total: {runSummary.Total}, Failed: {runSummary.Failed}, Skipped: {runSummary.Skipped}]";
                        Common.Log.Debug("XUnitTestRunnerRunAsyncIntegration: {Value}", debugMsg);
                    }

                    if (testCaseMetadata is { IsQuarantinedTest: true } or { IsDisabledTest: true })
                    {
                        // Quarantined or disabled test results should not be reported to the testing framework.
                        Common.Log.Debug("XUnitTestRunnerRunAsyncIntegration: Quarantined or disabled test: {DisplayName}", testRunnerState.TestRunner.DisplayName);

                        // Let's update the summary to not have a single test run
                        runSummary.Total = 1;
                        runSummary.Failed = 0;
                        runSummary.Skipped = 1;
                    }
                    else
                    {
                        Common.Log.Debug("XUnitTestRunnerRunAsyncIntegration: Flushing test: {DisplayName}", testRunnerState.TestRunner.DisplayName);
                        // Let's update the summary to have only one test run
                        var passed = runSummary.Total - runSummary.Skipped - runSummary.Failed;
                        if (passed > 0)
                        {
                            runSummary.Total = 1;
                            runSummary.Failed = 0;
                            runSummary.Skipped = 0;
                        }
                        else if (runSummary.Skipped > 0)
                        {
                            runSummary.Total = 1;
                            runSummary.Skipped = 1;
                            runSummary.Failed = 0;
                        }
                        else if (runSummary.Failed > 0)
                        {
                            runSummary.Total = 1;
                            runSummary.Skipped = 0;
                            runSummary.Failed = 1;
                        }
                    }

                    messageBus.FlushMessages(testCaseMetadata.UniqueID);

                    if (Common.Log.IsEnabled(LogEventLevel.Debug))
                    {
                        var debugMsg = $"EFD/Retry: Returned summary: {testRunnerState.TestRunner.DisplayName} [Total: {runSummary.Total}, Failed: {runSummary.Failed}, Skipped: {runSummary.Skipped}]";
                        Common.Log.Debug("XUnitTestRunnerRunAsyncIntegration: {Value}", debugMsg);
                    }
                }

                break;
            }

            // We report the result of a test as skipped to the testing framework if:
            // - Is a quarantined test, or
            // - Is a disabled test
            case { IsQuarantinedTest: true } or { IsDisabledTest: true }:
                Common.Log.Debug("XUnitTestRunnerRunAsyncIntegration: Quarantined or disabled test: {DisplayName}", testRunnerState.TestRunner.DisplayName);
                runSummary.Total = 1;
                runSummary.Failed = 0;
                runSummary.Skipped = 1;
                messageBus.FlushMessages(testCaseMetadata.UniqueID);
                break;

            // For everything else, we just flush the messages
            default:
                Common.Log.Debug("XUnitTestRunnerRunAsyncIntegration: Flushing messages for: {DisplayName}", testRunnerState.TestRunner.DisplayName);
                messageBus.FlushMessages(testCaseMetadata.UniqueID);
                break;
        }

        return returnValue;
    }

    /// <summary>
    /// Read-only check of remaining ATR budget for pre-check before span closes (XUnit v2).
    /// Returns -1 if budget is uninitialized, 0 if exhausted, or a positive number if available.
    /// </summary>
    internal static int GetRemainingAtrBudget()
        => Interlocked.CompareExchange(ref _totalRetries, 0, 0);

    private readonly struct TestRunnerState
    {
        public readonly DateTimeOffset StartTime;
        public readonly ITestRunner TestRunner;
        public readonly RetryMessageBus MessageBus;
        public readonly TestCaseMetadata TestCaseMetadata;

        public TestRunnerState(ITestRunner testRunner, RetryMessageBus messageBus, TestCaseMetadata testCaseMetadata)
        {
            StartTime = TraceClock.Instance.UtcNow;
            TestRunner = testRunner;
            MessageBus = messageBus;
            TestCaseMetadata = testCaseMetadata;
        }
    }
}
