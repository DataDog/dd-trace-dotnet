// <copyright file="AzurePerformanceCountersListenerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

using System;
using Datadog.Trace.RuntimeMetrics;
using Datadog.Trace.Vendors.StatsdClient;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.RuntimeMetrics
{
    public class AzurePerformanceCountersListenerTests
    {
        [Fact]
        public void PushEvents()
        {
            Environment.SetEnvironmentVariable(
                AzureAppServicePerformanceCounters.EnvironmentVariableName,
                "{\"bytesInAllHeaps\": 8069304,\"gcHandles\": 6796,\"gen0Collections\": 108,\"gen1Collections\": 76,\"gen2Collections\": 16,\"inducedGC\": 0,\"pinnedObjects\": 20,\"committedBytes\": 17788928,\"reservedBytes\": 50319360,\"timeInGC\": 99342447,\"timeInGCBase\": 385095681,\"allocatedBytes\": 761378928,\"gen0HeapSize\": 8388608,\"gen1HeapSize\": 1448968,\"gen2HeapSize\": 3857504,\"largeObjectHeapSize\": 2762832,\"currentAssemblies\": 104,\"currentClassesLoaded\": 177389,\"exceptionsThrown\": 913,\"appDomains\": 10,\"appDomainsUnloaded\": 8}");

            const double expectedGen0HeapSize = 8388608;
            const double expectedGen1HeapSize = 1448968;
            const double expectedGen2HeapSize = 3857504;
            const double expectedLohHeapSize = 2762832;

            var statsd = new Mock<IDogStatsd>();

            using var listener = new AzureAppServicePerformanceCounters(statsd.Object);

            listener.Refresh();

            statsd.Verify(s => s.Gauge(MetricsNames.Gen0HeapSize, expectedGen0HeapSize, 1, null), Times.Once);
            statsd.Verify(s => s.Gauge(MetricsNames.Gen1HeapSize, expectedGen1HeapSize, 1, null), Times.Once);
            statsd.Verify(s => s.Gauge(MetricsNames.Gen2HeapSize, expectedGen2HeapSize, 1, null), Times.Once);
            statsd.Verify(s => s.Gauge(MetricsNames.LohSize, expectedLohHeapSize, 1, null), Times.Once);

            statsd.VerifyNoOtherCalls();
            statsd.Invocations.Clear();

            listener.Refresh();

            statsd.Verify(s => s.Gauge(MetricsNames.Gen0HeapSize, expectedGen0HeapSize, 1, null), Times.Once);
            statsd.Verify(s => s.Gauge(MetricsNames.Gen1HeapSize, expectedGen1HeapSize, 1, null), Times.Once);
            statsd.Verify(s => s.Gauge(MetricsNames.Gen2HeapSize, expectedGen2HeapSize, 1, null), Times.Once);
            statsd.Verify(s => s.Gauge(MetricsNames.LohSize, expectedLohHeapSize, 1, null), Times.Once);

            // Those metrics aren't pushed the first time (differential count)
            statsd.Verify(s => s.Increment(MetricsNames.Gen0CollectionsCount, It.IsAny<int>(), 1, null), Times.Once);
            statsd.Verify(s => s.Increment(MetricsNames.Gen1CollectionsCount, It.IsAny<int>(), 1, null), Times.Once);
            statsd.Verify(s => s.Increment(MetricsNames.Gen2CollectionsCount, It.IsAny<int>(), 1, null), Times.Once);

            statsd.VerifyNoOtherCalls();
        }
    }
}

#endif
