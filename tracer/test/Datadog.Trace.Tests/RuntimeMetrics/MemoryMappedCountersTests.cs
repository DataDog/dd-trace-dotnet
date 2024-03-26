// <copyright file="MemoryMappedCountersTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

using Datadog.Trace.RuntimeMetrics;
using Datadog.Trace.Vendors.StatsdClient;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.RuntimeMetrics
{
    public class MemoryMappedCountersTests
    {
        [Fact]
        public void PushEvents()
        {
            var statsd = new Mock<IDogStatsd>();

            using var listener = new MemoryMappedCounters(statsd.Object);

            listener.Refresh();

            statsd.Verify(s => s.Gauge(MetricsNames.Gen0HeapSize, It.Is<double>(v => v > 0), 1, null), Times.Once);
            statsd.Verify(s => s.Gauge(MetricsNames.Gen1HeapSize, It.Is<double>(v => v > 0), 1, null), Times.Once);
            statsd.Verify(s => s.Gauge(MetricsNames.Gen2HeapSize, It.Is<double>(v => v > 0), 1, null), Times.Once);
            statsd.Verify(s => s.Gauge(MetricsNames.LohSize, It.Is<double>(v => v > 0), 1, null), Times.Once);

            statsd.VerifyNoOtherCalls();
            statsd.Invocations.Clear();

            listener.Refresh();

            statsd.Verify(s => s.Gauge(MetricsNames.Gen0HeapSize, It.Is<double>(v => v > 0), 1, null), Times.Once);
            statsd.Verify(s => s.Gauge(MetricsNames.Gen1HeapSize, It.Is<double>(v => v > 0), 1, null), Times.Once);
            statsd.Verify(s => s.Gauge(MetricsNames.Gen2HeapSize, It.Is<double>(v => v > 0), 1, null), Times.Once);
            statsd.Verify(s => s.Gauge(MetricsNames.LohSize, It.Is<double>(v => v > 0), 1, null), Times.Once);

            // Those metrics aren't pushed the first time (differential count)
            statsd.Verify(s => s.Increment(MetricsNames.Gen0CollectionsCount, It.IsAny<int>(), 1, null), Times.Once);
            statsd.Verify(s => s.Increment(MetricsNames.Gen1CollectionsCount, It.IsAny<int>(), 1, null), Times.Once);
            statsd.Verify(s => s.Increment(MetricsNames.Gen2CollectionsCount, It.IsAny<int>(), 1, null), Times.Once);
            statsd.Verify(s => s.Counter(MetricsNames.ContentionCount, It.IsAny<double>(), 1, null), Times.Once);

            statsd.VerifyNoOtherCalls();
        }
    }
}

#endif
