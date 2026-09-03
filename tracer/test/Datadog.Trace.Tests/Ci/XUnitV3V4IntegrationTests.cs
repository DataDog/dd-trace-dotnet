// <copyright file="XUnitV3V4IntegrationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit.V3;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit.V3V4;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

public class XUnitV3V4IntegrationTests
{
    [Fact]
    public void V3V4RunSummaryHasExpectedLayoutAndTimeEncoding()
    {
        Marshal.SizeOf<RunSummaryUnsafeStructV3V4>().Should().Be(24);
        Marshal.OffsetOf<RunSummaryUnsafeStructV3V4>(nameof(RunSummaryUnsafeStructV3V4.Total)).ToInt32().Should().Be(8);
        Marshal.OffsetOf<RunSummaryUnsafeStructV3V4>(nameof(RunSummaryUnsafeStructV3V4.Failed)).ToInt32().Should().Be(12);
        Marshal.OffsetOf<RunSummaryUnsafeStructV3V4>(nameof(RunSummaryUnsafeStructV3V4.Skipped)).ToInt32().Should().Be(16);
        Marshal.OffsetOf<RunSummaryUnsafeStructV3V4>(nameof(RunSummaryUnsafeStructV3V4.NotRun)).ToInt32().Should().Be(20);

        var summary = new RunSummaryUnsafeStructV3V4 { Time = 1.234m };

        summary.Time.Should().Be(1.234m);
    }

    [Fact]
    public void V3V4RunSummaryCompatibilityAcceptsExactLayout()
    {
        XUnitTestMethodRunnerBaseContextRunTestCaseV3V4Integration.IsRunSummaryCompatible<CompatibleV3V4RunSummary>().Should().BeTrue();
    }

    [Fact]
    public void V3V4RunSummaryCompatibilityRejectsV3Layout()
    {
        XUnitTestMethodRunnerBaseContextRunTestCaseV3V4Integration.IsRunSummaryCompatible<RunSummaryUnsafeStruct>().Should().BeFalse();
    }

    [Fact]
    public void V3RunSummaryCompatibilityRejectsV3V4Layout()
    {
        XUnitTestMethodRunnerBaseRunTestCaseV3Integration.IsRunSummaryCompatible<RunSummaryUnsafeStructV3V4>().Should().BeFalse();
    }

    [Fact]
    public void V3V4RunSummaryCompatibilityRejectsUnexpectedFields()
    {
        XUnitTestMethodRunnerBaseContextRunTestCaseV3V4Integration.IsRunSummaryCompatible<IncompatibleV3V4RunSummary>().Should().BeFalse();
    }

    [Theory]
    [InlineData(3, 2, 0, 1, 1, 1, 0, 0)]
    [InlineData(3, 1, 1, 0, 1, 0, 0, 0)]
    [InlineData(3, 0, 3, 0, 1, 0, 1, 0)]
    [InlineData(3, 3, 0, 0, 1, 1, 0, 0)]
    [InlineData(1, 0, 0, 1, 1, 0, 0, 1)]
    [InlineData(2, 0, 0, 1, 1, 0, 0, 0)]
    public void SharedRunSummaryNormalizesFrameworkResult(
        int total,
        int failed,
        int skipped,
        int notRun,
        int expectedTotal,
        int expectedFailed,
        int expectedSkipped,
        int expectedNotRun)
    {
        var summary = new XUnitRunSummary
        {
            Total = total,
            Failed = failed,
            Skipped = skipped,
            NotRun = notRun,
        };

        summary.NormalizeFrameworkResult();

        summary.Total.Should().Be(expectedTotal);
        summary.Failed.Should().Be(expectedFailed);
        summary.Skipped.Should().Be(expectedSkipped);
        summary.NotRun.Should().Be(expectedNotRun);
    }

    [Fact]
    public void SharedRunSummaryAggregatesEveryFieldAndNonZeroTime()
    {
        var summary = new XUnitRunSummary
        {
            Total = 2,
            Failed = 1,
            Skipped = 1,
            NotRun = 0,
            Time = 1.25m,
        };
        var retrySummary = new XUnitRunSummary
        {
            Total = 3,
            Failed = 0,
            Skipped = 1,
            NotRun = 2,
            Time = 2.75m,
        };

        summary.Aggregate(in retrySummary);

        summary.Total.Should().Be(5);
        summary.Failed.Should().Be(1);
        summary.Skipped.Should().Be(2);
        summary.NotRun.Should().Be(2);
        summary.Time.Should().Be(4m);
    }

    [Theory]
    [InlineData(false, false, 2, (int)XUnitRetryExecutionDecision.SuccessfulExecution, 2)]
    [InlineData(false, true, 2, (int)XUnitRetryExecutionDecision.NotRun, 2)]
    [InlineData(true, true, 2, (int)XUnitRetryExecutionDecision.NotRun, 2)]
    [InlineData(true, false, 2, (int)XUnitRetryExecutionDecision.Retry, 1)]
    [InlineData(true, false, 1, (int)XUnitRetryExecutionDecision.Retry, 0)]
    [InlineData(true, false, 0, (int)XUnitRetryExecutionDecision.RetryBudgetExhausted, 0)]
    public void SharedAutomaticRetryDecisionStopsAtTheExpectedBoundary(
        bool hasFailures,
        bool hasNotRun,
        int initialRetryBudget,
        int expectedDecision,
        int expectedRemainingBudget)
    {
        var metadata = new TestCaseMetadata("case", totalExecution: 2, countDownExecutionNumber: 1)
        {
            SelectedRetryMode = TestRetryMode.AutomaticTestRetry,
        };
        var retryBudget = initialRetryBudget;

        var decision = XUnitIntegration.GetRetryExecutionDecision(metadata, hasFailures, hasNotRun, ref retryBudget);

        decision.Should().Be((XUnitRetryExecutionDecision)expectedDecision);
        retryBudget.Should().Be(expectedRemainingBudget);
    }

    [Fact]
    public void SharedAutomaticRetryBudgetOnlyCountsScheduledRetries()
    {
        var metadata = new TestCaseMetadata("case", totalExecution: 4, countDownExecutionNumber: 3)
        {
            SelectedRetryMode = TestRetryMode.AutomaticTestRetry,
        };
        var retryBudget = 2;

        XUnitIntegration.GetRetryExecutionDecision(metadata, hasFailures: false, hasNotRun: false, ref retryBudget)
                        .Should().Be(XUnitRetryExecutionDecision.SuccessfulExecution);
        XUnitIntegration.GetRetryExecutionDecision(metadata, hasFailures: false, hasNotRun: true, ref retryBudget)
                        .Should().Be(XUnitRetryExecutionDecision.NotRun);
        retryBudget.Should().Be(2);

        XUnitIntegration.GetRetryExecutionDecision(metadata, hasFailures: true, hasNotRun: false, ref retryBudget)
                        .Should().Be(XUnitRetryExecutionDecision.Retry);
        XUnitIntegration.GetRetryExecutionDecision(metadata, hasFailures: true, hasNotRun: false, ref retryBudget)
                        .Should().Be(XUnitRetryExecutionDecision.Retry);
        XUnitIntegration.GetRetryExecutionDecision(metadata, hasFailures: true, hasNotRun: false, ref retryBudget)
                        .Should().Be(XUnitRetryExecutionDecision.RetryBudgetExhausted);
        retryBudget.Should().Be(0);
    }

    [Fact]
    public void SharedAutomaticRetryBudgetIsConsumedAtomicallyWithoutUnderflow()
    {
        const int initialRetryBudget = 8;
        const int executionCount = 32;
        var metadata = new TestCaseMetadata("case", totalExecution: executionCount + 1, countDownExecutionNumber: executionCount)
        {
            SelectedRetryMode = TestRetryMode.AutomaticTestRetry,
        };
        var retryBudget = initialRetryBudget;
        var scheduledRetries = 0;
        var exhaustedDecisions = 0;

        Parallel.For(
            0,
            executionCount,
            _ =>
            {
                var decision = XUnitIntegration.GetRetryExecutionDecision(metadata, hasFailures: true, hasNotRun: false, ref retryBudget);
                if (decision == XUnitRetryExecutionDecision.Retry)
                {
                    Interlocked.Increment(ref scheduledRetries);
                }
                else if (decision == XUnitRetryExecutionDecision.RetryBudgetExhausted)
                {
                    Interlocked.Increment(ref exhaustedDecisions);
                }
            });

        scheduledRetries.Should().Be(initialRetryBudget);
        exhaustedDecisions.Should().Be(executionCount - initialRetryBudget);
        retryBudget.Should().Be(0);
    }

    [Fact]
    public void SharedAutomaticRetryBudgetReservationIsStableUnderContention()
    {
        var metadata = new[]
        {
            CreateAutomaticRetryMetadata("case-1"),
            CreateAutomaticRetryMetadata("case-2"),
        };
        var decisions = new XUnitRetryExecutionDecision[metadata.Length];
        var retryBudget = 1;

        Parallel.For(
            0,
            metadata.Length,
            index => decisions[index] = XUnitIntegration.GetOrCreateRetryExecutionDecision(
                metadata[index],
                hasFailures: true,
                hasNotRun: false,
                ref retryBudget));

        decisions.Should().ContainSingle(decision => decision == XUnitRetryExecutionDecision.Retry);
        decisions.Should().ContainSingle(decision => decision == XUnitRetryExecutionDecision.RetryBudgetExhausted);
        retryBudget.Should().Be(0);

        for (var i = 0; i < metadata.Length; i++)
        {
            XUnitIntegration.GetOrCreateRetryExecutionDecision(metadata[i], hasFailures: false, hasNotRun: false, ref retryBudget)
                            .Should().Be(decisions[i]);
        }
    }

    [Fact]
    public void SharedRetryMetadataClearsOnlyPerAttemptStateBeforeRetry()
    {
        var metadata = CreateAutomaticRetryMetadata("case");
        metadata.HasAnException = true;
        metadata.InitialExecutionFailed = true;
        metadata.PendingRetryDecision = XUnitRetryExecutionDecision.Retry;

        metadata.PrepareForRetry();

        metadata.ExecutionIndex.Should().Be(1);
        metadata.HasAnException.Should().BeFalse();
        metadata.PendingRetryDecision.Should().BeNull();
        metadata.InitialExecutionFailed.Should().BeTrue();
    }

    [Fact]
    public void SharedRunSummaryKeepsFirstCompletedResultWhenNoExecutionPasses()
    {
        var failedThenSkipped = new XUnitRunSummary { Total = 1, Failed = 1 };
        var skipped = new XUnitRunSummary { Total = 1, Skipped = 1 };
        failedThenSkipped.Aggregate(in skipped);

        var skippedThenFailed = new XUnitRunSummary { Total = 1, Skipped = 1 };
        var failed = new XUnitRunSummary { Total = 1, Failed = 1 };
        skippedThenFailed.Aggregate(in failed);

        failedThenSkipped.NormalizeFrameworkResult();
        skippedThenFailed.NormalizeFrameworkResult();

        failedThenSkipped.Total.Should().Be(1);
        failedThenSkipped.Failed.Should().Be(1);
        failedThenSkipped.Skipped.Should().Be(0);
        failedThenSkipped.NotRun.Should().Be(0);
        skippedThenFailed.Total.Should().Be(1);
        skippedThenFailed.Failed.Should().Be(0);
        skippedThenFailed.Skipped.Should().Be(1);
        skippedThenFailed.NotRun.Should().Be(0);
    }

    [Fact]
    public async Task SharedRetryCoordinatorDoesNotRetryAfterThresholdAbort()
    {
        var innerBus = new RecordingMessageBus();
        using var retryBus = new RetryMessageBus(innerBus, totalExecutions: 2, executionNumber: 1);
        var metadata = retryBus.GetMetadata("case");
        metadata.SelectedRetryMode = TestRetryMode.EarlyFlakeDetection;
        metadata.AbortByThreshold = true;
        var summary = new XUnitRunSummary { Total = 1, Failed = 1, Time = 1m };

        var result = await XUnitRetryCoordinator.ProcessResultAsync(
                         retryBus,
                         metadata,
                         "case",
                         summary,
                         new UnexpectedRetryRunner());

        result.Total.Should().Be(summary.Total);
        result.Failed.Should().Be(summary.Failed);
        result.Skipped.Should().Be(summary.Skipped);
        result.NotRun.Should().Be(summary.NotRun);
        result.Time.Should().Be(summary.Time);
    }

    [Fact]
    public async Task RetryMessageBusKeepsConcurrentTestCasesIsolated()
    {
        var innerBus = new RecordingMessageBus();
        using var retryBus = new RetryMessageBus(innerBus, totalExecutions: 2, executionNumber: 1);
        var firstCase = retryBus.GetMetadata("case-1");
        var secondCase = retryBus.GetMetadata("case-2");

        await Task.WhenAll(
            Task.Run(() => QueueExecution(retryBus, "case-1", "method", passed: false, "first-1")),
            Task.Run(() => QueueExecution(retryBus, "case-2", "method", passed: true, "first-2")));

        firstCase.CountDownExecutionNumber = 0;
        secondCase.CountDownExecutionNumber = 0;

        await Task.WhenAll(
            Task.Run(() => QueueExecution(retryBus, "case-1", "method", passed: true, "retry-1")),
            Task.Run(() => QueueExecution(retryBus, "case-2", "method", passed: false, "retry-2")));

        await Task.WhenAll(
            Task.Run(() => retryBus.FlushMessages("case-1")),
            Task.Run(() => retryBus.FlushMessages("case-2")));

        GetCaseValues(innerBus, "case-1").Should().Equal("output-retry-1", "retry-1");
        GetCaseValues(innerBus, "case-2").Should().Equal("output-first-2", "first-2");
        innerBus.Messages.OfType<TestResult>().Should().NotContain(
            message => message.Value == "output-first-1" ||
                       message.Value == "first-1" ||
                       message.Value == "output-retry-2" ||
                       message.Value == "retry-2");
    }

    [Fact]
    public void RetryMessageBusSelectsFirstExecutionWhenEveryAttemptFails()
    {
        var innerBus = new RecordingMessageBus();
        using var retryBus = new RetryMessageBus(innerBus, totalExecutions: 2, executionNumber: 1);
        var metadata = retryBus.GetMetadata("case");

        QueueExecution(retryBus, "case", "method", passed: false, "first");
        metadata.CountDownExecutionNumber = 0;
        QueueExecution(retryBus, "case", "method", passed: false, "retry");

        retryBus.FlushMessages("case").Should().BeTrue();

        GetCaseValues(innerBus, "case").Should().Equal("output-first", "first");
    }

    [Fact]
    public void RetryMessageBusSelectsExecutionMatchingFrameworkResult()
    {
        var innerBus = new RecordingMessageBus();
        using var retryBus = new RetryMessageBus(innerBus, totalExecutions: 2, executionNumber: 1);
        var metadata = retryBus.GetMetadata("case");

        QueueExecution(retryBus, "case", "method", passed: false, "first");
        metadata.CountDownExecutionNumber = 0;
        retryBus.QueueMessage(new TestOutput("case", "method", "output-retry"));
        retryBus.QueueMessage(new TestSkipped("case", "method", "retry"));

        retryBus.FlushMessages("case", XUnitFrameworkResult.Skipped).Should().BeTrue();

        GetCaseValues(innerBus, "case").Should().Equal("output-retry", "retry");
        innerBus.Messages.OfType<TestFailed>().Should().BeEmpty();
    }

    [Fact]
    public async Task RetryMessageBusFlushesEachCaseOnlyOnce()
    {
        var innerBus = new RecordingMessageBus();
        using var retryBus = new RetryMessageBus(innerBus, totalExecutions: 1, executionNumber: 0);
        retryBus.QueueMessage(new TestPassed("case", "method", "result"));

        await Task.WhenAll(
            Task.Run(() => retryBus.FlushMessages("case")),
            Task.Run(() => retryBus.FlushMessages("case")));

        innerBus.Messages.OfType<TestPassed>().Should().ContainSingle(message => message.Value == "result");
    }

    [Fact]
    public void RetryMessageBusDisposeFlushesPendingCasesAndDisposesInnerBusOnce()
    {
        var innerBus = new RecordingMessageBus();
        var retryBus = new RetryMessageBus(innerBus, totalExecutions: 1, executionNumber: 0);
        retryBus.QueueMessage(new TestPassed("case", "method", "result"));

        retryBus.Dispose();
        retryBus.Dispose();

        innerBus.Messages.OfType<TestPassed>().Should().ContainSingle(message => message.Value == "result");
        innerBus.DisposeCount.Should().Be(1);
        retryBus.QueueMessage(new TestPassed("case", "method", "late-result")).Should().BeFalse();
        innerBus.Messages.OfType<TestPassed>().Should().NotContain(message => message.Value == "late-result");
    }

    [Fact]
    public void RetryMessageBusDisposeDoesNotCallInnerBusUnderLifecycleLock()
    {
        var innerBus = new RecordingMessageBus();
        var retryBus = new RetryMessageBus(innerBus, totalExecutions: 1, executionNumber: 0);
        var lifecycleLock = typeof(RetryMessageBus).GetField("_lifecycleLock", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(retryBus)!;
        innerBus.BeforeQueueMessage = () => Monitor.IsEntered(lifecycleLock).Should().BeFalse();
        innerBus.BeforeDispose = () => Monitor.IsEntered(lifecycleLock).Should().BeFalse();
        retryBus.QueueMessage(new TestPassed("case", "method", "result"));

        retryBus.Dispose();

        innerBus.Messages.OfType<TestPassed>().Should().ContainSingle(message => message.Value == "result");
        innerBus.DisposeCount.Should().Be(1);
    }

    [Fact]
    public void RetryMessageBusForwardsMethodOnlyMessagesImmediately()
    {
        var innerBus = new RecordingMessageBus();
        using var retryBus = new RetryMessageBus(innerBus, totalExecutions: 1, executionNumber: 0);
        var message = new TestMethodMessage("method");

        retryBus.QueueMessage(message).Should().BeTrue();

        innerBus.Messages.Should().ContainSingle().Which.Should().BeSameAs(message);
    }

    [Fact]
    public void RetryMessageBusFindsExistingMetadataWithoutCreatingMissingMetadata()
    {
        var innerBus = new RecordingMessageBus();
        using var retryBus = new RetryMessageBus(innerBus, totalExecutions: 1, executionNumber: 0);

        retryBus.TryGetMetadata("missing-case", out var missingMetadata).Should().BeFalse();
        missingMetadata.Should().BeNull();

        var createdMetadata = retryBus.GetMetadata("existing-case");

        retryBus.TryGetMetadata("existing-case", out var existingMetadata).Should().BeTrue();
        existingMetadata.Should().BeSameAs(createdMetadata);
    }

    private static void QueueExecution(RetryMessageBus retryBus, string testCaseUniqueID, string testMethodUniqueID, bool passed, string value)
    {
        retryBus.QueueMessage(new TestOutput(testCaseUniqueID, testMethodUniqueID, $"output-{value}"));
        retryBus.QueueMessage(
            passed ?
                new TestPassed(testCaseUniqueID, testMethodUniqueID, value) :
                new TestFailed(testCaseUniqueID, testMethodUniqueID, value));
    }

    private static TestCaseMetadata CreateAutomaticRetryMetadata(string uniqueID)
        => new(uniqueID, totalExecution: 4, countDownExecutionNumber: 3)
        {
            SelectedRetryMode = TestRetryMode.AutomaticTestRetry,
        };

    private static IEnumerable<string> GetCaseValues(RecordingMessageBus innerBus, string testCaseUniqueID)
        => innerBus.Messages.OfType<TestResult>()
                   .Where(message => message.TestCaseUniqueID == testCaseUniqueID)
                   .Select(message => message.Value);

    [StructLayout(LayoutKind.Sequential)]
#pragma warning disable SA1202
    private struct CompatibleV3V4RunSummary
    {
#pragma warning disable CS0169
        private long _timeInMilliseconds;
#pragma warning restore CS0169

        public int Total;
        public int Failed;
        public int Skipped;
        public int NotRun;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IncompatibleV3V4RunSummary
    {
#pragma warning disable CS0169
        private long _timeInMilliseconds;
#pragma warning restore CS0169

        public int Total;
        public int Failed;
        public int Skipped;
        public long NotRun;
    }
#pragma warning restore SA1202

    private readonly struct UnexpectedRetryRunner : IXUnitRetryRunner
    {
        public Task<XUnitRunSummary?> RunAsync()
            => throw new InvalidOperationException("The retry runner must not be called after a threshold abort");
    }

    private abstract class TestResult
    {
        protected TestResult(string testCaseUniqueID, string testMethodUniqueID, string value)
        {
            TestCaseUniqueID = testCaseUniqueID;
            TestMethodUniqueID = testMethodUniqueID;
            Value = value;
        }

        public string TestCaseUniqueID { get; }

        public string TestMethodUniqueID { get; }

        public string Value { get; }
    }

    private sealed class TestPassed(string testCaseUniqueID, string testMethodUniqueID, string value)
        : TestResult(testCaseUniqueID, testMethodUniqueID, value);

    private sealed class TestFailed(string testCaseUniqueID, string testMethodUniqueID, string value)
        : TestResult(testCaseUniqueID, testMethodUniqueID, value);

    private sealed class TestSkipped(string testCaseUniqueID, string testMethodUniqueID, string value)
        : TestResult(testCaseUniqueID, testMethodUniqueID, value);

    private sealed class TestOutput(string testCaseUniqueID, string testMethodUniqueID, string value)
        : TestResult(testCaseUniqueID, testMethodUniqueID, value);

    private sealed class TestMethodMessage(string testMethodUniqueID)
    {
        public string TestMethodUniqueID { get; } = testMethodUniqueID;
    }

    private sealed class RecordingMessageBus : IMessageBus
    {
        private readonly ConcurrentQueue<object> _messages = new();
        private int _disposeCount;

        public Action? BeforeQueueMessage { get; set; }

        public Action? BeforeDispose { get; set; }

        public IReadOnlyCollection<object> Messages => _messages.ToArray();

        public int DisposeCount => _disposeCount;

        public bool QueueMessage(object? message)
        {
            BeforeQueueMessage?.Invoke();
            if (message is not null)
            {
                _messages.Enqueue(message);
            }

            return true;
        }

        public void Dispose()
        {
            BeforeDispose?.Invoke();
            Interlocked.Increment(ref _disposeCount);
        }
    }
}
