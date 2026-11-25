// <copyright file="BorrowingTokenBucketTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Datadog.Trace.Debugger.RateLimiting;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Debugger;

public class BorrowingTokenBucketTests
{
    private const long Start = 1_000_000_000;

    private static readonly long Second = Stopwatch.Frequency;

    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(11)]
    [InlineData(13)]
    [InlineData(15)]
    [InlineData(60)]
    [InlineData(100)]
    public void TryTake_StartsFullWithOneSecondOfTokensThenRefillsAtTheRate(int rate)
    {
        var bucket = new BorrowingTokenBucket(rate);
        var interval = Interval(rate);

        Enumerable.Range(0, rate + 1).Count(_ => bucket.TryTake(Start)).Should().Be(rate);
        bucket.TryTake(Start + interval - 1).Should().BeFalse();
        bucket.TryTake(Start + interval).Should().BeTrue();
        bucket.TryTake(Start + interval).Should().BeFalse();
    }

    [Fact]
    public void TryTake_AfterAnIdlePeriod_HoldsNoMoreThanOneSecondOfTokens()
    {
        var bucket = new BorrowingTokenBucket(4);
        bucket.TryTake(Start).Should().BeTrue();

        var later = Start + (60 * Second);
        Enumerable.Range(0, 5).Select(_ => bucket.TryTake(later)).Should().Equal(true, true, true, true, false);
    }

    [Fact]
    public void Take_PastTheTokens_RefusesUntilTheRefillPaysTheDebtBack()
    {
        var bucket = new BorrowingTokenBucket(2);
        bucket.TryTake(Start).Should().BeTrue();
        bucket.TryTake(Start).Should().BeTrue();

        for (var i = 0; i < 6; i++)
        {
            bucket.Take(Start);
        }

        // 8 tokens at 2 per second from a full bucket of 2: the next one is due 7 intervals, 3.5 seconds, later.
        bucket.TryTake(Start + (7 * Interval(2)) - 1).Should().BeFalse();
        bucket.TryTake(Start + (7 * Interval(2))).Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(500)]
    [InlineData(1500)]
    public void TotalStaysWithinTheRateWhenKeptTokensArriveLater(int keepDelayMilliseconds)
    {
        // A parent is tried every millisecond for 100 seconds, and each one admitted is followed by 3 kept tokens.
        const int rate = 10;
        const int seconds = 100;
        const int keptPerSample = 3;
        var bucket = new BorrowingTokenBucket(rate);
        var keepDelay = Second * keepDelayMilliseconds / 1000;
        var pendingKeeps = new Queue<long>();
        var total = 0;

        for (var millisecond = 0; millisecond < seconds * 1000; millisecond++)
        {
            var now = Start + (Second * millisecond / 1000);
            while (pendingKeeps.Count > 0 && pendingKeeps.Peek() <= now)
            {
                bucket.Take(pendingKeeps.Dequeue());
                total++;
            }

            if (bucket.TryTake(now))
            {
                total++;
                for (var i = 0; i < keptPerSample; i++)
                {
                    pendingKeeps.Enqueue(now + keepDelay);
                }
            }
        }

        // A full bucket at the start, then the rate: 1010 tokens. Kept tokens that land late leave some debt at the end.
        const double expected = rate + (rate * seconds);
        total.Should().BeInRange((int)(expected * 0.98), (int)(expected * 1.02));
    }

    [Fact]
    public void TryTake_RateBelowOnePerSecond_AdmitsOnePerInterval()
    {
        var bucket = new BorrowingTokenBucket(1.0 / 300);
        var interval = Interval(1.0 / 300);

        bucket.TryTake(Start).Should().BeTrue();
        bucket.TryTake(Start + 1).Should().BeFalse();
        bucket.TryTake(Start + interval - 1).Should().BeFalse();
        bucket.TryTake(Start + interval).Should().BeTrue();

        bucket.Take(Start + interval);
        bucket.TryTake(Start + (2 * interval)).Should().BeFalse();
        bucket.TryTake(Start + (3 * interval)).Should().BeTrue();
    }

    [Fact]
    public void TryTake_FractionalRate_RefillsAtThatRate()
    {
        var bucket = new BorrowingTokenBucket(2.5);
        var admitted = 0;
        for (var millisecond = 0; millisecond < 10_000; millisecond++)
        {
            admitted += bucket.TryTake(Start + (Second * millisecond / 1000)) ? 1 : 0;
        }

        // A full bucket of 2.5 tokens, then 2.5 per second for 10 seconds.
        admitted.Should().BeInRange(26, 27);
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-1d)]
    [InlineData(double.NaN)]
    public void TryTake_RateNotAboveZero_AdmitsNothing(double rate)
    {
        var bucket = new BorrowingTokenBucket(rate);

        bucket.TryTake(Start).Should().BeFalse();
        bucket.Take(Start);
        bucket.TryTake(Start + (365L * 24 * 3600 * Second)).Should().BeFalse();
    }

    [Fact]
    public void TryTake_RateTooSmallForTheInterval_AdmitsOneAndSaturatesInsteadOfOverflowing()
    {
        var bucket = new BorrowingTokenBucket(double.Epsilon);

        bucket.TryTake(Start).Should().BeTrue();
        bucket.Take(Start);
        bucket.TryTake(Start + (365L * 24 * 3600 * Second)).Should().BeFalse();
    }

    [Theory]
    [InlineData(double.MaxValue)]
    [InlineData(double.PositiveInfinity)]
    public void TryTake_RateTooLargeForTheClock_IsCappedAtOneTokenPerTick(double rate)
    {
        var bucket = new BorrowingTokenBucket(rate);

        Enumerable.Range(0, 1_000).Select(_ => bucket.TryTake(Start)).Should().AllBeEquivalentTo(true);
    }

    [Fact]
    public void TryTake_ConcurrentCallers_AdmitOnlyTheTokensInTheBucket()
    {
        // A full bucket of a million tokens, all due at one timestamp, raced for by twice as many calls.
        const int threadCount = 8;
        const int rate = 1_000_000;
        var bucket = new BorrowingTokenBucket(rate);
        var admitted = 0;
        RunConcurrently(
            threadCount,
            () =>
            {
                for (var i = 0; i < 2 * rate / threadCount; i++)
                {
                    if (bucket.TryTake(Start))
                    {
                        Interlocked.Increment(ref admitted);
                    }
                }
            });

        admitted.Should().Be(rate);
    }

    [Fact]
    public void Take_ConcurrentCallers_CountsEveryToken()
    {
        const int threadCount = 8;
        const int takesPerThread = 100_000;
        var bucket = new BorrowingTokenBucket(1);
        RunConcurrently(
            threadCount,
            () =>
            {
                for (var i = 0; i < takesPerThread; i++)
                {
                    bucket.Take(Start);
                }
            });

        var paidBack = Start + (threadCount * takesPerThread * Interval(1));
        bucket.TryTake(paidBack - 1).Should().BeFalse();
        bucket.TryTake(paidBack).Should().BeTrue();
    }

    // The bucket's interval in Stopwatch ticks, rounded the same way, so boundaries hold for any Stopwatch.Frequency.
    private static long Interval(double rate) => (long)Math.Round(Second / rate, MidpointRounding.ToEven);

    private static void RunConcurrently(int threadCount, Action action)
    {
        using var allStarted = new Barrier(threadCount);
        var threads = Enumerable.Range(0, threadCount)
                                .Select(_ => new Thread(() =>
                                 {
                                     allStarted.SignalAndWait();
                                     action();
                                 }))
                                .ToArray();

        foreach (var thread in threads)
        {
            thread.Start();
        }

        foreach (var thread in threads)
        {
            thread.Join();
        }
    }
}
