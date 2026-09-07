// <copyright file="MsTestExecution.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.ClrProfiler.CallTarget.Handlers;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2;

internal sealed class MsTestExecution
{
    private static readonly AsyncLocal<MsTestExecution?> CurrentExecution = new();
    private static readonly ConditionalWeakTable<object, StrongBox<MsTestExecution?>> MethodExecutions = new();
    private IList? _firstResults;
    private List<IList>? _additionalResults;
    private List<NativeAttempt>? _nativeAttempts;
    private List<PendingTest>? _pendingTests;
    private int _nativeAttemptNumber = -1;
    private bool _datadogRetriesApplied;

    public MsTestExecution(CallTargetState previousState, MsTestExecution? parent)
    {
        PreviousState = previousState;
        Parent = parent;
    }

    public static MsTestExecution? Current
    {
        get => CurrentExecution.Value;
        set => CurrentExecution.Value = value;
    }

    public CallTargetState PreviousState { get; }

    public MsTestExecution? Parent { get; }

    public bool HasNativeRetry => _nativeAttempts is not null;

    public bool IsNativeRetry => _nativeAttemptNumber > 0;

    public static StrongBox<MsTestExecution?>? BindTestMethod(object? testMethod)
    {
        if (Current is not { } execution || testMethod is null)
        {
            return null;
        }

        // MSTest can restore the ExecutionContext captured by ClassInitialize before invoking a test.
        // Bind the actual method object before that switch; its arguments also distinguish data rows.
        var binding = MethodExecutions.GetOrCreateValue(testMethod);
        binding.Value = execution;
        return binding;
    }

    public static MsTestExecution? GetForTestMethod(object? testMethod)
        => testMethod is not null && MethodExecutions.TryGetValue(testMethod, out var binding) ? binding.Value : Current;

    public static bool IsAcceptableNativeResult(IList results)
    {
        foreach (var result in results)
        {
            if (result.DuckCast<ITestResult>()!.Outcome is UnitTestOutcome.Failed or UnitTestOutcome.Timeout)
            {
                return false;
            }
        }

        return true;
    }

    public void StartNativeAttempt() => _nativeAttemptNumber++;

    public void ObserveTestMethod(ITestMethod testMethod)
    {
        if (_nativeAttempts is null && testMethod.Instance.TryDuckCast<ITestMethodInfoWithRetry>(out var method) && method.RetryAttribute is not null)
        {
            _nativeAttempts = [];
            _pendingTests = [];
        }
    }

    public void ObserveResults(IList results)
    {
        if (_firstResults is null)
        {
            _firstResults = results;
            return;
        }

        (_additionalResults ??= []).Add(results);
    }

    public bool WasObserved(object result)
    {
        if (ContainsReference(_firstResults, result))
        {
            return true;
        }

        if (_additionalResults is null)
        {
            return false;
        }

        foreach (var results in _additionalResults)
        {
            if (ContainsReference(results, result))
            {
                return true;
            }
        }

        return false;

        static bool ContainsReference(IList? results, object result)
        {
            if (results is not null)
            {
                foreach (var observed in results)
                {
                    if (ReferenceEquals(observed, result))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }

    public void RecordNativeAttempt(IList results, TestMethodAttributeExecuteAsyncIntegration.TestRunnerState state, TestAttemptResult summary)
    {
        if (IsNativeRetry && FindInitialAttempt(state.Test!) is { } initial)
        {
            // A native retry must not reconsider a faulty-session decision made on the first attempt.
            summary.IsEfdTest = initial.Summary.IsEfdTest;
        }

        _nativeAttempts!.Add(new NativeAttempt(_nativeAttemptNumber, results, state, summary));
    }

    public void FinishAttempt(Test test, ITestResult? result, TestStatus status, string? skipReason, bool isDatadogRetry = false)
    {
        if (IsNativeRetry && FindInitialAttempt(test) is { } initial)
        {
            test.GetTags().TestIsNew = initial.State.Test!.GetTags().TestIsNew;
        }

        var span = test.GetInternalSpan();
        var duration = status == TestStatus.Skip ? TimeSpan.Zero : span.Context.TraceContext.Clock.ElapsedSince(span.StartTime);
        _pendingTests!.Add(new PendingTest(test, result, status, duration, isDatadogRetry));
        try
        {
            test.UnsafeFinishExecution(status, duration, skipReason);
        }
        catch (Exception ex)
        {
            Common.Log.Error(ex, "MSTest: Error completing test execution.");
        }
        finally
        {
            // End this attempt's active context without disposing its still-open scope.
            // This also covers Datadog retries, which invoke the method without CallTarget.
            if (Tracer.Instance.InternalActiveScope is { } scope && ReferenceEquals(scope.Span, span))
            {
                var parentContext = new CallTargetState(scope.Parent, span.Context.Parent as SpanContext, CallTargetState.GetDefault());
                IntegrationOptions.RestoreScopeFromAsyncExecution(parentContext);
            }
        }
    }

    public async Task ApplyDatadogRetriesAsync(IList results)
    {
        if (_datadogRetriesApplied || _nativeAttempts is not { Count: > 0 } || results.Count == 0)
        {
            return;
        }

        _datadogRetriesApplied = true;
        // Update only the final native attempt, before MSTest assigns retry metadata and runs cleanup.
        // Earlier attempts stay intact for MTP's retry history.
        foreach (var attempt in _nativeAttempts)
        {
            if (attempt.Number != _nativeAttemptNumber)
            {
                continue;
            }

            var originalResults = new object?[attempt.Results.Count];
            attempt.Results.CopyTo(originalResults, 0);
            for (var row = 0; row < originalResults.Length; row++)
            {
                // A custom executor can return several results. Retry each result independently;
                // a passing result must not hide another failure or receive its retry outcome.
                var original = originalResults[row].DuckCast<ITestResultV4_4>()!;
                var summary = attempt.Summary;
                summary.ResultStatus = TestMethodAttributeExecuteAsyncIntegration.GetStatusFromOutcome(original.Outcome);
                summary.AllowRetries = summary.ResultStatus != TestStatus.Skip;
                summary.InitialExecutionPassed = summary.ResultStatus == TestStatus.Pass;
                summary.InitialExecutionFailed = summary.ResultStatus == TestStatus.Fail;
                object?[] rowResults = [originalResults[row]];
                var retryName = FindPendingTest(original.Instance)?.Test.Name;
                var finalResults = await TestMethodAttributeExecuteAsyncIntegration.RunRetriesAsync(rowResults, attempt.State, summary, retryName).ConfigureAwait(false);
                ObserveResults(finalResults);
                for (var index = 0; index < results.Count; index++)
                {
                    if (!ReferenceEquals(results[index], originalResults[row]))
                    {
                        continue;
                    }

                    // InvokeAsync produces the execution result. The runner adds the row identity
                    // afterwards, so preserve it when a Datadog retry replaces that result.
                    var final = finalResults[0].DuckCast<ITestResultV4_4>()!;
                    final.DisplayName = original.DisplayName;
                    final.ExecutionId = original.ExecutionId;
                    final.ParentExecId = original.ParentExecId;
                    final.DatarowIndex = original.DatarowIndex;
                    final.AssociatedUnitTestElement = original.AssociatedUnitTestElement;
                    results[index] = finalResults[0];
                }

                attempt.Results[row] = finalResults[0];
            }
        }

        foreach (var completed in _pendingTests!)
        {
            var tags = completed.Test.GetTags();
            if (tags.IsQuarantined == "true" || tags.IsDisabled == "true" || tags.IsAttemptToFix == "true")
            {
                foreach (var result in results)
                {
                    if (completed.Result is { } completedResult && ReferenceEquals(completedResult.Instance, result) && result.TryDuckCast<ITestResultV4_4>(out var final) && !final.IsSupersededRetryAttempt)
                    {
                        final.Outcome = UnitTestOutcome.Ignored;
                        final.TestFailureException = null;
                    }
                }
            }
        }
    }

    public void CloseTests()
    {
        // ClassInitialize can retain its captured ExecutionContext until the assembly finishes.
        // Release attempt data even if that context still references this execution.
        var completedTests = _pendingTests;
        if (_nativeAttempts is { } nativeAttempts)
        {
            foreach (var attempt in nativeAttempts)
            {
                attempt.State.RetryContext?.Dispose();
            }
        }

        _firstResults = null;
        _additionalResults = null;
        _nativeAttempts = null;
        _pendingTests = null;
        if (completedTests is null)
        {
            return;
        }

        try
        {
            foreach (var completed in completedTests)
            {
                var tags = completed.Test.GetTags();
                if (completed.Test.IsClosed)
                {
                    continue;
                }

                PendingTest? last = null;
                PendingTest? lastNativeAttempt = null;
                var anyDatadogRetryPassed = false;
                var anyFailed = false;
                var executions = 0;
                var allRetriesFailed = true;
                foreach (var candidate in completedTests)
                {
                    var candidateTags = candidate.Test.GetTags();
                    if (candidateTags.Name != tags.Name || candidateTags.Parameters != tags.Parameters)
                    {
                        continue;
                    }

                    if (candidate.IsDatadogRetry)
                    {
                        anyDatadogRetryPassed |= candidate.Status == TestStatus.Pass;
                    }
                    else
                    {
                        lastNativeAttempt = candidate;
                    }

                    anyFailed |= candidate.Status == TestStatus.Fail;
                    if (executions++ > 0)
                    {
                        allRetriesFailed &= candidate.Status == TestStatus.Fail;
                    }

                    last = candidate;
                }

                // MSTest keeps the last native attempt, even if an earlier one passed.
                // Datadog's retries can recover that result; ATF still considers every failure.
                var anyPassed = lastNativeAttempt?.Status == TestStatus.Pass || anyDatadogRetryPassed;
                completed.Test.Close(
                    completed.Status,
                    completed.Duration,
                    skipReason: null,
                    beforeClose: test =>
                    {
                        var finalTags = test.GetTags();
                        finalTags.FinalStatus = ReferenceEquals(completed, last)
                                                    ? Common.CalculateFinalStatus(anyPassed, anyFailed, completed.Status == TestStatus.Skip, finalTags)
                                                    : null;
                        finalTags.HasFailedAllRetries = ReferenceEquals(completed, last) && executions > 1 && allRetriesFailed ? "true" : null;
                        if (finalTags.IsAttemptToFix == "true" && ReferenceEquals(completed, last) && executions > 1)
                        {
                            finalTags.AttemptToFixPassed = anyFailed ? "false" : "true";
                        }
                    });
            }
        }
        finally
        {
            foreach (var completed in completedTests)
            {
                try
                {
                    if (!completed.Test.IsClosed)
                    {
                        completed.Test.Close(completed.Status, completed.Duration);
                    }
                }
                catch (Exception ex)
                {
                    Common.Log.Error(ex, "MSTest: Error closing a completed test.");
                }
            }
        }
    }

    private PendingTest? FindPendingTest(object? result)
    {
        foreach (var test in _pendingTests!)
        {
            if (ReferenceEquals(test.Result?.Instance, result))
            {
                // The runner can change DisplayName after capture. Keep the original span identity.
                return test;
            }
        }

        return null;
    }

    private NativeAttempt? FindInitialAttempt(Test test)
    {
        foreach (var attempt in _nativeAttempts!)
        {
            var initialTags = attempt.State.Test!.GetTags();
            var tags = test.GetTags();
            if (initialTags.Name == tags.Name && initialTags.Parameters == tags.Parameters)
            {
                return attempt;
            }
        }

        return null;
    }

    private sealed class NativeAttempt(int number, IList results, TestMethodAttributeExecuteAsyncIntegration.TestRunnerState state, TestAttemptResult summary)
    {
        public int Number { get; } = number;

        public IList Results { get; } = results;

        public TestMethodAttributeExecuteAsyncIntegration.TestRunnerState State { get; } = state;

        public TestAttemptResult Summary { get; } = summary;
    }

    private sealed class PendingTest(Test test, ITestResult? result, TestStatus status, TimeSpan duration, bool isDatadogRetry)
    {
        public Test Test { get; } = test;

        public ITestResult? Result { get; } = result;

        public TestStatus Status { get; } = status;

        public TimeSpan Duration { get; } = duration;

        public bool IsDatadogRetry { get; } = isDatadogRetry;
    }
}
