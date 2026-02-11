// <copyright file="XUnitTestMethodRunnerBaseRunTestCaseV3Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit.V3;

/// <summary>
/// Xunit.v3.TestCaseRunner`3.RunTest calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "xunit.v3.core",
    TypeName = "Xunit.v3.XunitTestMethodRunnerBase`3",
    MethodName = "RunTestCase",
    ParameterTypeNames = ["!0", "!2"],
    ReturnTypeName = "System.Threading.Tasks.ValueTask`1[Xunit.v3.RunSummary]",
    MinimumVersion = "1.0.0",
    MaximumVersion = "3.*.*",
    IntegrationName = XUnitIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class XUnitTestMethodRunnerBaseRunTestCaseV3Integration
{
    private static int _totalRetries = -1;

    internal static CallTargetState OnMethodBegin<TTarget, TContext, TTestCase>(TTarget instance, TContext context, TTestCase testcaseOriginal)
        where TContext : IXunitTestMethodRunnerBaseContextV3
    {
        if (!XUnitIntegration.IsEnabled || instance is null || context.Instance is null)
        {
            return CallTargetState.GetDefault();
        }

        var testOptimization = TestOptimization.Instance;
        var testcase = testcaseOriginal.DuckCast<IXunitTestCaseV3>()!;
        var testRunnerData = new TestRunnerStruct
        {
            TestClass = testcase.TestMethod.TestClass.Class,
            TestMethod = testcase.TestMethod.Method,
            TestMethodArguments = testcase.TestMethod.TestMethodArguments!,
            TestCase = new CustomTestCase
            {
                DisplayName = testcase.TestCaseDisplayName,
                Traits = testcase.Traits.ToDictionary(k => k.Key, v => v.Value?.ToList()),
                UniqueID = testcase.UniqueID,
            },
            Aggregator = context.Aggregator,
            SkipReason = testcase.SkipReason,
        };

        // Skip the whole logic if the test has a skip reason
        if (testRunnerData.SkipReason is not null)
        {
            // Skip test support
            Common.Log.Debug("XUnitTestMethodRunnerBaseRunTestCaseV3Integration: Skipping test: {Class}.{Name} Reason: {Reason}", testcase.TestClass?.ToString() ?? string.Empty, testcase.TestMethod?.Method.Name ?? string.Empty, testRunnerData.SkipReason);
            XUnitIntegration.CreateTest(ref testRunnerData);
            return CallTargetState.GetDefault();
        }

        // Check if the test should be skipped by the ITR
        if (XUnitIntegration.ShouldSkip(ref testRunnerData, out _, out _))
        {
            Common.Log.Debug("XUnitTestMethodRunnerBaseRunTestCaseV3Integration: Test skipped by test skipping feature: {Class}.{Name}", testcase.TestClass?.ToString() ?? string.Empty, testcase.TestMethod?.Method.Name ?? string.Empty);
            // Refresh values after skip reason change, and create Skip by ITR span.
            testcase.SkipReason = IntelligentTestRunnerTags.SkippedByReason;
            testRunnerData.SkipReason = testcase.SkipReason;
            XUnitIntegration.CreateTest(ref testRunnerData);
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

        // If we have a RetryMessageBus means that we are in a retry context
        if (context.MessageBus is IDuckType { Instance: { } and RetryMessageBus retryMessageBus })
        {
            var testCaseMetadata = retryMessageBus.GetMetadata(testcase.TestMethod.UniqueID);

            // We skip the test if the tesk management property is set to Disabled and there's no attempt to fix
            if (XUnitIntegration.GetTestManagementProperties(ref testRunnerData) is { Disabled: true, AttemptToFix: false })
            {
                testcase.SkipReason = "Flaky test is disabled by Datadog";
                testRunnerData.SkipReason = testcase.SkipReason;
                Common.Log.Debug("XUnitTestMethodRunnerBaseRunTestCaseV3Integration: Skipping test: {Class}.{Name} Reason: {Reason}", testcase.TestClass?.ToString() ?? string.Empty, testcase.TestMethod.Method.Name, testcase.SkipReason);
                XUnitIntegration.CreateTest(ref testRunnerData, testCaseMetadata);
            }

            return new CallTargetState(null, new TestRunnerState(retryMessageBus, testCaseMetadata, context, testcase));
        }

        return CallTargetState.GetDefault();
    }

    internal static async Task<TReturn> OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
    {
        var testOptimization = TestOptimization.Instance;

        // If the state is not a TestRunnerState, we just return the original value
        if (instance is null || state.State is not TestRunnerState { MessageBus: { } messageBus, TestCaseMetadata: { } testCaseMetadata, Context: { Instance: { } } context, TestCase: { Instance: { } } testcase })
        {
            return returnValue;
        }

        if (!RunSummaryConverter<TReturn>.TryGetEditableRunSummary(returnValue, out var runSummaryUnsafe))
        {
            Common.Log.Debug("XUnitTestMethodRunnerBaseRunTestCaseV3Integration: TryGetEditableRunSummary failed. Flushing messages for: {TestCaseDisplayName}", testcase.TestCaseDisplayName);
            messageBus.FlushMessages(testcase.TestMethod.UniqueID);
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
                        var duration = TimeSpan.FromSeconds((double)runSummaryUnsafe.Time);
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
                        if (runSummaryUnsafe.Failed == 0)
                        {
                            Common.Log.Debug("XUnitTestMethodRunnerBaseRunTestCaseV3Integration: EFD/Retry: [FlakyRetryEnabled] A non failed test execution was detected, skipping the remaining executions.");
                            doRetry = false;
                        }
                        else if (runSummaryUnsafe.NotRun > 0)
                        {
                            Common.Log.Debug("XUnitTestMethodRunnerBaseRunTestCaseV3Integration: EFD/Retry: [FlakyRetryEnabled] A NotRun test was detected, skipping the remaining executions.");
                            doRetry = false;
                        }
                        else if (remainingTotalRetries < 1)
                        {
                            Common.Log.Debug("XUnitTestMethodRunnerBaseRunTestCaseV3Integration: EFD/Retry: [FlakyRetryEnabled] Exceeded number of total retries. [{Number}]", testOptimization.FlakyRetryFeature?.TotalFlakyRetryCount);
                            doRetry = false;
                        }
                    }

                    if (doRetry)
                    {
                        // check if is the first execution and the dynamic instrumentation feature is enabled
                        if (isFlakyRetryEnabled && isFirstExecution && testCaseMetadata.HasAnException && testOptimization.DynamicInstrumentationFeature?.Enabled == true)
                        {
                            // let's wait for the instrumentation of an exception has been done
                            Common.Log.Debug("XUnitTestMethodRunnerBaseRunTestCaseV3Integration: First execution with an exception detected. Waiting for the exception instrumentation.");
                            await testOptimization.DynamicInstrumentationFeature.WaitForExceptionInstrumentation(TestOptimizationDynamicInstrumentationFeature.DefaultExceptionHandlerTimeout).ConfigureAwait(false);
                            Common.Log.Debug("XUnitTestMethodRunnerBaseRunTestCaseV3Integration: Exception instrumentation was set or timed out.");
                        }

                        // Let's execute the retry
                        var retryNumber = testCaseMetadata.ExecutionIndex + 1;

                        // Set the retry as a continuation of this execution. This will be executing recursively until the execution count is 0/
                        Common.Log.Debug<int, int>("XUnitTestMethodRunnerBaseRunTestCaseV3Integration: EFD/Retry: [Retry {Num}] Test class runner is duck casted, running a retry. [Current retry value is {Value}]", retryNumber, testCaseMetadata.CountDownExecutionNumber);
                        var mrunner = instance.DuckCast<IXunitTestMethodRunnerV3>();

                        // Decrement the execution number (the method body will do the execution)
                        testCaseMetadata.CountDownExecutionNumber--;
                        var innerReturnValue = (TReturn)await mrunner.RunTestCase(context.Instance, testcase.Instance);
                        Common.Log.Debug<int, int, string?>("XUnitTestMethodRunnerBaseRunTestCaseV3Integration: EFD/Retry: [Retry {Num}] Retry finished. [Current retry value is {Value}]. DisplayName: {DisplayName}", retryNumber, testCaseMetadata.CountDownExecutionNumber, testcase.TestCaseDisplayName);

                        var innerReturnValueUnsafe = Unsafe.As<TReturn, RunSummaryUnsafeStruct>(ref innerReturnValue);
                        Common.Log.Debug<int>("XUnitTestMethodRunnerBaseRunTestCaseV3Integration: EFD/Retry: [Retry {Num}] Aggregating results.", retryNumber);
                        runSummaryUnsafe.Total += innerReturnValueUnsafe.Total;
                        runSummaryUnsafe.Failed += innerReturnValueUnsafe.Failed;
                        runSummaryUnsafe.Skipped += innerReturnValueUnsafe.Skipped;
                        runSummaryUnsafe.NotRun += innerReturnValueUnsafe.NotRun;
                        runSummaryUnsafe.Time += innerReturnValueUnsafe.Time;
                    }
                }
                else
                {
                    // If we are in the last execution, we write some debug logs
                    if (isFlakyRetryEnabled && runSummaryUnsafe.Failed == 0)
                    {
                        Common.Log.Debug("XUnitTestMethodRunnerBaseRunTestCaseV3Integration: EFD/Retry: [FlakyRetryEnabled] A non failed test execution was detected.");
                    }
                    else
                    {
                        Common.Log.Debug("XUnitTestMethodRunnerBaseRunTestCaseV3Integration: EFD/Retry: All retries were executed.");
                    }
                }

                if (isFirstExecution)
                {
                    // Let's clear the failed and skipped runs if we have at least one successful run
                    if (Common.Log.IsEnabled(LogEventLevel.Debug))
                    {
                        var debugMsg = $"EFD/Retry: Summary: {testcase.TestCaseDisplayName} [Total: {runSummaryUnsafe.Total}, Failed: {runSummaryUnsafe.Failed}, Skipped: {runSummaryUnsafe.Skipped}]";
                        Common.Log.Debug("XUnitTestMethodRunnerBaseRunTestCaseV3Integration: {Value}", debugMsg);
                    }

                    if (testCaseMetadata is { IsQuarantinedTest: true } or { IsDisabledTest: true })
                    {
                        // Quarantined or disabled test results should not be reported to the testing framework.
                        Common.Log.Debug("XUnitTestMethodRunnerBaseRunTestCaseV3Integration: Quarantined or disabled test: {TestCaseDisplayName}", testcase.TestCaseDisplayName);

                        // Let's update the summary to not have a single test run
                        runSummaryUnsafe.Total = 1;
                        runSummaryUnsafe.Failed = 0;
                        runSummaryUnsafe.Skipped = 0;
                        runSummaryUnsafe.NotRun = 0;
                    }
                    else
                    {
                        Common.Log.Debug("XUnitTestMethodRunnerBaseRunTestCaseV3Integration: Flushing test: {TestCaseDisplayName}", testcase.TestCaseDisplayName);

                        // Let's update the summary to have only one test run
                        var passed = runSummaryUnsafe.Total - runSummaryUnsafe.Skipped - runSummaryUnsafe.Failed;
                        if (passed > 0)
                        {
                            runSummaryUnsafe.Total = 1;
                            runSummaryUnsafe.Failed = 0;
                            runSummaryUnsafe.Skipped = 0;
                            runSummaryUnsafe.NotRun = 0;
                        }
                        else if (runSummaryUnsafe.Skipped > 0)
                        {
                            runSummaryUnsafe.Total = 1;
                            runSummaryUnsafe.Skipped = 1;
                            runSummaryUnsafe.Failed = 0;
                            runSummaryUnsafe.NotRun = 0;
                        }
                        else if (runSummaryUnsafe.Failed > 0)
                        {
                            runSummaryUnsafe.Total = 1;
                            runSummaryUnsafe.Skipped = 0;
                            runSummaryUnsafe.Failed = 1;
                            runSummaryUnsafe.NotRun = 0;
                        }
                    }

                    messageBus.FlushMessages(testcase.TestMethod.UniqueID);

                    if (Common.Log.IsEnabled(LogEventLevel.Debug))
                    {
                        var debugMsg = $"EFD/Retry: Returned summary: {testcase.TestCaseDisplayName} [Total: {runSummaryUnsafe.Total}, Failed: {runSummaryUnsafe.Failed}, Skipped: {runSummaryUnsafe.Skipped}]";
                        Common.Log.Debug("XUnitTestMethodRunnerBaseRunTestCaseV3Integration: {Value}", debugMsg);
                    }
                }

                break;
            }

            // We report the result of a test as skipped to the testing framework if:
            // - Is a quarantined test, or
            // - Is a disabled test
            case { IsQuarantinedTest: true } or { IsDisabledTest: true }:
                Common.Log.Debug("XUnitTestMethodRunnerBaseRunTestCaseV3Integration: Quarantined or disabled test: {TestCaseDisplayName}", testcase.TestCaseDisplayName);
                runSummaryUnsafe.Total = 1;
                runSummaryUnsafe.Failed = 0;
                runSummaryUnsafe.Skipped = 1;
                runSummaryUnsafe.NotRun = 0;
                messageBus.FlushMessages(testcase.TestMethod.UniqueID);
                break;

            // For everything else, we just flush the messages
            default:
                Common.Log.Debug("XUnitTestMethodRunnerBaseRunTestCaseV3Integration: Flushing messages for: {TestCaseDisplayName}", testcase.TestCaseDisplayName);
                messageBus.FlushMessages(testcase.TestMethod.UniqueID);
                break;
        }

        return returnValue;
    }

    /// <summary>
    /// Read-only snapshot of remaining ATR budget for pre-close checks (XUnit v3).
    /// Value meanings: -1 = uninitialized, 0 = exhausted, positive = nominally available.
    /// This value is observed before retry scheduling decrements budget, so values of 1 or 0 mean no
    /// further retry can run after the current failed execution.
    /// </summary>
    internal static int GetRemainingAtrBudget()
        => Interlocked.CompareExchange(ref _totalRetries, 0, 0);

    private readonly struct TestRunnerState
    {
        public readonly RetryMessageBus MessageBus;
        public readonly TestCaseMetadata TestCaseMetadata;
        public readonly IXunitTestMethodRunnerBaseContextV3 Context;
        public readonly IXunitTestCaseV3 TestCase;

        public TestRunnerState(RetryMessageBus messageBus, TestCaseMetadata testCaseMetadata, IXunitTestMethodRunnerBaseContextV3 context, IXunitTestCaseV3 testCase)
        {
            MessageBus = messageBus;
            TestCaseMetadata = testCaseMetadata;
            Context = context;
            TestCase = testCase;
        }
    }

    private static class RunSummaryConverter<TReturn>
    {
        // ReSharper disable once StaticMemberInGenericType
        private static readonly bool IsCompatible;

        static RunSummaryConverter()
        {
            if (Marshal.SizeOf<TReturn>() != Marshal.SizeOf<RunSummaryUnsafeStruct>())
            {
                IsCompatible = false;
                return;
            }

            if (typeof(TReturn).GetFields().Length != 5)
            {
                IsCompatible = false;
                return;
            }

            IsCompatible = true;
        }

        public static bool TryGetEditableRunSummary(TReturn returnValue, out RunSummaryUnsafeStruct editableRunSummary)
        {
            editableRunSummary = default;
            if (!IsCompatible)
            {
                return false;
            }

            editableRunSummary = Unsafe.As<TReturn, RunSummaryUnsafeStruct>(ref returnValue);
            return true;
        }
    }
}
