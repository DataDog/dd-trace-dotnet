#if NETFRAMEWORK
using Datadog.Trace.RuntimeMetrics;
using Datadog.Trace.Vendors.StatsdClient;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.RuntimeMetrics
{
    public class PerformanceCountersListenerTests
    {
        [Fact]
        public void PushEvents()
        {
            var statsd = new Mock<IDogStatsd>();

            using var listener = new PerformanceCountersListener(statsd.Object);

            listener.Refresh();

            statsd.Verify(s => s.Gauge(MetricsPaths.Gen0HeapSize, It.IsAny<long?>(), 1, null), Times.Once);
            statsd.Verify(s => s.Gauge(MetricsPaths.Gen1HeapSize, It.IsAny<long?>(), 1, null), Times.Once);
            statsd.Verify(s => s.Gauge(MetricsPaths.Gen2HeapSize, It.IsAny<long?>(), 1, null), Times.Once);
            statsd.Verify(s => s.Gauge(MetricsPaths.LohSize, It.IsAny<long?>(), 1, null), Times.Once);

            statsd.VerifyNoOtherCalls();
            statsd.ResetCalls();

            listener.Refresh();

            statsd.Verify(s => s.Gauge(MetricsPaths.Gen0HeapSize, It.IsAny<long?>(), 1, null), Times.Once);
            statsd.Verify(s => s.Gauge(MetricsPaths.Gen1HeapSize, It.IsAny<long?>(), 1, null), Times.Once);
            statsd.Verify(s => s.Gauge(MetricsPaths.Gen2HeapSize, It.IsAny<long?>(), 1, null), Times.Once);
            statsd.Verify(s => s.Gauge(MetricsPaths.LohSize, It.IsAny<long?>(), 1, null), Times.Once);

            // Those metrics aren't pushed the first time (differential count)
            statsd.Verify(s => s.Increment(MetricsPaths.Gen0CollectionsCount, It.IsAny<int>(), 1, null), Times.Once);
            statsd.Verify(s => s.Increment(MetricsPaths.Gen1CollectionsCount, It.IsAny<int>(), 1, null), Times.Once);
            statsd.Verify(s => s.Increment(MetricsPaths.Gen2CollectionsCount, It.IsAny<int>(), 1, null), Times.Once);
            statsd.Verify(s => s.Counter(MetricsPaths.ContentionCount, It.IsAny<long?>(), 1, null), Times.Once);

            statsd.VerifyNoOtherCalls();
        }
    }
}


#endif
