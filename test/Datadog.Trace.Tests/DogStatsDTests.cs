using System.Threading;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.DogStatsd;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.StatsdClient;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Tests
{
    public class DogStatsDTests
    {
        private readonly ITestOutputHelper _output;

        public DogStatsDTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void SendMetrics()
        {
            var statsd = new Mock<IStatsd>();
            var api = new Mock<IApi>();
            var agentPort = TcpPortProvider.GetOpenPort();

            using (var agent = new MockTracerAgent(agentPort))
            {
                var settings = new TracerSettings();
                var agentWriter = new AgentWriter(api.Object, statsd.Object);
                var tracer = new Tracer(settings, agentWriter, sampler: null, scopeManager: null, statsd: null);

                using (var rootScope = tracer.StartActive("root"))
                {
                    Thread.Sleep(5);
                }

                agent.WaitForSpans(1);
            }

            // for a single trace, these methods are called once with a value of "1"
            dogStatsD.Verify(
                statsd => statsd.Increment(TracerMetricNames.Queue.EnqueuedTraces, 1, 1D, null),
                Times.Once());

            dogStatsD.Verify(
                statsd => statsd.Increment(TracerMetricNames.Queue.EnqueuedSpans, 1, 1D, null),
                Times.Once());

            dogStatsD.Verify(
                statsd => statsd.Gauge(TracerMetricNames.Queue.DequeuedTraces, 1, 1D, null),
                Times.Once());

            dogStatsD.Verify(
                statsd => statsd.Gauge(TracerMetricNames.Queue.DequeuedSpans, 1, 1D, null),
                Times.Once());

            // these methods can be called multiple times with a "0" value (no more traces left)
            dogStatsD.Verify(
                statsd => statsd.Gauge(TracerMetricNames.Queue.DequeuedTraces, 0, 1D, null),
                Times.AtLeastOnce);

            dogStatsD.Verify(
                statsd => statsd.Gauge(TracerMetricNames.Queue.DequeuedSpans, 0, 1D, null),
                Times.AtLeastOnce());

            // these methods can be called multiple times with a "1000" value (the max buffer size, constant)
            dogStatsD.Verify(
                statsd => statsd.Gauge(TracerMetricNames.Queue.MaxCapacity, 1000, 1D, null),
                Times.AtLeastOnce());
        }
    }
}
