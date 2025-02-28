// <copyright file="Common.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Reflection;
using System.Threading;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing;

internal static class Common
{
    internal static readonly IDatadogLogger Log = TestOptimization.Instance.Log;

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

    internal static bool ShouldSkip(string testSuite, string testName, object[]? testMethodArguments, ParameterInfo[]? methodParameters)
    {
        var currentContext = SynchronizationContext.Current;
        try
        {
            SynchronizationContext.SetSynchronizationContext(null);
            var skippableTests = TestOptimization.Instance.SkippableFeature?.GetSkippableTestsFromSuiteAndName(testSuite, testName) ?? [];
            if (skippableTests.Count > 0)
            {
                foreach (var skippableTest in skippableTests)
                {
                    var parameters = skippableTest.GetParameters();

                    // Same test name and no parameters
                    if ((parameters?.Arguments is null || parameters.Arguments.Count == 0) &&
                        (testMethodArguments is null || testMethodArguments.Length == 0))
                    {
                        return true;
                    }

                    if (parameters?.Arguments is not null &&
                        testMethodArguments is not null &&
                        methodParameters is not null)
                    {
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
                            return true;
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

    internal static void SetEarlyFlakeDetectionTestTagsAndAbortReason(Test test, bool isRetry, ref long newTestCases, ref long totalTestCases)
    {
        // Early flake detection flags
        var testOptimization = TestOptimization.Instance;
        if (testOptimization.EarlyFlakeDetectionFeature?.Enabled == true)
        {
            var testTags = test.GetTags();
            if (testTags.TestIsNew == "true")
            {
                if (isRetry)
                {
                    testTags.TestIsRetry = "true";
                    testTags.TestRetryReason = "efd";
                }
                else
                {
                    CheckFaultyThreshold(test, Interlocked.Increment(ref newTestCases), Interlocked.Read(ref totalTestCases));
                }
            }
        }
    }

    internal static void SetFlakyRetryTags(Test test, bool isRetry)
    {
        if (TestOptimization.Instance.FlakyRetryFeature?.Enabled == true && isRetry)
        {
            var testTags = test.GetTags();
            testTags.TestIsRetry = "true";
            testTags.TestRetryReason = "atr";
        }
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
}
