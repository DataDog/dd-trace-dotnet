#if NETCOREAPP3_1 || NET5_0
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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

            using var listener = new RuntimeEventListener(statsd.Object);

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

            using var listener = new RuntimeEventListener(statsd.Object);

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
    }
}
#endif
