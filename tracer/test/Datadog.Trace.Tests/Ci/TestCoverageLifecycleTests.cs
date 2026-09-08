// <copyright file="TestCoverageLifecycleTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Ci.Coverage;
using Datadog.Trace.Ci.Coverage.Metadata;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Logging;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

[Collection(nameof(CoverageGlobalStateTestCollection))]
public class TestCoverageLifecycleTests : SettingsTestsBase
{
    [Fact]
    public void CompletedExecutionRemainsOpenUntilClose()
    {
        var handler = new CountingCoverageEventHandler();
        using var harness = new TestHarness(handler);
        var previousScope = Tracer.Instance.InternalActiveScope;
        var test = harness.Suite.CreateTest("native-retry-attempt");
        var duration = TimeSpan.FromMilliseconds(12);

        test.UnsafeFinishExecution(TestStatus.Fail, duration, skipReason: null);

        test.IsClosed.Should().BeFalse();
        Test.ActiveTests.Should().Contain(test);
        handler.FinishedCount.Should().Be(1);
        handler.Container.Should().BeNull();
        Tracer.Instance.InternalActiveScope!.Span.Should().BeSameAs(test.GetInternalSpan());
        test.GetInternalSpan().IsFinished.Should().BeFalse();
        test.GetTags().FinalStatus = "fail";
        test.Close(TestStatus.Fail);
        test.IsClosed.Should().BeTrue();
        Tracer.Instance.InternalActiveScope.Should().BeSameAs(previousScope);
        test.GetInternalSpan().IsFinished.Should().BeTrue();
        test.GetInternalSpan().Duration.Should().Be(duration);
        handler.FinishedCount.Should().Be(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompletionCallbacksRunOnceBeforeTheSpanIsClosed(bool finishExecutionEarly)
    {
        var handler = new CountingCoverageEventHandler();
        using var harness = new TestHarness(handler);
        var test = harness.Suite.CreateTest("completion-callbacks");
        var callbackCount = 0;
        var callbackObservedFinishedSpan = false;
        string? callbackStatus = null;
        test.AddOnExecutionCompletedAction(
            t =>
            {
                callbackCount++;
                callbackObservedFinishedSpan |= t.GetInternalSpan().IsFinished;
                callbackStatus = t.GetTags().Status;
            });

        if (finishExecutionEarly)
        {
            test.UnsafeFinishExecution(TestStatus.Fail, TimeSpan.FromMilliseconds(12), null);
            test.UnsafeFinishExecution(TestStatus.Pass, TimeSpan.FromSeconds(10), null);
            callbackCount.Should().Be(1);
            test.IsClosed.Should().BeFalse();
        }

        test.Close(TestStatus.Fail);
        test.Close(TestStatus.Fail);

        callbackCount.Should().Be(1);
        callbackObservedFinishedSpan.Should().BeFalse();
        callbackStatus.Should().Be("fail");
        handler.FinishedCount.Should().Be(1);
        test.GetInternalSpan().IsFinished.Should().BeTrue();
        Test.ActiveTests.Should().NotContain(test);
    }

    [Fact]
    public void ShutdownClosePreservesTheCompletedAttemptsDurationAndOutcome()
    {
        using var harness = new TestHarness(new CountingCoverageEventHandler());
        var test = harness.Suite.CreateTest("completed-before-shutdown");
        var duration = TimeSpan.FromMilliseconds(12);
        test.UnsafeFinishExecution(TestStatus.Fail, duration, null);

        test.Close(TestStatus.Skip, null, "Test is being closed due to test session shutdown.");

        test.GetInternalSpan().Duration.Should().Be(duration);
        test.GetTags().Status.Should().Be("fail");
        test.GetTags().SkipReason.Should().BeNull();
        test.IsClosed.Should().BeTrue();
        Test.ActiveTests.Should().NotContain(test);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FinalTagsAreAssignedOnlyWhileTheTestRemainsOpen(bool shutdownClosedFirst)
    {
        using var harness = new TestHarness(new CountingCoverageEventHandler());
        var test = harness.Suite.CreateTest("final-tags-and-shutdown");
        test.UnsafeFinishExecution(TestStatus.Fail, TimeSpan.FromMilliseconds(12), null);
        if (shutdownClosedFirst)
        {
            test.Close(TestStatus.Skip);
        }

        var assignedTags = false;
        var spanWasFinished = false;
        test.Close(
            TestStatus.Fail,
            duration: null,
            skipReason: null,
            beforeClose: t =>
            {
                assignedTags = true;
                spanWasFinished = t.GetInternalSpan().IsFinished;
                t.GetTags().FinalStatus = "fail";
            });

        assignedTags.Should().Be(!shutdownClosedFirst);
        spanWasFinished.Should().BeFalse();
        test.GetTags().FinalStatus.Should().Be(shutdownClosedFirst ? null : "fail");
        test.GetInternalSpan().IsFinished.Should().BeTrue();
        test.GetInternalSpan().Duration.Should().Be(TimeSpan.FromMilliseconds(12));
    }

    [Fact]
    public void ClosingAPreviousAttemptDoesNotClearTheCurrentTestOrItsCoverage()
    {
        var handler = new CountingCoverageEventHandler();
        using var harness = new TestHarness(handler);
        var previousScope = Tracer.Instance.InternalActiveScope;
        var first = harness.Suite.CreateTest("first-attempt");
        first.UnsafeFinishExecution(TestStatus.Fail, TimeSpan.FromMilliseconds(12), null);
        ((IScopeRawAccess)Tracer.Instance.TracerManager.ScopeManager).Active = previousScope;
        var second = harness.Suite.CreateTest("second-attempt");
        var secondCoverage = handler.Container;

        first.Close(TestStatus.Fail);

        Test.Current.Should().BeSameAs(second);
        Tracer.Instance.InternalActiveScope!.Span.Should().BeSameAs(second.GetInternalSpan());
        handler.Container.Should().BeSameAs(secondCoverage);
        handler.FinishedCount.Should().Be(1);
        second.Close(TestStatus.Pass);
        handler.FinishedCount.Should().Be(2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReentrantCloseWaitsForTheRemainingCallbacks(bool finishExecutionEarly)
    {
        using var harness = new TestHarness(new CountingCoverageEventHandler());
        var test = harness.Suite.CreateTest("reentrant-close");
        var secondCallbackRanBeforeClose = false;
        test.AddOnExecutionCompletedAction(t => t.Close(TestStatus.Pass));
        test.AddOnExecutionCompletedAction(t => secondCallbackRanBeforeClose = !t.GetInternalSpan().IsFinished);

        if (finishExecutionEarly)
        {
            test.UnsafeFinishExecution(TestStatus.Pass, TimeSpan.FromMilliseconds(12), null);
        }
        else
        {
            test.Close(TestStatus.Pass);
        }

        secondCallbackRanBeforeClose.Should().BeTrue();
        test.IsClosed.Should().BeTrue();
        test.GetInternalSpan().IsFinished.Should().BeTrue();
    }

    [Fact]
    public void AThrowingCallbackDoesNotPreventOtherCallbacksOrClose()
    {
        using var harness = new TestHarness(new CountingCoverageEventHandler());
        var test = harness.Suite.CreateTest("throwing-callback");
        var secondCallbackCount = 0;
        test.AddOnExecutionCompletedAction(_ => throw new InvalidOperationException("Injected callback failure."));
        test.AddOnExecutionCompletedAction(_ => secondCallbackCount++);

        test.UnsafeFinishExecution(TestStatus.Pass, TimeSpan.FromMilliseconds(12), null);
        test.Close(TestStatus.Pass);

        secondCallbackCount.Should().Be(1);
        test.GetInternalSpan().IsFinished.Should().BeTrue();
        Test.ActiveTests.Should().NotContain(test);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentCloseWaitsForExecutionAndFinalTags(bool assignFinalTagsOnClose)
    {
        var handler = new CountingCoverageEventHandler();
        using var harness = new TestHarness(handler);
        var test = harness.Suite.CreateTest("concurrent-execution-completion");
        using var callbackEntered = new ManualResetEventSlim();
        using var releaseCallback = new ManualResetEventSlim();
        using var closeStarted = new ManualResetEventSlim();
        var callbackCount = 0;
        var callbackObservedFinishedSpan = false;
        Action<Test> callback =
            t =>
            {
                callbackCount++;
                callbackEntered.Set();
                releaseCallback.Wait(TimeSpan.FromSeconds(10));
                callbackObservedFinishedSpan = t.GetInternalSpan().IsFinished;
                t.GetTags().FinalStatus = "fail";
            };
        if (assignFinalTagsOnClose)
        {
            test.UnsafeFinishExecution(TestStatus.Fail, TimeSpan.FromMilliseconds(12), null);
        }
        else
        {
            test.AddOnExecutionCompletedAction(callback);
        }

        var finish = Task.Run(
            () =>
            {
                if (assignFinalTagsOnClose)
                {
                    test.Close(TestStatus.Fail, null, null, beforeClose: callback);
                }
                else
                {
                    test.UnsafeFinishExecution(TestStatus.Fail, TimeSpan.FromMilliseconds(12), null);
                }
            });
        Task close = Task.CompletedTask;
        try
        {
            callbackEntered.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
            close = Task.Run(
                () =>
                {
                    closeStarted.Set();
                    test.Close(TestStatus.Skip);
                });
            closeStarted.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
            (await Task.WhenAny(close, Task.Delay(100))).Should().NotBeSameAs(close, "the callback has not finished assigning the test data");
        }
        finally
        {
            releaseCallback.Set();
            await Task.WhenAll(finish, close);
        }

        callbackCount.Should().Be(1);
        callbackObservedFinishedSpan.Should().BeFalse();
        handler.FinishedCount.Should().Be(1);
        test.GetTags().FinalStatus.Should().Be("fail");
        test.GetInternalSpan().Duration.Should().Be(TimeSpan.FromMilliseconds(12));
        test.GetInternalSpan().IsFinished.Should().BeTrue();
    }

    [Fact]
    public void ConstructorFailureAfterCoverageStartAbortsCoverageContext()
    {
        using var harness = new TestHarness();
        var logger = new Mock<IDatadogLogger>();
        logger.Setup(
                   x => x.Debug<string, string, string>(
                       "######### New Test Created: {Name} ({Suite} | {Module})",
                       It.IsAny<string>(),
                       It.IsAny<string>(),
                       It.IsAny<string>(),
                       It.IsAny<int>(),
                       It.IsAny<string>()))
              .Throws(new InvalidOperationException("Injected construction failure."));
        harness.TestOptimizationMock.Setup(x => x.Log).Returns(logger.Object);

        var action = () => harness.Suite.CreateTest("constructor-failure");

        action.Should().Throw<InvalidOperationException>().WithMessage("Injected construction failure.");
        AssertBalancedSuppressedCoverage(harness.GlobalHandler, GlobalCoverageFailureReason.TestConstructionFailed);
    }

    [Fact]
    public void CoverageEndFailureStillClosesAndDisposesTheContext()
    {
        var handler = new ThrowingCoverageEventHandler();
        using var harness = new TestHarness(handler);
        var test = harness.Suite.CreateTest("close-failure");

        var action = () => test.Close(TestStatus.Pass);

        action.Should().Throw<InvalidOperationException>().WithMessage("Injected coverage-end failure.");
        test.IsClosed.Should().BeTrue();
        test.GetInternalSpan().IsFinished.Should().BeTrue();
        handler.Container.Should().BeNull();
    }

    [Fact]
    public void EarlyCoverageFailureStillRunsCallbacksAndAllowsLaterClose()
    {
        using var harness = new TestHarness(new ThrowingCoverageEventHandler());
        var test = harness.Suite.CreateTest("early-coverage-failure");
        var callbackCount = 0;
        test.AddOnExecutionCompletedAction(_ => callbackCount++);

        var finish = () => test.UnsafeFinishExecution(TestStatus.Fail, TimeSpan.FromMilliseconds(12), null);

        finish.Should().Throw<InvalidOperationException>();
        callbackCount.Should().Be(1);
        test.IsClosed.Should().BeFalse();
        Test.ActiveTests.Should().Contain(test);
        test.Close(TestStatus.Fail);
        callbackCount.Should().Be(1);
        test.GetInternalSpan().IsFinished.Should().BeTrue();
    }

    [Fact]
    public async Task ConcurrentCloseClaimsCoverageSessionExactlyOnce()
    {
        var handler = new CountingCoverageEventHandler();
        using var harness = new TestHarness(handler);
        var test = harness.Suite.CreateTest("concurrent-close");
        using var start = new ManualResetEventSlim();

        var firstClose = Task.Run(
            () =>
            {
                start.Wait();
                test.Close(TestStatus.Pass);
            });
        var secondClose = Task.Run(
            () =>
            {
                start.Wait();
                test.Close(TestStatus.Pass);
            });

        start.Set();
        await Task.WhenAll(firstClose, secondClose);

        test.IsClosed.Should().BeTrue();
        handler.FinishedCount.Should().Be(1);
    }

    [Fact]
    public unsafe void NormalCustomerModuleDoesNotMaterializeAnIntermediateGlobalSnapshot()
    {
        var handler = new DefaultWithGlobalCoverageEventHandler();
        using var harness = new TestHarness(handler);
        var metadata = new TestModuleCoverageMetadata(
            8,
            0,
            [new FileCoverageMetadata("/src/global-fallback.cs", 0, 8, [0xff])]);
        handler.GlobalContainer.TryGetOrAddModuleValue(
                   metadata,
                   typeof(TestCoverageLifecycleTests).Module,
                   8,
                   out var module)
               .Should()
               .BeTrue();
        ((byte*)module!.FilesLines)[0] = 1;

        harness.Module.Close();
        handler.GlobalContainer.Clear();

        using var snapshot = handler.AcquireGlobalCoverageSnapshot().Snapshot!;
        snapshot.Model.Components.Should().BeEmpty(
            "a normal module does not consume the percentage and final publication captures the fallback separately");
    }

    private static void AssertBalancedSuppressedCoverage(DefaultWithGlobalCoverageEventHandler handler, GlobalCoverageFailureReason reason)
    {
        handler.Container.Should().BeNull();
        var snapshotResult = handler.AcquireGlobalCoverageSnapshot();
        snapshotResult.Status.Should().Be(GlobalCoverageSnapshotStatus.SuppressedIncomplete);
        snapshotResult.FailureReason.Should().Be(reason);
    }

    private sealed class TestHarness : IDisposable
    {
        private readonly ITestOptimization _previousTestOptimization;
        private readonly CoverageEventHandler _previousCoverageHandler;

        public TestHarness(CoverageEventHandler? handler = null)
        {
            _previousTestOptimization = TestOptimization.Instance;
            _previousCoverageHandler = CoverageReporter.Handler;

            var settings = new TestOptimizationSettings(
                CreateConfigurationSource((ConfigurationKeys.CIVisibility.CodeCoverage, "1")),
                NullConfigurationTelemetry.Instance);
            TestOptimizationMock = new Mock<ITestOptimization>();
            var hostInfo = new Mock<ITestOptimizationHostInfo>();
            hostInfo.Setup(x => x.GetOperatingSystemVersion()).Returns("test-os-version");
            TestOptimizationMock.Setup(x => x.Settings).Returns(settings);
            TestOptimizationMock.Setup(x => x.Log).Returns(DatadogLogging.GetLoggerFor(typeof(TestCoverageLifecycleTests)));
            TestOptimizationMock.Setup(x => x.CIValues).Returns(new TestCIEnvironmentValues(Directory.GetCurrentDirectory()));
            TestOptimizationMock.Setup(x => x.HostInfo).Returns(hostInfo.Object);

            Handler = handler ?? new DefaultWithGlobalCoverageEventHandler();
            TestOptimization.Instance = TestOptimizationMock.Object;
            CoverageReporter.Handler = Handler;
            Session = TestSession.GetOrCreate("dotnet test", workingDirectory: null, framework: "xunit", startDate: null);
            Module = Session.CreateModule("coverage-lifecycle");
            Suite = Module.GetOrCreateSuite("coverage-lifecycle-suite");
        }

        public Mock<ITestOptimization> TestOptimizationMock { get; }

        public CoverageEventHandler Handler { get; }

        public DefaultWithGlobalCoverageEventHandler GlobalHandler => (DefaultWithGlobalCoverageEventHandler)Handler;

        public TestSession Session { get; }

        public TestModule Module { get; }

        public TestSuite Suite { get; }

        public void Dispose()
        {
            Suite.Close();
            Module.Close();
            Session.Close(TestStatus.Pass);
            CoverageReporter.Handler = _previousCoverageHandler;
            TestOptimization.Instance = _previousTestOptimization;
        }
    }

    private sealed class ThrowingCoverageEventHandler : CoverageEventHandler
    {
        protected override void OnSessionStart(CoverageContextContainer context)
        {
        }

        protected override object? OnSessionFinished(
            CoverageContextContainer context,
            IReadOnlyList<ModuleValue> modules,
            out bool deferCompletion)
        {
            deferCompletion = false;
            throw new InvalidOperationException("Injected coverage-end failure.");
        }
    }

    private sealed class CountingCoverageEventHandler : CoverageEventHandler
    {
        private int _finishedCount;

        public int FinishedCount => _finishedCount;

        protected override void OnSessionStart(CoverageContextContainer context)
        {
        }

        protected override object? OnSessionFinished(
            CoverageContextContainer context,
            IReadOnlyList<ModuleValue> modules,
            out bool deferCompletion)
        {
            Interlocked.Increment(ref _finishedCount);
            deferCompletion = false;
            return null;
        }
    }

    private sealed class TestCIEnvironmentValues : CIEnvironmentValues
    {
        public TestCIEnvironmentValues(string workspacePath)
        {
            WorkspacePath = workspacePath;
            Repository = "https://github.com/DataDog/dd-trace-dotnet";
            Commit = "abcdef123456";
        }

        protected override void Setup(IGitInfo gitInfo)
        {
        }
    }
}
