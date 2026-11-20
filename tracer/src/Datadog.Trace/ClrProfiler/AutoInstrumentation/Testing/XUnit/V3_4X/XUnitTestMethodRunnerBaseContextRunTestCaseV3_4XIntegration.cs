// <copyright file="XUnitTestMethodRunnerBaseContextRunTestCaseV3_4XIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit.V3;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit.V3_4X;

/// <summary>
/// Instruments xUnit v3 4.x test case execution for skipping and retries.
/// </summary>
[InstrumentMethod(
    AssemblyName = "xunit.v3.core",
    TypeName = "Xunit.v3.XunitTestMethodRunnerBaseContext`2",
    MethodName = "RunTestCase",
    ParameterTypeNames = ["!1"],
    ReturnTypeName = "System.Threading.Tasks.ValueTask`1[Xunit.v3.RunSummary]",
    MinimumVersion = "4.0.0",
    MaximumVersion = "4.*.*",
    IntegrationName = XUnitIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class XUnitTestMethodRunnerBaseContextRunTestCaseV3_4XIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TTestCase>(TTarget instance, TTestCase testCaseOriginal)
    {
        if (!XUnitIntegration.IsEnabled || instance is null)
        {
            return CallTargetState.GetDefault();
        }

        var context = instance.DuckCast<IXunitTestMethodRunnerBaseContextV3_4X>();
        if (context?.Instance is null)
        {
            return CallTargetState.GetDefault();
        }

        var testOptimization = TestOptimization.Instance;
        var testCase = testCaseOriginal.DuckCast<IXunitTestCaseV3>()!;
        var testRunnerData = CreateTestRunnerData(testCaseOriginal, testCase, context.Aggregator);

        if (testRunnerData.SkipReason is not null)
        {
            return CallTargetState.GetDefault();
        }

        var isEarlyFlakeDetectionEnabled = testOptimization.EarlyFlakeDetectionFeature?.Enabled == true;
        var isFlakyRetryEnabled = testOptimization.FlakyRetryFeature?.Enabled == true;
        var isTestManagementEnabled = testOptimization.TestManagementFeature?.Enabled == true;
        var testManagementProperties = isTestManagementEnabled ? XUnitIntegration.GetTestManagementProperties(ref testRunnerData) : null;
        var isDisabledByTestManagement = Common.IsDisabledByTestManagement(testManagementProperties);

        if (Common.CanApplyItrSkip(testManagementProperties) &&
            XUnitIntegration.ShouldSkip(ref testRunnerData, out _, out _, out var skippableTest))
        {
            testCase.SkipReason = IntelligentTestRunnerTags.SkippedByReason;
            if (skippableTest is { } matchedSkippableTest)
            {
                Common.RecordTestSkipCoverageBackfill(matchedSkippableTest, XUnitIntegration.GetTestModuleName(ref testRunnerData));
            }
            else
            {
                Common.RecordTestSkipCoverageBackfill();
            }

            return CallTargetState.GetDefault();
        }

        if (!isEarlyFlakeDetectionEnabled && !isFlakyRetryEnabled && !isTestManagementEnabled)
        {
            return CallTargetState.GetDefault();
        }

        if (isFlakyRetryEnabled)
        {
            XUnitRetryCoordinator.InitializeRetryBudget(testOptimization);
        }

        if (context.MessageBus is not IDuckType { Instance: RetryMessageBus retryMessageBus })
        {
            return CallTargetState.GetDefault();
        }

        var testCaseMetadata = retryMessageBus.GetMetadata(testCase.UniqueID);
        testCaseMetadata.UsesRetryCoordinator = true;
        if (isDisabledByTestManagement)
        {
            testCase.SkipReason = "Flaky test is disabled by Datadog";
            testCaseMetadata.Skipped = true;
        }

        return new CallTargetState(null, new TestRunnerState(retryMessageBus, testCaseMetadata, context, testCase));
    }

    internal static async Task<TReturn> OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
    {
        if (state.State is not TestRunnerState { MessageBus: { } messageBus, TestCaseMetadata: { } testCaseMetadata, Context: { Instance: { } } context, TestCase: { Instance: { } } testCase })
        {
            return returnValue;
        }

        if (!RunSummaryConverter<TReturn>.TryGetEditableRunSummary(returnValue, out var unsafeSummary))
        {
            Common.Log.Error("xUnit v3 4.x: RunSummary layout is incompatible. Retries are disabled for {TestCaseDisplayName}.", testCase.TestCaseDisplayName);
            messageBus.FlushMessages(testCase.UniqueID);
            return returnValue;
        }

        var runSummary = new XUnitRunSummary
        {
            Total = unsafeSummary.Total,
            Failed = unsafeSummary.Failed,
            Skipped = unsafeSummary.Skipped,
            NotRun = unsafeSummary.NotRun,
            Time = unsafeSummary.Time,
        };

        runSummary = await XUnitRetryCoordinator.ProcessResultAsync(
                         messageBus,
                         testCaseMetadata,
                         testCase.TestCaseDisplayName,
                         runSummary,
                         new RetryRunner<TReturn>(context, testCase))
                     .ConfigureAwait(false);

        unsafeSummary.Total = runSummary.Total;
        unsafeSummary.Failed = runSummary.Failed;
        unsafeSummary.Skipped = runSummary.Skipped;
        unsafeSummary.NotRun = runSummary.NotRun;
        unsafeSummary.Time = runSummary.Time;
        return RunSummaryConverter<TReturn>.ToReturnValue(ref unsafeSummary);
    }

    internal static bool IsRunSummaryCompatible<TReturn>() => RunSummaryConverter<TReturn>.IsCompatible;

    private static TestRunnerStruct CreateTestRunnerData<TTestCase>(TTestCase original, IXunitTestCaseV3 testCase, IExceptionAggregator? aggregator)
    {
        return new TestRunnerStruct
        {
            TestClass = testCase.TestMethod.TestClass.Class,
            TestMethod = testCase.TestMethod.Method,
            TestMethodArguments = XUnitTestMethodRunnerBaseRunTestCaseV3Integration.GetTestCaseMethodArguments(original, testCase)!,
            TestCase = new CustomTestCase
            {
                DisplayName = testCase.TestCaseDisplayName,
                Traits = testCase.Traits.ToDictionary(keyValuePair => keyValuePair.Key, keyValuePair => keyValuePair.Value?.ToList()),
                UniqueID = testCase.UniqueID,
            },
            Aggregator = aggregator,
            SkipReason = testCase.SkipReason,
        };
    }

    private readonly struct RetryRunner<TReturn> : IXUnitRetryRunner
    {
        private readonly IXunitTestMethodRunnerBaseContextV3_4X _context;
        private readonly IXunitTestCaseV3 _testCase;

        public RetryRunner(IXunitTestMethodRunnerBaseContextV3_4X context, IXunitTestCaseV3 testCase)
        {
            _context = context;
            _testCase = testCase;
        }

        public async Task<XUnitRunSummary?> RunAsync()
        {
            var innerReturnValue = (TReturn)await _context.RunTestCase(_testCase.Instance!);
            if (!RunSummaryConverter<TReturn>.TryGetEditableRunSummary(innerReturnValue, out var innerSummary))
            {
                return null;
            }

            return new XUnitRunSummary
            {
                Total = innerSummary.Total,
                Failed = innerSummary.Failed,
                Skipped = innerSummary.Skipped,
                NotRun = innerSummary.NotRun,
                Time = innerSummary.Time,
            };
        }
    }

    private readonly struct TestRunnerState
    {
        public readonly RetryMessageBus MessageBus;
        public readonly TestCaseMetadata TestCaseMetadata;
        public readonly IXunitTestMethodRunnerBaseContextV3_4X Context;
        public readonly IXunitTestCaseV3 TestCase;

        public TestRunnerState(RetryMessageBus messageBus, TestCaseMetadata testCaseMetadata, IXunitTestMethodRunnerBaseContextV3_4X context, IXunitTestCaseV3 testCase)
        {
            MessageBus = messageBus;
            TestCaseMetadata = testCaseMetadata;
            Context = context;
            TestCase = testCase;
        }
    }

    private static class RunSummaryConverter<TReturn>
    {
        internal static readonly bool IsCompatible = CheckCompatibility();

        internal static bool TryGetEditableRunSummary(TReturn returnValue, out RunSummaryUnsafeStructV3_4X editableRunSummary)
        {
            editableRunSummary = default;
            if (!IsCompatible)
            {
                return false;
            }

            editableRunSummary = Unsafe.As<TReturn, RunSummaryUnsafeStructV3_4X>(ref returnValue);
            return true;
        }

        internal static TReturn ToReturnValue(ref RunSummaryUnsafeStructV3_4X runSummary)
            => Unsafe.As<RunSummaryUnsafeStructV3_4X, TReturn>(ref runSummary);

        private static bool CheckCompatibility()
        {
            var returnType = typeof(TReturn);
            if (!returnType.IsValueType || returnType.StructLayoutAttribute?.Value != LayoutKind.Sequential)
            {
                return false;
            }

            if (Marshal.SizeOf<TReturn>() != Marshal.SizeOf<RunSummaryUnsafeStructV3_4X>())
            {
                return false;
            }

            var fields = returnType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (fields.Length != 5)
            {
                return false;
            }

            return HasField(fields, typeof(long), 0) &&
                   HasField(fields, typeof(int), 8, "Total") &&
                   HasField(fields, typeof(int), 12, "Failed") &&
                   HasField(fields, typeof(int), 16, "Skipped") &&
                   HasField(fields, typeof(int), 20, "NotRun");

            bool HasField(FieldInfo[] candidateFields, Type fieldType, int offset, string? name = null)
            {
                return candidateFields.Any(
                    field => field.FieldType == fieldType &&
                             (name is null || field.Name == name) &&
                             Marshal.OffsetOf(returnType, field.Name).ToInt32() == offset);
            }
        }
    }
}
