// <copyright file="EmptyTestSessionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.DotnetTest;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

[Collection(nameof(TracerInstanceTestCollection))]
[TracerRestorer]
public class EmptyTestSessionTests : IDisposable
{
    private TestSession? _session;

    public EmptyTestSessionTests()
    {
        // A mock avoids waiting for agent discovery in every test.
        var testOptimization = new Mock<ITestOptimization>();
        testOptimization.Setup(x => x.IsRunning).Returns(true);
        testOptimization.Setup(x => x.Log).Returns(DatadogLogging.GetLoggerFor<EmptyTestSessionTests>());
        testOptimization.Setup(x => x.RunId).Returns(Guid.NewGuid().ToString("N"));
        testOptimization.Setup(x => x.Settings).Returns(TestOptimizationSettings.FromDefaultSources());
        testOptimization.Setup(x => x.CIValues).Returns(CIEnvironmentValues.Instance);
        testOptimization.Setup(x => x.HostInfo).Returns(Mock.Of<ITestOptimizationHostInfo>());
        testOptimization.Setup(x => x.Reset()).Callback(() => TestOptimization.Instance = new TestOptimization());
        TestOptimization.Instance = testOptimization.Object;
    }

    public void Dispose()
    {
        _session?.Close(TestStatus.Pass);
        TestOptimization.Instance.Reset();
    }

    [Theory]
    [InlineData(0L, false, false, false, TestTags.StatusSkip)]
    [InlineData(1L, false, false, false, TestTags.StatusPass)]
    [InlineData(null, false, false, false, TestTags.StatusPass)]
    [InlineData(0L, true, false, false, TestTags.StatusPass)]
    [InlineData(0L, false, true, false, TestTags.StatusPass)]
    [InlineData(0L, false, false, true, TestTags.StatusPass)]
    public void PassingSessionIsSkippedWhenVSTestRanNoTests(long? executedTests, bool aborted, bool canceled, bool error, string expectedStatus)
    {
        var session = CreateSession();

        // The count reaches the session over IPC, which is drained when the session closes.
        CompleteVSTestRun(executedTests, aborted, canceled, error);
        session.Close(TestStatus.Pass);

        AssertStatus(session, expectedStatus);
    }

    [Theory]
    [InlineData(TestTags.StatusSkip, 0L, 0L)]
    [InlineData(TestTags.StatusPass, 0L, 2L)]
    [InlineData(TestTags.StatusPass, 2L, 0L)]
    [InlineData(TestTags.StatusPass)]
    public void EveryTestRunnerMustReportZeroTests(string expectedStatus, params long[] executedTests)
    {
        var session = CreateSession();

        foreach (var count in executedTests)
        {
            session.RecordExecutedTests(count);
        }

        session.Close(TestStatus.Pass);

        AssertStatus(session, expectedStatus);
    }

    [Theory]
    [InlineData(0, TestTags.StatusSkip)]
    [InlineData(1, TestTags.StatusPass)]
    public void TestModulesInOtherProcessesReportTheirTests(int tests, string expectedStatus)
    {
        var session = CreateSession();

        // A VSTest project that ran no tests, and a module from a runner that doesn't report its own count,
        // such as Microsoft.Testing.Platform under dotnet test on SDK 9 and earlier.
        CompleteVSTestRun(0, aborted: false, canceled: false, error: false);
        RunTestModule(tests);
        session.Close(TestStatus.Pass);

        AssertStatus(session, expectedStatus);
    }

    [Fact]
    public void FailedSessionIsNotReportedAsEmpty()
    {
        var session = CreateSession();

        session.RecordExecutedTests(0);
        session.Close(TestStatus.Fail);

        AssertStatus(session, TestTags.StatusFail);
    }

    [Fact]
    public void RunnerExceptionKeepsTheSessionPassing()
    {
        var session = CreateSession();

        CompleteVSTestRun(0, aborted: false, canceled: false, error: false);
        DotnetCommon.FinalizeSession(session, exitCode: 0, new InvalidOperationException("Runner error"));

        AssertStatus(session, TestTags.StatusPass);
    }

    [Fact]
    public void SessionWithoutIpcServerIsNotReportedAsEmpty()
    {
        // Like a manual session that propagates its context to the test runner.
        var session = CreateSession(enableIpcServer: false);

        CompleteVSTestRun(0, aborted: false, canceled: false, error: false);
        session.Close(TestStatus.Pass);

        AssertStatus(session, TestTags.StatusPass);
    }

    private static void CompleteVSTestRun(long? executedTests, bool aborted, bool canceled, bool error)
    {
        var args = new
        {
            IsAborted = aborted,
            IsCanceled = canceled,
            Error = error ? new InvalidOperationException("Run error") : null,
            TestRunStatistics = executedTests is { } count ? new { ExecutedTests = count } : null,
        }.DuckCast<TestRunRequestOnRunCompletionIntegration.ITestRunCompleteEventArgs>();

        TestRunRequestOnRunCompletionIntegration.OnMethodBegin(new object(), null, args);
    }

    private static void RunTestModule(int tests)
    {
        // Like the test framework integrations, the module connects to the session it finds in the environment.
        var module = TestModule.Create("module", "xUnit", "2.9.3");
        module.EnableIpcClient().Should().BeTrue();
        var suite = module.GetOrCreateSuite("suite");
        for (var i = 0; i < tests; i++)
        {
            suite.CreateTest("test" + i).Close(TestStatus.Pass);
        }

        suite.Close();
        module.Close();
    }

    private static void AssertStatus(TestSession session, string expectedStatus)
    {
        var empty = expectedStatus == TestTags.StatusSkip;
        session.Tags.Status.Should().Be(expectedStatus);
        session.Tags.GetTag(TestTags.SkipReason).Should().Be(empty ? TestTags.SkipReasonNoTestsExecuted : null);
        session.Tags.GetTag(TestTags.SessionEmptyReason).Should().Be(empty ? TestTags.SessionEmptyReasonZeroTests : null);
    }

    private TestSession CreateSession(bool enableIpcServer = true)
    {
        // The integration finds the session through the context it propagates to the environment.
        _session = TestSession.GetOrCreate("dotnet test", workingDirectory: null, framework: null, startDate: null, propagateEnvironmentVariables: true);
        if (enableIpcServer)
        {
            _session.EnableIpcServer().Should().BeTrue();
        }

        return _session;
    }
}
