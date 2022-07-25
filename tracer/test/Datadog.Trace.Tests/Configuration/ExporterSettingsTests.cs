// <copyright file="ExporterSettingsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Specialized;
using System.Linq;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using FluentAssertions;
using Xunit;
using MetricsTransportType = Datadog.Trace.Vendors.StatsdClient.Transport.TransportType;

namespace Datadog.Trace.Tests.Configuration
{
    public class ExporterSettingsTests
    {
        [Fact]
        public void DefaultValues()
        {
            var settings = new ExporterSettings();
            CheckDefaultValues(settings);
        }

        [Fact]
        public void AgentUri()
        {
            var param = "http://someUrl";
            var uri = new Uri(param);
            var settings = new ExporterSettings { AgentUri = uri };
            var settingsFromSource = Setup("DD_TRACE_AGENT_URL", param);

            AssertHttpIsConfigured(settingsFromSource, uri);
            // The Uri is used to connect to dogstatsd as well, by getting the host from the uri
            Assert.Equal(expected: MetricsTransportType.UDP, actual: settingsFromSource.MetricsTransport);
            Assert.Equal(expected: ExporterSettings.DefaultDogstatsdPort, actual: settingsFromSource.DogStatsdPort);
            AssertHttpIsConfigured(settings, uri);
            Assert.Equal(expected: MetricsTransportType.UDP, actual: settings.MetricsTransport);
            Assert.Equal(expected: ExporterSettings.DefaultDogstatsdPort, actual: settings.DogStatsdPort);
        }

        [Theory]
        [InlineData(@"C:\temp\someval")]
        // [InlineData(@"\\temp\someval")] // network path doesn't work with uris, so we don't support it :/
        public void UnixAgentUriOnWindows(string path)
        {
            var uri = new Uri($"unix://{path}");

            var settingsFromSource = Setup(FileExistsMock(path.Replace("\\", "/")), $"DD_TRACE_AGENT_URL:unix://{path}");
            AssertUdsIsConfigured(settingsFromSource, path.Replace("\\", "/"));
            settingsFromSource.AgentUri.Should().Be(uri);
            settingsFromSource.ValidationWarnings.Should().BeEmpty();
        }

        [Fact]
        public void InvalidAgentUrlShouldNotThrow()
        {
            var param = "http://Invalid=%Url!!";
            var settingsFromSource = Setup("DD_TRACE_AGENT_URL", param);
            CheckDefaultValues(settingsFromSource);
            settingsFromSource.ValidationWarnings.Should().Contain($"The Uri: '{param}' is not valid. It won't be taken into account to send traces. Note that only absolute urls are accepted.");
        }

        [Theory]
        [InlineData("unix://some/socket.soc", "/socket.soc")]
        [InlineData("unix://./socket.soc", "/socket.soc")]
        public void RelativeAgentUrlShouldWarn(string param, string expectedSocket)
        {
            var settingsFromSource = Setup("DD_TRACE_AGENT_URL", param);
            Assert.Equal(expected: TracesTransportType.UnixDomainSocket, actual: settingsFromSource.TracesTransport);
            Assert.Equal(expected: expectedSocket, actual: settingsFromSource.TracesUnixDomainSocketPath);
            Assert.Equal(new Uri(param), settingsFromSource.AgentUri);
            CheckDefaultValues(settingsFromSource, "TracesUnixDomainSocketPath", "AgentUri", "TracesTransport");
            settingsFromSource.ValidationWarnings.Should().Contain($"The provided Uri {param} contains a relative path which may not work. This is the path to the socket that will be used: /socket.soc");
        }

        [Theory]
        [InlineData("some/socket.soc", "/socket.soc")]
        [InlineData("./socket.soc", "/socket.soc")]
        public void RelativeDomainSocketShouldWarn(string param, string expectedSocket)
        {
            var settingsFromSource = Setup("DD_APM_RECEIVER_SOCKET", param);
            var uri = new Uri(ExporterSettings.UnixDomainSocketPrefix + param);

            Assert.Equal(expected: TracesTransportType.UnixDomainSocket, actual: settingsFromSource.TracesTransport);
            Assert.Equal(expected: expectedSocket, actual: settingsFromSource.TracesUnixDomainSocketPath);
            Assert.Equal(uri, settingsFromSource.AgentUri);
            CheckDefaultValues(settingsFromSource, "TracesUnixDomainSocketPath", "AgentUri", "TracesTransport");
            settingsFromSource.ValidationWarnings.Should().Contain($"The provided Uri {uri.AbsoluteUri} contains a relative path which may not work. This is the path to the socket that will be used: {expectedSocket}");
            settingsFromSource.ValidationWarnings.Should().Contain($"The socket provided {expectedSocket} cannot be found. The tracer will still rely on this socket to send traces.");
        }

        [Fact]
        public void AgentHost()
        {
            var param = "SomeHost";
            var settingsFromSource = Setup("DD_AGENT_HOST", param);

            AssertHttpIsConfigured(settingsFromSource, new Uri("http://SomeHost:8126"));
        }

        [Fact]
        public void AgentPort()
        {
            var param = 9333;
            var settingsFromSource = Setup("DD_TRACE_AGENT_PORT", param.ToString());

            AssertHttpIsConfigured(settingsFromSource, new Uri("http://127.0.0.1:9333"));
        }

        [Fact]
        public void TracesPipeName()
        {
            var param = @"C:\temp\someval";
            var settings = new ExporterSettings() { TracesPipeName = param };
            var settingsFromSource = Setup("DD_TRACE_PIPE_NAME", param);

            AssertPipeIsConfigured(settingsFromSource, param);
            settings.TracesPipeName.Should().Be(param);
        }

        [Fact]
        public void MetricsUnixDomainSocketPath()
        {
            var param = "/var/path";
            var settings = new ExporterSettings() { MetricsUnixDomainSocketPath = param };
            var settingsFromSource = Setup("DD_DOGSTATSD_SOCKET", param);

            AssertMetricsUdsIsConfigured(settingsFromSource, param);
            settings.MetricsUnixDomainSocketPath.Should().Be(param);
            // AssertUdsIsConfigured(settings, param); //This is actually not working as we don't recompute the transport when setting the property
        }

        [Fact]
        public void MetricsPipeName()
        {
            var param = "/var/path";
            var settings = new ExporterSettings() { MetricsPipeName = param };
            var settingsFromSource = Setup("DD_DOGSTATSD_PIPE_NAME", param);

            settings.MetricsPipeName.Should().Be(param);
            settingsFromSource.MetricsPipeName.Should().Be(param);

            AssertMetricsPipeIsConfigured(settingsFromSource, param);
            // AssertMetricsPipeIsConfigured(settings, param); // This is actually not working as we don't recompute the transport when setting the property
        }

        [Fact]
        public void DogStatsdPort()
        {
            var param = 9333;
            var settings = new ExporterSettings() { DogStatsdPort = param };
            var settingsFromSource = Setup("DD_DOGSTATSD_PORT", param.ToString());

            settings.DogStatsdPort.Should().Be(param);
            settingsFromSource.DogStatsdPort.Should().Be(param);

            CheckDefaultValues(settings, "DogStatsdPort");
            CheckDefaultValues(settingsFromSource, "DogStatsdPort");
        }

        [Fact]
        public void PartialFlushEnabled()
        {
            var param = true;
            var settings = new ExporterSettings() { PartialFlushEnabled = param };
            var settingsFromSource = Setup("DD_TRACE_PARTIAL_FLUSH_ENABLED", param.ToString());

            settings.PartialFlushEnabled.Should().Be(param);
            settingsFromSource.PartialFlushEnabled.Should().Be(param);

            CheckDefaultValues(settings, "PartialFlushEnabled");
            CheckDefaultValues(settingsFromSource, "PartialFlushEnabled");
        }

        [Fact]
        public void PartialFlushMinSpans()
        {
            var param = 200;
            var settings = new ExporterSettings() { PartialFlushMinSpans = param };
            var settingsFromSource = Setup("DD_TRACE_PARTIAL_FLUSH_MIN_SPANS", param.ToString());

            settings.PartialFlushMinSpans.Should().Be(param);
            settingsFromSource.PartialFlushMinSpans.Should().Be(param);

            CheckDefaultValues(settings, "PartialFlushMinSpans");
            CheckDefaultValues(settingsFromSource, "PartialFlushMinSpans");
        }

        [Fact]
        public void InvalidPartialFlushMinSpans()
        {
            var param = -200;
            var settingsFromSource = Setup("DD_TRACE_PARTIAL_FLUSH_MIN_SPANS", param.ToString());
            settingsFromSource.PartialFlushMinSpans.Should().Be(500);
            Assert.Throws<ArgumentException>(() => new ExporterSettings() { PartialFlushMinSpans = param });
        }

        [Fact]
        public void UnixDomainSocketPathWellFormed()
        {
            var settingsFromSource = Setup("DD_TRACE_AGENT_URL", "unix:///var/datadog/myscocket.soc");
            AssertUdsIsConfigured(settingsFromSource, "/var/datadog/myscocket.soc");

            var settings = new ExporterSettings();
            AssertHttpIsConfigured(settings, new Uri("http://127.0.0.1:8126/"));

            settings.AgentUri = new Uri("unix:///var/datadog/myscocket.soc");
            AssertUdsIsConfigured(settings, "/var/datadog/myscocket.soc");
        }

        [Fact]
        public void Traces_SocketFilesExist_NoExplicitConfig_UsesTraceSocket()
        {
            var settings = Setup(DefaultTraceSocketFilesExist());
            AssertUdsIsConfigured(settings, ExporterSettings.DefaultTracesUnixDomainSocket);
        }

        [Fact]
        public void Traces_SocketFilesExist_ExplicitAgentHost_UsesDefaultTcp()
        {
            var agentHost = "someotherhost";
            var expectedUri = new Uri($"http://{agentHost}:8126");
            var settings = Setup(DefaultSocketFilesExist(), "DD_AGENT_HOST:someotherhost");
            AssertHttpIsConfigured(settings, expectedUri);
        }

        [Fact]
        public void Traces_SocketFilesExist_ExplicitTraceAgentPort_UsesDefaultTcp()
        {
            var expectedUri = new Uri($"http://127.0.0.1:8111");
            var settings = Setup(DefaultTraceSocketFilesExist(), "DD_TRACE_AGENT_PORT:8111");
            AssertHttpIsConfigured(settings, expectedUri);
        }

        [Fact]
        public void Traces_SocketFilesExist_ExplicitWindowsPipeConfig_UsesWindowsNamedPipe()
        {
            var settings = Setup(DefaultTraceSocketFilesExist(), "DD_TRACE_PIPE_NAME:somepipe");
            AssertPipeIsConfigured(settings, "somepipe");
        }

        /// <summary>
        /// This test is not actually important for functionality, it is just to document existing behavior.
        /// If for some reason the priority needs to change in the future, there is no compelling reason why this test can't change.
        /// </summary>
        [Fact]
        public void Traces_SocketFilesExist_ExplicitConfigForWindowsPipeAndUdp_PrioritizesWindowsPipe()
        {
            var settings = Setup(DefaultTraceSocketFilesExist(), "DD_TRACE_PIPE_NAME:somepipe", "DD_APM_RECEIVER_SOCKET:somesocket");
            AssertPipeIsConfigured(settings, "somepipe");
        }

        [Fact]
        public void Traces_SocketFilesExist_ExplicitConfigForAll_UsesDefaultTcp()
        {
            var settings = Setup(DefaultTraceSocketFilesExist(), "DD_TRACE_AGENT_URL:http://toto:1234", "DD_TRACE_PIPE_NAME:somepipe", "DD_APM_RECEIVER_SOCKET:somesocket");
            AssertHttpIsConfigured(settings, new Uri("http://toto:1234"));
        }

        [Fact]
        public void Metrics_SocketFilesExist_NoExplicitConfig_UsesMetricsSocket()
        {
            var settings = Setup(DefaultMetricsSocketFilesExist());
            AssertMetricsUdsIsConfigured(settings, ExporterSettings.DefaultMetricsUnixDomainSocket);
        }

        [Fact]
        public void Metrics_SocketFilesExist_ExplicitMetricsPort_UsesUdp()
        {
            var expectedPort = 11125;
            var config = Setup(DefaultSocketFilesExist(), "DD_DOGSTATSD_PORT:11125");
            Assert.Equal(expected: MetricsTransportType.UDP, actual: config.MetricsTransport);
            Assert.Equal(expected: expectedPort, actual: config.DogStatsdPort);
        }

        [Fact]
        public void Metrics_SocketFilesExist_ExplicitWindowsPipeConfig_UsesWindowsNamedPipe()
        {
            var settings = Setup(DefaultMetricsSocketFilesExist(), "DD_DOGSTATSD_PIPE_NAME:somepipe");
            AssertMetricsPipeIsConfigured(settings, "somepipe");
        }

        [Fact]
        public void Metrics_SocketFilesExist_ExplicitUdsConfig_UsesExplicitConfig()
        {
            var settings = Setup(DefaultMetricsSocketFilesExist(), "DD_DOGSTATSD_SOCKET:somesocket");
            AssertMetricsUdsIsConfigured(settings, "somesocket");
        }

        [Fact]
        public void Metrics_SocketFilesExist_ExplicitConfigForAll_UsesDefaultTcp()
        {
            var config = Setup(DefaultSocketFilesExist(), "DD_AGENT_HOST:someotherhost", "DD_DOGSTATSD_PIPE_NAME:somepipe", "DD_DOGSTATSD_SOCKET:somesocket");
            Assert.Equal(expected: TracesTransportType.Default, actual: config.TracesTransport);
        }

        private ExporterSettings Setup(string key, string value)
        {
            return new ExporterSettings(BuildSource(key + ":" + value), NoFile());
        }

        private ExporterSettings Setup(Func<string, bool> fileExistsMock, params string[] config)
        {
            return new ExporterSettings(BuildSource(config), fileExistsMock);
        }

        private NameValueConfigurationSource BuildSource(params string[] config)
        {
            var configNameValues = new NameValueCollection();

            foreach (var item in config)
            {
                var separatorIndex = item.IndexOf(':');
                configNameValues.Add(item.Substring(0, separatorIndex), item.Substring(separatorIndex + 1));
            }

            return new NameValueConfigurationSource(configNameValues);
        }

        private void AssertHttpIsConfigured(ExporterSettings settings, Uri expectedUri)
        {
            Assert.Equal(expected: TracesTransportType.Default, actual: settings.TracesTransport);
            Assert.Equal(expected: expectedUri, actual: settings.AgentUri);
            Assert.False(string.Equals(settings.AgentUri.Host, "localhost", StringComparison.OrdinalIgnoreCase));
            CheckDefaultValues(settings, "AgentUri", "TracesTransport");
        }

        private void AssertUdsIsConfigured(ExporterSettings settings, string socketPath)
        {
            Assert.Equal(expected: TracesTransportType.UnixDomainSocket, actual: settings.TracesTransport);
            Assert.Equal(expected: socketPath, actual: settings.TracesUnixDomainSocketPath);
            Assert.False(string.Equals(settings.AgentUri.Host, "localhost", StringComparison.OrdinalIgnoreCase));
            CheckDefaultValues(settings, "TracesUnixDomainSocketPath", "AgentUri", "TracesTransport");
        }

        private void AssertMetricsUdsIsConfigured(ExporterSettings settings, string socketPath)
        {
            Assert.Equal(expected: MetricsTransportType.UDS, actual: settings.MetricsTransport);
            Assert.Equal(expected: socketPath, actual: settings.MetricsUnixDomainSocketPath);
            CheckDefaultValues(settings, "MetricsUnixDomainSocketPath", "MetricsTransport", "DogStatsdPort");
        }

        private void AssertPipeIsConfigured(ExporterSettings settings, string pipeName)
        {
            Assert.Equal(expected: TracesTransportType.WindowsNamedPipe, actual: settings.TracesTransport);
            Assert.Equal(expected: pipeName, actual: settings.TracesPipeName);
            Assert.NotNull(settings.AgentUri);
            Assert.False(string.Equals(settings.AgentUri.Host, "localhost", StringComparison.OrdinalIgnoreCase));
            CheckDefaultValues(settings, "TracesPipeName", "AgentUri", "TracesTransport", "TracesPipeTimeoutMs");
        }

        private void AssertMetricsPipeIsConfigured(ExporterSettings settings, string pipeName)
        {
            Assert.Equal(expected: MetricsTransportType.NamedPipe, actual: settings.MetricsTransport);
            Assert.Equal(expected: pipeName, actual: settings.MetricsPipeName);
            CheckDefaultValues(settings, "MetricsTransport", "MetricsPipeName", "DogStatsdPort");
        }

        private Func<string, bool> NoFile()
        {
            return (f) => false;
        }

        private Func<string, bool> DefaultMetricsSocketFilesExist()
        {
            return FileExistsMock(ExporterSettings.DefaultMetricsUnixDomainSocket);
        }

        private Func<string, bool> DefaultTraceSocketFilesExist()
        {
            return FileExistsMock(ExporterSettings.DefaultTracesUnixDomainSocket);
        }

        private Func<string, bool> DefaultSocketFilesExist()
        {
            return FileExistsMock(ExporterSettings.DefaultTracesUnixDomainSocket, ExporterSettings.DefaultMetricsUnixDomainSocket);
        }

        private Func<string, bool> FileExistsMock(params string[] existingFiles)
        {
            return (f) =>
            {
                return existingFiles.Contains(f);
            };
        }

        private void CheckDefaultValues(ExporterSettings settings, params string[] paramToIgnore)
        {
            if (!paramToIgnore.Contains("AgentUri"))
            {
                settings.AgentUri.AbsoluteUri.Should().Be("http://127.0.0.1:8126/");
            }

            if (!paramToIgnore.Contains("TracesTransport"))
            {
                settings.TracesTransport.Should().Be(TracesTransportType.Default);
            }

            if (!paramToIgnore.Contains("MetricsTransport"))
            {
                settings.MetricsTransport.Should().Be(MetricsTransportType.UDP);
            }

            // TracesTimeoutMs
            if (!paramToIgnore.Contains("TracesTimeoutMs"))
            {
                settings.TracesTimeoutMs.Should().Be(15_000);
            }

            if (!paramToIgnore.Contains("TracesPipeName"))
            {
                settings.TracesPipeName.Should().BeNull();
            }

            if (!paramToIgnore.Contains("TracesPipeTimeoutMs"))
            {
                settings.TracesPipeTimeoutMs.Should().Be(500);
            }

            if (!paramToIgnore.Contains("MetricsPipeName"))
            {
                settings.MetricsPipeName.Should().BeNull();
            }

            if (!paramToIgnore.Contains("TracesUnixDomainSocketPath"))
            {
                settings.TracesUnixDomainSocketPath.Should().BeNull();
            }

            if (!paramToIgnore.Contains("MetricsUnixDomainSocketPath"))
            {
                settings.MetricsUnixDomainSocketPath.Should().BeNull();
            }

            if (!paramToIgnore.Contains("DogStatsdPort"))
            {
                settings.DogStatsdPort.Should().Be(ExporterSettings.DefaultDogstatsdPort);
            }

            if (!paramToIgnore.Contains("PartialFlushEnabled"))
            {
                settings.PartialFlushEnabled.Should().BeFalse();
            }

            if (!paramToIgnore.Contains("PartialFlushMinSpans"))
            {
                settings.PartialFlushMinSpans.Should().Be(500);
            }
        }
    }
}
