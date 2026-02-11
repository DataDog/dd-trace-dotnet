// <copyright file="MemoryPressureMonitorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.RateLimiting;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Debugger.RateLimiting
{
#if NETFRAMEWORK || NETCOREAPP3_1_OR_GREATER
    public class MemoryPressureMonitorTests
    {
        [Fact]
        public void Gen2CollectionsPerSecond_TracksGC()
        {
            using var scheduler = new TestScheduler();
            var gc = new FakeGCInfoProvider()
                    .WithGen2Counts(0, 3)
                    .WithConstantMemoryRatio(0.1);
            var clock = new FakeClock().WithTicksAtSeconds(0, 0, 1, 1);

            var config = MemoryPressureConfig.Default with
            {
                HighPressureThresholdRatio = 0.95,
                MaxGen2PerSecond = 100
            };

            using var monitor = new MemoryPressureMonitor(
                config,
                scheduler: scheduler,
                gcInfoProvider: gc,
                clock: clock);

            scheduler.TriggerRefresh();

            var rate = monitor.Gen2CollectionsPerSecond;

            // Should be 3 collections per second
            rate.Should().BeApproximately(3.0, 0.01);
        }

        [Fact]
        public async Task ConcurrentReadsDuringRefresh_NoDeadlock()
        {
            var config = MemoryPressureConfig.Default with
            {
                HighPressureThresholdRatio = 0.80,
                MaxGen2PerSecond = 2
            };

            using var monitor = new MemoryPressureMonitor(config);

            var readCount = 0;
            var exceptions = 0;
            var duration = TimeSpan.FromSeconds(2);
            var sw = System.Diagnostics.Stopwatch.StartNew();

            // Read while refresh is happening every timer interval
            var tasks = Enumerable.Range(0, 8)
                                  .Select(i => Task.Run(() =>
                                   {
                                       try
                                       {
                                           while (sw.Elapsed < duration)
                                           {
                                               _ = monitor.IsHighMemoryPressure;
                                               _ = monitor.MemoryUsagePercent;
                                               _ = monitor.Gen2CollectionsPerSecond;
                                               Interlocked.Increment(ref readCount);
                                           }
                                       }
                                       catch
                                       {
                                           Interlocked.Increment(ref exceptions);
                                       }
                                   }))
                                  .ToArray();

            await Task.WhenAll(tasks);

            exceptions.Should().Be(0, "No exceptions should occur during concurrent reads");
            readCount.Should().BeGreaterThan(1000, "Should achieve reasonable read throughput without lock contention");
        }

        [Fact]
        public void Constructor_AcceptsExtremeParameterValues()
        {
            using var scheduler = new TestScheduler();

            // Test negative values
            var config = MemoryPressureConfig.Default with
            {
                HighPressureThresholdRatio = -0.1,
                MaxGen2PerSecond = -5
            };

            using var monitor1 = new MemoryPressureMonitor(config, scheduler);

            // Test values > 1
            config = MemoryPressureConfig.Default with
            {
                HighPressureThresholdRatio = 2,
            };
            using var monitor2 = new MemoryPressureMonitor(config, scheduler: scheduler);

            // Test zero values
            config = MemoryPressureConfig.Default with
            {
                HighPressureThresholdRatio = 0,
                MaxGen2PerSecond = 0
            };
            using var monitor3 = new MemoryPressureMonitor(config, scheduler: scheduler);

            // All should initialize without throwing
            monitor1.Should().NotBeNull();
            monitor2.Should().NotBeNull();
            monitor3.Should().NotBeNull();
        }

        [Fact]
        public void WindowsMemoryInfo_TryGetMemoryLoadRatio_ReturnsValidValue()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            var result = WindowsMemoryInfo.TryGetMemoryLoadRatio(out var ratio);

            result.Should().BeTrue();
            ratio.Should().BeGreaterThan(0);
            ratio.Should().BeLessOrEqualTo(1);
        }

        [Fact]
        public void WindowsMemoryInfo_TryGetAvailablePhysicalMemory_ReturnsValidValue()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            var result = WindowsMemoryInfo.TryGetAvailablePhysicalMemory(out var bytes);

            result.Should().BeTrue();
            bytes.Should().BeGreaterThan(0);
        }

        [Fact]
        public void Dispose_PreventsFurtherOperations()
        {
            using var scheduler = new TestScheduler();
            var monitor = new MemoryPressureMonitor(MemoryPressureConfig.Default, scheduler: scheduler);

            monitor.Dispose();

            // Further operations should not throw but may not update
            scheduler.TriggerRefresh();

            // Access properties - should not throw
            var usage = monitor.MemoryUsagePercent;
            var gen2Rate = monitor.Gen2CollectionsPerSecond;
            var isHigh = monitor.IsHighMemoryPressure;

            // Values should be valid (last known values or defaults)
            usage.Should().BeGreaterOrEqualTo(0);
            gen2Rate.Should().BeGreaterOrEqualTo(0);
        }

        [Fact]
        public void Dispose_StopsScheduledRefresh()
        {
            using var scheduler = new TestScheduler();
            var gc = new FakeGCInfoProvider()
                    .WithConstantMemoryRatio(0.10) // below threshold
                    .WithConstantGen2Count(0); // no GC pressure
            var clock = new FakeClock().WithTicksAtSeconds(0, 1);

            var config = MemoryPressureConfig.Default with
            {
                HighPressureThresholdRatio = 0.99,
                MaxGen2PerSecond = 1000
            };

            var monitor = new MemoryPressureMonitor(
                config,
                scheduler: scheduler,
                gcInfoProvider: gc,
                clock: clock);

            monitor.IsHighMemoryPressure.Should().BeFalse();

            // Queue an event that would flip to high on next refresh
            monitor.RecordMemoryPressureEvent();

            // Dispose should remove the scheduled callback; triggering should not process the event
            monitor.Dispose();
            scheduler.TriggerRefresh();

            monitor.IsHighMemoryPressure.Should().BeFalse();
        }

        [Fact]
        public void ConsecutiveLowToExit_DelaysExit()
        {
            using var scheduler = new TestScheduler();
            var config = new MemoryPressureConfig
            {
                HighPressureThresholdRatio = 2.0,
                MaxGen2PerSecond = 1000,
                MemoryExitMargin = 0.0,
                Gen2ExitMargin = 0,
                ConsecutiveHighToEnter = 1,
                ConsecutiveLowToExit = 2
            };

            using var monitor = new MemoryPressureMonitor(config, scheduler);

            // Enter high via pressure event (bypasses other checks)
            monitor.RecordMemoryPressureEvent();
            scheduler.TriggerRefresh();
            monitor.IsHighMemoryPressure.Should().BeTrue();

            // No new events; low cycle #1: should remain high
            scheduler.TriggerRefresh();
            monitor.IsHighMemoryPressure.Should().BeTrue();

            // Low cycle #2: should EXIT
            scheduler.TriggerRefresh();
            monitor.IsHighMemoryPressure.Should().BeFalse();
        }

        [Fact]
        public void PressureEvent_BypassesConsecutiveHighRequirement()
        {
            using var scheduler = new TestScheduler();
            var config = new MemoryPressureConfig
            {
                HighPressureThresholdRatio = 0.99,
                MaxGen2PerSecond = 1000,
                ConsecutiveHighToEnter = 3,
                ConsecutiveLowToExit = 1
            };

            using var monitor = new MemoryPressureMonitor(config, scheduler);

            monitor.RecordMemoryPressureEvent();
            scheduler.TriggerRefresh();

            // Should enter immediately due to event bypass
            monitor.IsHighMemoryPressure.Should().BeTrue();
        }

        [Fact]
        public void PressureEvents_ResetToZero_AfterProcessing()
        {
            using var scheduler = new TestScheduler();

            var config = new MemoryPressureConfig
            {
                HighPressureThresholdRatio = 2.0,
                MaxGen2PerSecond = 1000,
                MemoryExitMargin = 0.0,
                Gen2ExitMargin = 0
            };

            using var monitor = new MemoryPressureMonitor(config, scheduler);

            // Record events
            monitor.RecordMemoryPressureEvent();
            monitor.RecordMemoryPressureEvent();

            // First refresh processes them
            scheduler.TriggerRefresh();
            monitor.IsHighMemoryPressure.Should().BeTrue();

            // Record one more event
            monitor.RecordMemoryPressureEvent();

            // Second refresh processes the new event
            scheduler.TriggerRefresh();
            monitor.IsHighMemoryPressure.Should().BeTrue();

            // Third refresh with no new events should exit high pressure
            scheduler.TriggerRefresh();
            monitor.IsHighMemoryPressure.Should().BeFalse();
        }

        [Fact]
        public void ThresholdBoundary_JustAboveMemoryThreshold_Enters()
        {
            using var scheduler = new TestScheduler();
            var gc = new FakeGCInfoProvider()
               .WithConstantMemoryRatio(0.8006); // Just above threshold (0.80)
            var clock = new FakeClock().WithTicksAtSeconds(0, 1);

            var config = MemoryPressureConfig.Default with
            {
                HighPressureThresholdRatio = 0.80,
                MaxGen2PerSecond = 1000
            };

            using var monitor = new MemoryPressureMonitor(
                config,
                scheduler: scheduler,
                gcInfoProvider: gc,
                clock: clock);

            scheduler.TriggerRefresh();

            // Should enter because > threshold
            monitor.IsHighMemoryPressure.Should().BeTrue();
        }

        [Fact]
        public void Monitor_Disables_WhenNoSignalsAvailable()
        {
            using var scheduler = new TestScheduler();
            var clock = new FakeClock().WithTicksAtSeconds(0, 1, 2);
            var config = MemoryPressureConfig.Default with
            {
                HighPressureThresholdRatio = 0.80,
                MaxGen2PerSecond = 2
            };

            using var monitor = new MemoryPressureMonitor(
                config,
                scheduler: scheduler,
                gcInfoProvider: new NoSignalsGCInfoProvider(),
                clock: clock);

            // Constructor performs an initial refresh and disables immediately -> no subscription
            scheduler.SubscriptionCount.Should().Be(0);

            // First refresh should detect no signals and disable the monitor
            scheduler.TriggerRefresh();

            monitor.IsHighMemoryPressure.Should().BeFalse();
            monitor.MemoryUsagePercent.Should().Be(0);
            monitor.Gen2CollectionsPerSecond.Should().Be(0);

            // Monitor should have unsubscribed
            scheduler.SubscriptionCount.Should().Be(0);

            // Further refresh calls should have no effect
            scheduler.TriggerRefresh();
            monitor.IsHighMemoryPressure.Should().BeFalse();
            monitor.MemoryUsagePercent.Should().Be(0);
            monitor.Gen2CollectionsPerSecond.Should().Be(0);
        }

        [Fact]
        public void MemoryOnly_AppliesThresholds_WhenGcUnavailable()
        {
            using var scheduler = new TestScheduler();
            var clock = new FakeClock().WithTicksAtSeconds(0, 1, 2, 3);
            var config = MemoryPressureConfig.Default with
            {
                HighPressureThresholdRatio = 0.80,
                MaxGen2PerSecond = 1000
            };

            using var monitor = new MemoryPressureMonitor(
                config,
                scheduler: scheduler,
                gcInfoProvider: new MemoryOnlyGCInfoProvider(0.50, 0.85, 0.74), // Add a below-threshold sample for ctor refresh, then enter, then exit
                clock: clock);

            // First refresh: memory 0.85 > 0.80 => enter high
            scheduler.TriggerRefresh();
            monitor.IsHighMemoryPressure.Should().BeTrue();
            monitor.Gen2CollectionsPerSecond.Should().Be(0);

            // Second refresh: memory 0.74 < 0.80 - 0.05 (exit threshold 0.75) => exit
            scheduler.TriggerRefresh();
            monitor.IsHighMemoryPressure.Should().BeFalse();
            monitor.Gen2CollectionsPerSecond.Should().Be(0);
        }

        [Fact]
        public void GcOnly_AppliesThresholds_WhenMemoryUnavailable()
        {
            using var scheduler = new TestScheduler();
            var clock = new FakeClock().WithTicksAtSeconds(0, 0, 1, 1);
            var config = MemoryPressureConfig.Default with
            {
                HighPressureThresholdRatio = 0.99,
                MaxGen2PerSecond = 2
            };

            using var monitor = new MemoryPressureMonitor(
                config,
                scheduler: scheduler,
                gcInfoProvider: new GCOnlyInfoProvider([0, 3]),
                clock: clock);

            // First scheduled refresh: ctor has already established baseline; rate is 3/sec > 2 => enter high
            scheduler.TriggerRefresh();
            monitor.Gen2CollectionsPerSecond.Should().BeApproximately(3.0, 0.01);
            monitor.IsHighMemoryPressure.Should().BeTrue();

            // Second refresh: no additional collections => rate ~0
            scheduler.TriggerRefresh();
            monitor.Gen2CollectionsPerSecond.Should().BeLessOrEqualTo(0.001);
        }

        [Fact]
        public void ThresholdBoundary_JustBelowExitThreshold_RemainsHigh()
        {
            using var scheduler = new TestScheduler();

            // Add an initial below-threshold sample to offset constructor's initial refresh
            var gc = new FakeGCInfoProvider()
               .WithMemoryRatios(0.70, 0.90, 0.8001); // Enter high on 0.90, then drop to just above exit (needs 0.05 to exit)
            var clock = new FakeClock().WithTicksAtSeconds(0, 1, 2, 3);

            var config = MemoryPressureConfig.Default with
            {
                HighPressureThresholdRatio = 0.85,
                MaxGen2PerSecond = 1000,
                MemoryExitMargin = 0.05
            };

            using var monitor = new MemoryPressureMonitor(
                config,
                scheduler: scheduler,
                gcInfoProvider: gc,
                clock: clock);

            // Enter high pressure
            scheduler.TriggerRefresh();
            monitor.IsHighMemoryPressure.Should().BeTrue();

            // Drop to just above exit threshold (0.8001 > 0.80)
            scheduler.TriggerRefresh();

            // Should remain high
            monitor.IsHighMemoryPressure.Should().BeTrue();
        }

        [Fact]
        public void ThresholdBoundary_JustAboveGen2Threshold_Enters()
        {
            using var scheduler = new TestScheduler();
            var gc = new FakeGCInfoProvider()
                    .WithGen2Counts(0, 3) // 3 Gen2/sec > threshold
                    .WithConstantMemoryRatio(0.1);
            var clock = new FakeClock().WithTicksAtSeconds(0, 0, 1, 1);
            var config = MemoryPressureConfig.Default with
            {
                HighPressureThresholdRatio = 0.99,
                MaxGen2PerSecond = 2,
            };

            using var monitor = new MemoryPressureMonitor(
                config,
                scheduler: scheduler,
                gcInfoProvider: gc,
                clock: clock);

            scheduler.TriggerRefresh();

            // Should enter because > threshold
            monitor.IsHighMemoryPressure.Should().BeTrue();
        }

        [Fact]
        public void Gen2ExitMargin_Hysteresis_ExitOnlyWhenBelowMargin()
        {
            using var scheduler = new TestScheduler();

            // Counts: baseline (ctor refresh) at 0, then +3, then +2, then +1
            // Rates: 3/sec (enter), 2/sec (>1 remain high), 1/sec (==1 exit)
            var gcOnly = new GCOnlyInfoProvider([0, 3, 5, 6]);
            var clock = new FakeClock().WithTicksAtSeconds(0, 0, 1, 2, 3);
            var config = MemoryPressureConfig.Default with
            {
                HighPressureThresholdRatio = 1.0,
                MaxGen2PerSecond = 2,
                Gen2ExitMargin = 1
            };

            using var monitor = new MemoryPressureMonitor(
                config,
                scheduler: scheduler,
                gcInfoProvider: gcOnly,
                clock: clock);

            // First scheduled refresh: 3/sec -> ENTER
            scheduler.TriggerRefresh();
            monitor.IsHighMemoryPressure.Should().BeTrue();

            // Second: 2/sec (>1) -> remain HIGH
            scheduler.TriggerRefresh();
            monitor.IsHighMemoryPressure.Should().BeTrue();

            // Third: 1/sec (==1) -> EXIT
            scheduler.TriggerRefresh();
            monitor.IsHighMemoryPressure.Should().BeFalse();
        }

        [Fact]
        public void ConsecutiveHighToEnter_WithMetrics_RequiresMultipleCycles()
        {
            using var scheduler = new TestScheduler();
            var memOnly = new MemoryOnlyGCInfoProvider(0.50, 0.86, 0.86); // below for ctor; then above twice
            var clock = new FakeClock().WithTicksAtSeconds(0, 1, 2, 3);
            var config = MemoryPressureConfig.Default with
            {
                HighPressureThresholdRatio = 0.85,
                MaxGen2PerSecond = 1000,
                ConsecutiveHighToEnter = 2
            };

            using var monitor = new MemoryPressureMonitor(
                config,
                scheduler: scheduler,
                gcInfoProvider: memOnly,
                clock: clock);

            // First above-threshold cycle -> not enough to enter
            scheduler.TriggerRefresh();
            monitor.IsHighMemoryPressure.Should().BeFalse();

            // Second consecutive above-threshold -> ENTER
            scheduler.TriggerRefresh();
            monitor.IsHighMemoryPressure.Should().BeTrue();
        }

        [Fact]
        public void EstablishesGen2Baseline_OnFirstSuccessfulRefresh()
        {
            // This test exercises the generic IGCInfoProvider contract, not the current SystemGCInfoProvider.
            // It simulates a transient GC failure during the ctor's initial Refresh:
            //  - First call to GetGen2CollectionCount() throws, so no Gen2 baseline is established yet.
            //  - A later refresh, once the provider starts succeeding, establishes the baseline (rate still 0).
            //  - Subsequent refreshes then compute non-zero rates from that baseline and can enter high pressure.
            // With the current SystemGCInfoProvider implementation GC.CollectionCount(2) is not expected to throw,
            // but we validate that MemoryPressureMonitor behaves correctly if a custom or future provider does.

            using var scheduler = new TestScheduler();
            var gc = new TransientGcThrowProvider(
                initialMemoryRatio: 0.1,
                countsAfterRecovery: new[] { 0, 3, 6 }); // after recovery: baseline 0, then 3, then 6
            var clock = new FakeClock().WithTicksAtSeconds(0, 0, 1, 2, 3);
            var config = MemoryPressureConfig.Default with
            {
                HighPressureThresholdRatio = 1.0,
                MaxGen2PerSecond = 2,
            };

            using var monitor = new MemoryPressureMonitor(
                config,
                scheduler: scheduler,
                gcInfoProvider: gc,
                clock: clock);

            // First scheduled refresh: GC threw in ctor; this establishes baseline (rate still 0)
            scheduler.TriggerRefresh();
            monitor.Gen2CollectionsPerSecond.Should().Be(0);
            monitor.IsHighMemoryPressure.Should().BeFalse();

            // Second scheduled refresh: depending on timestamp alignment, rate may still be ~0
            scheduler.TriggerRefresh();
            monitor.Gen2CollectionsPerSecond.Should().BeGreaterOrEqualTo(0);

            // Third scheduled refresh: delta of 3 over 1 second => ~3/sec and ENTER high
            scheduler.TriggerRefresh();
            monitor.Gen2CollectionsPerSecond.Should().BeApproximately(3.0, 0.05);
            monitor.IsHighMemoryPressure.Should().BeTrue();
        }

        [Fact]
        public void MemoryUsagePercent_ScalesRatioToPercent()
        {
            using var scheduler = new TestScheduler();
            var gc = new FakeGCInfoProvider()
                    .WithConstantMemoryRatio(0.80)
                    .WithConstantGen2Count(0);
            var clock = new FakeClock().WithTicksAtSeconds(0, 1);

            var config = MemoryPressureConfig.Default with
            {
                HighPressureThresholdRatio = 2.0,
            };

            using var monitor = new MemoryPressureMonitor(
                config,
                scheduler: scheduler,
                gcInfoProvider: gc,
                clock: clock);

            scheduler.TriggerRefresh();

            monitor.MemoryUsagePercent.Should().BeApproximately(80.0, 0.1);
        }

        [Fact]
        public void CycleWithoutTime_Gen2RateDoesNotInflate()
        {
            using var scheduler = new TestScheduler();
            var gc = new FakeGCInfoProvider()
               .WithGen2Counts(0, 2, 4);
            var clock = new FakeClock()
               .WithTicksAtSeconds(1.0, 1.0, 1.0); // No time progress!
            var config = MemoryPressureConfig.Default with
            {
                HighPressureThresholdRatio = 0.99,
                MaxGen2PerSecond = 1000
            };

            using var monitor = new MemoryPressureMonitor(
                config,
                scheduler: scheduler,
                gcInfoProvider: gc,
                clock: clock);

            // First refresh: sets baseline
            scheduler.TriggerRefresh();

            // Second refresh: no time elapsed, should compute rate as 0
            scheduler.TriggerRefresh();

            var rate = monitor.Gen2CollectionsPerSecond;

            // Should be 0 or very small, not infinity or large number
            rate.Should().BeLessOrEqualTo(0.001);
        }

        [Fact]
        public void SchedulerDispose_ThenTriggerRefresh_DoesNotThrow()
        {
            var scheduler = new TestScheduler();
            using var monitor = new MemoryPressureMonitor(MemoryPressureConfig.Default, scheduler: scheduler);

            scheduler.Dispose();

            // Should not throw even though scheduler is disposed
            Action act = () => scheduler.TriggerRefresh();
            act.Should().NotThrow();

            // Monitor should still be accessible
            monitor.MemoryUsagePercent.Should().BeGreaterOrEqualTo(0);
        }

        [Fact]
        public void NegativeGen2Delta_TreatedAsZero()
        {
            using var scheduler = new TestScheduler();
            var gc = new FakeGCInfoProvider()
               .WithGen2Counts(100, 50); // Count goes backward (shouldn't happen in reality)
            var clock = new FakeClock().WithTicksAtSeconds(0, 0, 1, 1);
            var config = MemoryPressureConfig.Default with
            {
                HighPressureThresholdRatio = 0.99,
                MaxGen2PerSecond = 1000
            };

            using var monitor = new MemoryPressureMonitor(
                config,
                scheduler: scheduler,
                gcInfoProvider: gc,
                clock: clock);

            scheduler.TriggerRefresh();

            var rate = monitor.Gen2CollectionsPerSecond;

            // Should be 0, not negative
            rate.Should().Be(0);
        }

        [Fact]
        public void ExitMargin_WithPositiveMargin_ExitsOnlyBelowMargin()
        {
            using var scheduler = new TestScheduler();

            // Add an initial below-threshold sample to offset constructor's initial refresh
            var gc = new FakeGCInfoProvider()
                    .WithMemoryRatios(0.50, 0.85, 0.85, 0.81, 0.76, 0.74) // Enter high, stay, drop gradually
                    .WithConstantGen2Count(0);

            var clock = new FakeClock().WithTicksAtSeconds(0, 1, 2, 3, 4, 5, 6);

            var config = MemoryPressureConfig.Default with
            {
                HighPressureThresholdRatio = 0.80,
                MaxGen2PerSecond = 1000,
                MemoryExitMargin = 0.05
            };

            using var monitor = new MemoryPressureMonitor(
                config,
                scheduler: scheduler,
                gcInfoProvider: gc,
                clock: clock);

            // Enter (0.85 > 0.80)
            scheduler.TriggerRefresh();
            monitor.IsHighMemoryPressure.Should().BeTrue();

            // Stay high (0.85 > 0.75)
            scheduler.TriggerRefresh();
            monitor.IsHighMemoryPressure.Should().BeTrue();

            // Still high (0.81 > 0.75)
            scheduler.TriggerRefresh();
            monitor.IsHighMemoryPressure.Should().BeTrue();

            // Still high (0.76 > 0.75)
            scheduler.TriggerRefresh();
            monitor.IsHighMemoryPressure.Should().BeTrue();

            // Exit (0.74 <= 0.75)
            scheduler.TriggerRefresh();
            monitor.IsHighMemoryPressure.Should().BeFalse();
        }

        [Fact]
        public void ProviderException_DoesNotCrashMonitor()
        {
            using var scheduler = new TestScheduler();
            var gc = new ThrowingGCInfoProvider();
            var clock = new FakeClock().WithTicksAtSeconds(0, 1, 2);
            var config = MemoryPressureConfig.Default with
            {
                HighPressureThresholdRatio = 0.80,
                MaxGen2PerSecond = 10,
            };

            using var monitor = new MemoryPressureMonitor(
                config,
                scheduler: scheduler,
                gcInfoProvider: gc,
                clock: clock);

            // Constructor performs an initial refresh that disables due to no signals -> no subscription
            scheduler.SubscriptionCount.Should().Be(0);

            // Refresh should not throw even if provider throws
            Action act = () => scheduler.TriggerRefresh();
            act.Should().NotThrow();

            // Properties should still be accessible (with default/previous values)
            monitor.MemoryUsagePercent.Should().BeGreaterOrEqualTo(0);
            monitor.Gen2CollectionsPerSecond.Should().BeGreaterOrEqualTo(0);

            // Should not enter high pressure due to provider failures
            monitor.IsHighMemoryPressure.Should().BeFalse();

            // Monitor should disable itself (unsubscribe) as no signals are available
            scheduler.SubscriptionCount.Should().Be(0);
        }

        /// <summary>
        /// GC provider that throws for both memory and GC info, simulating unsupported platform.
        /// </summary>
        private class NoSignalsGCInfoProvider : IGCInfoProvider
        {
            public int GetGen2CollectionCount() => throw new InvalidOperationException("No GC info available");

#if NETCOREAPP3_1_OR_GREATER
            public GCMemoryInfo GetGCMemoryInfo() => new GCMemoryInfo();
#endif

            public double GetMemoryUsageRatio() => throw new InvalidOperationException("No memory info available");
        }

        /// <summary>
        /// GC provider that supplies memory ratios only and throws for GC counts.
        /// </summary>
        private class MemoryOnlyGCInfoProvider : IGCInfoProvider
        {
            private readonly System.Collections.Generic.Queue<double> _ratios = new();

            public MemoryOnlyGCInfoProvider(params double[] ratios)
            {
                foreach (var r in ratios)
                {
                    _ratios.Enqueue(r);
                }
            }

            public int GetGen2CollectionCount() => throw new InvalidOperationException("GC count unavailable");

#if NETCOREAPP3_1_OR_GREATER
            public GCMemoryInfo GetGCMemoryInfo() => new GCMemoryInfo();
#endif

            public double GetMemoryUsageRatio()
            {
                if (_ratios.Count == 0)
                {
                    return 0;
                }

                return _ratios.Dequeue();
            }
        }

        /// <summary>
        /// GC provider that supplies Gen2 counts only and throws for memory ratio.
        /// </summary>
        private class GCOnlyInfoProvider : IGCInfoProvider
        {
            private readonly System.Collections.Generic.Queue<int> _counts = new();

            public GCOnlyInfoProvider(int[] counts)
            {
                foreach (var c in counts)
                {
                    _counts.Enqueue(c);
                }
            }

            public int GetGen2CollectionCount()
            {
                if (_counts.Count == 0)
                {
                    return 0;
                }

                return _counts.Dequeue();
            }

#if NETCOREAPP3_1_OR_GREATER
            public GCMemoryInfo GetGCMemoryInfo() => new GCMemoryInfo();
#endif

            public double GetMemoryUsageRatio() => throw new InvalidOperationException("Memory ratio unavailable");
        }

        /// <summary>
        /// Helper provider that throws exceptions to test error handling
        /// </summary>
        private class ThrowingGCInfoProvider : IGCInfoProvider
        {
            public int GetGen2CollectionCount()
            {
                throw new InvalidOperationException("Test exception from GC provider");
            }

#if NETCOREAPP3_1_OR_GREATER
            public GCMemoryInfo GetGCMemoryInfo()
            {
                throw new InvalidOperationException("Test exception from GC provider");
            }
#endif

            public double GetMemoryUsageRatio()
            {
                throw new InvalidOperationException("Test exception from memory provider");
            }
        }

        /// <summary>
        /// GC provider that throws for GetGen2CollectionCount() on first invocation only,
        /// then returns provided counts. Memory ratio always available and low.
        /// </summary>
        private class TransientGcThrowProvider : IGCInfoProvider
        {
            private readonly System.Collections.Generic.Queue<int> _counts = new();
            private readonly double _memoryRatio;
            private bool _hasThrown = false;

            public TransientGcThrowProvider(double initialMemoryRatio, int[] countsAfterRecovery)
            {
                _memoryRatio = initialMemoryRatio;
                foreach (var c in countsAfterRecovery)
                {
                    _counts.Enqueue(c);
                }
            }

            public int GetGen2CollectionCount()
            {
                if (!_hasThrown)
                {
                    _hasThrown = true;
                    throw new InvalidOperationException("Transient GC failure");
                }

                if (_counts.Count == 0)
                {
                    return 0;
                }

                return _counts.Dequeue();
            }

#if NETCOREAPP3_1_OR_GREATER
            public GCMemoryInfo GetGCMemoryInfo() => new GCMemoryInfo();
#endif

            public double GetMemoryUsageRatio() => _memoryRatio;
        }
    }
#endif
}
