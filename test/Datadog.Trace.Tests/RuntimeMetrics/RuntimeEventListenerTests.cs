#if NETCOREAPP3_1 || NET5_0
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Threading;
using Datadog.Trace.RuntimeMetrics;
using Datadog.Trace.Vendors.StatsdClient;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.RuntimeMetrics
{
    public class RuntimeEventListenerTests
    {
        [Fact]
        public void PushEvents()
        {
            var statsd = new Mock<IDogStatsd>();

            using var listener = new RuntimeEventListener(statsd.Object, TimeSpan.FromSeconds(10));

            listener.Refresh();

            statsd.Verify(s => s.Gauge(MetricsNames.ContentionTime, It.IsAny<double>(), 1, null), Times.Once);
            statsd.Verify(s => s.Counter(MetricsNames.ContentionCount, It.IsAny<long>(), 1, null), Times.Once);
            statsd.Verify(s => s.Gauge(MetricsNames.ThreadPoolWorkersCount, It.IsAny<int>(), 1, null), Times.Once);
        }

        [Fact]
        public void MonitorGarbageCollections()
        {
            string[] compactingGcTags = { "compacting_gc:true" };

            var statsd = new Mock<IDogStatsd>();

            var mutex = new ManualResetEventSlim();

            // GcPauseTime is pushed on the GcRestartEnd event, which should be the last event for any GC
            statsd.Setup(s => s.Timer(MetricsNames.GcPauseTime, It.IsAny<double>(), It.IsAny<double>(), null))
                .Callback(() => mutex.Set());

            using var listener = new RuntimeEventListener(statsd.Object, TimeSpan.FromSeconds(10));

            statsd.ResetCalls();

            for (int i = 0; i < 3; i++)
            {
                mutex.Reset(); // In case a GC was triggered when creating the listener

                GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);

                // GC events are pushed asynchronously, wait for the last one to be processed
                mutex.Wait();
            }

            statsd.Verify(s => s.Gauge(MetricsNames.Gen0HeapSize, It.IsAny<ulong>(), It.IsAny<double>(), null), Times.AtLeastOnce);
            statsd.Verify(s => s.Gauge(MetricsNames.Gen1HeapSize, It.IsAny<ulong>(), It.IsAny<double>(), null), Times.AtLeastOnce);
            statsd.Verify(s => s.Gauge(MetricsNames.Gen2HeapSize, It.IsAny<ulong>(), It.IsAny<double>(), null), Times.AtLeastOnce);
            statsd.Verify(s => s.Gauge(MetricsNames.LohSize, It.IsAny<ulong>(), It.IsAny<double>(), null), Times.AtLeastOnce);
            statsd.Verify(s => s.Timer(MetricsNames.GcPauseTime, It.IsAny<double>(), It.IsAny<double>(), null), Times.AtLeastOnce);
            statsd.Verify(s => s.Gauge(MetricsNames.GcMemoryLoad, It.IsAny<uint>(), It.IsAny<double>(), null), Times.AtLeastOnce);
            statsd.Verify(s => s.Increment(MetricsNames.Gen2CollectionsCount, 1, It.IsAny<double>(), compactingGcTags), Times.AtLeastOnce);
        }

        [Fact]
        public void PushEventCounters()
        {
            // Pretending we're aspnetcore
            var eventSource = new EventSource("Microsoft.AspNetCore.Hosting");

            var mutex = new ManualResetEventSlim();

            Func<double> callback = () =>
            {
                mutex.Set();
                return 0.0;
            };

            var counters = new List<DiagnosticCounter>
            {
                new PollingCounter("current-requests", eventSource, () => 1.0),
                new PollingCounter("failed-requests", eventSource, () => 2.0),
                new PollingCounter("total-requests", eventSource, () => 4.0),
                new PollingCounter("request-queue-length", eventSource, () => 8.0),
                new PollingCounter("connection-queue-length", eventSource, () => 16.0),
                new PollingCounter("total-connections", eventSource, () => 32.0),

                // This counter sets the mutex, so it needs to be created last
                new PollingCounter("Dummy", eventSource, callback)
            };

            var statsd = new Mock<IDogStatsd>();
            using var listener = new RuntimeEventListener(statsd.Object, TimeSpan.FromSeconds(1));

            // Wait for the counters to be refreshed
            mutex.Wait();

            statsd.Verify(s => s.Gauge(MetricsNames.AspNetCoreCurrentRequests, 1.0, 1, null), Times.AtLeastOnce);
            statsd.Verify(s => s.Gauge(MetricsNames.AspNetCoreFailedRequests, 2.0, 1, null), Times.AtLeastOnce);
            statsd.Verify(s => s.Gauge(MetricsNames.AspNetCoreTotalRequests, 4.0, 1, null), Times.AtLeastOnce);
            statsd.Verify(s => s.Gauge(MetricsNames.AspNetCoreRequestQueueLength, 8.0, 1, null), Times.AtLeastOnce);
            statsd.Verify(s => s.Gauge(MetricsNames.AspNetCoreConnectionQueueLength, 16.0, 1, null), Times.AtLeastOnce);
            statsd.Verify(s => s.Gauge(MetricsNames.AspNetCoreTotalConnections, 32.0, 1, null), Times.AtLeastOnce);

            foreach (var counter in counters)
            {
                counter.Dispose();
            }
        }
    }
}
#endif
