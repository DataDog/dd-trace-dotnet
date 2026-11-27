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
        var bucket = BorrowingTokenBucket.Create(rate);
        var interval = Interval(rate);

        Enumerable.Range(0, rate + 1).Count(_ => bucket.TryTake(Start)).Should().Be(rate);
        bucket.TryTake(Start + interval - 1).Should().BeFalse();
        bucket.TryTake(Start + interval).Should().BeTrue();
        bucket.TryTake(Start + interval).Should().BeFalse();
    }

    [Fact]
    public void TryTake_AfterAnIdlePeriod_HoldsNoMoreThanOneSecondOfTokens()
    {
        var bucket = BorrowingTokenBucket.Create(4);
        bucket.TryTake(Start).Should().BeTrue();

        var later = Start + (60 * Second);
        Enumerable.Range(0, 5).Select(_ => bucket.TryTake(later)).Should().Equal(true, true, true, true, false);
    }

    [Fact]
    public void Take_PastTheTokens_RefusesUntilTheRefillPaysTheDebtBack()
    {
        const int rate = 2;
        const int borrowed = 6;
        var bucket = BorrowingTokenBucket.Create(rate);
        Enumerable.Range(0, rate).Count(_ => bucket.TryTake(Start)).Should().Be(rate);

        for (var i = 0; i < borrowed; i++)
        {
            bucket.Take(Start);
        }

        // The empty bucket's next token was one interval away, and each borrowed token pushes it back one more.
        var paidBack = Start + ((borrowed + 1) * Interval(rate));
        bucket.TryTake(paidBack - 1).Should().BeFalse();
        bucket.TryTake(paidBack).Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(500)]
    [InlineData(1500)]
    public void TotalStaysWithinTheRateWhenKeptTokensArriveLater(int keepDelayMilliseconds)
    {
        // A parent is tried every millisecond, and each one admitted is followed by kept tokens after the delay.
        const int rate = 10;
        const int seconds = 100;
        const int keptPerSample = 3;
        var bucket = BorrowingTokenBucket.Create(rate);
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

        // A full bucket at the start, then the rate. Kept tokens that land late leave some debt at the end.
        const double expected = rate + (rate * seconds);
        total.Should().BeInRange((int)(expected * 0.98), (int)(expected * 1.02));
    }

    [Fact]
    public void Keep_OnAnEmptyBucket_BorrowsTheNextToken()
    {
        // On the real clock, at one token a minute so the refill can't land during the test.
        var bucket = BorrowingTokenBucket.Create(1.0 / 60);
        bucket.Sample().Should().BeTrue();
        bucket.Keep().Should().BeTrue();

        bucket.TryTake(Stopwatch.GetTimestamp() + Interval(1.0 / 60)).Should().BeFalse();
    }

    [Fact]
    public void TryTake_RateBelowOnePerSecond_AdmitsOnePerInterval()
    {
        var bucket = BorrowingTokenBucket.Create(1.0 / 300);
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
        var bucket = BorrowingTokenBucket.Create(2.5);
        var admitted = 0;
        for (var millisecond = 0; millisecond < 10_000; millisecond++)
        {
            admitted += bucket.TryTake(Start + (Second * millisecond / 1000)) ? 1 : 0;
        }

        // A full bucket, then the rate, with the fraction kept rather than truncated.
        admitted.Should().BeInRange(26, 27);
    }

    [Theory]
    [InlineData(0d, false)]
    [InlineData(-1d, false)]
    [InlineData(double.NaN, false)]
    [InlineData(double.Epsilon, true)] // Its interval overflows a long, so only the first token is ever due.
    public void TryTake_RateThatNeverRefills_AdmitsAtMostTheFirstToken(double rate, bool admitsFirst)
    {
        var bucket = BorrowingTokenBucket.Create(rate);

        bucket.TryTake(Start).Should().Be(admitsFirst);
        bucket.Take(Start);
        bucket.TryTake(Start + (365L * 24 * 3600 * Second)).Should().BeFalse();
    }

    [Theory]
    [InlineData(double.MaxValue)]
    [InlineData(double.PositiveInfinity)]
    public void TryTake_RateTooLargeForTheClock_AdmitsWithoutOverflowing(double rate)
    {
        var bucket = BorrowingTokenBucket.Create(rate);

        Enumerable.Range(0, 1_000).Select(_ => bucket.TryTake(Start)).Should().AllBeEquivalentTo(true);
    }

    [Fact]
    public void TryTake_ConcurrentCallers_AdmitOnlyTheTokensInTheBucket()
    {
        // A full bucket, all due at one timestamp, raced for by twice as many calls as it holds.
        const int threadCount = 8;
        const int rate = 1_000_000;
        var bucket = BorrowingTokenBucket.Create(rate);
        var admitted = 0;
        RunConcurrently(
            threadCount,
            _ =>
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
    public void TryTakeAndTake_ConcurrentCallers_AdmitAtMostTheBucketAndCountEveryToken()
    {
        // A bucket large enough that its tokens are still being raced for once every thread is running.
        const int threadCount = 8;
        const int rate = 100_000;
        const int callsPerThread = 50_000;
        var bucket = BorrowingTokenBucket.Create(rate);
        var admitted = 0;
        var takes = 0;
        RunConcurrently(
            threadCount,
            threadIndex =>
            {
                for (var i = 0; i < callsPerThread; i++)
                {
                    if (threadIndex % 2 == 0)
                    {
                        bucket.Take(Start);
                        Interlocked.Increment(ref takes);
                    }
                    else if (bucket.TryTake(Start))
                    {
                        Interlocked.Increment(ref admitted);
                    }
                }
            });

        admitted.Should().BeLessThanOrEqualTo(rate);

        // Each token admitted or taken moved the due time on one interval from that of a full bucket.
        var due = Start - ((rate - 1) * Interval(rate)) + ((long)(admitted + takes) * Interval(rate));
        bucket.TryTake(due - 1).Should().BeFalse();
        bucket.TryTake(due).Should().BeTrue();
    }

    // The bucket's interval in Stopwatch ticks, rounded the same way, so boundaries hold for any Stopwatch.Frequency.
    private static long Interval(double rate) => (long)Math.Round(Second / rate, MidpointRounding.ToEven);

    private static void RunConcurrently(int threadCount, Action<int> action)
    {
        using var allStarted = new Barrier(threadCount);
        var threads = Enumerable.Range(0, threadCount)
                                .Select(index => new Thread(() =>
                                 {
                                     allStarted.SignalAndWait();
                                     action(index);
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
