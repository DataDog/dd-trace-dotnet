// <copyright file="EvaluationBudget.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Datadog.Trace.Debugger.Expressions;

// Besides the time budget, the budget carries whether a runtime redaction guard redacted a value that isn't read from a
// predicate's item: every compiled expression already receives it by reference, so the flag costs a store only when a
// guard fires. A guard of an item only makes that item not match, so it doesn't set the flag. The evaluator clears
// it before each compiled expression and reads it after, so each redaction is reported for its own expression.
internal struct EvaluationBudget
{
    private const int OperationsBeforeTimeCheck = 32;
    private static readonly double StopwatchTicksPerMillisecond = Stopwatch.Frequency / 1000.0;

    // Stores an absolute deadline while running and remaining ticks while paused.
    // Reusing one field keeps this hot-path struct compact.
    private long _deadlineOrRemainingStopwatchTicks;
    private int _operationsUntilTimeCheck;
    private EvaluationBudgetState _state;

    private bool _hasRuntimeRedaction;

    private EvaluationBudget(long deadlineTimestamp)
    {
        _deadlineOrRemainingStopwatchTicks = deadlineTimestamp;
        _operationsUntilTimeCheck = OperationsBeforeTimeCheck;
        _state = EvaluationBudgetState.Running;
    }

    private enum EvaluationBudgetState : byte
    {
        Uninitialized,
        Running,
        Paused,
        TimedOut
    }

    internal readonly bool IsInitialized => _state != EvaluationBudgetState.Uninitialized;

    internal readonly bool IsPaused => _state == EvaluationBudgetState.Paused;

    internal readonly bool TimedOut => _state == EvaluationBudgetState.TimedOut;

    internal static EvaluationBudget Create(int maxEvaluationTimeInMilliseconds)
    {
        var now = Stopwatch.GetTimestamp();
        var duration = ToStopwatchTicks(maxEvaluationTimeInMilliseconds);
        var deadline = long.MaxValue - now <= duration ? long.MaxValue : now + duration;
        return new EvaluationBudget(deadline);
    }

    internal static void ThrowIfExceeded(ref EvaluationBudget budget)
    {
        budget.ThrowIfExceeded();
    }

    internal static void ThrowIfExceededImmediately(ref EvaluationBudget budget)
    {
        budget.ThrowIfExceededImmediately();
    }

    internal static void MarkRuntimeRedaction(ref EvaluationBudget budget)
    {
        budget._hasRuntimeRedaction = true;
    }

    private static long ToStopwatchTicks(int milliseconds)
    {
        if (milliseconds <= 0)
        {
            return 0;
        }

        return (long)(milliseconds * StopwatchTicksPerMillisecond);
    }

    [DoesNotReturn]
    private static void ThrowTimedOut()
    {
        throw new EvaluationTimeBudgetExceededException();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ThrowIfExceeded()
    {
        if (_state != EvaluationBudgetState.Running)
        {
            MarkTimedOutAndThrow();
        }

        if (--_operationsUntilTimeCheck > 0)
        {
            return;
        }

        ThrowIfTimeExceeded();
    }

    internal void ThrowIfExceededImmediately()
    {
        ThrowIfTimeExceeded();
    }

    internal void Pause()
    {
        if (_state != EvaluationBudgetState.Running)
        {
            return;
        }

        _deadlineOrRemainingStopwatchTicks -= Stopwatch.GetTimestamp();
        _state = EvaluationBudgetState.Paused;
    }

    internal void Resume()
    {
        if (_state != EvaluationBudgetState.Paused)
        {
            return;
        }

        var remainingStopwatchTicks = _deadlineOrRemainingStopwatchTicks;
        var now = Stopwatch.GetTimestamp();
        _deadlineOrRemainingStopwatchTicks =
            remainingStopwatchTicks <= 0
                ? now
                : long.MaxValue - now <= remainingStopwatchTicks
                    ? long.MaxValue
                    : now + remainingStopwatchTicks;
        _state = EvaluationBudgetState.Running;
    }

    internal TimeSpan GetRemainingTimeout()
    {
        ThrowIfTimeExceeded();

        var remainingStopwatchTicks = _deadlineOrRemainingStopwatchTicks - Stopwatch.GetTimestamp();
        if (remainingStopwatchTicks <= 0)
        {
            MarkTimedOutAndThrow();
        }

        var milliseconds = Math.Max(1, (int)(remainingStopwatchTicks / StopwatchTicksPerMillisecond));
        return TimeSpan.FromMilliseconds(milliseconds);
    }

    internal void MarkTimedOut()
    {
        _state = EvaluationBudgetState.TimedOut;
    }

    internal void ClearRuntimeRedaction()
    {
        _hasRuntimeRedaction = false;
    }

    internal bool TakeRuntimeRedaction()
    {
        if (!_hasRuntimeRedaction)
        {
            return false;
        }

        _hasRuntimeRedaction = false;
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowIfTimeExceeded()
    {
        if (_state != EvaluationBudgetState.Running)
        {
            MarkTimedOutAndThrow();
        }

        _operationsUntilTimeCheck = OperationsBeforeTimeCheck;
        if (Stopwatch.GetTimestamp() >= _deadlineOrRemainingStopwatchTicks)
        {
            MarkTimedOutAndThrow();
        }
    }

    [DoesNotReturn]
    private void MarkTimedOutAndThrow()
    {
        _state = EvaluationBudgetState.TimedOut;
        ThrowTimedOut();
    }
}
