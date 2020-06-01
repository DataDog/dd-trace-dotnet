using System;
using System.Collections.Immutable;
using System.Threading;
using Datadog.Core.Tools;
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
        public void Do_not_send_metrics_when_disabled()
        {
            var statsd = new Mock<IStatsd>();
            var spans = SendSpan(tracerMetricsEnabled: false, statsd);

            Assert.True(spans.Count == 1, "Expected one span");

            // no methods should be called on the IStatsd
            statsd.VerifyNoOtherCalls();
        }

        [Fact]
        public void Send_metrics_when_enabled()
        {
            var statsd = new Mock<IStatsd>();

            // Setup mock to set a bool when receiving a successful response from the agent, so we know to verify success or error.
            var markOneSuccess = false;
            statsd.Setup(s => s.Add<Statsd.Counting, int>(TracerMetricNames.Api.Responses, 1, 1, "status:200")).Callback(() => markOneSuccess = true);

            var spans = SendSpan(tracerMetricsEnabled: true, statsd);

            Assert.True(spans.Count == 1, "Expected one span");

            // for a single trace, these methods are called once with a value of "1"
            statsd.Verify(
                s => s.Add<Statsd.Counting, int>(TracerMetricNames.Queue.EnqueuedTraces, 1, 1, null),
                Times.Once());

            statsd.Verify(
                s => s.Add<Statsd.Counting, int>(TracerMetricNames.Queue.EnqueuedSpans, 1, 1, null),
                Times.Once());

            statsd.Verify(
                s => s.Add<Statsd.Counting, int>(TracerMetricNames.Queue.DequeuedTraces, 1, 1, null),
                Times.Once());

            statsd.Verify(
                s => s.Add<Statsd.Counting, int>(TracerMetricNames.Queue.DequeuedSpans, 1, 1, null),
                Times.Once());

            statsd.Verify(
                s => s.Add<Statsd.Counting, int>(TracerMetricNames.Api.Requests, 1, 1, null),
                Times.Once());

            // Verify success or error was called once
            statsd.Verify(
                markOneSuccess switch
                {
                    true => (s => s.Add<Statsd.Counting, int>(TracerMetricNames.Api.Responses, 1, 1, "status:200")),
                    false => (s => s.Add<Statsd.Counting, int>(TracerMetricNames.Api.Errors, 1, 1, null)),
                },
                Times.Once());

            // If success, verify error was called zero times, or vice-versa.
            statsd.Verify(
                markOneSuccess switch
                {
                    true => (s => s.Add<Statsd.Counting, int>(TracerMetricNames.Api.Errors, 1, 1, null)),
                    false => (s => s.Add<Statsd.Counting, int>(TracerMetricNames.Api.Responses, 1, 1, "status:200")),
                },
                Times.Never());

            // these methods can be called multiple times with a "0" value (no more traces left)
            /*
            statsd.Verify(
                s => s.Add<Statsd.Gauge, int>(TracerMetricNames.Queue.DequeuedTraces, 0, 1, null),
                Times.AtLeastOnce);

            statsd.Verify(
                s => s.Add<Statsd.Gauge, int>(TracerMetricNames.Queue.DequeuedSpans, 0, 1, null),
                Times.AtLeastOnce());
            */

            // these method can be called multiple times with a "1000" value (the max buffer size, constant)
            statsd.Verify(
                s => s.Add<Statsd.Gauge, int>(TracerMetricNames.Queue.MaxTraces, 1000, 1, null),
                Times.AtLeastOnce());

            // these method can be called multiple times (send buffered commands)
            statsd.Verify(
                s => s.Send(),
                Times.AtLeastOnce());

            // these method can be called multiple times (send heartbeat)
            statsd.Verify(
                s => s.Add<Statsd.Gauge, int>(TracerMetricNames.Health.Heartbeat, It.IsAny<int>(), 1, null),
                Times.AtLeastOnce());

            // no other methods should be called on the IStatsd
            statsd.VerifyNoOtherCalls();
        }

        private static IImmutableList<MockTracerAgent.Span> SendSpan(bool tracerMetricsEnabled, Mock<IStatsd> statsd)
        {
            IImmutableList<MockTracerAgent.Span> spans;
            var agentPort = TcpPortProvider.GetOpenPort();

            using (var agent = new MockTracerAgent(agentPort))
            {
                var settings = new TracerSettings
                               {
                                   AgentUri = new Uri($"http://localhost:{agent.Port}"),
                                   TracerMetricsEnabled = tracerMetricsEnabled
                               };

                var tracer = new Tracer(settings, agentWriter: null, sampler: null, scopeManager: null, statsd.Object);

                using (var scope = tracer.StartActive("root"))
                {
                    scope.Span.ResourceName = "resource";
                    Thread.Sleep(5);
                }

                spans = agent.WaitForSpans(1);
            }

            return spans;
        }
    }
}
