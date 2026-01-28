// <copyright file="XUnitIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Net;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Configuration;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit;

internal static class XUnitIntegration
{
    internal const string IntegrationName = nameof(IntegrationId.XUnit);
    internal const IntegrationId IntegrationId = Configuration.IntegrationId.XUnit;

    private static readonly ConditionalWeakTable<Test, TestCaseMetadata?> TestCasesMetadata = new();
    private static long _totalTestCases;
    private static long _newTestCases;

    internal static bool IsEnabled => TestOptimization.Instance.IsRunning && Tracer.Instance.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId);

    internal static Test? CreateTest(ref TestRunnerStruct runnerInstance, TestCaseMetadata? testCaseMetadata = null)
    {
        // Get the test suite instance
        var testSuite = TestSuite.Current;
        if (testSuite is null)
        {
            Common.Log.Warning("XUnitIntegration: Test suite cannot be found.");
            return null;
        }

        var testMethod = runnerInstance.TestMethod;
        if (testMethod is not null)
        {
            // Prepare the method by jit-compiling it before running the test (if possible) and avoid the overhead of the first execution
            RuntimeHelpers.PrepareMethod(testMethod.MethodHandle);
        }

        var testOptimization = TestOptimization.Instance;
        var test = testSuite.CreateTest(testMethod?.Name ?? string.Empty);
        var testTags = test.GetTags();

        // Store test case metadata
#if NETCOREAPP3_1_OR_GREATER
        TestCasesMetadata.AddOrUpdate(test, testCaseMetadata);
#else
        TestCasesMetadata.Remove(test);
        TestCasesMetadata.Add(test, testCaseMetadata);
#endif

        // Get test parameters
        var testMethodArguments = runnerInstance.TestMethodArguments;
        var methodParameters = testMethod?.GetParameters();
        if (methodParameters?.Length > 0 && testMethodArguments?.Length > 0)
        {
            var testParameters = new TestParameters
            {
                Metadata = new Dictionary<string, object?>(),
                Arguments = new Dictionary<string, object?>()
            };
            testParameters.Metadata[TestTags.MetadataTestName] = runnerInstance.TestCase.DisplayName ?? string.Empty;

            for (var i = 0; i < methodParameters.Length; i++)
            {
                var key = methodParameters[i].Name ?? string.Empty;
                if (i < testMethodArguments.Length)
                {
                    testParameters.Arguments[key] = Common.GetParametersValueData(testMethodArguments[i]);
                }
                else
                {
                    testParameters.Arguments[key] = "(default)";
                }
            }

            test.SetParameters(testParameters);
        }

        // Get traits
        if (runnerInstance.TestCase.Traits is { } traits)
        {
            // Unskippable tests support
            if (testOptimization.Settings.IntelligentTestRunnerEnabled)
            {
                ShouldSkip(ref runnerInstance, out var isUnskippable, out var isForcedRun, traits);
                testTags.Unskippable = isUnskippable ? "true" : "false";
                testTags.ForcedRun = isForcedRun ? "true" : "false";
                traits.Remove(IntelligentTestRunnerTags.UnskippableTraitName);
            }

            test.SetTraits(traits);
        }
        else if (testOptimization.Settings.IntelligentTestRunnerEnabled)
        {
            // Unskippable tests support
            testTags.Unskippable = "false";
            testTags.ForcedRun = "false";
        }

        // Known tests
        var testIsNew = false;
        if (testOptimization.KnownTestsFeature?.Enabled == true)
        {
            testIsNew = !testOptimization.KnownTestsFeature.IsAKnownTest(test.Suite.Module.Name, test.Suite.Name, test.Name ?? string.Empty);
            if (testIsNew)
            {
                testTags.TestIsNew = "true";

                if (testCaseMetadata is null || testCaseMetadata.ExecutionIndex == 0)
                {
                    Interlocked.Increment(ref _newTestCases);
                }
            }
        }

        if (testCaseMetadata is not null)
        {
            // Early flake detection flags
            if (testOptimization.EarlyFlakeDetectionFeature?.Enabled == true)
            {
                testCaseMetadata.EarlyFlakeDetectionEnabled = testIsNew;
                if (testIsNew && testCaseMetadata.ExecutionIndex > 0)
                {
                    testTags.TestIsRetry = "true";
                    testTags.TestRetryReason = TestTags.TestRetryReasonEfd;
                }

                Common.CheckFaultyThreshold(test, Interlocked.Read(ref _newTestCases), Interlocked.Read(ref _totalTestCases));
            }

            var isRetry = testCaseMetadata is { ExecutionIndex: > 0 };

            // Flaky retries
            testCaseMetadata.FlakyRetryEnabled = Common.SetFlakyRetryTags(test, isRetry);

            // Test management feature
            var testManagementData = Common.SetTestManagementFeature(test, isRetry);
            testCaseMetadata.IsRetry = isRetry;
            testCaseMetadata.IsQuarantinedTest = testManagementData.Quarantined;
            testCaseMetadata.IsDisabledTest = testManagementData.Disabled;
            testCaseMetadata.IsAttemptToFix = testManagementData.AttemptToFix;
        }

        // Test code and code owners
        if (testMethod is not null)
        {
            test.SetTestMethodInfo(testMethod);
        }

        // Telemetry
        Tracer.Instance.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId);

        // Skip tests
        if (runnerInstance.SkipReason is { } skipReason)
        {
            // Set final_status = skip for pre-execution skipped tests (ITR/attribute-based skips)
            testTags.FinalStatus = TestTags.StatusSkip;
            test.Close(TestStatus.Skip, skipReason: skipReason, duration: TimeSpan.Zero);
            return null;
        }

        test.ResetStartTime();
        return test;
    }

    internal static void FinishTest(Test test, IExceptionAggregator? exceptionAggregator)
    {
        var clearExceptions = false;
        try
        {
            TimeSpan? duration = null;

            if (TestCasesMetadata.TryGetValue(test, out var testCaseMetadata) && testCaseMetadata is not null)
            {
                if (testCaseMetadata.EarlyFlakeDetectionEnabled)
                {
                    var testTags = test.GetTags();
                    if (testTags.TestIsNew == "true" && test.GetInternalSpan() is { } internalSpan)
                    {
                        duration = internalSpan.Context.TraceContext.Clock.ElapsedSince(internalSpan.StartTime);
                        if (duration.Value.TotalMinutes >= 5)
                        {
                            testTags.EarlyFlakeDetectionTestAbortReason = "slow";
                        }
                    }
                }

                clearExceptions = testCaseMetadata.IsDisabledTest || testCaseMetadata.IsQuarantinedTest;
            }

            if (exceptionAggregator?.ToException() is { } exception)
            {
                if (exception.GetType().Name == "SkipException")
                {
                    if (testCaseMetadata?.TotalExecutions > 1)
                    {
                        testCaseMetadata.AllRetriesFailed = false;
                    }

                    // Set Skipped flag for dynamic skips
                    if (testCaseMetadata is not null)
                    {
                        testCaseMetadata.Skipped = true;
                    }

                    WriteFinalTagsFromMetadata(test, testCaseMetadata, isSkip: true);

                    // Handle null metadata - set final_status for tests without retry features
                    if (testCaseMetadata is null && test.GetTags() is { } nullMetaSkipTags)
                    {
                        nullMetaSkipTags.FinalStatus = TestTags.StatusSkip;
                    }

                    var skipReason = exception.Message.Replace("$XunitDynamicSkip$", string.Empty);
                    test.Close(TestStatus.Skip, TimeSpan.Zero, skipReason);
                }
                else
                {
                    if (testCaseMetadata != null)
                    {
                        testCaseMetadata.HasAnException = true;
                        // ATF: AllAttemptsPassed clears only on actual failure (not skip)
                        if (testCaseMetadata.IsAttemptToFix)
                        {
                            testCaseMetadata.AllAttemptsPassed = false;
                        }

                        // Track initial execution failure for ATF final_status
                        if (testCaseMetadata.ExecutionIndex == 0)
                        {
                            testCaseMetadata.InitialExecutionFailed = true;
                        }
                    }

                    WriteFinalTagsFromMetadata(test, testCaseMetadata, isSkip: false);

                    // Handle null metadata - set final_status for tests without retry features
                    if (testCaseMetadata is null && test.GetTags() is { } nullMetaFailTags)
                    {
                        nullMetaFailTags.FinalStatus = TestTags.StatusFail;
                    }

                    if (Common.Log.IsEnabled(LogEventLevel.Debug))
                    {
                        var span = Tracer.Instance.ActiveScope?.Span;
                        Common.Log.Debug("XUnitIntegration: Reporting exception {ExceptionType} for test {TestName}", exception.GetType().FullName, test.Name);
                        Common.Log.Debug("XUnitIntegration: Tracer.ActiveScope: TraceId: {TraceId}, SpanId: {SpanId}, ResourceName: {ResourceName}", span?.TraceId, span?.SpanId, span?.ResourceName);
                    }

                    test.SetErrorInfo(exception);
                    test.Close(TestStatus.Fail, duration);
                }
            }
            else
            {
                // Test passed
                if (testCaseMetadata is not null)
                {
                    // Track pass status for final_status calculation
                    if (testCaseMetadata.ExecutionIndex == 0)
                    {
                        // Initial execution passed
                        testCaseMetadata.InitialExecutionPassed = true;
                    }
                    else
                    {
                        // Retry execution passed
                        testCaseMetadata.AnyRetryPassed = true;
                    }

                    if (testCaseMetadata.TotalExecutions > 1)
                    {
                        testCaseMetadata.AllRetriesFailed = false;
                    }
                }

                WriteFinalTagsFromMetadata(test, testCaseMetadata, isSkip: false);

                // Handle null metadata - set final_status for tests without retry features
                if (testCaseMetadata is null && test.GetTags() is { } nullMetaPassTags)
                {
                    nullMetaPassTags.FinalStatus = TestTags.StatusPass;
                }

                test.Close(TestStatus.Pass, duration);
            }
        }
        catch (Exception ex)
        {
            TestOptimization.Instance.Log.Warning(ex, "XUnitIntegration: Error finishing test scope");
            test.Close(TestStatus.Pass);
        }
        finally
        {
            if (clearExceptions)
            {
                exceptionAggregator?.Clear();
            }
        }
    }

    private static void WriteFinalTagsFromMetadata(Test test, TestCaseMetadata? testCaseMetadata, bool isSkip)
    {
        if (testCaseMetadata == null)
        {
            return;
        }

        var tags = test.GetTags();

        // Per-span guard to prevent duplicate setting
        if (tags.FinalStatus is not null)
        {
            return;
        }

        // Determine if this is a "final execution" for final_status calculation
        // XUnit has a timing issue: TotalExecutions is stale during initial EFD execution
        // (it's updated in OnAsyncMethodEnd AFTER FinishTest). Use guards to handle this.
        var isInitialEfdOrAtfExecution = testCaseMetadata.ExecutionIndex == 0 &&
                                          (testCaseMetadata.EarlyFlakeDetectionEnabled || testCaseMetadata.IsAttemptToFix);

        // Check if EFD/ATF will retry (even if TotalExecutions is not yet set)
        // If EFD is enabled for a new test, retries will happen regardless of initial result
        // If ATF is enabled, retries will happen regardless of initial result
        var willHaveRetries = isInitialEfdOrAtfExecution && testCaseMetadata.TotalExecutions <= 1;

        // ATR early exit detection
        var isAtrRetry = testCaseMetadata.IsRetry &&
                         tags.TestRetryReason == TestTags.TestRetryReasonAtr;
        var isAtrEarlyExit = isAtrRetry &&
                             !testCaseMetadata.HasAnException &&
                             !testCaseMetadata.Skipped &&
                             !testCaseMetadata.IsLastRetry;

        // ATR budget exhaustion detection (Edge Case 23)
        var isAtrBudgetExhausted = false;
        if (isAtrRetry && testCaseMetadata.HasAnException && !testCaseMetadata.IsLastRetry)
        {
            var remainingBudget = GetRemainingAtrBudget();
            isAtrBudgetExhausted = remainingBudget <= 1;
        }

        // Single-execution test: TotalExecutions == 1 means no retries were scheduled
        var isSingleExecution = testCaseMetadata.TotalExecutions == 1 && !willHaveRetries;

        var isFinalExecution = testCaseMetadata.IsLastRetry || isSingleExecution || isAtrEarlyExit || isAtrBudgetExhausted;

        if (!isFinalExecution)
        {
            return;
        }

        // Only set retry-specific tags for tests with actual retries
        if (testCaseMetadata.TotalExecutions > 1)
        {
            if (testCaseMetadata.AllRetriesFailed)
            {
                tags.HasFailedAllRetries = "true";
            }
        }

        // Calculate final_status
        // For single-execution tests, use HasAnException to determine pass/fail
        var anyExecutionPassed = testCaseMetadata.TotalExecutions == 1
            ? !testCaseMetadata.HasAnException && !testCaseMetadata.Skipped // Single: no exception and not skipped = passed
            : testCaseMetadata.InitialExecutionPassed || testCaseMetadata.AnyRetryPassed; // Retry: tracked values

        // For ATF: any actual failure (initial or retry) means the fix didn't work (test is still flaky)
        // Note: skip does NOT count as failure per ATF semantics
        var anyExecutionFailed = testCaseMetadata.TotalExecutions == 1
            ? testCaseMetadata.HasAnException && !testCaseMetadata.Skipped // Single: exception and not skip = failed
            : testCaseMetadata.InitialExecutionFailed || !testCaseMetadata.AllAttemptsPassed; // Retry: initial failed OR any retry failed

        var isSkippedOrInconclusive = isSkip || testCaseMetadata.Skipped;
        tags.FinalStatus = Common.CalculateFinalStatus(anyExecutionPassed, anyExecutionFailed, isSkippedOrInconclusive, tags);

        // ATF: AttemptToFixPassed should be consistent with final_status
        // If any execution failed, the fix didn't work
        if (testCaseMetadata.TotalExecutions > 1 && testCaseMetadata.IsAttemptToFix)
        {
            tags.AttemptToFixPassed = anyExecutionFailed ? "false" : "true";
        }
    }

    internal static bool ShouldSkip(ref TestRunnerStruct runnerInstance, out bool isUnskippable, out bool isForcedRun, Dictionary<string, List<string>?>? traits = null)
    {
        isUnskippable = false;
        isForcedRun = false;

        if (TestOptimization.Instance.Settings.IntelligentTestRunnerEnabled != true)
        {
            return false;
        }

        var testClassName = runnerInstance.TestClass?.ToString() ?? string.Empty;
        var testMethod = runnerInstance.TestMethod;
        var itrShouldSkip = Common.ShouldSkip(testClassName, testMethod?.Name ?? string.Empty, runnerInstance.TestMethodArguments, testMethod?.GetParameters());
        traits ??= runnerInstance.TestCase.Traits;
        isUnskippable = traits?.TryGetValue(IntelligentTestRunnerTags.UnskippableTraitName, out _) == true;
        isForcedRun = itrShouldSkip && isUnskippable;
        return itrShouldSkip && !isUnskippable;
    }

    internal static TestOptimizationClient.TestManagementResponseTestPropertiesAttributes GetTestManagementProperties(ref TestRunnerStruct runnerInstance)
    {
        var testOptimization = TestOptimization.Instance;
        if (testOptimization.TestManagementFeature?.Enabled == true)
        {
            var testAssembly = runnerInstance.TestClass?.Assembly.GetName().Name ?? string.Empty;
            var testClassName = runnerInstance.TestClass?.ToString() ?? string.Empty;
            var testMethod = runnerInstance.TestMethod?.Name ?? string.Empty;
            return testOptimization.TestManagementFeature.GetTestProperties(testAssembly, testClassName, testMethod);
        }

        return TestOptimizationClient.TestManagementResponseTestPropertiesAttributes.Default;
    }

    internal static void IncrementTotalTestCases()
    {
        Interlocked.Increment(ref _totalTestCases);
    }

    /// <summary>
    /// Unified read-only check of remaining ATR budget for pre-check before span closes.
    /// Uses Math.Max to handle both v2 and v3 scenarios (they have separate budgets).
    /// Returns -1 if budget is uninitialized, 0 if exhausted, or a positive number if available.
    /// </summary>
    internal static int GetRemainingAtrBudget()
    {
        var v2Budget = XUnitTestRunnerRunAsyncIntegration.GetRemainingAtrBudget();
        var v3Budget = V3.XUnitTestMethodRunnerBaseRunTestCaseV3Integration.GetRemainingAtrBudget();
        return Math.Max(v2Budget, v3Budget);
    }
}
