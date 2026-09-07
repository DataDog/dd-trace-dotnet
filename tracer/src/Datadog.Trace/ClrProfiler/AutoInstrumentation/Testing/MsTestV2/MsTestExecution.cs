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
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2;

internal sealed class MsTestExecution
{
    private static readonly AsyncLocal<MsTestExecution?> CurrentExecution = new();
    private static readonly ConditionalWeakTable<object, StrongBox<MsTestExecution?>> MethodExecutions = new();
    private IList? _firstResults;
    private List<IList>? _additionalResults;
    private List<NativeAttempt>? _nativeAttempts;
    private List<CompletedTest>? _completedTests;
    private int _nativeAttemptNumber = -1;

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

    public void StartNativeAttempt() => _nativeAttemptNumber++;

    public void ObserveTestMethod(ITestMethod testMethod)
    {
        if (_nativeAttempts is null && testMethod.Instance.TryDuckCast<ITestMethodInfoWithRetry>(out var method) && method.RetryAttribute is not null)
        {
            _nativeAttempts = [];
            _completedTests = [];
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

    public void CloseAttempt(Test test, ITestResult? result, TestStatus status, string? skipReason)
    {
        if (IsNativeRetry && FindInitialAttempt(test) is { } initial)
        {
            test.GetTags().TestIsNew = initial.State.Test!.GetTags().TestIsNew;
        }

        var span = test.GetInternalSpan();
        var duration = status == TestStatus.Skip ? TimeSpan.Zero : span.Context.TraceContext.Clock.ElapsedSince(span.StartTime);
        _completedTests!.Add(new CompletedTest(test, result, status, duration));
        test.CloseWithDeferredSpan(status, duration, skipReason);
    }

    public async Task CompleteAsync(IList results, Exception? exception)
    {
        if (_nativeAttempts is not { Count: > 0 } || exception is not null || results.Count == 0)
        {
            return;
        }

        // Retry metadata is assigned by CombineRetryAttempts, after ExecuteAsync has returned.
        // Keep MSTest's historical entries intact: MTP publishes those entries to its consumers.
        foreach (var attempt in _nativeAttempts)
        {
            if (attempt.Number != _nativeAttemptNumber)
            {
                continue;
            }

            var originalResults = new object?[attempt.Results.Count];
            attempt.Results.CopyTo(originalResults, 0);
            var finalResults = await TestMethodAttributeExecuteAsyncIntegration.RunRetriesAsync(attempt.Results, attempt.State, attempt.Summary).ConfigureAwait(false);
            ObserveResults(finalResults);
            for (var row = 0; row < originalResults.Length; row++)
            {
                for (var index = 0; index < results.Count; index++)
                {
                    if (!ReferenceEquals(results[index], originalResults[row]))
                    {
                        continue;
                    }

                    if (originalResults[row].TryDuckCast<ITestResultV4_4>(out var original) &&
                        finalResults[row].TryDuckCast<ITestResultV4_4>(out var final))
                    {
                        final.RetryAttemptNumber = original.RetryAttemptNumber;
                        final.IsSupersededRetryAttempt = original.IsSupersededRetryAttempt;
                    }

                    results[index] = finalResults[row];
                }
            }
        }

        foreach (var completed in _completedTests!)
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

    public void FinishSpans()
    {
        // ClassInitialize can retain its captured ExecutionContext until the assembly finishes.
        // Release attempt data even if that context still references this execution.
        var completedTests = _completedTests;
        _firstResults = null;
        _additionalResults = null;
        _nativeAttempts = null;
        _completedTests = null;
        if (completedTests is null)
        {
            return;
        }

        try
        {
            foreach (var completed in completedTests)
            {
                var tags = completed.Test.GetTags();
                CompletedTest? last = null;
                var anyPassed = false;
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

                    anyPassed |= candidate.Status == TestStatus.Pass;
                    anyFailed |= candidate.Status == TestStatus.Fail;
                    if (executions++ > 0)
                    {
                        allRetriesFailed &= candidate.Status == TestStatus.Fail;
                    }

                    last = candidate;
                }

                // Final tags must be set before Span.Finish can enqueue the event for writing.
                tags.FinalStatus = ReferenceEquals(completed, last)
                                       ? Common.CalculateFinalStatus(anyPassed, anyFailed, completed.Status == TestStatus.Skip, tags)
                                       : null;
                tags.HasFailedAllRetries = ReferenceEquals(completed, last) && executions > 1 && allRetriesFailed ? "true" : null;
                if (tags.IsAttemptToFix == "true" && ReferenceEquals(completed, last) && executions > 1)
                {
                    tags.AttemptToFixPassed = anyFailed ? "false" : "true";
                }
            }
        }
        finally
        {
            foreach (var completed in completedTests)
            {
                completed.Test.GetInternalSpan().Finish(completed.Duration);
            }
        }
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

    private sealed class CompletedTest(Test test, ITestResult? result, TestStatus status, TimeSpan duration)
    {
        public Test Test { get; } = test;

        public ITestResult? Result { get; } = result;

        public TestStatus Status { get; } = status;

        public TimeSpan Duration { get; } = duration;
    }
}
