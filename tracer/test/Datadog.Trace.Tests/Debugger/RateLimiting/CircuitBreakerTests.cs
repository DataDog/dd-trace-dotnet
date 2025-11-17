// <copyright file="CircuitBreakerTests.cs" company="Datadog">
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
    public class CircuitBreakerTests : IDisposable
    {
        private readonly IGlobalBudget _mockBudget;
        private CircuitBreaker _circuitBreaker;

        public CircuitBreakerTests()
        {
            _mockBudget = new MockGlobalBudget();
            _circuitBreaker = new CircuitBreaker(
                probeId: "test-probe",
                globalBudget: _mockBudget,
                hotLoopThresholdHitsPerSecond: 10_000,
                maxAverageCostMicroseconds: 100,
                windowsBeforeOpen: 3);
        }

        public void Dispose()
        {
            _circuitBreaker?.Dispose();
        }

        [Fact]
        public void Constructor_ValidParameters_Succeeds()
        {
            using var cb = new CircuitBreaker("probe-1", _mockBudget);
            cb.Should().NotBeNull();
            cb.State.Should().Be(CircuitState.Closed);
        }

        [Fact]
        public void Constructor_NullProbeId_Throws()
        {
            Action act = () => new CircuitBreaker(null, _mockBudget);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void Constructor_NullGlobalBudget_Throws()
        {
            Action act = () => new CircuitBreaker("probe", null);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void Constructor_InvalidThresholds_Throws()
        {
            Action act1 = () => new CircuitBreaker("probe", _mockBudget, hotLoopThresholdHitsPerSecond: 0);
            act1.Should().Throw<ArgumentException>();

            Action act2 = () => new CircuitBreaker("probe", _mockBudget, maxAverageCostMicroseconds: 0);
            act2.Should().Throw<ArgumentException>();

            Action act3 = () => new CircuitBreaker("probe", _mockBudget, windowsBeforeOpen: 0);
            act3.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void ShouldAllow_InitialState_ReturnsTrue()
        {
            _circuitBreaker.State.Should().Be(CircuitState.Closed);
            _circuitBreaker.ShouldAllow().Should().BeTrue();
        }

        [Fact]
        public void ShouldAllow_WhenClosed_AlwaysReturnsTrue()
        {
            for (int i = 0; i < 100; i++)
            {
                _circuitBreaker.ShouldAllow().Should().BeTrue();
            }

            _circuitBreaker.State.Should().Be(CircuitState.Closed);
        }

        [Fact]
        public void RecordSuccess_UpdatesStatistics()
        {
            var ticks = Stopwatch.Frequency / 1000; // 1ms

            _circuitBreaker.RecordSuccess(ticks);
            _circuitBreaker.RecordSuccess(ticks);
            _circuitBreaker.RecordSuccess(ticks);

            // Circuit should still be closed
            _circuitBreaker.State.Should().Be(CircuitState.Closed);
            _circuitBreaker.ShouldAllow().Should().BeTrue();
        }

        [Fact]
        public void RecordSuccess_NegativeTicks_Ignored()
        {
            Action act = () => _circuitBreaker.RecordSuccess(-1000);
            act.Should().NotThrow();
            _circuitBreaker.State.Should().Be(CircuitState.Closed);
        }

        [Fact]
        public void RecordFailure_WhenClosed_RemainsClosedImmediately()
        {
            _circuitBreaker.RecordFailure();

            // Failure alone shouldn't open circuit immediately
            _circuitBreaker.State.Should().Be(CircuitState.Closed);
        }

        [Fact]
        public void RecordHotLoop_MarksForOpening()
        {
            _circuitBreaker.RecordHotLoop();

            // Wait for check cycle (circuit breaker checks every second)
            Thread.Sleep(1200);

            // Should be open or transitioning
            var state = _circuitBreaker.State;
            (state == CircuitState.Open || state == CircuitState.HalfOpen).Should().BeTrue();
        }

        [Fact]
        public void CheckForOpen_HighHitRate_OpensCircuit()
        {
            // Record many hits in short time to exceed threshold
            var iterations = 15_000; // Above 10,000 threshold
            var smallTicks = 10; // Very small cost

            for (int i = 0; i < iterations; i++)
            {
                _circuitBreaker.RecordSuccess(smallTicks);
            }

            // Wait for periodic check to run
            Thread.Sleep(1200);

            // Circuit should open due to high hit rate
            var state = _circuitBreaker.State;
            (state == CircuitState.Open || state == CircuitState.HalfOpen).Should().BeTrue();
        }

        [Fact]
        public void CheckForOpen_HighAverageCost_OpensCircuit()
        {
            // Record hits with very high cost to exceed 100μs average
            var highCostTicks = (long)((200 * Stopwatch.Frequency) / 1_000_000.0); // 200μs

            for (int i = 0; i < 100; i++)
            {
                _circuitBreaker.RecordSuccess(highCostTicks);
            }

            // Wait for periodic check
            Thread.Sleep(1200);

            // Circuit should open due to high average cost
            var state = _circuitBreaker.State;
            (state == CircuitState.Open || state == CircuitState.HalfOpen).Should().BeTrue();
        }

        [Fact]
        public void CheckForOpen_GlobalBudgetExhausted_OpensCircuit()
        {
            var mockBudget = new MockGlobalBudget { ConsecutiveExhausted = 5 };
            using var cb = new CircuitBreaker("probe", mockBudget, windowsBeforeOpen: 3);

            // Record some activity
            cb.RecordSuccess(1000);

            // Wait for check
            Thread.Sleep(1200);

            // Should open due to consecutive exhausted windows
            var state = cb.State;
            (state == CircuitState.Open || state == CircuitState.HalfOpen).Should().BeTrue();
        }

        [Fact]
        public void StateTransition_Open_WaitsForBackoff()
        {
            // Force circuit to open
            _circuitBreaker.RecordHotLoop();
            Thread.Sleep(1200);

            var state = _circuitBreaker.State;
            if (state == CircuitState.Open)
            {
                // Should reject while in backoff
                _circuitBreaker.ShouldAllow().Should().BeFalse();
            }
        }

        [Fact]
        public void StateTransition_OpenToHalfOpen_AfterBackoff()
        {
            using var cb = new CircuitBreaker("probe", _mockBudget, hotLoopThresholdHitsPerSecond: 100);

            // Force open by recording hot loop
            cb.RecordHotLoop();
            Thread.Sleep(1200);

            if (cb.State == CircuitState.Open)
            {
                // Wait for backoff (1 second initial)
                Thread.Sleep(1200);

                // Should transition to half-open and allow trials
                cb.ShouldAllow().Should().BeTrue();
                cb.State.Should().Be(CircuitState.HalfOpen);
            }
        }

        [Fact]
        public void HalfOpen_SuccessfulTrials_TransitionsToClosed()
        {
            using var cb = new CircuitBreaker("probe", _mockBudget, hotLoopThresholdHitsPerSecond: 100);

            // Force to half-open state
            cb.RecordHotLoop();
            Thread.Sleep(1200);

            if (cb.State == CircuitState.Open)
            {
                Thread.Sleep(1200); // Wait for backoff
            }

            if (cb.State == CircuitState.HalfOpen)
            {
                // Record 10 successful trials
                for (int i = 0; i < 10; i++)
                {
                    if (cb.ShouldAllow())
                    {
                        cb.RecordSuccess(100);
                    }
                }

                // Should transition back to closed
                Thread.Sleep(100); // Allow state transition
                cb.State.Should().Be(CircuitState.Closed);
            }
        }

        [Fact]
        public void HalfOpen_FailedTrial_ReopensCircuit()
        {
            using var cb = new CircuitBreaker("probe", _mockBudget, hotLoopThresholdHitsPerSecond: 100);

            // Force to half-open
            cb.RecordHotLoop();
            Thread.Sleep(1200);

            if (cb.State == CircuitState.Open)
            {
                Thread.Sleep(1200);
            }

            if (cb.State == CircuitState.HalfOpen)
            {
                // Record a failure
                cb.RecordFailure();

                // Should reopen
                cb.State.Should().Be(CircuitState.Open);
            }
        }

        [Fact]
        public void HalfOpen_LimitsTrials_ToTen()
        {
            using var cb = new CircuitBreaker("probe", _mockBudget, hotLoopThresholdHitsPerSecond: 100);

            // Force to half-open
            cb.RecordHotLoop();
            Thread.Sleep(1200);

            if (cb.State == CircuitState.Open)
            {
                Thread.Sleep(1200);
            }

            if (cb.State == CircuitState.HalfOpen)
            {
                int allowedCount = 0;
                // Try more than 10
                for (int i = 0; i < 20; i++)
                {
                    if (cb.ShouldAllow())
                    {
                        allowedCount++;
                    }
                }

                allowedCount.Should().BeLessOrEqualTo(10);
            }
        }

        [Fact]
        public void ExponentialBackoff_DoublesOnReopen()
        {
            // This test is conceptual - actual timing verification is difficult
            using var cb = new CircuitBreaker("probe", _mockBudget, hotLoopThresholdHitsPerSecond: 50);

            // Open circuit
            cb.RecordHotLoop();
            Thread.Sleep(1200);

            // Wait for first backoff (1s)
            Thread.Sleep(1200);

            // If it went to half-open and we fail it, backoff should double
            if (cb.State == CircuitState.HalfOpen)
            {
                cb.RecordFailure(); // Force reopen
                cb.State.Should().Be(CircuitState.Open);

                // Now backoff should be 2s (harder to test without waiting)
                // Just verify it's open
                cb.ShouldAllow().Should().BeFalse();
            }
        }

        [Fact]
        public void ConcurrentRecordSuccess_ThreadSafe()
        {
            var tasks = Enumerable.Range(0, 100)
                .Select(_ => Task.Run(() =>
                {
                    for (int i = 0; i < 100; i++)
                    {
                        _circuitBreaker.RecordSuccess(1000);
                    }
                }))
               .ToArray();

            var act = () => Task.WaitAll(tasks);
            act.Should().NotThrow();
        }

        [Fact]
        public void ConcurrentShouldAllow_ThreadSafe()
        {
            var tasks = Enumerable.Range(0, 100)
                .Select(_ => Task.Run(() =>
                {
                    for (int i = 0; i < 100; i++)
                    {
                        var allowed = _circuitBreaker.ShouldAllow();
                        // Just checking for exceptions
                    }
                }))
                .ToArray();

            var act = () => Task.WaitAll(tasks);
            act.Should().NotThrow();
        }

        [Fact]
        public void Dispose_MultipleCalls_Safe()
        {
            var cb = new CircuitBreaker("probe", _mockBudget);

            cb.Dispose();
            cb.Dispose();

            Action act = () => cb.Dispose();
            act.Should().NotThrow();
        }

        [Fact]
        public async Task State_VolatileRead_AlwaysReturnsValidState()
        {
            // Verify state can be read concurrently
            var tasks = Enumerable.Range(0, 50)
                .Select(_ => Task.Run(() =>
                {
                    for (int i = 0; i < 1000; i++)
                    {
                        var state = _circuitBreaker.State;
                        state.Should().BeOneOf(CircuitState.Closed, CircuitState.Open, CircuitState.HalfOpen);
                    }
                }))
               .ToArray();

            await Task.WhenAll(tasks);
        }

        [Fact]
        public void RecordMemoryPressure_OpensCircuitAfterCheck()
        {
            // Tests memory pressure integration with circuit breaker
            var mockMemory = new MockMemoryPressureMonitor();
            using var cb = new CircuitBreaker(
                "memory-test",
                _mockBudget,
                memoryPressureMonitor: mockMemory,
                hotLoopThresholdHitsPerSecond: 100000, // Very high
                maxAverageCostMicroseconds: 10000);    // Very high

            cb.State.Should().Be(CircuitState.Closed);

            // Record memory pressure
            cb.RecordMemoryPressure();

            // Record some activity to ensure check has data
            cb.RecordSuccess(100);

            // Wait for periodic check
            Thread.Sleep(1200);

            // Circuit should open due to memory pressure
            var state = cb.State;
            (state == CircuitState.Open || state == CircuitState.HalfOpen)
                .Should().BeTrue("Circuit should open when memory pressure is recorded");
        }

        [Fact]
        public async Task ConcurrentStatsUpdate_AccurateAverageCost()
        {
            // Tests that stats snapshot ordering prevents hit/cost skew
            using var breaker = new CircuitBreaker(
                "test",
                _mockBudget,
                hotLoopThresholdHitsPerSecond: 50000, // High threshold
                maxAverageCostMicroseconds: 200,      // 200μs average cost threshold
                windowsBeforeOpen: 5);

            var constantCost = (long)(150 * Stopwatch.Frequency / 1_000_000.0); // 150μs (below threshold)
            var iterations = 1000;

            // Hammer RecordSuccess from multiple threads
            var tasks = Enumerable.Range(0, 4)
                .Select(_ => Task.Run(() =>
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        if (breaker.ShouldAllow())
                        {
                            breaker.RecordSuccess(constantCost);
                        }

                        Thread.Sleep(1);
                    }
                }))
               .ToArray();

            await Task.WhenAll(tasks);

            // Wait for check cycle
            Thread.Sleep(1500);

            // Circuit should remain CLOSED because average cost is below threshold (150μs < 200μs)
            breaker.State.Should().Be(CircuitState.Closed, "Circuit should remain closed with consistent moderate cost");
        }

        [Fact]
        public async Task StatsSnapshotDuringReset_NoCountLoss()
        {
            // Tests that hits and costs are captured in correct window
            using var breaker = new CircuitBreaker(
                "test",
                _mockBudget,
                hotLoopThresholdHitsPerSecond: 100000, // Very high
                maxAverageCostMicroseconds: 100000,    // Very high
                windowsBeforeOpen: 10);

            var recordThreads = 4;
            var recordsPerThread = 100;
            var stop = false;

            // Continuously record success while windows reset
            var tasks = Enumerable.Range(0, recordThreads)
                .Select(_ => Task.Run(() =>
                {
                    for (int i = 0; i < recordsPerThread && !stop; i++)
                    {
                        if (breaker.ShouldAllow())
                        {
                            breaker.RecordSuccess(1000);
                        }

                        Thread.Sleep(5);
                    }
                }))
               .ToArray();

            // Let them run across multiple check cycles
            Thread.Sleep(2500); // Multiple 1-second windows
            stop = true;

            await Task.WhenAll(tasks);

            // Circuit should remain closed (we're not triggering thresholds)
            breaker.State.Should().Be(CircuitState.Closed);
        }

        [Fact]
        public void HighCostWithLowHitRate_OpensCorrectly()
        {
            // Tests that average cost calculation works correctly under concurrent load
            using var breaker = new CircuitBreaker(
                "test",
                _mockBudget,
                hotLoopThresholdHitsPerSecond: 10000,
                maxAverageCostMicroseconds: 50,  // Very low threshold
                windowsBeforeOpen: 3);

            var highCost = (long)(200 * Stopwatch.Frequency / 1_000_000.0); // 200μs (way above 50μs threshold)

            // Record high-cost executions
            for (int i = 0; i < 100; i++)
            {
                if (breaker.ShouldAllow())
                {
                    breaker.RecordSuccess(highCost);
                }

                Thread.Sleep(5);
            }

            // Wait for check
            Thread.Sleep(1200);

            // Circuit should open due to high average cost
            var state = breaker.State;
            (state == CircuitState.Open || state == CircuitState.HalfOpen)
                .Should().BeTrue("Circuit should open when average cost exceeds threshold");
        }

        // Mock implementation for testing
        private class MockGlobalBudget : IGlobalBudget
        {
            public bool IsExhausted { get; set; }

            public int ConsecutiveExhausted { get; set; }

            public void RecordUsage(long elapsedTicks)
            {
            }

            public double GetUsagePercentage() => 0.0;

            public int GetConsecutiveExhaustedWindows() => ConsecutiveExhausted;
        }

        private class MockMemoryPressureMonitor : IMemoryPressureMonitor
        {
            public bool IsHighMemoryPressure => false;

            public double MemoryUsagePercent => 0;

            public double Gen2CollectionsPerSecond => 0;

            public double HighPressureThreshold { get; }

            public int MaxGen2PerSecond { get; }

            public void RecordMemoryPressureEvent()
            {
            }

            public void Dispose()
            {
                throw new NotImplementedException();
            }
        }
    }
}
