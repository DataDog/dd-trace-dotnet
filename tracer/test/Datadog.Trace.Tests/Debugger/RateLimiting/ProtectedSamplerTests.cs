// <copyright file="ProtectedSamplerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading;
using Datadog.Trace.Debugger.RateLimiting;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Debugger.RateLimiting
{
    public class ProtectedSamplerTests : IDisposable
    {
        private MockAdaptiveSampler _innerSampler;
        private MockCircuitBreaker _circuitBreaker;
        private MockGlobalBudget _globalBudget;
        private ProtectedSampler _protectedSampler;

        public ProtectedSamplerTests()
        {
            _innerSampler = new MockAdaptiveSampler();
            _circuitBreaker = new MockCircuitBreaker();
            _globalBudget = new MockGlobalBudget();
            _protectedSampler = new ProtectedSampler("test-probe", _innerSampler, _circuitBreaker, _globalBudget);
        }

        public void Dispose()
        {
            _protectedSampler?.Dispose();
        }

        [Fact]
        public void Constructor_ValidParameters_Succeeds()
        {
            using var sampler = new ProtectedSampler("probe", _innerSampler, _circuitBreaker, _globalBudget);
            sampler.Should().NotBeNull();
            sampler.CircuitState.Should().Be(CircuitState.Closed);
        }

        [Fact]
        public void Constructor_NullParameters_Throws()
        {
            Action act1 = () => new ProtectedSampler(null, _innerSampler, _circuitBreaker, _globalBudget);
            act1.Should().Throw<ArgumentNullException>();

            Action act2 = () => new ProtectedSampler("probe", null, _circuitBreaker, _globalBudget);
            act2.Should().Throw<ArgumentNullException>();

            Action act3 = () => new ProtectedSampler("probe", _innerSampler, null, _globalBudget);
            act3.Should().Throw<ArgumentNullException>();

            Action act4 = () => new ProtectedSampler("probe", _innerSampler, _circuitBreaker, null);
            act4.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void Sample_KillSwitchEnabled_ReturnsFalse()
        {
            _protectedSampler.KillSwitch = true;

            _protectedSampler.Sample().Should().BeFalse();

            // Other layers should not be consulted
            _innerSampler.SampleCalled.Should().BeFalse();
        }

        [Fact]
        public void Sample_KillSwitchDisabled_ProceedsToNextLayer()
        {
            _protectedSampler.KillSwitch = false;
            _innerSampler.ShouldSample = true;

            _protectedSampler.Sample().Should().BeTrue();
        }

        [Fact]
        public void Sample_GlobalBudgetExhausted_ReturnsFalse()
        {
            ThreadLocalPrefilter.SetFilterMask(0); // Disable prefilter for this test
            _globalBudget.IsExhausted = true;

            _protectedSampler.Sample().Should().BeFalse();

            // Inner sampler should not be called
            _innerSampler.SampleCalled.Should().BeFalse();

            // Cleanup
            ThreadLocalPrefilter.SetFilterMask(0);
        }

        [Fact]
        public void Sample_CircuitOpen_ReturnsFalse()
        {
            ThreadLocalPrefilter.SetFilterMask(0); // Disable prefilter
            _globalBudget.IsExhausted = false;
            _circuitBreaker.ShouldAllowValue = false;

            _protectedSampler.Sample().Should().BeFalse();

            // Inner sampler should not be called
            _innerSampler.SampleCalled.Should().BeFalse();

            // Cleanup
            ThreadLocalPrefilter.SetFilterMask(0);
        }

        [Fact]
        public void Sample_InnerSamplerRejects_ReturnsFalse()
        {
            ThreadLocalPrefilter.SetFilterMask(0); // Disable prefilter
            _globalBudget.IsExhausted = false;
            _circuitBreaker.ShouldAllowValue = true;
            _innerSampler.ShouldSample = false;

            _protectedSampler.Sample().Should().BeFalse();

            // Inner sampler should have been called
            _innerSampler.SampleCalled.Should().BeTrue();

            // Cleanup
            ThreadLocalPrefilter.SetFilterMask(0);
        }

        [Fact]
        public void Sample_AllLayersPass_ReturnsTrue()
        {
            ThreadLocalPrefilter.SetFilterMask(0); // Disable prefilter
            _protectedSampler.KillSwitch = false;
            _globalBudget.IsExhausted = false;
            _circuitBreaker.ShouldAllowValue = true;
            _innerSampler.ShouldSample = true;

            _protectedSampler.Sample().Should().BeTrue();

            // Cleanup
            ThreadLocalPrefilter.SetFilterMask(0);
        }

        [Fact]
        public void SampleWithBehaviour_KillSwitch_ReturnsSkip()
        {
            _protectedSampler.KillSwitch = true;

            var result = _protectedSampler.SampleWithBehaviour(out var behaviour);

            result.Should().BeFalse();
            behaviour.Should().Be(CaptureBehaviour.Skip);
        }

        [Fact]
        public void SampleWithBehaviour_BudgetExhausted_ReturnsSkip()
        {
            ThreadLocalPrefilter.SetFilterMask(0);
            _protectedSampler.KillSwitch = false;
            _globalBudget.IsExhausted = true;

            var result = _protectedSampler.SampleWithBehaviour(out var behaviour);

            result.Should().BeFalse();
            behaviour.Should().Be(CaptureBehaviour.Skip);

            ThreadLocalPrefilter.SetFilterMask(0);
        }

        [Fact]
        public void SampleWithBehaviour_CircuitOpen_ReturnsSkip()
        {
            ThreadLocalPrefilter.SetFilterMask(0);
            _protectedSampler.KillSwitch = false;
            _globalBudget.IsExhausted = false;
            _circuitBreaker.ShouldAllowValue = false;

            var result = _protectedSampler.SampleWithBehaviour(out var behaviour);

            result.Should().BeFalse();
            behaviour.Should().Be(CaptureBehaviour.Skip);

            ThreadLocalPrefilter.SetFilterMask(0);
        }

        [Fact]
        public void SampleWithBehaviour_LowPressure_ReturnsFull()
        {
            ThreadLocalPrefilter.SetFilterMask(0);
            _protectedSampler.KillSwitch = false;
            _globalBudget.IsExhausted = false;
            _globalBudget.UsagePercentage = 25.0;
            _circuitBreaker.ShouldAllowValue = true;
            _circuitBreaker.StateValue = CircuitState.Closed;
            _innerSampler.ShouldSample = true;

            var result = _protectedSampler.SampleWithBehaviour(out var behaviour);

            result.Should().BeTrue();
            behaviour.Should().Be(CaptureBehaviour.Full);

            ThreadLocalPrefilter.SetFilterMask(0);
        }

        [Fact]
        public void SampleWithBehaviour_HighPressure_ReturnsLight()
        {
            ThreadLocalPrefilter.SetFilterMask(0);
            _protectedSampler.KillSwitch = false;
            _globalBudget.IsExhausted = false;
            _globalBudget.UsagePercentage = 80.0;
            _circuitBreaker.ShouldAllowValue = true;
            _circuitBreaker.StateValue = CircuitState.Closed;
            _innerSampler.ShouldSample = true;

            var result = _protectedSampler.SampleWithBehaviour(out var behaviour);

            result.Should().BeTrue();
            behaviour.Should().Be(CaptureBehaviour.Light);

            ThreadLocalPrefilter.SetFilterMask(0);
        }

        [Fact]
        public void SampleWithBehaviour_HalfOpen_ReturnsLight()
        {
            ThreadLocalPrefilter.SetFilterMask(0);
            _protectedSampler.KillSwitch = false;
            _globalBudget.IsExhausted = false;
            _globalBudget.UsagePercentage = 25.0;
            _circuitBreaker.ShouldAllowValue = true;
            _circuitBreaker.StateValue = CircuitState.HalfOpen;
            _innerSampler.ShouldSample = true;

            var result = _protectedSampler.SampleWithBehaviour(out var behaviour);

            result.Should().BeTrue();
            behaviour.Should().Be(CaptureBehaviour.Light);

            ThreadLocalPrefilter.SetFilterMask(0);
        }

        [Fact]
        public void SampleWithBehaviour_ModeratePressure_MixOfFullAndLight()
        {
            ThreadLocalPrefilter.SetFilterMask(0);
            _protectedSampler.KillSwitch = false;
            _globalBudget.IsExhausted = false;
            _globalBudget.UsagePercentage = 60.0; // Moderate pressure
            _circuitBreaker.ShouldAllowValue = true;
            _circuitBreaker.StateValue = CircuitState.Closed;
            _innerSampler.ShouldSample = true;

            var fullCount = 0;
            var lightCount = 0;
            var iterations = 100;

            for (int i = 0; i < iterations; i++)
            {
                var result = _protectedSampler.SampleWithBehaviour(out var behaviour);
                if (result)
                {
                    if (behaviour == CaptureBehaviour.Full)
                    {
                        fullCount++;
                    }
                    else if (behaviour == CaptureBehaviour.Light)
                    {
                        lightCount++;
                    }
                }
            }

            // Should have mix of both (approximately 50/50)
            fullCount.Should().BeGreaterThan(0);
            lightCount.Should().BeGreaterThan(0);
            (fullCount + lightCount).Should().Be(iterations);

            ThreadLocalPrefilter.SetFilterMask(0);
        }

        [Fact]
        public void RecordExecution_UpdatesGlobalBudget()
        {
            var ticks = 1000L;

            _protectedSampler.RecordExecution(ticks, success: true);

            _globalBudget.RecordedTicks.Should().Be(ticks);
        }

        [Fact]
        public void RecordExecution_Success_UpdatesCircuitBreaker()
        {
            var ticks = 1000L;

            _protectedSampler.RecordExecution(ticks, success: true);

            _circuitBreaker.SuccessCalled.Should().BeTrue();
            _circuitBreaker.RecordedTicks.Should().Be(ticks);
            _circuitBreaker.FailureCalled.Should().BeFalse();
        }

        [Fact]
        public void RecordExecution_Failure_UpdatesCircuitBreaker()
        {
            var ticks = 1000L;

            _protectedSampler.RecordExecution(ticks, success: false);

            _circuitBreaker.FailureCalled.Should().BeTrue();
            _circuitBreaker.SuccessCalled.Should().BeFalse();
        }

        [Fact]
        public void MarkHotLoop_CallsCircuitBreaker()
        {
            _protectedSampler.MarkHotLoop();

            _circuitBreaker.HotLoopCalled.Should().BeTrue();
        }

        [Fact]
        public void Keep_DelegatesToInnerSampler()
        {
            _innerSampler.KeepResult = true;

            _protectedSampler.Keep().Should().BeTrue();
            _innerSampler.KeepCalled.Should().BeTrue();
        }

        [Fact]
        public void Drop_DelegatesToInnerSampler()
        {
            _innerSampler.DropResult = false;

            _protectedSampler.Drop().Should().BeFalse();
            _innerSampler.DropCalled.Should().BeTrue();
        }

        [Fact]
        public void NextDouble_DelegatesToInnerSampler()
        {
            _innerSampler.NextDoubleResult = 0.42;

            _protectedSampler.NextDouble().Should().Be(0.42);
        }

        [Fact]
        public void CircuitState_ReflectsCircuitBreakerState()
        {
            _circuitBreaker.StateValue = CircuitState.Open;
            _protectedSampler.CircuitState.Should().Be(CircuitState.Open);

            _circuitBreaker.StateValue = CircuitState.HalfOpen;
            _protectedSampler.CircuitState.Should().Be(CircuitState.HalfOpen);

            _circuitBreaker.StateValue = CircuitState.Closed;
            _protectedSampler.CircuitState.Should().Be(CircuitState.Closed);
        }

        [Fact]
        public void Dispose_DisposesInnerComponents()
        {
            _protectedSampler.Dispose();

            _innerSampler.DisposeCalled.Should().BeTrue();
            _circuitBreaker.DisposeCalled.Should().BeTrue();
        }

        [Fact]
        public void MemoryPressureMarker_OneShotPerPeriod()
        {
            // Tests that memory pressure is recorded ONCE per sustained pressure period, not on every call
            var mockSampler = new MockAdaptiveSampler();
            var mockCircuit = new MockCircuitBreaker();
            var mockBudget = new MockGlobalBudget();
            var mockMemory = new MockMemoryPressureMonitor { IsHighPressureValue = true };

            using var sampler = new ProtectedSampler("test", mockSampler, mockCircuit, mockBudget, mockMemory);

            ThreadLocalPrefilter.SetFilterMask(0);

            // Call Sample() multiple times under sustained memory pressure
            for (int i = 0; i < 100; i++)
            {
                sampler.Sample(); // All should return false due to memory pressure
            }

            // Circuit breaker should be notified only ONCE, not 100 times
            mockCircuit.MemoryPressureCallCount.Should().Be(1, "Should record memory pressure only once per sustained period");

            ThreadLocalPrefilter.SetFilterMask(0);
        }

        [Fact]
        public void MemoryPressureMarker_ResetsWhenPressureClears()
        {
            // Tests that marker resets when pressure clears, allowing future detection
            var mockSampler = new MockAdaptiveSampler { ShouldSample = true };
            var mockCircuit = new MockCircuitBreaker();
            var mockBudget = new MockGlobalBudget();
            var mockMemory = new MockMemoryPressureMonitor();

            using var sampler = new ProtectedSampler("test", mockSampler, mockCircuit, mockBudget, mockMemory);

            ThreadLocalPrefilter.SetFilterMask(0);

            // First pressure period
            mockMemory.IsHighPressureValue = true;
            for (int i = 0; i < 10; i++)
            {
                sampler.Sample();
            }

            var firstCount = mockCircuit.MemoryPressureCallCount;
            firstCount.Should().Be(1, "First pressure period should record once");

            // Pressure clears
            mockMemory.IsHighPressureValue = false;
            sampler.Sample(); // This should reset the marker

            // Second pressure period
            mockMemory.IsHighPressureValue = true;
            for (int i = 0; i < 10; i++)
            {
                sampler.Sample();
            }

            mockCircuit.MemoryPressureCallCount.Should().Be(2, "Second pressure period should record again");

            ThreadLocalPrefilter.SetFilterMask(0);
        }

        [Fact]
        public void Degradation_UsesSharedRandom_NoAllocations()
        {
            // Tests that degradation logic uses ThreadSafeRandom.Shared (no allocations)
            var mockSampler = new MockAdaptiveSampler { ShouldSample = true };
            var mockCircuit = new MockCircuitBreaker { StateValue = CircuitState.Closed };
            var mockBudget = new MockGlobalBudget { UsagePercentage = 60.0 }; // Moderate pressure
            var mockMemory = new MockMemoryPressureMonitor { MemoryUsagePercentValue = 65.0 };

            using var sampler = new ProtectedSampler("test", mockSampler, mockCircuit, mockBudget, mockMemory);

            ThreadLocalPrefilter.SetFilterMask(0);

            // Force GC to get baseline
            GC.Collect(2, GCCollectionMode.Forced, blocking: true);
            GC.WaitForPendingFinalizers();
            var gen0Before = GC.CollectionCount(0);

            // Call SampleWithBehaviour many times (should use ThreadSafeRandom.Shared, no allocations)
            for (int i = 0; i < 1000; i++)
            {
                sampler.SampleWithBehaviour(out var behaviour);
            }

            var gen0After = GC.CollectionCount(0);

            // Should have zero or minimal Gen0 collections
            (gen0After - gen0Before).Should().BeLessThan(2, "Should have minimal allocations in degradation path");

            ThreadLocalPrefilter.SetFilterMask(0);
        }

        // Mock implementations for testing
        private class MockAdaptiveSampler : IAdaptiveSampler, IDisposable
        {
            public bool ShouldSample { get; set; } = true;

            public bool SampleCalled { get; set; }

            public bool KeepCalled { get; set; }

            public bool DropCalled { get; set; }

            public bool KeepResult { get; set; } = true;

            public bool DropResult { get; set; } = false;

            public double NextDoubleResult { get; set; } = 0.5;

            public bool DisposeCalled { get; set; }

            public bool Sample()
            {
                SampleCalled = true;
                return ShouldSample;
            }

            public bool Keep()
            {
                KeepCalled = true;
                return KeepResult;
            }

            public bool Drop()
            {
                DropCalled = true;
                return DropResult;
            }

            public double NextDouble() => NextDoubleResult;

            public void Dispose() => DisposeCalled = true;
        }

        private class MockCircuitBreaker : ICircuitBreaker, IDisposable
        {
            public CircuitState StateValue { get; set; } = CircuitState.Closed;

            public bool ShouldAllowValue { get; set; } = true;

            public bool SuccessCalled { get; set; }

            public bool FailureCalled { get; set; }

            public bool HotLoopCalled { get; set; }

            public long RecordedTicks { get; set; }

            public bool DisposeCalled { get; set; }

            public int MemoryPressureCallCount { get; set; }

            public CircuitState State => StateValue;

            public bool ShouldAllow() => ShouldAllowValue;

            public void RecordSuccess(long elapsedTicks)
            {
                SuccessCalled = true;
                RecordedTicks = elapsedTicks;
            }

            public void RecordFailure()
            {
                FailureCalled = true;
            }

            public void RecordHotLoop()
            {
                HotLoopCalled = true;
            }

            public void RecordMemoryPressure()
            {
                MemoryPressureCallCount++;
            }

            public void Dispose()
            {
                DisposeCalled = true;
            }
        }

        private class MockGlobalBudget : IGlobalBudget
        {
            public bool IsExhausted { get; set; }

            public double UsagePercentage { get; set; }

            public int ConsecutiveExhaustedWindows { get; set; }

            public long RecordedTicks { get; set; }

            public void RecordUsage(long elapsedTicks)
            {
                RecordedTicks = elapsedTicks;
            }

            public double GetUsagePercentage() => UsagePercentage;

            public int GetConsecutiveExhaustedWindows() => ConsecutiveExhaustedWindows;
        }

        private class MockMemoryPressureMonitor : IMemoryPressureMonitor
        {
            public bool IsHighPressureValue { get; set; }

            public double MemoryUsagePercentValue { get; set; }

            public bool IsHighMemoryPressure => IsHighPressureValue;

            public double MemoryUsagePercent => MemoryUsagePercentValue;

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
