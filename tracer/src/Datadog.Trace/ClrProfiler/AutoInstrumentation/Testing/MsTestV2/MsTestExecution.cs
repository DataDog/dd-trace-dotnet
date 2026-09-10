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

/// <summary>
/// Owns one runner invocation, including its data rows and native and Datadog retries.
/// Attempt execution ends immediately; spans close only after the native policy selects its result.
/// </summary>
internal sealed class MsTestExecution
{
    private static readonly AsyncLocal<MsTestExecution?> CurrentExecution = new();
    private static readonly ConditionalWeakTable<object, TestMethodBinding> MethodExecutions = new();
    private IList? _firstResults;
    private List<IList>? _additionalResults;
    private List<NativeAttempt>? _nativeAttempts;
    private List<PendingTest>? _pendingTests;
    private int _nativeAttemptNumber = -1;
    private int _nextNativeRowIndex;
    private int _datadogRetryRowIndex;
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

    public int NextNativeRowIndex => _nextNativeRowIndex;

    /// <summary>
    /// Associates the method object with this execution across MSTest ExecutionContext switches.
    /// The caller must dispose the binding so MSTest's method cache cannot retain completed attempts.
    /// </summary>
    public static IDisposable? BindTestMethod(object? testMethod)
    {
        if (Current is not { } execution || testMethod is null)
        {
            return null;
        }

        // MSTest can restore the ExecutionContext captured by ClassInitialize before invoking a test.
        // Bind the actual method object before that switch; its arguments also distinguish data rows.
        var binding = MethodExecutions.GetValue(testMethod, static _ => new TestMethodBinding());
        binding.Execution = execution;
        return binding;
    }

    /// <summary>
    /// Recovers the bound execution when ClassInitialize has restored an older ExecutionContext.
    /// </summary>
    public static MsTestExecution? GetForTestMethod(object? testMethod)
        => testMethod is not null && MethodExecutions.TryGetValue(testMethod, out var binding) ? binding.Execution : Current;

    /// <summary>
    /// Matches MSTest's decision to skip its retry policy after an acceptable first attempt.
    /// </summary>
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

    /// <summary>
    /// Counts a runner invocation once, before any of its data rows execute.
    /// </summary>
    public void StartNativeAttempt()
    {
        _nativeAttemptNumber++;
        _nextNativeRowIndex = 0;
    }

    /// <summary>
    /// Allocates native retry state only for methods with a resolved retry attribute.
    /// </summary>
    public void ObserveTestMethod(ITestMethod testMethod)
    {
        if (_nativeAttempts is null && testMethod.Instance.TryDuckCast<ITestMethodInfoWithRetry>(out var method) && method.RetryAttribute is not null)
        {
            _nativeAttempts = [];
            _pendingTests = [];
        }
    }

    /// <summary>
    /// Remembers executor results so the outer runner does not create duplicate test spans.
    /// Additional result storage is allocated only when another executor invocation returns.
    /// </summary>
    public void ObserveResults(IList results)
    {
        if (_firstResults is null)
        {
            _firstResults = results;
            return;
        }

        (_additionalResults ??= []).Add(results);
    }

    /// <summary>
    /// Checks result identity rather than outcome or name, which custom executors may reuse.
    /// </summary>
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
    }

    /// <summary>
    /// Retains the inputs and retry decision needed if the native policy selects this attempt.
    /// </summary>
    public void RecordNativeAttempt(IList results, TestMethodAttributeExecuteAsyncIntegration.TestRunnerState state, TestAttemptResult summary)
    {
        if (IsNativeRetry && FindInitialAttempt(state.NativeRowIndex) is { } initial)
        {
            // A native retry must not reconsider a faulty-session decision made on the first attempt.
            summary.IsEfdTest = initial.Summary.IsEfdTest;
        }

        _nativeAttempts!.Add(new NativeAttempt(results, state, summary));
    }

    /// <summary>
    /// Captures duration and completes coverage and callbacks while the attempt context is active.
    /// Keeps the test and span open for final retry tags, then restores the parent tracing context.
    /// </summary>
    public void FinishAttempt(Test test, ITestResult? result, TestStatus status, string? skipReason, bool isDatadogRetry = false)
    {
        // MSTest enumerates folded rows and executor results again for each native attempt.
        // Names/arguments can repeat, and execution IDs are assigned later and change on retry.
        // Keep the result's position instead; Datadog retries inherit the selected row explicitly.
        var rowIndex = isDatadogRetry ? _datadogRetryRowIndex : _nextNativeRowIndex++;
        if (IsNativeRetry && FindInitialAttempt(rowIndex) is { } initial)
        {
            test.GetTags().TestIsNew = initial.State.Test!.GetTags().TestIsNew;
        }

        var span = test.GetInternalSpan();
        var duration = status == TestStatus.Skip ? TimeSpan.Zero : span.Context.TraceContext.Clock.ElapsedSince(span.StartTime);
        _pendingTests!.Add(new PendingTest(test, result, status, duration, isDatadogRetry, rowIndex));
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

    /// <summary>
    /// Retries only results selected by the native policy, before MSTest runs class cleanup.
    /// Also handles an acceptable first attempt, for which MSTest never invokes its retry policy.
    /// </summary>
    public async Task ApplyDatadogRetriesAsync(IList results)
    {
        if (_datadogRetriesApplied || _nativeAttempts is not { Count: > 0 } || results.Count == 0)
        {
            return;
        }

        _datadogRetriesApplied = true;
        // A custom policy can return an earlier attempt. Retry only the results it selected,
        // before MSTest assigns retry metadata and runs cleanup.
        foreach (var attempt in _nativeAttempts)
        {
            var originalResults = new object?[attempt.Results.Count];
            attempt.Results.CopyTo(originalResults, 0);
            for (var resultIndex = 0; resultIndex < originalResults.Length; resultIndex++)
            {
                var originalResult = originalResults[resultIndex];
                if (ContainsReference(results, originalResult))
                {
                    await RetrySelectedResultAsync(results, attempt, resultIndex, originalResult).ConfigureAwait(false);
                }
            }
        }

        MaskTestManagementOutcomes(results);
    }

    /// <summary>
    /// Applies the final outcome and closes pending spans under Test's shutdown guard.
    /// Releases retained retry contexts even when the framework exits with an error.
    /// </summary>
    public void CloseTests()
    {
        var completedTests = _pendingTests;
        // ClassInitialize can retain its captured ExecutionContext until the assembly finishes.
        // Release retry inputs even if that context still references this execution.
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
            // Aggregate once per row, preserving attempt order and policy selection.
            // Keep the original list for closing: grouping must not change the order spans are emitted.
            var outcomes = new Dictionary<int, RetryOutcome>();
            foreach (var completed in completedTests)
            {
                if (!outcomes.TryGetValue(completed.RowIndex, out var outcome))
                {
                    outcome = new RetryOutcome();
                    outcomes.Add(completed.RowIndex, outcome);
                }

                outcome.Add(completed);
            }

            foreach (var completed in completedTests)
            {
                if (!completed.Test.IsClosed)
                {
                    completed.Test.Close(
                        completed.Status,
                        completed.Duration,
                        skipReason: null,
                        beforeClose: outcomes[completed.RowIndex].ApplyFinalTags);
                }
            }
        }
        finally
        {
            // If finalization fails, still close every remaining span with its captured duration.
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

    /// <summary>
    /// Matches the exact result object; equal-looking results can belong to different attempts.
    /// </summary>
    private static bool ContainsReference(IList? results, object? result)
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

    /// <summary>
    /// Runs Datadog retries independently for one selected result and updates the framework result.
    /// </summary>
    private async Task RetrySelectedResultAsync(IList selectedResults, NativeAttempt attempt, int resultIndex, object? result)
    {
        // A custom executor can return several results. Retry each result independently;
        // a passing result must not hide another failure or receive its retry outcome.
        var originalResult = result.DuckCast<ITestResultV4_4>()!;
        var originalResultInstance = originalResult.Instance;

        // The runner can change DisplayName after capture, so match the original result object.
        PendingTest? pendingTest = null;
        foreach (var test in _pendingTests!)
        {
            if (ReferenceEquals(test.Result?.Instance, originalResultInstance))
            {
                pendingTest = test;
                break;
            }
        }

        if (pendingTest is not null)
        {
            pendingTest.IsSelectedNativeResult = true;
        }

        var summary = attempt.Summary;
        summary.ResultStatus = TestMethodAttributeExecuteAsyncIntegration.GetStatusFromOutcome(originalResult.Outcome);
        summary.AllowRetries = summary.ResultStatus != TestStatus.Skip;
        summary.InitialExecutionPassed = summary.ResultStatus == TestStatus.Pass;
        summary.InitialExecutionFailed = summary.ResultStatus == TestStatus.Fail;
        object?[] initialResults = [result];
        _datadogRetryRowIndex = pendingTest?.RowIndex ?? attempt.State.NativeRowIndex + resultIndex;
        var retryResults = await TestMethodAttributeExecuteAsyncIntegration.RunRetriesAsync(initialResults, attempt.State, summary, pendingTest?.Test.Name).ConfigureAwait(false);
        ObserveResults(retryResults);
        var replacementResult = retryResults[0];

        // Preserve the runner-assigned row identity when replacing its selected result.
        // MTP uses these identifiers to associate the result with the discovered test.
        for (var index = 0; index < selectedResults.Count; index++)
        {
            if (ReferenceEquals(selectedResults[index], originalResult.Instance))
            {
                var replacement = replacementResult.DuckCast<ITestResultV4_4>()!;
                replacement.DisplayName = originalResult.DisplayName;
                replacement.ExecutionId = originalResult.ExecutionId;
                replacement.ParentExecId = originalResult.ParentExecId;
                replacement.DatarowIndex = originalResult.DatarowIndex;
                replacement.AssociatedUnitTestElement = originalResult.AssociatedUnitTestElement;
                selectedResults[index] = replacementResult;
            }
        }

        attempt.Results[resultIndex] = retryResults[0];
    }

    /// <summary>
    /// Applies quarantine and test-management masking after retries, without changing attempt spans.
    /// </summary>
    private void MaskTestManagementOutcomes(IList selectedResults)
    {
        foreach (var completed in _pendingTests!)
        {
            if (completed.Result is not { } completedResult)
            {
                continue;
            }

            var tags = completed.Test.GetTags();
            if (tags.IsQuarantined != "true" && tags.IsDisabled != "true" && tags.IsAttemptToFix != "true")
            {
                continue;
            }

            foreach (var result in selectedResults)
            {
                if (ReferenceEquals(completedResult.Instance, result) && result.TryDuckCast<ITestResultV4_4>(out var final) && !final.IsSupersededRetryAttempt)
                {
                    final.Outcome = UnitTestOutcome.Ignored;
                    final.TestFailureException = null;
                }
            }
        }
    }

    /// <summary>
    /// Finds the first matching data row so native retries preserve its new-test and EFD decisions.
    /// </summary>
    private NativeAttempt? FindInitialAttempt(int rowIndex)
    {
        foreach (var attempt in _nativeAttempts!)
        {
            // A custom executor can produce several results for one method invocation.
            if (rowIndex >= attempt.State.NativeRowIndex && rowIndex < attempt.State.NativeRowIndex + attempt.Results.Count)
            {
                return attempt;
            }
        }

        return null;
    }

    /// <summary>
    /// Accumulates one test's retry outcome and shares its final-tag callback across all attempts.
    /// Native policy selection takes precedence over order; Attempt to Fix considers every failure.
    /// </summary>
    private sealed class RetryOutcome
    {
        private PendingTest? _lastAttempt;
        private PendingTest? _lastNativeAttempt;
        private PendingTest? _selectedNativeResult;
        private bool _anyDatadogRetryPassed;
        private bool _anyFailed;
        private int _executionCount;
        private bool _allRetriesFailed = true;

        public RetryOutcome() => ApplyFinalTags = ApplyTags;

        // Cache one delegate per test identity, rather than capturing state for every attempt.
        public Action<Test> ApplyFinalTags { get; }

        public void Add(PendingTest attempt)
        {
            if (attempt.IsDatadogRetry)
            {
                _anyDatadogRetryPassed |= attempt.Status == TestStatus.Pass;
            }
            else
            {
                _lastNativeAttempt = attempt;
                if (attempt.IsSelectedNativeResult)
                {
                    _selectedNativeResult = attempt;
                }
            }

            _anyFailed |= attempt.Status == TestStatus.Fail;
            if (_executionCount > 0)
            {
                _allRetriesFailed &= attempt.Status == TestStatus.Fail;
            }

            _executionCount++;
            _lastAttempt = attempt;
        }

        /// <summary>
        /// Assigns aggregate retry tags inside Test.Close so shutdown cannot finish the span between writes.
        /// </summary>
        private void ApplyTags(Test test)
        {
            var tags = test.GetTags();
            var isLastAttempt = ReferenceEquals(test, _lastAttempt!.Test);
            // If the policy threw or returned no results, use the last native attempt.
            var anyPassed = (_selectedNativeResult ?? _lastNativeAttempt)?.Status == TestStatus.Pass || _anyDatadogRetryPassed;
            tags.FinalStatus = isLastAttempt
                                   ? Common.CalculateFinalStatus(anyPassed, _anyFailed, _lastAttempt.Status == TestStatus.Skip, tags)
                                   : null;
            tags.HasFailedAllRetries = isLastAttempt && _executionCount > 1 && _allRetriesFailed ? "true" : null;
            if (tags.IsAttemptToFix == "true" && isLastAttempt && _executionCount > 1)
            {
                tags.AttemptToFixPassed = _anyFailed ? "false" : "true";
            }
        }
    }

    private sealed class TestMethodBinding : IDisposable
    {
        public MsTestExecution? Execution { get; set; }

        // The type cache retains methods across attempts, but must not retain completed executions.
        public void Dispose() => Execution = null;
    }

    private sealed class NativeAttempt(IList results, TestMethodAttributeExecuteAsyncIntegration.TestRunnerState state, TestAttemptResult summary)
    {
        public IList Results { get; } = results;

        public TestMethodAttributeExecuteAsyncIntegration.TestRunnerState State { get; } = state;

        public TestAttemptResult Summary { get; } = summary;
    }

    private sealed class PendingTest(Test test, ITestResult? result, TestStatus status, TimeSpan duration, bool isDatadogRetry, int rowIndex)
    {
        public Test Test { get; } = test;

        public ITestResult? Result { get; } = result;

        public TestStatus Status { get; } = status;

        public TimeSpan Duration { get; } = duration;

        public bool IsDatadogRetry { get; } = isDatadogRetry;

        public int RowIndex { get; } = rowIndex;

        public bool IsSelectedNativeResult { get; set; }
    }
}
