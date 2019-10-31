using System.Threading;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.DogStatsD;
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
            var dogStatsD = new Mock<IDogStatsd>();
            var api = new Mock<IApi>();
            var agentPort = TcpPortProvider.GetOpenPort();

            using (var agent = new MockTracerAgent(agentPort))
            {
                var settings = new TracerSettings();
                var agentWriter = new AgentWriter(api.Object, dogStatsD.Object);
                var tracer = new Tracer(settings, agentWriter, sampler: null, scopeManager: null, dogStatsdClient: null);

                using (var rootScope = tracer.StartActive("root"))
                {
                    Thread.Sleep(5);
                }

                agent.WaitForSpans(1);
            }

            // for a single trace, these methods are called once with a value of "1"
            dogStatsD.Verify(
                statsd => statsd.Increment(TracerMetricNames.Queue.PushedTraces, 1, 1D, null),
                Times.Once());

            dogStatsD.Verify(
                statsd => statsd.Increment(TracerMetricNames.Queue.PushedSpans, 1, 1D, null),
                Times.Once());

            dogStatsD.Verify(
                statsd => statsd.Gauge(TracerMetricNames.Queue.PoppedTraces, 1, 1D, null),
                Times.Once());

            dogStatsD.Verify(
                statsd => statsd.Gauge(TracerMetricNames.Queue.PoppedSpans, 1, 1D, null),
                Times.Once());

            // these methods can be called multiple times with a "0" value (no more traces left)
            dogStatsD.Verify(
                statsd => statsd.Gauge(TracerMetricNames.Queue.PoppedTraces, 0, 1D, null),
                Times.AtLeastOnce);

            dogStatsD.Verify(
                statsd => statsd.Gauge(TracerMetricNames.Queue.PoppedSpans, 0, 1D, null),
                Times.AtLeastOnce());

            // these methods can be called multiple times with a "1000" value (the max buffer size, constant)
            dogStatsD.Verify(
                statsd => statsd.Gauge(TracerMetricNames.Queue.BufferedTracesLimit, 1000, 1D, null),
                Times.AtLeastOnce());
        }
    }
}
