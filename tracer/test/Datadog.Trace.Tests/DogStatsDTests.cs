// <copyright file="DogStatsDTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
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
            var statsd = new Mock<IDogStatsd>();
            var spans = SendSpan(tracerMetricsEnabled: false, statsd.Object);

            Assert.True(spans.Count == 1, AssertionFailureMessage(1, spans));

            // no methods should be called on the IStatsd
            statsd.VerifyNoOtherCalls();
        }

        [Fact]
        public void Send_metrics_when_enabled()
        {
            var statsd = new Mock<IDogStatsd>();

            // Setup mock to set a bool when receiving a successful response from the agent, so we know to verify success or error.
            var requestSuccessful = false;
            var requestEncounteredErrors = false;
            statsd.Setup(s => s.Counter(TracerMetricNames.Api.Responses, 1, 1, It.IsAny<string[]>())).Callback(() => requestSuccessful = true);
            statsd.Setup(s => s.Counter(TracerMetricNames.Api.Errors, 1, 1, It.IsAny<string[]>())).Callback(() => requestEncounteredErrors = true);

            var spans = SendSpan(tracerMetricsEnabled: true, statsd.Object);

            Assert.True(spans.Count == 1, AssertionFailureMessage(1, spans));

            // for a single trace, these methods are called once with a value of "1"
            statsd.Verify(
                s => s.Increment(TracerMetricNames.Queue.EnqueuedTraces, 1, 1, null),
                Times.Once());

            statsd.Verify(
                s => s.Increment(TracerMetricNames.Queue.EnqueuedSpans, 1, 1, null),
                Times.Once());

            statsd.Verify(
                s => s.Increment(TracerMetricNames.Queue.DequeuedTraces, 1, 1, null),
                Times.Once());

            statsd.Verify(
                s => s.Increment(TracerMetricNames.Queue.DequeuedSpans, 1, 1, null),
                Times.Once());

            statsd.Verify(
                s => s.Increment(TracerMetricNames.Api.Requests, 1, 1, null),
                Times.AtLeastOnce());

            if (requestSuccessful)
            {
                statsd.Verify(
                    s => s.Increment(TracerMetricNames.Api.Responses, 1, 1, new[] { "status:200" }),
                    Times.Once());
            }

            if (requestEncounteredErrors)
            {
                statsd.Verify(
                    s => s.Counter(TracerMetricNames.Api.Errors, 1, 1, null),
                    Times.AtLeastOnce());
            }

            // Remove test flakiness by not requiring the heartbeat metric, which is Timer-based
            // and not expected to fire when a trace is sent
            /*
            statsd.Verify(
                s => s.Gauge(TracerMetricNames.Health.Heartbeat, It.IsAny<double>(), 1, null),
                Times.AtLeastOnce());
            */
        }

        private static IImmutableList<MockSpan> SendSpan(bool tracerMetricsEnabled, IDogStatsd statsd)
        {
            IImmutableList<MockSpan> spans;
            var agentPort = TcpPortProvider.GetOpenPort();

            using (var agent = MockTracerAgent.Create(agentPort))
            {
                var settings = new TracerSettings
                {
                    Exporter = new ExporterSettings()
                    {
                        AgentUri = new Uri($"http://127.0.0.1:{agent.Port}"),
                    },
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

        private static string AssertionFailureMessage(int expected, IImmutableList<MockSpan> spans)
        {
            return $"Expected {expected} span, received {spans.Count}: {Environment.NewLine}{string.Join(Environment.NewLine, spans.Select(s => s.ToString()))}";
        }
    }
}
