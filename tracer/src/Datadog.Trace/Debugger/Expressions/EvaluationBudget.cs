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

    private readonly long _deadlineTimestamp;
    private int _operationsUntilTimeCheck;

    private EvaluationBudget(long deadlineTimestamp)
    {
        _deadlineTimestamp = deadlineTimestamp;
        _operationsUntilTimeCheck = OperationsBeforeTimeCheck;
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

    internal TimeSpan GetRemainingTimeout()
    {
        ThrowIfTimeExceeded();

        var remainingStopwatchTicks = _deadlineTimestamp - Stopwatch.GetTimestamp();
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
        _operationsUntilTimeCheck = OperationsBeforeTimeCheck;
        if (TimedOut || Stopwatch.GetTimestamp() >= _deadlineTimestamp)
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
