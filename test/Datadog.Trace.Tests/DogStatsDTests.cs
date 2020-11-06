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

#pragma warning disable CS0618 // Type or member is obsolete
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
        public void Send_batched_commands()
        {
            var statsdUdp = new Mock<IStatsdUDP>();
            var statsd = BuildMockStatsd(statsdUdp.Object);

            var batch = statsd.Object.StartBatch();

            batch.Append("1");
            batch.Append("2");

            batch.Send();

            statsdUdp.Verify(s => s.Send("1\n2"), Times.Once());
        }

        [Fact]
        public void Do_not_send_metrics_when_disabled()
        {
            var statsd = BuildMockStatsd();
            var spans = SendSpan(tracerMetricsEnabled: false, statsd.Object);

            Assert.True(spans.Count == 1, "Expected one span");

            // no methods should be called on the IStatsd
            statsd.VerifyNoOtherCalls();
        }

        [Fact]
        public void Send_metrics_when_enabled()
        {
            var statsdUdp = new Mock<IStatsdUDP>();
            var statsd = BuildMockStatsd(statsdUdp.Object);

            // Setup mock to set a bool when receiving a successful response from the agent, so we know to verify success or error.
            var requestSuccessful = false;
            var requestEncounteredErrors = false;
            statsd.Setup(s => s.GetCommand<Statsd.Counting, int>(TracerMetricNames.Api.Responses, 1, 1, It.IsAny<string[]>())).Callback(() => requestSuccessful = true);
            statsd.Setup(s => s.GetCommand<Statsd.Counting, int>(TracerMetricNames.Api.Errors, 1, 1, It.IsAny<string[]>())).Callback(() => requestEncounteredErrors = true);

            var spans = SendSpan(tracerMetricsEnabled: true, statsd.Object);

            Assert.True(spans.Count == 1, "Expected one span");

            // for a single trace, these methods are called once with a value of "1"
            statsd.Verify(
                s => s.GetCommand<Statsd.Counting, int>(TracerMetricNames.Queue.EnqueuedTraces, 1, 1, null),
                Times.Once());

            statsd.Verify(
                s => s.GetCommand<Statsd.Counting, int>(TracerMetricNames.Queue.EnqueuedSpans, 1, 1, null),
                Times.Once());

            statsd.Verify(
                s => s.GetCommand<Statsd.Counting, int>(TracerMetricNames.Queue.DequeuedTraces, 1, 1, null),
                Times.Once());

            statsd.Verify(
                s => s.GetCommand<Statsd.Counting, int>(TracerMetricNames.Queue.DequeuedSpans, 1, 1, null),
                Times.Once());

            statsd.Verify(
                s => s.GetCommand<Statsd.Counting, int>(TracerMetricNames.Api.Requests, 1, 1, null),
                Times.Once());

            if (requestSuccessful)
            {
                statsd.Verify(
                    s => s.GetCommand<Statsd.Counting, int>(TracerMetricNames.Api.Responses, 1, 1, new[] { "status:200" }),
                    Times.Once());
            }

            if (requestEncounteredErrors)
            {
                statsd.Verify(
                    s => s.GetCommand<Statsd.Counting, int>(TracerMetricNames.Api.Errors, 1, 1, null),
                    Times.AtLeastOnce());
            }

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
                s => s.GetCommand<Statsd.Gauge, int>(TracerMetricNames.Queue.MaxTraces, 1000, 1, null),
                Times.AtLeastOnce());

            // these method can be called multiple times (send buffered commands)
            statsdUdp.Verify(
                s => s.Send(It.IsAny<string>()),
                Times.AtLeastOnce());

            // these method can be called multiple times (send heartbeat)
            statsd.Verify(
                s => s.GetCommand<Statsd.Gauge, int>(TracerMetricNames.Health.Heartbeat, It.IsAny<int>(), 1, null),
                Times.AtLeastOnce());
        }

        private static IImmutableList<MockTracerAgent.Span> SendSpan(bool tracerMetricsEnabled, IBatchStatsd statsd)
        {
            IImmutableList<MockTracerAgent.Span> spans;
            var agentPort = TcpPortProvider.GetOpenPort();

            using (var agent = new MockTracerAgent(agentPort))
            {
                var settings = new TracerSettings
                {
                    AgentUri = new Uri($"http://127.0.0.1:{agent.Port}"),
                    TracerMetricsEnabled = tracerMetricsEnabled,
                    StartupDiagnosticLogEnabled = false,
                };

                var tracer = new Tracer(settings, agentWriter: null, sampler: null, scopeManager: null, statsd);

                using (var scope = tracer.StartActive("root"))
                {
                    scope.Span.ResourceName = "resource";
                    Thread.Sleep(5);
                }

                spans = agent.WaitForSpans(1);
            }

            return spans;
        }

        private static Mock<BatchStatsd> BuildMockStatsd(IStatsdUDP statsdUdp = null)
        {
            var statsd = new Mock<BatchStatsd>(statsdUdp ?? Mock.Of<IStatsdUDP>(), string.Empty, null);

            // statsd.Setup(s => s.StartBatch()).Returns(() => new Batch(statsd.Object));

            return statsd;
        }
    }
}
#pragma warning restore CS0618 // Type or member is obsolete
