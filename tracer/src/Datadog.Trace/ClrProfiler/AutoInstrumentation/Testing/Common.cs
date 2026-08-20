// <copyright file="Common.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Net;
using Datadog.Trace.Ci.Tagging;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing;

internal static class Common
{
    internal static readonly IDatadogLogger Log = TestOptimization.Instance.Log;

    internal static void ApplyRetryTags(TestSpanTags tags, bool isRetry, TestRetryMode retryMode)
    {
        if (!isRetry || retryMode == TestRetryMode.None)
        {
            tags.TestIsRetry = null;
            tags.TestRetryReason = null;
            return;
        }

        tags.TestIsRetry = "true";
        tags.TestRetryReason = retryMode switch
        {
            TestRetryMode.EarlyFlakeDetection => TestTags.TestRetryReasonEfd,
            TestRetryMode.AutomaticTestRetry => TestTags.TestRetryReasonAtr,
            TestRetryMode.AttemptToFix => TestTags.TestRetryReasonAttemptToFix,
            _ => null
        };
    }

    internal static bool IsDisabledByTestManagement(TestOptimizationClient.TestManagementResponseTestPropertiesAttributes? testManagementProperties)
        => testManagementProperties is { Disabled: true, AttemptToFix: false };

    internal static bool CanApplyItrSkip(TestOptimizationClient.TestManagementResponseTestPropertiesAttributes? testManagementProperties)
        => testManagementProperties is not { Quarantined: true } &&
           testManagementProperties is not { AttemptToFix: true } &&
           !IsDisabledByTestManagement(testManagementProperties);

    internal static string GetParametersValueData(object? paramValue)
    {
        if (paramValue is null)
        {
            return "(null)";
        }

        if (paramValue is string strValue)
        {
            return strValue;
        }

        if (paramValue is Array pValueArray)
        {
            const int maxArrayLength = 50;
            var length = pValueArray.Length > maxArrayLength ? maxArrayLength : pValueArray.Length;

            var strValueArray = new string[length];
            for (var i = 0; i < length; i++)
            {
                strValueArray[i] = GetParametersValueData(pValueArray.GetValue(i));
            }

            return "[" + string.Join(", ", strValueArray) + (pValueArray.Length > maxArrayLength ? ", ..." : string.Empty) + "]";
        }

        if (paramValue is Delegate pValueDelegate)
        {
            return $"{paramValue}[{pValueDelegate.Target}|{pValueDelegate.Method}]";
        }

        return paramValue.ToString() ?? "(null)";
    }

    internal static bool ShouldSkip(string testSuite, string testName, object[]? testMethodArguments, ParameterInfo[]? methodParameters, string? moduleName = null, string? metadataTestName = null, bool allowParametersMetadataMismatch = false)
        => ShouldSkip(testSuite, testName, testMethodArguments, methodParameters, out _, moduleName, metadataTestName, allowParametersMetadataMismatch);

    internal static bool ShouldSkip(string testSuite, string testName, object[]? testMethodArguments, ParameterInfo[]? methodParameters, out SkippableTest? skippableTest, string? moduleName = null, string? metadataTestName = null, bool allowParametersMetadataMismatch = false)
    {
        skippableTest = null;
        var currentContext = SynchronizationContext.Current;
        try
        {
            SynchronizationContext.SetSynchronizationContext(null);
            moduleName ??= TestModule.Current?.Tags.Bundle ?? TestModule.Current?.Tags.Module;
            var skippableTests = GetSkippableTestsFromSuiteAndNames(testSuite, testName, moduleName, metadataTestName);
            if (skippableTests.Count > 0)
            {
                foreach (var candidate in skippableTests)
                {
                    var parameters = candidate.GetParameters();

                    // Same test name and no parameters
                    if ((parameters?.Arguments is null || parameters.Arguments.Count == 0) &&
                        (testMethodArguments is null || testMethodArguments.Length == 0) &&
                        ParametersMetadataMatches(parameters, metadataTestName, allowParametersMetadataMismatch))
                    {
                        return CanSkipForCoverage(candidate, moduleName, out skippableTest);
                    }

                    if (parameters?.Arguments is not null &&
                        testMethodArguments is not null &&
                        methodParameters is not null)
                    {
                        if (parameters.Arguments.Count != methodParameters.Length)
                        {
                            continue;
                        }

                        var matchSignature = true;
                        for (var i = 0; i < methodParameters.Length; i++)
                        {
                            var targetValue = "(default)";
                            if (i < testMethodArguments.Length)
                            {
                                targetValue = GetParametersValueData(testMethodArguments[i]);
                            }

                            if (!parameters.Arguments.TryGetValue(methodParameters[i].Name ?? string.Empty, out var argValue))
                            {
                                matchSignature = false;
                                break;
                            }

                            if (argValue is not string strArgValue)
                            {
                                strArgValue = argValue?.ToString() ?? "(null)";
                            }

                            if (strArgValue != targetValue)
                            {
                                matchSignature = false;
                                break;
                            }
                        }

                        if (matchSignature)
                        {
                            if (ParametersMetadataMatches(parameters, metadataTestName, allowParametersMetadataMismatch))
                            {
                                return CanSkipForCoverage(candidate, moduleName, out skippableTest);
                            }
                        }
                    }
                }
            }
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(currentContext);
        }

        return false;
    }

    private static List<SkippableTest> GetSkippableTestsFromSuiteAndNames(string testSuite, string testName, string? moduleName, string? metadataTestName)
    {
        var skippableFeature = TestOptimization.Instance.SkippableFeature;
        if (skippableFeature is null)
        {
            return [];
        }

        var skippableTests = new List<SkippableTest>();
        var seen = new HashSet<SkippableTest>();
        AddSkippableTests(skippableTests, seen, skippableFeature.GetSkippableTestsFromSuiteAndName(testSuite, testName, moduleName));
        if (!StringUtil.IsNullOrEmpty(metadataTestName) &&
            !metadataTestName!.Equals(testName, StringComparison.Ordinal))
        {
            AddSkippableTests(skippableTests, seen, skippableFeature.GetSkippableTestsFromSuiteAndName(testSuite, metadataTestName, moduleName));
        }

        return skippableTests;
    }

    private static void AddSkippableTests(List<SkippableTest> skippableTests, HashSet<SkippableTest> seen, IList<SkippableTest>? candidates)
    {
        if (candidates is null)
        {
            return;
        }

        foreach (var candidate in candidates)
        {
            if (seen.Add(candidate))
            {
                skippableTests.Add(candidate);
            }
        }
    }

    private static bool ParametersMetadataMatches(TestParameters? parameters, string? metadataTestName, bool allowParametersMetadataMismatch)
    {
        if (parameters?.Metadata is null ||
            !parameters.Metadata.TryGetValue(TestTags.MetadataTestName, out var expectedMetadataTestName))
        {
            return true;
        }

        var expectedTestName = expectedMetadataTestName?.ToString();
        if (StringUtil.IsNullOrEmpty(expectedTestName))
        {
            return true;
        }

        if (allowParametersMetadataMismatch && StringUtil.IsNullOrEmpty(metadataTestName))
        {
            return false;
        }

        return !StringUtil.IsNullOrEmpty(metadataTestName) &&
               string.Equals(expectedTestName, metadataTestName, StringComparison.Ordinal);
    }

    /// <summary>
    /// Checks whether an ITR candidate can be skipped without making the active coverage report inaccurate.
    /// </summary>
    /// <param name="skippableTest">Backend skippable candidate matched to the current framework test.</param>
    /// <param name="moduleName">Local test module or bundle that is about to skip the test.</param>
    /// <param name="matchedSkippableTest">Backend candidate matched to the current framework test.</param>
    /// <returns>True when the test can be skipped safely.</returns>
    private static bool CanSkipForCoverage(SkippableTest skippableTest, string? moduleName, out SkippableTest? matchedSkippableTest)
    {
        matchedSkippableTest = skippableTest;
        var skippableFeature = TestOptimization.Instance.SkippableFeature;
        if (skippableFeature?.IsCoverageBackfillRequired() != true)
        {
            return true;
        }

        if (!skippableFeature.CanSkipWithCoverageBackfill(skippableTest, moduleName, out var reason))
        {
            Log.Debug("Common: Test cannot be skipped because coverage backfill is required but unsafe: {Reason}", reason);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Records coverage-backfill state once a framework integration has committed to skipping a test by ITR.
    /// </summary>
    /// <param name="moduleName">Local test module or bundle that skipped the test, or null to use the current module.</param>
    internal static void RecordTestSkipCoverageBackfill(string? moduleName = null)
    {
        var skippableFeature = TestOptimization.Instance.SkippableFeature;
        if (skippableFeature?.IsCoverageBackfillRequired() != true)
        {
            return;
        }

        moduleName ??= TestModule.Current?.Tags.Bundle ?? TestModule.Current?.Tags.Module;
        skippableFeature.RecordTestSkipCoverageBackfill(moduleName);
    }

    /// <summary>
    /// Records coverage-backfill state for the exact backend candidate that a framework actually skipped by ITR.
    /// </summary>
    /// <param name="skippableTest">Backend skippable candidate that was skipped.</param>
    /// <param name="moduleName">Local test module or bundle that skipped the test, or null to use the current module.</param>
    internal static void RecordTestSkipCoverageBackfill(SkippableTest skippableTest, string? moduleName)
    {
        var skippableFeature = TestOptimization.Instance.SkippableFeature;
        if (skippableFeature?.IsCoverageBackfillRequired() != true)
        {
            return;
        }

        moduleName ??= TestModule.Current?.Tags.Bundle ?? TestModule.Current?.Tags.Module;
        skippableFeature.RecordTestSkipCoverageBackfill(skippableTest, moduleName);
    }

    internal static int GetNumberOfExecutionsForDuration(TimeSpan duration)
    {
        var earlyFlakeDetectionFeature = TestOptimization.Instance.EarlyFlakeDetectionFeature;
        if (earlyFlakeDetectionFeature?.Enabled != true)
        {
            return 1;
        }

        int numberOfExecutions;
        var slowRetriesSettings = earlyFlakeDetectionFeature?.EarlyFlakeDetectionSettings.SlowTestRetries ?? default;
        if (slowRetriesSettings.FiveSeconds.HasValue && duration.TotalSeconds < 5)
        {
            numberOfExecutions = slowRetriesSettings.FiveSeconds.Value;
            Log.Information<int>("Common: EFD: Number of executions has been set to {Value} for this test that runs under 5 seconds.", numberOfExecutions);
        }
        else if (slowRetriesSettings.TenSeconds.HasValue && duration.TotalSeconds < 10)
        {
            numberOfExecutions = slowRetriesSettings.TenSeconds.Value;
            Log.Information<int>("Common: EFD: Number of executions has been set to {Value} for this test that runs under 10 seconds.", numberOfExecutions);
        }
        else if (slowRetriesSettings.ThirtySeconds.HasValue && duration.TotalSeconds < 30)
        {
            numberOfExecutions = slowRetriesSettings.ThirtySeconds.Value;
            Log.Information<int>("Common: EFD: Number of executions has been set to {Value} for this test that runs under 30 seconds.", numberOfExecutions);
        }
        else if (slowRetriesSettings.FiveMinutes.HasValue && duration.TotalMinutes < 5)
        {
            numberOfExecutions = slowRetriesSettings.FiveMinutes.Value;
            Log.Information<int>("Common: EFD: Number of executions has been set to {Value} for this test that runs under 5 minutes.", numberOfExecutions);
        }
        else
        {
            numberOfExecutions = 1;
            Log.Information("Common: EFD: Number of executions has been set to 1 (No retries). Current test duration is {Value}", duration);
        }

        return numberOfExecutions;
    }

    internal static void SetKnownTestsFeatureTags(Test test)
    {
        // Known tests feature
        var testOptimization = TestOptimization.Instance;
        if (testOptimization.KnownTestsFeature?.Enabled == true)
        {
            var isTestNew = !testOptimization.KnownTestsFeature.IsAKnownTest(test.Suite.Module.Name, test.Suite.Name, test.Name ?? string.Empty);
            if (isTestNew)
            {
                var testTags = test.GetTags();
                testTags.TestIsNew = "true";
            }
        }
    }

    internal static bool SetEarlyFlakeDetectionTestTagsAndAbortReason(Test test, bool isRetry, ref long newTestCases, ref long totalTestCases)
    {
        // Early flake detection flags
        var testOptimization = TestOptimization.Instance;
        var earlyFlakeDetectionEnabled = testOptimization.EarlyFlakeDetectionFeature?.Enabled == true;
        if (earlyFlakeDetectionEnabled)
        {
            var testTags = test.GetTags();
            if (testTags.TestIsNew == "true")
            {
                if (isRetry)
                {
                    ApplyRetryTags(testTags, isRetry, TestRetryMode.EarlyFlakeDetection);
                }
                else
                {
                    CheckFaultyThreshold(test, Interlocked.Increment(ref newTestCases), Interlocked.Read(ref totalTestCases));
                }
            }
            else
            {
                earlyFlakeDetectionEnabled = false;
            }
        }

        return earlyFlakeDetectionEnabled;
    }

    internal static bool SetFlakyRetryTags(Test test, bool isRetry)
    {
        var flakyRetryFeature = TestOptimization.Instance.FlakyRetryFeature?.Enabled == true;
        if (flakyRetryFeature && isRetry)
        {
            ApplyRetryTags(test.GetTags(), isRetry, TestRetryMode.AutomaticTestRetry);
        }

        return flakyRetryFeature;
    }

    internal static TestOptimizationClient.TestManagementResponseTestPropertiesAttributes SetTestManagementFeature(Test test, bool isRetry)
    {
        // Test management feature
        var testOptimization = TestOptimization.Instance;
        if (testOptimization.TestManagementFeature?.Enabled == true)
        {
            var testTags = test.GetTags();
            var testManagementProperties = testOptimization.TestManagementFeature.GetTestProperties(test.Suite.Module.Name, test.Suite.Name, test.Name ?? string.Empty);
            if (testManagementProperties.Quarantined)
            {
                Log.Debug("Common: Test is quarantined. [Suite: {SuiteName}, Test: {TestName}]", test.Suite.Name, test.Name);
                testTags.IsQuarantined = "true";
            }

            if (testManagementProperties.Disabled)
            {
                Log.Debug("Common: Test is disabled. [Suite: {SuiteName}, Test: {TestName}]", test.Suite.Name, test.Name);
                testTags.IsDisabled = "true";
            }

            if (testManagementProperties.AttemptToFix)
            {
                Log.Debug("Common: Test is an attempt to fix. [Suite: {SuiteName}, Test: {TestName}, IsRetry: {IsRetry}]", test.Suite.Name, test.Name, isRetry);
                testTags.IsAttemptToFix = "true";
                if (isRetry)
                {
                    ApplyRetryTags(testTags, isRetry, TestRetryMode.AttemptToFix);
                }
            }

            return testManagementProperties;
        }

        return TestOptimizationClient.TestManagementResponseTestPropertiesAttributes.Default;
    }

    internal static void CheckFaultyThreshold(Test test, long nTestCases, long tTestCases)
    {
        if (tTestCases > 0 && TestOptimization.Instance.EarlyFlakeDetectionFeature?.EarlyFlakeDetectionSettings.FaultySessionThreshold is { } faultySessionThreshold and > 0 and < 100)
        {
            if (((double)nTestCases * 100 / (double)tTestCases) > faultySessionThreshold)
            {
                /* Spec:
                 * If the number of new tests goes above a threshold:
                 *      We will stop the feature altogether: no more tests will be considered new and no retries will be done.
                 */

                // We need to stop the EFD feature off and set the session as a faulty.
                // But session object is not available from the test host
                test.SetTag(TestTags.TestIsNew, (string?)null);
                test.Suite?.Module?.TrySetSessionTag(EarlyFlakeDetectionTags.AbortReason, "faulty");
                Log.Warning<long, long, int>("EFD: The number of new tests goes above the Faulty Session Threshold. Disabling early flake detection for this session. [NewCases={NewCases}/TotalCases={TotalCases} | {FaltyThreshold}%]", nTestCases, tTestCases, faultySessionThreshold);
            }
        }
    }

    /// <summary>
    /// Calculates the final status for a test based on execution results and test management tags.
    /// Priority order (first match wins):
    /// 1. Quarantined/disabled -> skip (always mask to skip)
    /// 2. For ATF tests: any execution failed -> fail (flaky test = fix didn't work)
    /// 3. Any execution passed -> pass
    /// 4. Skip/inconclusive AND no pass -> skip
    /// 5. All executions failed -> fail
    /// </summary>
    /// <param name="anyExecutionPassed">True if any execution (initial or retry) passed.</param>
    /// <param name="anyExecutionFailed">True if any execution (initial or retry) failed.</param>
    /// <param name="isSkippedOrInconclusive">True if the current/last execution was skip or inconclusive.</param>
    /// <param name="testTags">The test tags to check for quarantine/disabled/ATF status.</param>
    /// <returns>The final status string: "pass", "fail", or "skip".</returns>
    internal static string CalculateFinalStatus(bool anyExecutionPassed, bool anyExecutionFailed, bool isSkippedOrInconclusive, TestSpanTags? testTags)
    {
        // Priority 1: Quarantined/disabled tests always mask to skip
        if (testTags?.IsQuarantined == "true" || testTags?.IsDisabled == "true")
        {
            return TestTags.StatusSkip;
        }

        // Priority 2: For ATF tests, any failure means fix didn't work (test is still flaky)
        // This must be checked BEFORE anyPassed for ATF tests
        if (testTags?.IsAttemptToFix == "true" && anyExecutionFailed)
        {
            return TestTags.StatusFail;
        }

        // Priority 3: Any execution passed -> pass (pass takes precedence over skip)
        if (anyExecutionPassed)
        {
            return TestTags.StatusPass;
        }

        // Priority 4: Skip/inconclusive AND no pass -> skip
        if (isSkippedOrInconclusive)
        {
            return TestTags.StatusSkip;
        }

        // Priority 5: All executions failed -> fail
        return TestTags.StatusFail;
    }
}
