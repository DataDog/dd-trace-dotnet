// <copyright file="VSTestEmptySessionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.DotnetTest;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

[Collection(nameof(TracerInstanceTestCollection))]
[TracerRestorer]
public class VSTestEmptySessionTests : IDisposable
{
    private readonly Lazy<TestSession> _session = new(() =>
    {
        TestOptimization.Instance.Reset();
        return TestSession.GetOrCreate("dotnet vstest synthetic.dll", null, null, null, false);
    });

    [Theory]
    [InlineData(0, false, false, false, 0, "skip")]
    [InlineData(1, false, false, false, 0, "pass")]
    [InlineData(-1, false, false, false, 0, "pass")]
    [InlineData(null, false, false, false, 0, "pass")]
    [InlineData(0, true, false, false, 0, "pass")]
    [InlineData(0, false, true, false, 0, "pass")]
    [InlineData(0, false, false, true, 0, "pass")]
    [InlineData(0, false, false, false, 1, "fail")]
    public void UsesRunnerCompletionNotReportedTestEvents(int? executed, bool aborted, bool canceled, bool error, int exitCode, string expectedStatus)
    {
        var request = new object();
        VSTestRunTracker.Start(request, _session.Value);
        Complete(request, executed, aborted, canceled, error);
        VSTestRunTracker.IsEmpty(_session.Value).Should().Be(executed == 0 && !aborted && !canceled && !error);

        AssertSessionStatus(exitCode, expectedStatus);
    }

    [Fact]
    public void MissingCompletionDoesNotMarkEmptySessionSkipped()
    {
        VSTestRunTracker.Start(new object(), _session.Value);
        AssertSessionStatus(0, "pass");
    }

    [Fact]
    public void MissingRunDoesNotMarkEmptySessionSkipped()
    {
        AssertSessionStatus(0, "pass");
    }

    [Fact]
    public void ExceptionDoesNotMarkEmptySessionSkipped()
    {
        var request = new object();
        VSTestRunTracker.Start(request, _session.Value);
        Complete(request, 0);
        AssertSessionStatus(0, "pass", new InvalidOperationException("Synthetic runner error"));
    }

    [Theory]
    [InlineData(0, 0, "skip")]
    [InlineData(1, 0, "pass")]
    [InlineData(0, 1, "pass")]
    [InlineData(null, 0, "pass")]
    public void EveryRunMustConfirmZeroTests(int? firstCount, int? secondCount, string expectedStatus)
    {
        var first = new object();
        var second = new object();
        VSTestRunTracker.Start(first, _session.Value);
        VSTestRunTracker.Start(second, _session.Value);
        Complete(first, firstCount);
        Complete(second, secondCount);
        AssertSessionStatus(0, expectedStatus);
    }

    [Fact]
    public void DuplicateCompletionDoesNotHideAnOutstandingRun()
    {
        var first = new object();
        VSTestRunTracker.Start(first, _session.Value);
        VSTestRunTracker.Start(new object(), _session.Value);
        Complete(first, 0);
        Complete(first, 0);
        AssertSessionStatus(0, "pass");
    }

    public void Dispose()
    {
        if (_session.IsValueCreated)
        {
            _session.Value.Close(TestStatus.Pass);
            TestOptimization.Instance.Close();
            TestOptimization.Instance.Reset();
        }
    }

    private static void Complete(object request, long? executed, bool aborted = false, bool canceled = false, bool error = false)
    {
        var args = new
        {
            IsAborted = aborted,
            IsCanceled = canceled,
            Error = error ? new InvalidOperationException("Synthetic runner error") : null,
            TestRunStatistics = executed.HasValue ? new { ExecutedTests = executed.Value } : null,
        }.DuckCast<ITestRunCompleteEventArgs>();
        // The real callback can arrive on a thread without the session's AsyncLocal context.
        TestSession.Current = null;
        TestRunRequestHandleTestRunCompleteIntegration.OnMethodBegin(request, args, null, null, null);
    }

    private void AssertSessionStatus(int exitCode, string expectedStatus, Exception? exception = null)
    {
        DotnetCommon.FinalizeSession(_session.Value, exitCode, exception);
        _session.Value.Tags.Status.Should().Be(expectedStatus);
        _session.Value.Tags.GetTag(TestTags.SessionEmptyReason).Should().Be(expectedStatus == "skip" ? "zero_tests" : null);
        _session.Value.Tags.GetTag(TestTags.SkipReason).Should().Be(expectedStatus == "skip" ? "VSTest reported zero tests." : null);
    }
}
