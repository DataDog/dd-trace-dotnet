// <copyright file="ExporterSettingsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using FluentAssertions;
using Xunit;
using MetricsTransportType = Datadog.Trace.Vendors.StatsdClient.Transport.TransportType;

namespace Datadog.Trace.Tests.Configuration
{
    /// <summary>
    /// Testing for the parts of exporter settings that is specific to Datadog.Trace (not the dd-dotnet tool)
    /// </summary>
    public partial class ExporterSettingsTests
    {
#if NETCOREAPP3_1_OR_GREATER
        [Fact]
        public void MetricsUnixDomainSocketPath()
        {
            var param = "/var/path";
            var settingsFromSource = Setup("DD_DOGSTATSD_SOCKET", param);

            AssertMetricsUdsIsConfigured(settingsFromSource, param);
        }
#else
        [Fact]
        public void MetricsUnixDomainSocketPath_UdsUnsupported_UsesDefaultUdp()
        {
            var param = "/var/path";
            var settingsFromSource = Setup("DD_DOGSTATSD_SOCKET", param);

            AssertMetricsUdpIsConfigured(settingsFromSource);
            settingsFromSource.ValidationWarnings.Should().NotBeEmpty().And.ContainMatch("*current runtime doesn't support UDS*");
        }
#endif

        [Fact]
        public void MetricsPipeName()
        {
            var param = "/var/path";
            var settingsFromSource = Setup("DD_DOGSTATSD_PIPE_NAME", param);

            settingsFromSource.MetricsPipeName.Should().Be(param);

            AssertMetricsPipeIsConfigured(settingsFromSource, param);
        }

        [Fact]
        public void DogStatsdPort()
        {
            var param = 9333;
            var settingsFromSource = Setup("DD_DOGSTATSD_PORT", param.ToString());

            settingsFromSource.DogStatsdPort.Should().Be(param);

            CheckDefaultValues(settingsFromSource, "DogStatsdPort");
        }

        [Theory]
        [InlineData("udp://someurl", ExporterSettings.DefaultDogstatsdPort)]
        [InlineData("udp://someurl:1234", 1234)]
        [InlineData("udp://someurl:8125", 8125)]
        [InlineData("udp://someurl:0", ExporterSettings.DefaultDogstatsdPort)]
        public void Metrics_DogStatsdUrl_UDP(string sourceUrl, int expectedPort)
        {
            var settingsFromSource = Setup("DD_DOGSTATSD_URL", sourceUrl);
            AssertMetricsUdpIsConfigured(settingsFromSource, "someurl", expectedPort);
        }

#if NETCOREAPP3_1_OR_GREATER
        [Fact]
        public void Metrics_DogStatsdUrl_UdsOnWindows()
        {
            var path = @"C:\temp\someval";
            var socketPath = path.Replace("\\", "/");
            var settingsFromSource = Setup(FileExistsMock(socketPath), $"DD_DOGSTATSD_URL:unix://{path}", "DD_TRACE_AGENT_URL:http://localhost:8126");
            AssertMetricsUdsIsConfigured(settingsFromSource, socketPath);
            settingsFromSource.ValidationWarnings.Should().BeEmpty();
        }

        [Fact]
        public void Metrics_DogStatsdUrl_UdsOnLinux()
        {
            var socketPath = @"/some/socket.soc";
            var settingsFromSource = Setup(FileExistsMock(socketPath), $"DD_DOGSTATSD_URL:unix://{socketPath}", "DD_TRACE_AGENT_URL:http://localhost:8126");
            AssertMetricsUdsIsConfigured(settingsFromSource, socketPath);
            settingsFromSource.ValidationWarnings.Should().BeEmpty();
        }

        [Theory]
        [InlineData("""C:\Users\andrew.lock\AppData\Local\Temp\nltdjakv.cbs""")]
        [InlineData("/some/socket.soc")]
        public void Metrics_UdsTraceAgent_UsesDefaultUdp(string socket)
        {
            var config = Setup(FileExistsMock(socket.Replace('\\', '/')), $"DD_TRACE_AGENT_URL:{ExporterSettings.UnixDomainSocketPrefix}{socket}");
            AssertUdsIsConfigured(config, socket.Replace('\\', '/'));
            AssertMetricsUdpIsConfigured(config);
            config.ValidationWarnings.Should().BeEmpty();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void Metrics_UdsTraceAgent_EmptyDogStatsdUrl_UsesDefaultUdp(string metricsSocket)
        {
            var socket = "/some/socket.soc";
            var config = Setup(FileExistsMock(socket.Replace('\\', '/')), $"DD_DOGSTATSD_URL:{metricsSocket}", $"DD_TRACE_AGENT_URL:{ExporterSettings.UnixDomainSocketPrefix}{socket}");
            AssertUdsIsConfigured(config, socket);
            AssertMetricsUdpIsConfigured(config);
            config.ValidationWarnings.Should().BeEmpty();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void Metrics_UdsTraceAgent_EmptyDogStatsdSocket_UsesDefaultUdp(string metricsSocket)
        {
            var socket = "/some/socket.soc";
            var config = Setup(FileExistsMock(socket.Replace('\\', '/')), $"DD_DOGSTATSD_SOCKET:{metricsSocket}", $"DD_TRACE_AGENT_URL:{ExporterSettings.UnixDomainSocketPrefix}{socket}");
            AssertUdsIsConfigured(config, socket);
            AssertMetricsUdpIsConfigured(config);
            config.ValidationWarnings.Should().BeEmpty();
        }
#else
        [Fact]
        public void Metrics_DogStatsdUrl_UdsUnsupported_UsesDefaultUdpOnWindows()
        {
            var path = @"C:\temp\someval";
            var socketPath = path.Replace("\\", "/");
            var settings = Setup(FileExistsMock(socketPath), $"DD_DOGSTATSD_URL:unix://{path}", "DD_TRACE_AGENT_URL:http://localhost:8126");
            AssertMetricsUdpIsConfigured(settings);
            settings.ValidationWarnings.Should().NotBeEmpty().And.ContainMatch("*current runtime doesn't support UDS*");
        }

        [Fact]
        public void Metrics_DogStatsdUrl_UdsUnsupported_UsesDefaultUdpOnLinux()
        {
            var socketPath = @"/some/socket.soc";
            var settings = Setup(FileExistsMock(socketPath), $"DD_DOGSTATSD_URL:unix://{socketPath}", "DD_TRACE_AGENT_URL:http://localhost:8126");
            AssertMetricsUdpIsConfigured(settings);
            settings.ValidationWarnings.Should().NotBeEmpty().And.ContainMatch("*current runtime doesn't support UDS*");
        }
#endif

        [Fact]
        public void Metrics_DogStatsdUrl_InvalidUrlShouldNotThrow()
        {
            var param = "http://Invalid=%Url!!";
            var settingsFromSource = Setup(DefaultSocketFilesExist(), $"DD_DOGSTATSD_URL:{param}", "DD_TRACE_AGENT_URL:http://localhost:8126");
            CheckDefaultValues(settingsFromSource);
            settingsFromSource.ValidationWarnings.Should().Contain($"The Uri: '{param}' in {ConfigurationKeys.MetricsUri} is not valid. It won't be taken into account to send metrics. Note that only absolute urls are accepted.");
        }

#if NETCOREAPP3_1_OR_GREATER
        [Theory]
        [InlineData("unix://some/socket.soc", "/socket.soc")]
        [InlineData("unix://./socket.soc", "/socket.soc")]
        public void Metrics_DogStatsdUrl_RelativeAgentUrlShouldWarn(string param, string expectedSocket)
        {
            var settingsFromSource = Setup(DefaultSocketFilesExist(), $"DD_DOGSTATSD_URL:{param}", "DD_TRACE_AGENT_URL:http://localhost:8126");
            AssertMetricsUdsIsConfigured(settingsFromSource, expectedSocket);
            settingsFromSource.ValidationWarnings.Should().Contain($"The provided metrics Uri {param} contains a relative path which may not work. This is the path to the socket that will be used: {expectedSocket}");
        }

        [Theory(Skip = "Currently we _don't_ normalise the uds path when it's set directly, so this test is invalid. If we fix it, then we can unskip and delete Metrics_DogStatsdSocket_RelativeDomainSocketShouldNotWarn")]
        [InlineData("some/socket.soc", "/socket.soc")]
        [InlineData("./socket.soc", "/socket.soc")]
        public void Metrics_DogStatsdSocket_RelativeDomainSocketShouldWarn(string param, string expectedSocket)
        {
            var settingsFromSource = Setup("DD_DOGSTATSD_SOCKET", param);
            var uri = new Uri(ExporterSettings.UnixDomainSocketPrefix + param);

            settingsFromSource.MetricsTransport.Should().Be(MetricsTransportType.UDS);
            settingsFromSource.MetricsUnixDomainSocketPath.Should().Be(expectedSocket);
            CheckDefaultValues(settingsFromSource, "MetricsUnixDomainSocketPath", "AgentUri", "MetricsTransport");
            settingsFromSource.ValidationWarnings.Should().Contain($"The provided metrics Uri {uri.AbsolutePath} contains a relative path which may not work. This is the path to the socket that will be used: {expectedSocket}");
            settingsFromSource.ValidationWarnings.Should().Contain($"The socket {expectedSocket} provided in '{ConfigurationKeys.MetricsUnixDomainSocketPath}' cannot be found. The tracer will still rely on this socket to send metrics.");
        }

        [Theory]
        [InlineData("some/socket.soc")]
        [InlineData("./socket.soc")]
        public void Metrics_DogStatsdSocket_RelativeDomainSocketShouldNotWarn(string param)
        {
            var settingsFromSource = Setup("DD_DOGSTATSD_SOCKET", param);
            var uri = new Uri(ExporterSettings.UnixDomainSocketPrefix + param);

            settingsFromSource.MetricsTransport.Should().Be(MetricsTransportType.UDS);
            settingsFromSource.MetricsUnixDomainSocketPath.Should().Be(param);
            CheckDefaultValues(settingsFromSource, "MetricsUnixDomainSocketPath", "AgentUri", "MetricsTransport");

            // TODO: This isn't really the behaviour we _want_, but it's what we have
            // if we fix it, we can unskip Metrics_DogStatsdSocket_RelativeDomainSocketShouldWarn
            settingsFromSource.ValidationWarnings.Should().NotContainMatch("The provided metrics Uri.*");
            settingsFromSource.ValidationWarnings.Should().Contain($"The socket {param} provided in '{ConfigurationKeys.MetricsUnixDomainSocketPath}' cannot be found. The tracer will still rely on this socket to send metrics.");
        }

        [Fact]
        public void Metrics_SocketFilesExist_NoExplicitConfig_UsesMetricsSocket()
        {
            var settings = Setup(DefaultMetricsSocketFilesExist());
            AssertMetricsUdsIsConfigured(settings, ExporterSettings.DefaultMetricsUnixDomainSocket);
        }

#else
        [Fact]
        public void Metrics_SocketFilesExist_NoExplicitConfig_UdsUnsupported_UsesDefaultUdp()
        {
            var settings = Setup(DefaultMetricsSocketFilesExist());
            AssertMetricsUdpIsConfigured(settings);
        }

#endif
        [Fact]
        public void Metrics_SocketFilesExist_ExplicitMetricsPort_UsesUdp()
        {
            var expectedPort = 11125;
            var config = Setup(DefaultSocketFilesExist(), "DD_DOGSTATSD_PORT:11125");
            AssertMetricsUdpIsConfigured(config, port: expectedPort);
        }

        [Fact]
        public void Metrics_SocketFilesExist_ExplicitWindowsPipeConfig_UsesWindowsNamedPipe()
        {
            var settings = Setup(DefaultMetricsSocketFilesExist(), "DD_DOGSTATSD_PIPE_NAME:somepipe");
            AssertMetricsPipeIsConfigured(settings, "somepipe");
        }

#if NETCOREAPP3_1_OR_GREATER
        [Fact]
        public void Metrics_SocketFilesExist_ExplicitUdsConfig_UsesExplicitConfig()
        {
            var settings = Setup(DefaultMetricsSocketFilesExist(), "DD_DOGSTATSD_SOCKET:somesocket");
            AssertMetricsUdsIsConfigured(settings, "somesocket");
        }

#else
        [Fact]
        public void Metrics_SocketFilesExist_ExplicitUdsConfig_UdsUnsupported_UsesDefaultUdp()
        {
            var settings = Setup(DefaultMetricsSocketFilesExist(), "DD_DOGSTATSD_SOCKET:somesocket");
            AssertMetricsUdpIsConfigured(settings);
            settings.ValidationWarnings.Should().NotBeEmpty().And.ContainMatch("*current runtime doesn't support UDS*");
        }

#endif
        [Fact]
        public void Metrics_SocketFilesExist_ExplicitConfigForAll_UsesUdp()
        {
            var config = Setup(DefaultSocketFilesExist(), "DD_AGENT_HOST:someotherhost", "DD_DOGSTATSD_PIPE_NAME:somepipe", "DD_DOGSTATSD_SOCKET:somesocket");
            AssertMetricsUdpIsConfigured(config, "someotherhost");
        }

#if NETCOREAPP3_1_OR_GREATER
        [Fact]
        public void Metrics_SocketFilesExist_DogstatsdUrl_ExplicitConfigForAll_UsesUds()
        {
            var config = Setup(
                DefaultSocketFilesExist(),
                "DD_DOGSTATSD_URL:unix:///var/datadog/mysocket.soc",
                "DD_AGENT_HOST:someotherhost",
                "DD_DOGSTATSD_PIPE_NAME:somepipe",
                "DD_DOGSTATSD_SOCKET:somesocket");
            AssertMetricsUdsIsConfigured(config, "/var/datadog/mysocket.soc");
        }

#else
        [Fact]
        public void Metrics_SocketFilesExist_DogstatsdUrl_ExplicitConfigForAll_UdsUnsupported_UsesDefaultUdp()
        {
            var config = Setup(
                DefaultSocketFilesExist(),
                "DD_DOGSTATSD_URL:unix:///var/datadog/mysocket.soc",
                "DD_AGENT_HOST:someotherhost",
                "DD_DOGSTATSD_PIPE_NAME:somepipe",
                "DD_DOGSTATSD_SOCKET:somesocket");
            AssertMetricsUdpIsConfigured(config);
            config.ValidationWarnings.Should().NotBeEmpty().And.ContainMatch("*current runtime doesn't support UDS*");
        }
#endif

        [Fact]
        public void Metrics_SocketFilesExist_TraceAgentUrlAndAgentHostIsSet_UsesCorrectHost()
        {
            var settingsFromSource = Setup(DefaultSocketFilesExist(), "DD_TRACE_AGENT_URL:unix:///var/datadog/myscocket.soc", "DD_AGENT_HOST:someotherhost");
            AssertMetricsUdpIsConfigured(settingsFromSource, hostname: "someotherhost");
        }

        [Theory]
        [InlineData(null, null, null)]
        [InlineData("http://someUrl", null, null)]
        [InlineData("http://someUrl:1234", null, null)]
        [InlineData(null, "somehost", null)]
        [InlineData(null, "somehost", "1234")]
        [InlineData(null, null, "1234")]
        public void TraceAgentUriBase_WhenTcp_MatchesAgentUri(string agentUrl, string host, string port)
        {
            var settings = Setup(
                BuildSource(
                    $"DD_TRACE_AGENT_URL:{agentUrl}",
                    $"DD_AGENT_HOST:{host}",
                    $"DD_TRACE_AGENT_PORT:{port}"),
                NoFile());

            settings.TracesTransport.Should().Be(TracesTransportType.Default);
            settings.TraceAgentUriBase.Should().Be(settings.AgentUri.ToString());
        }

#if NETCOREAPP3_1_OR_GREATER
        [Theory]
        [InlineData("unix://some/socket.soc", null)]
        [InlineData(null, "some/socket.soc")]
        [InlineData(null, "./socket.soc")]
        public void TraceAgentUriBase_WhenUnix_MatchesAgentUri(string agentUrl, string apmReceiverSocket)
        {
            var settings = Setup(
                BuildSource(
                    $"DD_TRACE_AGENT_URL:{agentUrl}",
                    $"DD_APM_RECEIVER_SOCKET:{apmReceiverSocket}"),
                NoFile());

            settings.TracesTransport.Should().Be(TracesTransportType.UnixDomainSocket);
            settings.TraceAgentUriBase.Should().Be(settings.AgentUri.ToString());
        }
#endif

        [Fact]
        public void TraceAgentUriBase_WhenNamedPipes_ShowsPipeName()
        {
            var pipeName = "something";
            var settings = Setup("DD_TRACE_PIPE_NAME", pipeName);

            settings.TracesTransport.Should().Be(TracesTransportType.WindowsNamedPipe);
            settings.TraceAgentUriBase.Should().Be(@"\\.\pipe\" + pipeName);
        }

        private static ExporterSettings Setup(IConfigurationSource source, Func<string, bool> fileExists)
        {
            return new ExporterSettings(source, fileExists, NullConfigurationTelemetry.Instance);
        }

        private void AssertMetricsUdpIsConfigured(ExporterSettings settings, string hostname = ExporterSettings.DefaultDogstatsdHostname, int port = ExporterSettings.DefaultDogstatsdPort)
        {
            settings.MetricsTransport.Should().Be(MetricsTransportType.UDP);
            settings.MetricsHostname.Should().Be(hostname);
            settings.DogStatsdPort.Should().Be(port);
            settings.MetricsHostname.Should().NotBeEquivalentTo("localhost");
        }

        private void AssertMetricsUdsIsConfigured(ExporterSettings settings, string socketPath)
        {
            settings.MetricsTransport.Should().Be(MetricsTransportType.UDS);
            settings.MetricsUnixDomainSocketPath.Should().Be(socketPath);
            CheckDefaultValues(settings, "MetricsUnixDomainSocketPath", "MetricsTransport", "DogStatsdPort", "AgentUri");
        }

        private void AssertMetricsPipeIsConfigured(ExporterSettings settings, string pipeName)
        {
            settings.MetricsTransport.Should().Be(MetricsTransportType.NamedPipe);
            settings.MetricsPipeName.Should().Be(pipeName);
            CheckDefaultValues(settings, "MetricsTransport", "MetricsPipeName", "DogStatsdPort");
        }

        private Func<string, bool> DefaultMetricsSocketFilesExist()
        {
            return FileExistsMock(ExporterSettings.DefaultMetricsUnixDomainSocket);
        }

        private Func<string, bool> DefaultSocketFilesExist()
        {
            return FileExistsMock(ExporterSettings.DefaultTracesUnixDomainSocket, ExporterSettings.DefaultMetricsUnixDomainSocket);
        }

        private void CheckSpecificDefaultValues(ExporterSettings settings, string[] paramToIgnore)
        {
            if (!paramToIgnore.Contains("TracesPipeTimeoutMs"))
            {
                settings.TracesPipeTimeoutMs.Should().Be(500);
            }

            if (!paramToIgnore.Contains("MetricsTransport"))
            {
                settings.MetricsTransport.Should().Be(MetricsTransportType.UDP);
            }

            if (!paramToIgnore.Contains("MetricsPipeName"))
            {
                settings.MetricsPipeName.Should().BeNull();
            }

            if (!paramToIgnore.Contains("MetricsUnixDomainSocketPath"))
            {
                settings.MetricsUnixDomainSocketPath.Should().BeNull();
            }

            if (!paramToIgnore.Contains("DogStatsdPort"))
            {
                settings.DogStatsdPort.Should().Be(ExporterSettings.DefaultDogstatsdPort);
            }

            if (!paramToIgnore.Contains(nameof(settings.MetricsHostname)))
            {
                settings.MetricsHostname.Should().Be(ExporterSettings.DefaultDogstatsdHostname);
            }
        }
    }
}
