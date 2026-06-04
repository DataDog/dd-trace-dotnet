// <copyright file="MemoryPressureMonitorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.RateLimiting;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using FluentAssertions;
using Xunit;

#nullable enable

namespace Datadog.Trace.Tests.Debugger.RateLimiting
{
#if NETFRAMEWORK || NETCOREAPP3_1_OR_GREATER
    public class MemoryPressureMonitorTests
    {
        [Fact]
        public void RefreshIfStale_DoesNotSampleWhileIdle()
        {
            var gc = new CountingGCInfoProvider();

            using var monitor = CreateMonitor(null, gc.TryGetMemoryUsageRatio, gc.TryGetGen2CollectionCount);

            gc.MemoryRatioCallCount.Should().Be(0);
            gc.Gen2CallCount.Should().Be(0);
        }

        [Fact]
        public void RefreshIfStale_SamplesWhenStaleOnly()
        {
            var gc = new CountingGCInfoProvider();
            using var monitor = CreateMonitor(null, gc.TryGetMemoryUsageRatio, gc.TryGetGen2CollectionCount);

            monitor.RefreshIfStale(0);
            monitor.RefreshIfStale(500);
            monitor.RefreshIfStale(1000);

            gc.MemoryRatioCallCount.Should().Be(2);
            gc.Gen2CallCount.Should().Be(2);
        }

        [Fact]
        public async Task ConcurrentRefresh_DoesNotOverlap()
        {
            using var gc = new BlockingMemoryRatioProvider(blockedRatio: 0.90);
            using var monitor = CreateMonitor(
                MemoryPressureConfig.Default with { HighPressureThresholdRatio = 0.80, MaxGen2PerSecond = 1000 },
                gc.TryGetMemoryUsageRatio,
                gc.TryGetGen2CollectionCount);

            monitor.RefreshIfStale(0);
            var refreshTask = Task.Run(() => monitor.RefreshIfStale(1000));
            gc.WaitForBlockedCall();

            monitor.RefreshIfStale(2000);
            gc.MemoryRatioCallCount.Should().Be(2);

            gc.ReleaseBlockedCall();
            await refreshTask;
            monitor.IsHighMemoryPressure.Should().BeTrue();
        }

        [Fact]
        public async Task Dispose_DuringInFlightRefresh_DoesNotCommitState()
        {
            using var gc = new BlockingMemoryRatioProvider(blockedRatio: 0.90);
            var monitor = CreateMonitor(
                MemoryPressureConfig.Default with { HighPressureThresholdRatio = 0.80, MaxGen2PerSecond = 1000 },
                gc.TryGetMemoryUsageRatio,
                gc.TryGetGen2CollectionCount);

            monitor.RefreshIfStale(0);
            var refreshTask = Task.Run(() => monitor.RefreshIfStale(1000));
            gc.WaitForBlockedCall();

            var disposeTask = Task.Run(() => monitor.Dispose());
            gc.ReleaseBlockedCall();

            await refreshTask;
            await disposeTask;
            monitor.IsHighMemoryPressure.Should().BeFalse();
        }

        [Fact]
        public void Dispose_PreventsFurtherRefresh()
        {
            var gc = new CountingGCInfoProvider();
            var monitor = CreateMonitor(null, gc.TryGetMemoryUsageRatio, gc.TryGetGen2CollectionCount);

            monitor.Dispose();
            monitor.RefreshIfStale(1000);

            gc.MemoryRatioCallCount.Should().Be(0);
            monitor.MemoryUsagePercent.Should().Be(0);
            monitor.Gen2CollectionsPerSecond.Should().Be(0);
            monitor.IsHighMemoryPressure.Should().BeFalse();
        }

        [Fact]
        public void MemoryOnly_AppliesThresholdsAndHysteresis()
        {
            var gc = new MemoryOnlyGCInfoProvider(0.85, 0.76, 0.74);
            using var monitor = CreateMonitor(
                MemoryPressureConfig.Default with { HighPressureThresholdRatio = 0.80, MaxGen2PerSecond = 1000, MemoryExitMargin = 0.05 },
                gc.TryGetMemoryUsageRatio,
                gc.TryGetGen2CollectionCount);

            monitor.RefreshIfStale(0);
            monitor.IsHighMemoryPressure.Should().BeTrue();
            monitor.MemoryUsagePercent.Should().BeApproximately(85, 0.1);

            monitor.RefreshIfStale(1000);
            monitor.IsHighMemoryPressure.Should().BeTrue();

            monitor.RefreshIfStale(2000);
            monitor.IsHighMemoryPressure.Should().BeFalse();
        }

        [Fact]
        public void GcOnly_AppliesThresholdsAndRates()
        {
            var gc = new GCOnlyInfoProvider([0, 3, 5, 6]);
            using var monitor = CreateMonitor(
                MemoryPressureConfig.Default with { HighPressureThresholdRatio = 0.99, MaxGen2PerSecond = 2, Gen2ExitMargin = 1 },
                gc.TryGetMemoryUsageRatio,
                gc.TryGetGen2CollectionCount);

            monitor.RefreshIfStale(0);
            monitor.IsHighMemoryPressure.Should().BeFalse();

            monitor.RefreshIfStale(1000);
            monitor.Gen2CollectionsPerSecond.Should().BeApproximately(3.0, 0.01);
            monitor.IsHighMemoryPressure.Should().BeTrue();

            monitor.RefreshIfStale(2000);
            monitor.IsHighMemoryPressure.Should().BeTrue();

            monitor.RefreshIfStale(3000);
            monitor.IsHighMemoryPressure.Should().BeFalse();
        }

        [Fact]
        public void ConsecutiveHighAndLowCycles_AreHonored()
        {
            var config = MemoryPressureConfig.Default with
            {
                HighPressureThresholdRatio = 0.80,
                MaxGen2PerSecond = 1000,
                ConsecutiveHighToEnter = 2,
                ConsecutiveLowToExit = 2
            };
            var gc = new MemoryOnlyGCInfoProvider(0.90, 0.91, 0.10, 0.10);
            using var monitor = CreateMonitor(config, gc.TryGetMemoryUsageRatio, gc.TryGetGen2CollectionCount);

            monitor.RefreshIfStale(0);
            monitor.IsHighMemoryPressure.Should().BeFalse();

            monitor.RefreshIfStale(1000);
            monitor.IsHighMemoryPressure.Should().BeTrue();

            monitor.RefreshIfStale(2000);
            monitor.IsHighMemoryPressure.Should().BeTrue();

            monitor.RefreshIfStale(3000);
            monitor.IsHighMemoryPressure.Should().BeFalse();
        }

        [Fact]
        public void Monitor_Disables_WhenNoSignalsAvailable()
        {
            var observer = new TestMemoryPressureObserver();
            var gc = new NoSignalsGCInfoProvider();
            using var monitor = CreateMonitor(null, gc.TryGetMemoryUsageRatio, gc.TryGetGen2CollectionCount, observer);

            monitor.RefreshIfStale(0);
            monitor.RefreshIfStale(1000);

            gc.MemoryRatioCallCount.Should().Be(1);
            monitor.IsHighMemoryPressure.Should().BeFalse();
            monitor.MemoryUsagePercent.Should().Be(0);
            monitor.Gen2CollectionsPerSecond.Should().Be(0);
            observer.Disables.Should().ContainSingle().Which.Should().Be(MetricTags.DebuggerMemoryPressureDisabledReason.NoSignals);
        }

        [Fact]
        public void ProviderException_DoesNotCrashMonitor()
        {
            var observer = new TestMemoryPressureObserver();
            var gc = new ThrowingGCInfoProvider();
            using var monitor = CreateMonitor(null, gc.TryGetMemoryUsageRatio, gc.TryGetGen2CollectionCount, observer);

            Action act = () => monitor.RefreshIfStale(0);

            act.Should().NotThrow();
            monitor.RefreshIfStale(1000);
            gc.MemoryRatioCallCount.Should().Be(1);
            monitor.IsHighMemoryPressure.Should().BeFalse();
            observer.Disables.Should().ContainSingle().Which.Should().Be(MetricTags.DebuggerMemoryPressureDisabledReason.Error);
        }

        [Fact]
        public void Trigger_IsGen2_WhenOnlyGcSignalEntersHigh()
        {
            var observer = new TestMemoryPressureObserver();
            var gc = new GCOnlyInfoProvider([0, 5]);
            using var monitor = CreateMonitor(
                MemoryPressureConfig.Default with { HighPressureThresholdRatio = 0.99, MaxGen2PerSecond = 2 },
                gc.TryGetMemoryUsageRatio,
                gc.TryGetGen2CollectionCount,
                observer);

            monitor.RefreshIfStale(0);
            monitor.RefreshIfStale(1000);

            monitor.IsHighMemoryPressure.Should().BeTrue();
            observer.Transitions.Should().ContainSingle().Which.Trigger.Should().Be(MetricTags.DebuggerMemoryPressureTrigger.Gc);
        }

        [Fact]
        public void GcOnlyTransition_DoesNotReportUnavailableMemoryAsZero()
        {
            var observer = new TestMemoryPressureObserver();
            var gc = new GCOnlyInfoProvider([0, 5]);
            using var monitor = CreateMonitor(
                MemoryPressureConfig.Default with { HighPressureThresholdRatio = 0.99, MaxGen2PerSecond = 2 },
                gc.TryGetMemoryUsageRatio,
                gc.TryGetGen2CollectionCount,
                observer);

            monitor.RefreshIfStale(0);
            monitor.RefreshIfStale(1000);

            var transition = observer.Transitions.Should().ContainSingle().Subject;
            transition.MemoryUsagePercent.Should().BeNull();
            transition.Gen2CollectionsPerSecond.Should().BeApproximately(5.0, 0.01);
        }

        [Fact]
        public void GcOnlyTransition_DoesNotRecordMemoryUsageTelemetry()
        {
            var collector = new MetricsTelemetryCollector(Timeout.InfiniteTimeSpan);
            var previousMetrics = TelemetryFactory.SetMetricsForTesting(collector);
            try
            {
                var gc = new GCOnlyInfoProvider([0, 5]);
                using var monitor = new MemoryPressureMonitor(
                    MemoryPressureConfig.Default with { HighPressureThresholdRatio = 0.99, MaxGen2PerSecond = 2 },
                    memoryRatioReader: gc.TryGetMemoryUsageRatio,
                    gen2Reader: gc.TryGetGen2CollectionCount,
                    onTransition: null);

                monitor.RefreshIfStale(0);
                monitor.RefreshIfStale(1000);
                collector.AggregateMetrics();

                var metrics = collector.GetMetrics();
                metrics.Metrics.Should().NotContain(x =>
                    x.Metric == Count.DebuggerMemoryPressureMemoryUsagePct.GetName());
                metrics.Metrics.Should().Contain(x =>
                    x.Metric == Count.DebuggerMemoryPressureGcActivity.GetName() &&
                    x.Tags != null &&
                    x.Tags.Length == 2 &&
                    x.Tags[0] == "state:enter" &&
                    x.Tags[1] == "bucket:gte_5" &&
                    x.Points.Single().Value == 1);
            }
            finally
            {
                TelemetryFactory.SetMetricsForTesting(previousMetrics);
            }
        }

        [Fact]
        public void Trigger_IsBoth_WhenMemoryAndGcEnterHighTogether()
        {
            var observer = new TestMemoryPressureObserver();
            // First cycle stays below the memory threshold so entry only happens on the second cycle,
            // where the gen2 rate is also computable - exercising the "both signals high at entry" path.
            var gc = new MemoryAndGcInfoProvider(ratios: [0.10, 0.90], gen2Counts: [0, 5]);
            using var monitor = CreateMonitor(
                MemoryPressureConfig.Default with { HighPressureThresholdRatio = 0.80, MaxGen2PerSecond = 2 },
                gc.TryGetMemoryUsageRatio,
                gc.TryGetGen2CollectionCount,
                observer);

            monitor.RefreshIfStale(0);
            monitor.RefreshIfStale(1000);

            monitor.IsHighMemoryPressure.Should().BeTrue();
            observer.Transitions.Should().ContainSingle().Which.Trigger.Should().Be(MetricTags.DebuggerMemoryPressureTrigger.Both);
        }

        [Fact]
        public void Observer_EmitsOnceOnEnterAndExit_WithSeverityAndDuration()
        {
            var observer = new TestMemoryPressureObserver();
            var gc = new MemoryOnlyGCInfoProvider(0.90, 0.91, 0.70, 0.70);
            using var monitor = CreateMonitor(
                MemoryPressureConfig.Default with { HighPressureThresholdRatio = 0.80, MaxGen2PerSecond = 1000 },
                gc.TryGetMemoryUsageRatio,
                gc.TryGetGen2CollectionCount,
                observer);

            monitor.RefreshIfStale(0);
            monitor.RefreshIfStale(1000);
            monitor.RefreshIfStale(2000);
            monitor.RefreshIfStale(3000);

            observer.Transitions.Should().HaveCount(2);
            observer.Transitions[0].Should().BeEquivalentTo(new Transition(true, MetricTags.DebuggerMemoryPressureTrigger.Memory, 90, null, 0));
            observer.Transitions[1].IsHighPressure.Should().BeFalse();
            observer.Transitions[1].Trigger.Should().Be(MetricTags.DebuggerMemoryPressureTrigger.None);
            observer.Transitions[1].MemoryUsagePercent.Should().Be(70);
            observer.Transitions[1].HighPressureDurationMs.Should().BeApproximately(2000, 0.1);
        }

        [Fact]
        public void Constructor_AcceptsExtremeParameterValues()
        {
            using var monitor1 = new MemoryPressureMonitor(MemoryPressureConfig.Default with { HighPressureThresholdRatio = -0.1, MaxGen2PerSecond = -5 });
            using var monitor2 = new MemoryPressureMonitor(MemoryPressureConfig.Default with { HighPressureThresholdRatio = 2 });
            using var monitor3 = new MemoryPressureMonitor(MemoryPressureConfig.Default with { HighPressureThresholdRatio = 0, MaxGen2PerSecond = 0 });

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

            WindowsMemoryInfo.TryGetMemoryLoadRatio(out var ratio).Should().BeTrue();
            ratio.Should().BeInRange(0, 1);
        }

        [Fact]
        public void WindowsMemoryInfo_TryGetAvailablePhysicalMemory_ReturnsValidValue()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            WindowsMemoryInfo.TryGetAvailablePhysicalMemory(out var bytes).Should().BeTrue();
            bytes.Should().BeGreaterThan(0);
        }

        // Fakes are passed as production reader delegates (method groups), so no shared test-only interface is needed.
        private static MemoryPressureMonitor CreateMonitor(
            MemoryPressureConfig? config,
            TryReadMemoryUsageRatio memoryReader,
            TryReadGen2CollectionCount gen2Reader,
            TestMemoryPressureObserver? observer = null)
        {
            return new MemoryPressureMonitor(
                config ?? MemoryPressureConfig.Default,
                memoryRatioReader: memoryReader,
                gen2Reader: gen2Reader,
                onTransition: observer is null ? null : observer.OnTransition,
                onDisabled: observer is null ? null : observer.OnDisabled);
        }

        private readonly record struct Transition(bool IsHighPressure, MetricTags.DebuggerMemoryPressureTrigger Trigger, double? MemoryUsagePercent, double? Gen2CollectionsPerSecond, double HighPressureDurationMs);

        private sealed class TestMemoryPressureObserver
        {
            public List<Transition> Transitions { get; } = [];

            public List<MetricTags.DebuggerMemoryPressureDisabledReason> Disables { get; } = [];

            public void OnTransition(bool isHighPressure, MetricTags.DebuggerMemoryPressureTrigger trigger, double? memoryUsagePercent, double? gen2CollectionsPerSecond, double highPressureDurationMs)
            {
                Transitions.Add(new Transition(isHighPressure, trigger, memoryUsagePercent, gen2CollectionsPerSecond, highPressureDurationMs));
            }

            public void OnDisabled(MetricTags.DebuggerMemoryPressureDisabledReason reason)
            {
                Disables.Add(reason);
            }
        }

        private sealed class CountingGCInfoProvider
        {
            public int MemoryRatioCallCount { get; private set; }

            public int Gen2CallCount { get; private set; }

            public bool TryGetGen2CollectionCount(out int count)
            {
                Gen2CallCount++;
                count = 0;
                return true;
            }

            public bool TryGetMemoryUsageRatio(out double ratio)
            {
                MemoryRatioCallCount++;
                ratio = 0.10;
                return true;
            }
        }

        private sealed class NoSignalsGCInfoProvider
        {
            public int MemoryRatioCallCount { get; private set; }

            public bool TryGetGen2CollectionCount(out int count)
            {
                count = 0;
                return false;
            }

            public bool TryGetMemoryUsageRatio(out double ratio)
            {
                MemoryRatioCallCount++;
                ratio = 0;
                return false;
            }
        }

        private sealed class MemoryOnlyGCInfoProvider
        {
            private readonly Queue<double> _ratios = new();

            public MemoryOnlyGCInfoProvider(params double[] ratios)
            {
                foreach (var ratio in ratios)
                {
                    _ratios.Enqueue(ratio);
                }
            }

            public bool TryGetGen2CollectionCount(out int count)
            {
                count = 0;
                return false;
            }

            public bool TryGetMemoryUsageRatio(out double ratio)
            {
                ratio = _ratios.Count == 0 ? 0 : _ratios.Dequeue();
                return true;
            }
        }

        private sealed class GCOnlyInfoProvider
        {
            private readonly Queue<int> _counts = new();

            public GCOnlyInfoProvider(int[] counts)
            {
                foreach (var count in counts)
                {
                    _counts.Enqueue(count);
                }
            }

            public bool TryGetGen2CollectionCount(out int count)
            {
                count = _counts.Count == 0 ? 0 : _counts.Dequeue();
                return true;
            }

            public bool TryGetMemoryUsageRatio(out double ratio)
            {
                ratio = 0;
                return false;
            }
        }

        private sealed class MemoryAndGcInfoProvider
        {
            private readonly Queue<double> _ratios = new();
            private readonly Queue<int> _gen2Counts = new();

            public MemoryAndGcInfoProvider(double[] ratios, int[] gen2Counts)
            {
                foreach (var ratio in ratios)
                {
                    _ratios.Enqueue(ratio);
                }

                foreach (var count in gen2Counts)
                {
                    _gen2Counts.Enqueue(count);
                }
            }

            public bool TryGetGen2CollectionCount(out int count)
            {
                count = _gen2Counts.Count == 0 ? 0 : _gen2Counts.Dequeue();
                return true;
            }

            public bool TryGetMemoryUsageRatio(out double ratio)
            {
                ratio = _ratios.Count == 0 ? 0 : _ratios.Dequeue();
                return true;
            }
        }

        private sealed class ThrowingGCInfoProvider
        {
            public int MemoryRatioCallCount { get; private set; }

            public bool TryGetGen2CollectionCount(out int count)
            {
                count = 0;
                throw new InvalidOperationException("Test exception from GC provider");
            }

            public bool TryGetMemoryUsageRatio(out double ratio)
            {
                MemoryRatioCallCount++;
                ratio = 0;
                throw new InvalidOperationException("Test exception from memory provider");
            }
        }

        private sealed class BlockingMemoryRatioProvider : IDisposable
        {
            private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

            private readonly ManualResetEventSlim _blocked = new(false);
            private readonly ManualResetEventSlim _release = new(false);
            private readonly double _blockedRatio;
            private int _memoryRatioCallCount;

            public BlockingMemoryRatioProvider(double blockedRatio)
            {
                _blockedRatio = blockedRatio;
            }

            public int MemoryRatioCallCount => Volatile.Read(ref _memoryRatioCallCount);

            public bool TryGetGen2CollectionCount(out int count)
            {
                count = 0;
                return true;
            }

            public bool TryGetMemoryUsageRatio(out double ratio)
            {
                if (Interlocked.Increment(ref _memoryRatioCallCount) == 1)
                {
                    ratio = 0.10;
                    return true;
                }

                _blocked.Set();
                _release.Wait(WaitTimeout).Should().BeTrue("the test should release the blocked memory sample");
                ratio = _blockedRatio;
                return true;
            }

            public void WaitForBlockedCall() => _blocked.Wait(WaitTimeout).Should().BeTrue("the memory sample should reach the blocking point");

            public void ReleaseBlockedCall() => _release.Set();

            public void Dispose()
            {
                _release.Set();
                _blocked.Dispose();
                _release.Dispose();
            }
        }
    }
#endif
}
