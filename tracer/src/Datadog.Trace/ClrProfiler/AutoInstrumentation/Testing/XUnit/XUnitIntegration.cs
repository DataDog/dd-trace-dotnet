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
                    testTags.TestRetryReason = "efd";
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

                    WriteFinalTagsFromMetadata(test, testCaseMetadata);
                    var skipReason = exception.Message.Replace("$XunitDynamicSkip$", string.Empty);
                    test.Close(TestStatus.Skip, TimeSpan.Zero, skipReason);
                }
                else
                {
                    if (testCaseMetadata != null)
                    {
                        testCaseMetadata.HasAnException = true;
                        if (testCaseMetadata.IsAttemptToFix)
                        {
                            testCaseMetadata.AllAttemptsPassed = false;
                        }
                    }

                    WriteFinalTagsFromMetadata(test, testCaseMetadata);
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
                if (testCaseMetadata?.TotalExecutions > 1)
                {
                    testCaseMetadata.AllRetriesFailed = false;
                }

                WriteFinalTagsFromMetadata(test, testCaseMetadata);
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

    private static void WriteFinalTagsFromMetadata(Test test, TestCaseMetadata? testCaseMetadata)
    {
        if (testCaseMetadata == null)
        {
            return;
        }

        var tags = test.GetTags();
        if (!testCaseMetadata.IsLastRetry)
        {
            return;
        }

        if (testCaseMetadata.IsAttemptToFix)
        {
            tags.AttemptToFixPassed = testCaseMetadata.AllAttemptsPassed ? "true" : "false";
        }

        if (testCaseMetadata.AllRetriesFailed)
        {
            tags.HasFailedAllRetries = "true";
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
}
