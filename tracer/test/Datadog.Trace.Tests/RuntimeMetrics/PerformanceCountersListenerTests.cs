// <copyright file="PerformanceCountersListenerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.DogStatsd;
using Datadog.Trace.RuntimeMetrics;
using Datadog.Trace.TestHelpers.Stats;
using Datadog.Trace.Vendors.StatsdClient;
using FluentAssertions;
using Moq;
using Xunit;
using Xunit.Abstractions;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Datadog.Trace.Tests.RuntimeMetrics
{
    public class PerformanceCountersListenerTests
    {
        private const int MaxRecordedFailuresPerPhase = 20;
        private static readonly TimeSpan SameListenerBudget = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan FreshListenerBudget = TimeSpan.FromMinutes(3);
        private static readonly TimeSpan SameListenerDelay = TimeSpan.FromMilliseconds(10);

        private readonly ITestOutputHelper _output;

        public PerformanceCountersListenerTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task PushEvents()
        {
            var statsd = new Mock<IDogStatsd>();

            using var listener = new PerformanceCountersListener(new TestStatsdManager(statsd.Object));

            await listener.WaitForInitialization();

            listener.Refresh();

            var diag = Describe(listener, 0, statsd.Invocations.Count);
            _output.WriteLine(diag);
            statsd.Invocations.Should().NotBeEmpty(diag);

            statsd.Verify(s => s.Gauge(MetricsNames.Gen0HeapSize, It.IsAny<double>(), 1, null), Times.Once);
            statsd.Verify(s => s.Gauge(MetricsNames.Gen1HeapSize, It.IsAny<double>(), 1, null), Times.Once);
            statsd.Verify(s => s.Gauge(MetricsNames.Gen2HeapSize, It.IsAny<double>(), 1, null), Times.Once);
            statsd.Verify(s => s.Gauge(MetricsNames.LohSize, It.IsAny<double>(), 1, null), Times.Once);

            statsd.VerifyNoOtherCalls();
            statsd.Invocations.Clear();

            listener.Refresh();

            statsd.Verify(s => s.Gauge(MetricsNames.Gen0HeapSize, It.IsAny<double>(), 1, null), Times.Once);
            statsd.Verify(s => s.Gauge(MetricsNames.Gen1HeapSize, It.IsAny<double>(), 1, null), Times.Once);
            statsd.Verify(s => s.Gauge(MetricsNames.Gen2HeapSize, It.IsAny<double>(), 1, null), Times.Once);
            statsd.Verify(s => s.Gauge(MetricsNames.LohSize, It.IsAny<double>(), 1, null), Times.Once);

            // Those metrics aren't pushed the first time (differential count)
            statsd.Verify(s => s.Increment(MetricsNames.Gen0CollectionsCount, It.IsAny<int>(), 1, null), Times.Once);
            statsd.Verify(s => s.Increment(MetricsNames.Gen1CollectionsCount, It.IsAny<int>(), 1, null), Times.Once);
            statsd.Verify(s => s.Increment(MetricsNames.Gen2CollectionsCount, It.IsAny<int>(), 1, null), Times.Once);
            statsd.Verify(s => s.Counter(MetricsNames.ContentionCount, It.IsAny<double>(), 1, null), Times.Once);

            statsd.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task PushEventsStressDiagnostic()
        {
            var phaseAFailures = new List<string>();
            var phaseBFailures = new List<string>();
            var started = DateTime.UtcNow;
            var sameListenerRefreshes = 0;
            var sameListenerFailures = 0;
            var freshListeners = 0;
            var freshListenerFailures = 0;

            try
            {
                var statsd = new Mock<IDogStatsd>();
                using (var listener = new PerformanceCountersListener(new TestStatsdManager(statsd.Object)))
                {
                    await listener.WaitForInitialization();
                    var sw = Stopwatch.StartNew();
                    while (sw.Elapsed < SameListenerBudget)
                    {
                        statsd.Invocations.Clear();
                        sameListenerRefreshes++;
                        try
                        {
                            listener.Refresh();
                            var gauges = CountHeapGauges(statsd);
                            if (gauges < 4)
                            {
                                sameListenerFailures++;
                                if (phaseAFailures.Count < MaxRecordedFailuresPerPhase)
                                {
                                    phaseAFailures.Add("phase=A " + Describe(listener, sameListenerRefreshes, gauges));
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            sameListenerFailures++;
                            if (phaseAFailures.Count < MaxRecordedFailuresPerPhase)
                            {
                                phaseAFailures.Add("phase=A threw " + ex + " " + Describe(listener, sameListenerRefreshes, -1));
                            }
                        }

                        await Task.Delay(SameListenerDelay);
                    }
                }

                var swFresh = Stopwatch.StartNew();
                while (swFresh.Elapsed < FreshListenerBudget)
                {
                    var freshStatsd = new Mock<IDogStatsd>();
                    using var listener = new PerformanceCountersListener(new TestStatsdManager(freshStatsd.Object));
                    await listener.WaitForInitialization();
                    freshListeners++;
                    try
                    {
                        listener.Refresh();
                        var gauges = CountHeapGauges(freshStatsd);
                        if (gauges < 4)
                        {
                            freshListenerFailures++;
                            if (phaseBFailures.Count < MaxRecordedFailuresPerPhase)
                            {
                                phaseBFailures.Add("phase=B " + Describe(listener, freshListeners, gauges));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        freshListenerFailures++;
                        if (phaseBFailures.Count < MaxRecordedFailuresPerPhase)
                        {
                            phaseBFailures.Add("phase=B threw " + ex + " " + Describe(listener, freshListeners, -1));
                        }
                    }
                }
            }
            finally
            {
                _output.WriteLine(
                    $"DIAG summary: started={started:O} ended={DateTime.UtcNow:O} " +
                    $"sameListenerRefreshes={sameListenerRefreshes} sameListenerFailures={sameListenerFailures} " +
                    $"freshListeners={freshListeners} freshListenerFailures={freshListenerFailures}");
                foreach (var failure in phaseAFailures.Concat(phaseBFailures))
                {
                    _output.WriteLine(failure);
                }
            }

            var summary = $"DIAG sameListenerFailures={sameListenerFailures}/{sameListenerRefreshes} freshListenerFailures={freshListenerFailures}/{freshListeners}";
            phaseAFailures.Concat(phaseBFailures).Should().BeEmpty(summary + Environment.NewLine + string.Join(Environment.NewLine, phaseAFailures.Concat(phaseBFailures)));
        }

        [Fact]
        public void AsynchronousInitialization()
        {
            var barrier = new Barrier(2);
            void Callback()
            {
                barrier.SignalAndWait();
                barrier.SignalAndWait();
            }

            var statsd = new Mock<IDogStatsd>();

            using var listener = new TestPerformanceCounterListener(new TestStatsdManager(statsd.Object), Callback);

            // The first SignalAndWait will deadlock if InitializePerformanceCounters is not called asynchronously
            barrier.SignalAndWait();

            listener.WaitForInitialization().IsCompleted.Should().BeFalse();

            // Initialization is still pending, make sure Refresh doesn't throw
            listener.Refresh();

            // Nothing should have been pushed to statsd since counters are not initialized
            statsd.VerifyNoOtherCalls();

            // All done, free the thread and cleanup
            barrier.SignalAndWait();
        }

        private static int CountHeapGauges(Mock<IDogStatsd> statsd)
            => statsd.Invocations.Count(i => i.Method.Name == nameof(IDogStatsd.Gauge));

        private static string Describe(PerformanceCountersListener listener, int iteration, int observed)
        {
            var sb = new StringBuilder();
            sb.Append("DIAG utc=").Append(DateTime.UtcNow.ToString("O"))
              .Append(" iteration=").Append(iteration)
              .Append(" observed=").Append(observed)
              .Append(" pid=").Append(listener.ProcessId)
              .Append(" processName=").Append(listener.ProcessName)
              .Append(" fullInstanceName=").Append(listener.IsFullInstanceName)
              .Append(" instanceName=").Append(listener.CurrentInstanceName ?? "<null>");

            var snapshot = listener.LastObservedInstanceNames;
            sb.Append(" snapshotCount=").Append(snapshot?.Length.ToString() ?? "<not captured>")
              .Append(" matching=").Append(snapshot is null ? "<n/a>" : string.Join("|", snapshot.Where(n => n.StartsWith(listener.ProcessName))));

            foreach (var counter in listener.Counters)
            {
                var ex = counter?.LastReadException;
                sb.Append(" [").Append(counter?.CounterName ?? "<null counter>").Append(": ")
                  .Append(ex is null ? "ok" : ex.GetType().Name + ": " + ex.Message)
                  .Append(']');
            }

            return sb.ToString();
        }

        private class TestPerformanceCounterListener : PerformanceCountersListener
        {
            // The field needs to be volatile because it's used concurrently from two threads
            private volatile Action _callback;

            public TestPerformanceCounterListener(IStatsdManager statsd, Action callback)
                : base(statsd)
            {
                _callback = callback;
            }

            protected override void InitializePerformanceCounters()
            {
                while (_callback == null)
                {
                    // There is a subtle race condition because InitializePerformanceCounters is virtual
                    // and called from the base constructor
                    Thread.SpinWait(1);
                }

                _callback();

                base.InitializePerformanceCounters();
            }
        }
    }
}

#endif
