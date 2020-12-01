#if NETCOREAPP3_1 || NET5_0
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

            statsd.Verify(s => s.Gauge(MetricsPaths.ContentionTime, It.IsAny<double>(), 1, null), Times.Once);
            statsd.Verify(s => s.Counter(MetricsPaths.ContentionCount, It.IsAny<long>(), 1, null), Times.Once);
            statsd.Verify(s => s.Gauge(MetricsPaths.ThreadPoolWorkersCount, It.IsAny<int>(), 1, null), Times.Once);
        }
    }
}
#endif
