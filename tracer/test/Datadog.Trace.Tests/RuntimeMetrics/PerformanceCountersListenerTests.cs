// <copyright file="PerformanceCountersListenerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.RuntimeMetrics;
using Datadog.Trace.Vendors.StatsdClient;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.RuntimeMetrics
{
    public class PerformanceCountersListenerTests
    {
        [Fact]
        public async Task PushEvents()
        {
            var statsd = new Mock<IDogStatsd>();

            using var listener = new PerformanceCountersListener(statsd.Object);

            await listener.WaitForInitialization();

            listener.Refresh();

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
        public void AsynchronousInitialization()
        {
            var barrier = new Barrier(2);
            void Callback()
            {
                barrier.SignalAndWait();
                barrier.SignalAndWait();
            }

            var statsd = new Mock<IDogStatsd>();

            using var listener = new TestPerformanceCounterListener(statsd.Object, Callback);

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

        private class TestPerformanceCounterListener : PerformanceCountersListener
        {
            // The field needs to be volatile because it's used concurrently from two threads
            private volatile Action _callback;

            public TestPerformanceCounterListener(IDogStatsd statsd, Action callback)
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
