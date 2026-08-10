// <copyright file="EvaluationBudget.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Datadog.Trace.Debugger.Expressions;

internal struct EvaluationBudget
{
    private const int OperationsBeforeTimeCheck = 32;
    private static readonly double StopwatchTicksPerMillisecond = Stopwatch.Frequency / 1000.0;

    // Stores an absolute deadline while running and remaining ticks while paused.
    // Reusing one field keeps this hot-path struct compact.
    private long _deadlineOrRemainingStopwatchTicks;
    private int _operationsUntilTimeCheck;
    private bool _isPaused;

    private EvaluationBudget(long deadlineTimestamp)
    {
        _deadlineOrRemainingStopwatchTicks = deadlineTimestamp;
        _operationsUntilTimeCheck = OperationsBeforeTimeCheck;
        _isPaused = false;
        TimedOut = false;
    }

    internal bool TimedOut { get; private set; }

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

    private static long ToStopwatchTicks(int milliseconds)
    {
        if (milliseconds <= 0)
        {
            return 0;
        }

        return (long)(milliseconds * StopwatchTicksPerMillisecond);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowTimedOut()
    {
        throw new EvaluationTimeBudgetExceededException();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ThrowIfExceeded()
    {
        if (TimedOut)
        {
            ThrowTimedOut();
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Pause()
    {
        if (_isPaused || TimedOut)
        {
            return;
        }

        _deadlineOrRemainingStopwatchTicks -= Stopwatch.GetTimestamp();
        _isPaused = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Resume()
    {
        if (!_isPaused || TimedOut)
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
        _isPaused = false;
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
        TimedOut = true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowIfTimeExceeded()
    {
        Debug.Assert(!_isPaused, "The evaluation budget must be resumed before checking its deadline.");
        _operationsUntilTimeCheck = OperationsBeforeTimeCheck;
        if (TimedOut || Stopwatch.GetTimestamp() >= _deadlineOrRemainingStopwatchTicks)
        {
            MarkTimedOutAndThrow();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void MarkTimedOutAndThrow()
    {
        TimedOut = true;
        ThrowTimedOut();
    }
}
