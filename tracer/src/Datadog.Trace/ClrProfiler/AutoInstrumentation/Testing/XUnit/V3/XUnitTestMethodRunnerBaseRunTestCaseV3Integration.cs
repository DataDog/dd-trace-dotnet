// <copyright file="XUnitTestMethodRunnerBaseRunTestCaseV3Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

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
            TestMethodArguments = GetTestCaseMethodArguments(testcaseOriginal, testcase)!,
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

        var isEarlyFlakeDetectionEnabled = testOptimization.EarlyFlakeDetectionFeature?.Enabled == true;
        var isFlakyRetryEnabled = testOptimization.FlakyRetryFeature?.Enabled == true;
        var isTestManagementEnabled = testOptimization.TestManagementFeature?.Enabled == true;
        var testManagementProperties = isTestManagementEnabled ? XUnitIntegration.GetTestManagementProperties(ref testRunnerData) : null;
        var isDisabledByTestManagement = Common.IsDisabledByTestManagement(testManagementProperties);

        // Check if the test should be skipped by the ITR
        if (Common.CanApplyItrSkip(testManagementProperties) &&
            XUnitIntegration.ShouldSkip(ref testRunnerData, out _, out _, out var skippableTest))
        {
            Common.Log.Debug("XUnitTestMethodRunnerBaseRunTestCaseV3Integration: Test skipped by test skipping feature: {Class}.{Name}", testcase.TestClass?.ToString() ?? string.Empty, testcase.TestMethod?.Method.Name ?? string.Empty);
            // Refresh values after skip reason change, and create Skip by ITR span.
            testcase.SkipReason = IntelligentTestRunnerTags.SkippedByReason;
            testRunnerData.SkipReason = testcase.SkipReason;
            if (skippableTest is { } matchedSkippableTest)
            {
                var moduleName = XUnitIntegration.GetTestModuleName(ref testRunnerData);
                Common.RecordTestSkipCoverageBackfill(matchedSkippableTest, moduleName);
            }
            else
            {
                Common.RecordTestSkipCoverageBackfill();
            }

            XUnitIntegration.CreateTest(ref testRunnerData);
            return CallTargetState.GetDefault();
        }

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
            XUnitRetryCoordinator.InitializeRetryBudget(testOptimization);
        }

        // If we have a RetryMessageBus means that we are in a retry context
        if (context.MessageBus is IDuckType { Instance: { } and RetryMessageBus retryMessageBus })
        {
            var testCaseMetadata = retryMessageBus.GetMetadata(testcase.UniqueID);

            // We skip the test if the tesk management property is set to Disabled and there's no attempt to fix
            if (isDisabledByTestManagement)
            {
                testcase.SkipReason = "Flaky test is disabled by Datadog";
                testRunnerData.SkipReason = testcase.SkipReason;
                testCaseMetadata.Skipped = true;
                Common.Log.Debug("XUnitTestMethodRunnerBaseRunTestCaseV3Integration: Skipping test: {Class}.{Name} Reason: {Reason}", testcase.TestClass?.ToString() ?? string.Empty, testcase.TestMethod.Method.Name, testcase.SkipReason);
                XUnitIntegration.CreateTest(ref testRunnerData, testCaseMetadata);
            }

            return new CallTargetState(null, new TestRunnerState(retryMessageBus, testCaseMetadata, context, testcase));
        }

        return CallTargetState.GetDefault();
    }

    internal static async Task<TReturn> OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
    {
        // If the state is not a TestRunnerState, we just return the original value
        if (instance is null || state.State is not TestRunnerState { MessageBus: { } messageBus, TestCaseMetadata: { } testCaseMetadata, Context: { Instance: { } } context, TestCase: { Instance: { } } testcase })
        {
            return returnValue;
        }

        if (!RunSummaryConverter<TReturn>.TryGetEditableRunSummary(returnValue, out var runSummaryUnsafe))
        {
            Common.Log.Debug("XUnitTestMethodRunnerBaseRunTestCaseV3Integration: TryGetEditableRunSummary failed. Flushing messages for: {TestCaseDisplayName}", testcase.TestCaseDisplayName);
            messageBus.FlushMessages(testcase.UniqueID);
            return returnValue;
        }

        var runSummary = new XUnitRunSummary
        {
            Total = runSummaryUnsafe.Total,
            Failed = runSummaryUnsafe.Failed,
            Skipped = runSummaryUnsafe.Skipped,
            NotRun = runSummaryUnsafe.NotRun,
            Time = runSummaryUnsafe.Time,
        };

        runSummary = await XUnitRetryCoordinator.ProcessResultAsync(
                         messageBus,
                         testCaseMetadata,
                         testcase.TestCaseDisplayName,
                         runSummary,
                         new RetryRunner<TTarget, TReturn>(instance, context, testcase))
                     .ConfigureAwait(false);

        runSummaryUnsafe.Total = runSummary.Total;
        runSummaryUnsafe.Failed = runSummary.Failed;
        runSummaryUnsafe.Skipped = runSummary.Skipped;
        runSummaryUnsafe.NotRun = runSummary.NotRun;
        runSummaryUnsafe.Time = runSummary.Time;

        return RunSummaryConverter<TReturn>.ToReturnValue(ref runSummaryUnsafe);
    }

    /// <summary>
    /// Read-only snapshot of remaining ATR budget for pre-close checks (XUnit v3).
    /// Value meanings: -1 = uninitialized, 0 = exhausted, positive = available retry slots.
    /// This value is observed before retry scheduling consumes a slot, so a value of 1 permits one
    /// final retry and a value of 0 permits none.
    /// </summary>
    internal static int GetRemainingAtrBudget()
        => XUnitRetryCoordinator.GetRemainingAtrBudget();

    internal static bool IsRunSummaryCompatible<TReturn>()
        => RunSummaryConverter<TReturn>.IsCompatible;

    /// <summary>
    /// Gets row-specific test method arguments from the test case when xUnit exposes them.
    /// </summary>
    /// <param name="testcaseOriginal">Original xUnit test case instance.</param>
    /// <param name="testcase">Duck-typed xUnit test case.</param>
    /// <typeparam name="TTestCase">Original xUnit test case type.</typeparam>
    /// <returns>Arguments attached to the current test case, or the method-level fallback.</returns>
    internal static object?[]? GetTestCaseMethodArguments<TTestCase>(TTestCase testcaseOriginal, IXunitTestCaseV3 testcase)
    {
        return testcaseOriginal.TryDuckCast<IXunitTestCaseMethodArgumentsV3>(out var testCaseWithMethodArguments) ?
                   testCaseWithMethodArguments.TestMethodArguments :
                   testcase.TestMethod.TestMethodArguments;
    }

    private readonly struct RetryRunner<TTarget, TReturn> : IXUnitRetryRunner
    {
        private readonly TTarget _instance;
        private readonly IXunitTestMethodRunnerBaseContextV3 _context;
        private readonly IXunitTestCaseV3 _testCase;

        public RetryRunner(TTarget instance, IXunitTestMethodRunnerBaseContextV3 context, IXunitTestCaseV3 testCase)
        {
            _instance = instance;
            _context = context;
            _testCase = testCase;
        }

        public async Task<XUnitRunSummary?> RunAsync()
        {
            var methodRunner = _instance!.DuckCast<IXunitTestMethodRunnerV3>();
            var innerReturnValue = (TReturn)await methodRunner.RunTestCase(_context.Instance!, _testCase.Instance!);
            if (!RunSummaryConverter<TReturn>.TryGetEditableRunSummary(innerReturnValue, out var innerRunSummary))
            {
                return null;
            }

            return new XUnitRunSummary
            {
                Total = innerRunSummary.Total,
                Failed = innerRunSummary.Failed,
                Skipped = innerRunSummary.Skipped,
                NotRun = innerRunSummary.NotRun,
                Time = innerRunSummary.Time,
            };
        }
    }

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
        internal static readonly bool IsCompatible;

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

        /// <summary>
        /// Converts the edited unsafe run summary back to the framework return type.
        /// </summary>
        /// <param name="runSummary">Edited run summary value.</param>
        /// <returns>Run summary represented as the original framework return type.</returns>
        public static TReturn ToReturnValue(ref RunSummaryUnsafeStruct runSummary)
        {
            return Unsafe.As<RunSummaryUnsafeStruct, TReturn>(ref runSummary);
        }
    }
}
