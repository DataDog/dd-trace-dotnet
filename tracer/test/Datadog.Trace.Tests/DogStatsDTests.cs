// <copyright file="DogStatsDTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.DogStatsd;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.StatsdClient;
using Datadog.Trace.Vendors.StatsdClient.Transport;
using FluentAssertions;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Tests
{
    [Collection(nameof(EnvironmentVariablesTestCollection))]
    [EnvironmentRestorer(
        ConfigurationKeys.AgentHost,
        ConfigurationKeys.AgentUri,
        ConfigurationKeys.AgentPort,
        ConfigurationKeys.DogStatsdPort,
        ConfigurationKeys.TracesPipeName,
        ConfigurationKeys.TracesUnixDomainSocketPath,
        ConfigurationKeys.MetricsPipeName,
        ConfigurationKeys.MetricsUnixDomainSocketPath)]
    public class DogStatsDTests
    {
        private readonly ITestOutputHelper _output;

        public DogStatsDTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task Do_not_send_metrics_when_disabled()
        {
            var statsd = new Mock<IDogStatsd>();
            var spans = await SendSpan(tracerMetricsEnabled: false, statsd.Object);

            Assert.True(spans.Count == 1, AssertionFailureMessage(1, spans));

            // no methods should be called on the IStatsd
            statsd.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Send_metrics_when_enabled()
        {
            var statsd = new Mock<IDogStatsd>();

            // Setup mock to set a bool when receiving a successful response from the agent, so we know to verify success or error.
            var requestSuccessful = false;
            var requestEncounteredErrors = false;
            statsd.Setup(s => s.Counter(TracerMetricNames.Api.Responses, 1, 1, It.IsAny<string[]>())).Callback(() => requestSuccessful = true);
            statsd.Setup(s => s.Counter(TracerMetricNames.Api.Errors, 1, 1, It.IsAny<string[]>())).Callback(() => requestEncounteredErrors = true);

            var spans = await SendSpan(tracerMetricsEnabled: true, statsd.Object);

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

        [SkippableTheory]
        [InlineData(null, null, null)] // Should default to udp
        [InlineData("http://127.0.0.1:1234", null, null)]
        [InlineData(null, "127.0.0.1", null)]
        [InlineData(null, "127.0.0.1", "1234")]
        public void CanCreateDogStatsD_UDP_FromTraceAgentSettings(string agentUri, string agentHost, string port)
        {
            SkipOn.Platform(SkipOn.PlatformValue.MacOs);

            var settings = new TracerSettings(new NameValueConfigurationSource(new()
            {
                { ConfigurationKeys.AgentUri, agentUri },
                { ConfigurationKeys.AgentHost, agentHost },
                { ConfigurationKeys.DogStatsdPort, port },
            })).Build();

            settings.Exporter.MetricsTransport.Should().Be(TransportType.UDP);
            var expectedPort = settings.Exporter.DogStatsdPort;

            // Dogstatsd tries to actually contact the agent during creation, so need to have something listening
            // No guarantees it's actually using the _right_ config here, but it's better than nothing
            using var agent = MockTracerAgent.Create(_output, useStatsd: true, requestedStatsDPort: expectedPort);

            var dogStatsD = TracerManagerFactory.CreateDogStatsdClient(settings, "test service", null);

            // If there's an error during configuration, we get a no-op instance, so using this as a test
            dogStatsD.Should()
                     .NotBeNull()
                     .And.BeOfType<DogStatsdService>();
        }

        [SkippableFact]
        public void CanCreateDogStatsD_NamedPipes_FromTraceAgentSettings()
        {
            SkipOn.Platform(SkipOn.PlatformValue.MacOs);
            SkipOn.Platform(SkipOn.PlatformValue.Linux);

            using var agent = MockTracerAgent.Create(_output, new WindowsPipesConfig($"trace-{Guid.NewGuid()}", $"metrics-{Guid.NewGuid()}") { UseDogstatsD = true });

            var settings = new TracerSettings(new NameValueConfigurationSource(new()
            {
                { ConfigurationKeys.MetricsPipeName, agent.StatsWindowsPipeName },
            })).Build();

            settings.Exporter.MetricsTransport.Should().Be(TransportType.NamedPipe);

            // Dogstatsd tries to actually contact the agent during creation, so need to have something listening
            // No guarantees it's actually using the _right_ config here, but it's better than nothing
            var dogStatsD = TracerManagerFactory.CreateDogStatsdClient(settings, "test service", null);

            // If there's an error during configuration, we get a no-op instance, so using this as a test
            dogStatsD.Should()
                     .NotBeNull()
                     .And.BeOfType<DogStatsdService>();
        }

#if NETCOREAPP3_1_OR_GREATER
        [SkippableFact]
        public void CanCreateDogStatsD_UDS_FromTraceAgentSettings()
        {
            // UDP Datagrams over UDP are not supported on Windows
            SkipOn.Platform(SkipOn.PlatformValue.Windows);
            SkipOn.Platform(SkipOn.PlatformValue.MacOs);

            var tracesPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var metricsPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            using var agent = MockTracerAgent.Create(_output, new UnixDomainSocketConfig(tracesPath, metricsPath) { UseDogstatsD = true });

            var settings = new TracerSettings(new NameValueConfigurationSource(new()
            {
                { ConfigurationKeys.AgentUri, $"unix://{tracesPath}" },
                { ConfigurationKeys.MetricsUnixDomainSocketPath, $"unix://{metricsPath}" },
            })).Build();

            settings.Exporter.MetricsTransport.Should().Be(TransportType.UDS);

            // Dogstatsd tries to actually contact the agent during creation, so need to have something listening
            // No guarantees it's actually using the _right_ config here, but it's better than nothing
            var dogStatsD = TracerManagerFactory.CreateDogStatsdClient(settings, "test service", null);

            // If there's an error during configuration, we get a no-op instance, so using this as a test
            dogStatsD.Should()
                     .NotBeNull()
                     .And.BeOfType<DogStatsdService>();
        }

        [SkippableFact]
        public void CanCreateDogStatsD_UDS_FallsBackToUdp_FromTraceAgentSettings()
        {
            SkipOn.Platform(SkipOn.PlatformValue.MacOs);

            var tracesPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            using var udsAgent = MockTracerAgent.Create(_output, new UnixDomainSocketConfig(tracesPath, null) { UseDogstatsD = false });
            using var udpAgent = MockTracerAgent.Create(_output, useStatsd: true);

            var settings = new TracerSettings(new NameValueConfigurationSource(new()
            {
                { ConfigurationKeys.AgentUri, $"unix://{tracesPath}" },
            })).Build();

            // If we're not using the "default" UDS path, then we fallback to UDP for stats
            // Should fallback to the "default" stats location
            settings.Exporter.MetricsTransport.Should().Be(TransportType.UDP);

            // Dogstatsd tries to actually contact the agent during creation, so need to have something listening
            // No guarantees it's actually using the _right_ config here, but it's better than nothing
            var dogStatsD = TracerManagerFactory.CreateDogStatsdClient(settings, "test service", null);

            // If there's an error during configuration, we get a no-op instance, so using this as a test
            dogStatsD.Should()
                     .NotBeNull()
                     .And.BeOfType<DogStatsdService>();
        }
#endif

        private static async Task<IImmutableList<MockSpan>> SendSpan(bool tracerMetricsEnabled, IDogStatsd statsd)
        {
            IImmutableList<MockSpan> spans;
            var agentPort = TcpPortProvider.GetOpenPort();

            using (var agent = MockTracerAgent.Create(null, agentPort))
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

                await using var tracer = TracerHelper.Create(settings, agentWriter: null, sampler: null, scopeManager: null, statsd);

                using (var scope = tracer.StartActive("root"))
                {
                    scope.Span.ResourceName = "resource";
                    await Task.Delay(5);
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
