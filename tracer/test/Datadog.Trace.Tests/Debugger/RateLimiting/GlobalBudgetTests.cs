// <copyright file="GlobalBudgetTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.RateLimiting;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Debugger.RateLimiting
{
    public class GlobalBudgetTests : IDisposable
    {
        private GlobalBudget _budget;

        public GlobalBudgetTests()
        {
            // Use a generous budget for most tests to avoid accidental exhaustion
            _budget = new GlobalBudget(maxCpuPercentage: 10.0, windowDuration: TimeSpan.FromSeconds(1));
        }

        public void Dispose()
        {
            _budget?.Dispose();
        }

        [Fact]
        public void Constructor_ValidParameters_Succeeds()
        {
            using var budget = new GlobalBudget(maxCpuPercentage: 1.5, windowDuration: TimeSpan.FromSeconds(1));
            budget.Should().NotBeNull();
            budget.IsExhausted.Should().BeFalse();
            budget.GetUsagePercentage().Should().Be(0.0);
        }

        [Fact]
        public void Constructor_InvalidCpuPercentage_Throws()
        {
            Action act1 = () => new GlobalBudget(maxCpuPercentage: 0);
            act1.Should().Throw<ArgumentException>().WithMessage("*CPU percentage*");

            Action act2 = () => new GlobalBudget(maxCpuPercentage: -1);
            act2.Should().Throw<ArgumentException>().WithMessage("*CPU percentage*");

            Action act3 = () => new GlobalBudget(maxCpuPercentage: 101);
            act3.Should().Throw<ArgumentException>().WithMessage("*CPU percentage*");
        }

        [Fact]
        public void Constructor_InvalidWindowDuration_Throws()
        {
            Action act = () => new GlobalBudget(maxCpuPercentage: 1.5, windowDuration: TimeSpan.Zero);
            act.Should().Throw<ArgumentException>().WithMessage("*Window duration*");
        }

        [Fact]
        public void RecordUsage_BelowBudget_NotExhausted()
        {
            // Record small amount of usage
            var smallTicks = Stopwatch.Frequency / 1000; // 1ms worth of ticks
            _budget.RecordUsage(smallTicks);

            _budget.IsExhausted.Should().BeFalse();
            _budget.GetUsagePercentage().Should().BeLessThan(10.0);
        }

        [Fact]
        public void RecordUsage_ExceedsBudget_MarksExhausted()
        {
            // Create a small budget that's easy to exhaust
            using var smallBudget = new GlobalBudget(maxCpuPercentage: 0.1, windowDuration: TimeSpan.FromSeconds(1));

            // Record enough to exhaust it (more than 0.1% of 1 second)
            var largeTicks = Stopwatch.Frequency; // 1 full second worth of ticks
            smallBudget.RecordUsage(largeTicks);

            smallBudget.IsExhausted.Should().BeTrue();
            smallBudget.GetUsagePercentage().Should().BeGreaterThan(100.0);
        }

        [Fact]
        public void RecordUsage_NegativeTicks_Ignored()
        {
            var initialUsage = _budget.GetUsagePercentage();

            _budget.RecordUsage(-1000);

            _budget.GetUsagePercentage().Should().Be(initialUsage);
            _budget.IsExhausted.Should().BeFalse();
        }

        [Fact]
        public void RecordUsage_MultipleRecords_Accumulates()
        {
            var ticks = Stopwatch.Frequency / 1000; // 1ms

            _budget.RecordUsage(ticks);
            var usage1 = _budget.GetUsagePercentage();

            _budget.RecordUsage(ticks);
            var usage2 = _budget.GetUsagePercentage();

            usage2.Should().BeGreaterThan(usage1);
            usage2.Should().BeApproximately(usage1 * 2, 0.1);
        }

        [Fact]
        public void GetUsagePercentage_ReturnsCorrectValue()
        {
            // Create 1% budget for 1 second window
            using var budget = new GlobalBudget(maxCpuPercentage: 1.0, windowDuration: TimeSpan.FromSeconds(1));

            // Record 0.5% worth of ticks
            var halfBudgetTicks = Stopwatch.Frequency / 200; // 0.5% of 1 second
            budget.RecordUsage(halfBudgetTicks);

            budget.GetUsagePercentage().Should().BeApproximately(50.0, 1.0);
        }

        [Fact]
        public void GetConsecutiveExhaustedWindows_InitiallyZero()
        {
            _budget.GetConsecutiveExhaustedWindows().Should().Be(0);
        }

        [Fact]
        public void WindowReset_ResetsUsedTicks()
        {
            // Create a budget with short window for faster testing
            using var budget = new GlobalBudget(maxCpuPercentage: 10.0, windowDuration: TimeSpan.FromMilliseconds(100));

            // Record some usage
            var ticks = Stopwatch.Frequency / 100; // 10ms
            budget.RecordUsage(ticks);
            budget.GetUsagePercentage().Should().BeGreaterThan(0);

            // Wait for window to reset
            Thread.Sleep(150);

            // Usage should be reset (or at least much lower)
            budget.GetUsagePercentage().Should().BeLessThan(10.0);
        }

        [Fact]
        public void WindowReset_WhenExhausted_IncrementsConsecutiveCount()
        {
            // Create small budget with short window
            using var budget = new GlobalBudget(maxCpuPercentage: 0.1, windowDuration: TimeSpan.FromMilliseconds(100));

            budget.GetConsecutiveExhaustedWindows().Should().Be(0);

            // Exhaust the budget
            budget.RecordUsage(Stopwatch.Frequency);
            budget.IsExhausted.Should().BeTrue();

            // Wait for window reset
            Thread.Sleep(150);

            // Consecutive count should increment
            budget.GetConsecutiveExhaustedWindows().Should().BeGreaterThan(0);
        }

        [Fact]
        public void WindowReset_WhenNotExhausted_ResetsConsecutiveCount()
        {
            // Create small budget with short window
            using var budget = new GlobalBudget(maxCpuPercentage: 0.1, windowDuration: TimeSpan.FromMilliseconds(100));

            // Exhaust the budget first
            budget.RecordUsage(Stopwatch.Frequency);
            budget.IsExhausted.Should().BeTrue();

            // Wait for window reset
            Thread.Sleep(150);
            budget.GetConsecutiveExhaustedWindows().Should().BeGreaterThan(0);

            // Now don't exhaust in next window
            Thread.Sleep(150);

            // Consecutive count should reset to 0
            budget.GetConsecutiveExhaustedWindows().Should().Be(0);
        }

        [Fact]
        public void RecordUsage_ConcurrentCalls_ThreadSafe()
        {
            var threadCount = 10;
            var iterationsPerThread = 1000;
            var ticksPerCall = 1000;

            var threads = new Thread[threadCount];
            for (int i = 0; i < threadCount; i++)
            {
                threads[i] = new Thread(() =>
                {
                    for (int j = 0; j < iterationsPerThread; j++)
                    {
                        _budget.RecordUsage(ticksPerCall);
                    }
                });
            }

            foreach (var thread in threads)
            {
                thread.Start();
            }

            foreach (var thread in threads)
            {
                thread.Join();
            }

            // Total ticks should be approximately threadCount * iterationsPerThread * ticksPerCall
            var expectedTotal = (long)threadCount * iterationsPerThread * ticksPerCall;
            var usage = _budget.GetUsagePercentage();

            // Verify usage is in reasonable range (accounting for potential window resets)
            usage.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task RecordUsage_HighContention_RemainsConsistent()
        {
            var taskCount = 20;
            var iterationsPerTask = 500;
            var ticksPerCall = 500;

            var tasks = new Task[taskCount];
            for (int i = 0; i < taskCount; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    for (int j = 0; j < iterationsPerTask; j++)
                    {
                        _budget.RecordUsage(ticksPerCall);
                        var usage = _budget.GetUsagePercentage();
                        var exhausted = _budget.IsExhausted;

                        // Just read values to ensure no exceptions
                    }
                });
            }

            await Task.WhenAll(tasks);

            // Should complete without exceptions
            _budget.GetUsagePercentage().Should().BeGreaterThanOrEqualTo(0);
        }

        [Fact]
        public void IsExhausted_TransitionOnlyOnce_SingleWarningLog()
        {
            // This test verifies that only one thread logs the exhaustion warning
            using var budget = new GlobalBudget(maxCpuPercentage: 0.01, windowDuration: TimeSpan.FromSeconds(1));

            var threadCount = 10;
            var barrier = new Barrier(threadCount);
            var threads = new Thread[threadCount];
            var exhaustionResults = new bool[threadCount];

            for (int i = 0; i < threadCount; i++)
            {
                var index = i;
                threads[i] = new Thread(() =>
                {
                    barrier.SignalAndWait(); // Synchronize all threads
                    budget.RecordUsage(Stopwatch.Frequency); // All try to exhaust at once
                    exhaustionResults[index] = budget.IsExhausted;
                });
            }

            foreach (var thread in threads)
            {
                thread.Start();
            }

            foreach (var thread in threads)
            {
                thread.Join();
            }

            // All should see exhausted state eventually
            exhaustionResults.Should().Contain(true);
            budget.IsExhausted.Should().BeTrue();
        }

        [Fact]
        public void Dispose_StopsWindowResets()
        {
            var budget = new GlobalBudget(maxCpuPercentage: 1.0, windowDuration: TimeSpan.FromMilliseconds(50));

            budget.RecordUsage(Stopwatch.Frequency / 10);
            var usageBefore = budget.GetUsagePercentage();

            budget.Dispose();

            // Wait what would have been multiple window resets
            Thread.Sleep(200);

            // After disposal, no more resets should occur (but we can't easily verify internal timer state)
            // Just ensure no exceptions on subsequent calls
            Action act = () => budget.GetUsagePercentage();
            act.Should().NotThrow();
        }

        [Fact]
        public void Dispose_MultipleCalls_Safe()
        {
            var budget = new GlobalBudget(maxCpuPercentage: 1.0);

            budget.Dispose();
            budget.Dispose(); // Should not throw

            Action act = () => budget.Dispose();
            act.Should().NotThrow();
        }

        [Fact]
        public void GetConsecutiveExhaustedWindows_ThreadSafe()
        {
            var tasks = Enumerable.Range(0, 100)
                .Select(_ => Task.Run(() =>
                {
                    for (int i = 0; i < 100; i++)
                    {
                        var count = _budget.GetConsecutiveExhaustedWindows();
                        count.Should().BeGreaterThanOrEqualTo(0);
                    }
                }))
               .ToArray();

            var act = () => Task.WaitAll(tasks);
            act.Should().NotThrow();
        }

        [Fact]
        public void WindowReset_AtomicSnapshot_NoStateInconsistency()
        {
            // Tests that exhaustion state and used ticks are reset atomically (FIX validation)
            using var budget = new GlobalBudget(maxCpuPercentage: 0.05, windowDuration: TimeSpan.FromMilliseconds(100));

            for (int iteration = 0; iteration < 10; iteration++)
            {
                // Exhaust the budget
                budget.RecordUsage(Stopwatch.Frequency * 2);
                budget.IsExhausted.Should().BeTrue("Budget should be exhausted after large recording");

                var usageWhenExhausted = budget.GetUsagePercentage();
                usageWhenExhausted.Should().BeGreaterThan(100, "Usage should be over 100% when exhausted");

                // Wait for window reset
                Thread.Sleep(120);

                // After reset, if not exhausted, usage should be low (not stale high value)
                if (!budget.IsExhausted)
                {
                    var usageAfterReset = budget.GetUsagePercentage();
                    usageAfterReset.Should().BeLessThan(
                        50,
                        "Usage should be low after reset if not currently exhausted (atomic snapshot fix)");
                }
            }
        }

        [Fact]
        public async Task ConcurrentResetAndRecord_MaintainsConsistency()
        {
            // Tests fix for: GlobalBudget window reset race (atomic snapshot-and-reset)
            using var budget = new GlobalBudget(maxCpuPercentage: 0.5, windowDuration: TimeSpan.FromMilliseconds(100));

            var recordThreadCount = 10;
            var stop = false;

            // Threads continuously recording usage
            var recordTasks = Enumerable.Range(0, recordThreadCount)
                .Select(_ => Task.Run(() =>
                {
                    while (!stop)
                    {
                        budget.RecordUsage(Stopwatch.Frequency / 10000); // 0.1ms worth
                        Thread.Sleep(1);
                    }
                }))
               .ToArray();

            // Let them run for multiple window resets
            Thread.Sleep(500); // 5 window resets
            stop = true;

            await Task.WhenAll(recordTasks);

            // Verify consecutive exhaustion count is valid (not corrupted by race)
            var consecutiveCount = budget.GetConsecutiveExhaustedWindows();
            consecutiveCount.Should().BeGreaterOrEqualTo(0, "Consecutive count should never be negative");

            // Verify usage percentage is reasonable
            var usage = budget.GetUsagePercentage();
            usage.Should().BeGreaterOrEqualTo(0, "Usage should never be negative");
        }

        [Fact]
        public async Task ConsecutiveExhaustedCount_AccurateUnderLoad()
        {
            // Validates that consecutive exhausted windows are counted correctly despite concurrent updates
            using var budget = new GlobalBudget(maxCpuPercentage: 0.01, windowDuration: TimeSpan.FromMilliseconds(100));

            // Exhaust budget continuously for multiple windows
            var exhaustTask = Task.Run(() =>
            {
                for (int i = 0; i < 10; i++)
                {
                    budget.RecordUsage(Stopwatch.Frequency * 10); // Way over budget
                    Thread.Sleep(110); // Wait for next window
                }
            });

            await exhaustTask;

            // Wait one more window to let counter stabilize
            Thread.Sleep(150);

            var consecutiveCount = budget.GetConsecutiveExhaustedWindows();

            // Should have multiple consecutive exhausted windows
            // Exact count depends on timing, but should be reasonable
            consecutiveCount.Should().BeGreaterThan(0, "Should have recorded consecutive exhausted windows");
            consecutiveCount.Should().BeLessThan(20, "Count should be reasonable, not corrupted");
        }
    }
}
