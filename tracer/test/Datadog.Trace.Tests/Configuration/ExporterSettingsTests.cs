// <copyright file="ExporterSettingsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Specialized;
using System.Linq;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Xunit;
using MetricsTransportType = Datadog.Trace.Vendors.StatsdClient.Transport.TransportType;

namespace Datadog.Trace.Tests.Configuration
{
    public class ExporterSettingsTests
    {
        [Fact]
        public void Traces_UrlShouldBeTheDefaultEvenIfEverythingElseIsSet()
        {
            var settings = Setup(FileExistsMock(), "DD_TRACE_AGENT_URL:http://thisIsTheOne", "DD_AGENT_HOST:someotherhost", "DD_TRACE_AGENT_PORT:8111", "DD_TRACE_PIPE_NAME:somepipe", "DD_APM_RECEIVER_SOCKET:somesocket");

            AssertHttpIsConfigured(settings, "http://thisIsTheOne");
            Assert.Equal(expected: 8125, actual: settings.DogStatsdPort);
            Assert.False(settings.PartialFlushEnabled);
            Assert.Equal(expected: 500, actual: settings.PartialFlushMinSpans);
        }

        [Fact]
        public void Traces_UrlWithUnixPathShouldBeTheDefaultEvenIfEverythingElseIsSet()
        {
            var settings = Setup(FileExistsMock(), "DD_TRACE_AGENT_URL:unix:///thisIsTheOneSocket", "DD_AGENT_HOST:someotherhost", "DD_TRACE_AGENT_PORT:8111", "DD_TRACE_PIPE_NAME:somepipe", "DD_APM_RECEIVER_SOCKET:somesocket");

            AssertUdsIsConfigured(settings, "/thisIsTheOneSocket");
            Assert.False(settings.PartialFlushEnabled);
            Assert.Equal(expected: 500, actual: settings.PartialFlushMinSpans);
        }

        [Fact]
        public void Traces_Uds_Have_Precedence_Over_Http()
        {
            // Should work even if the file isn't present
            var settings = Setup(NoFile(), "DD_AGENT_HOST:someotherhost", "DD_TRACE_PIPE_NAME:somepipe", "DD_APM_RECEIVER_SOCKET:somesocket");
            AssertUdsIsConfigured(settings, "somesocket");
        }

        [Fact]
        public void Traces_WindowsPipe_Have_Precedence_Over_Http()
        {
            // Should not even check if a file exists
            var settings = Setup("DD_TRACE_PIPE_NAME:somepipe", "DD_AGENT_HOST:someotherhost");
            Assert.Equal(expected: TracesTransportType.WindowsNamedPipe, actual: settings.TracesTransport);
            Assert.Equal(expected: "somepipe", actual: settings.TracesPipeName);
        }

        [Fact]
        public void Traces_ExplicitAgentHost_UsesHttp()
        {
            var agentHost = "someotherhost";
            var expectedUri = $"http://{agentHost}:8126";

            // Should not even check if a file exists
            var settings = Setup($"DD_AGENT_HOST:{agentHost}");

            AssertHttpIsConfigured(settings, expectedUri);
        }

        [Fact]
        public void Traces_SocketFilesExist_ExplicitTraceAgentPort_UsesDefaultHttp()
        {
            var settings = Setup(DefaultSocketFilesExist(), "DD_TRACE_AGENT_PORT:8111");
            AssertHttpIsConfigured(settings, "http://127.0.0.1:8111");
        }

        [Fact]
        public void Traces_SocketFilesExist_NoExplicitConfig_UsesTraceSocket()
        {
            var settings = Setup(DefaultSocketFilesExist());
            AssertUdsIsConfigured(settings, ExporterSettings.DefaultTracesUnixDomainSocket);
        }

        [Fact]
        public void NoSocketFiles_NoExplicitConfiguration_DefaultsMatchExpectation()
        {
            var settings = Setup(NoFile());
            AssertHttpIsConfigured(settings, "http://127.0.0.1:8126");
            Assert.Equal(expected: MetricsTransportType.UDP, actual: settings.MetricsTransport);
            Assert.Equal(expected: 8125, actual: settings.DogStatsdPort);
            Assert.False(settings.PartialFlushEnabled);
            Assert.Equal(expected: 500, actual: settings.PartialFlushMinSpans);
        }

        [Fact]
        public void InvalidUrl_Fallbacks_On_Default()
        {
            var settings = Setup(NoFile(), "DD_TRACE_AGENT_URL:http://Invalid=%Url!!");
            AssertHttpIsConfigured(settings, "http://127.0.0.1:8126");
        }

        [Fact]
        public void InvalidHost_Fallbacks_On_Uds_If_File()
        {
            var settings = Setup(DefaultSocketFilesExist(), "DD_AGENT_HOST:Invalid=%Host!!");
            AssertUdsIsConfigured(settings, ExporterSettings.DefaultTracesUnixDomainSocket);
            Assert.Equal(expected: new Uri("http://127.0.0.1:8126"), actual: settings.AgentUri);
        }

        [Fact]
        public void InvalidHost_Fallbacks_On_Default()
        {
            var settings = Setup(NoFile(), "DD_AGENT_HOST:Invalid=%Host!!");
            AssertHttpIsConfigured(settings, "http://127.0.0.1:8126");
        }

        [Theory]
        [InlineData("DD_APM_RECEIVER_SOCKET:somesocket")]
        [InlineData("DD_TRACE_PIPE_NAME:somepipe")]
        public void InvalidHost_And_UdsOrPipe_Throws(string correctConfig)
        {
            Assert.Throws<UriFormatException>(() => Setup(NoFile(), "DD_AGENT_HOST:Invalid=%Host!!", correctConfig));
        }

        [Fact]
        public void PartialFlushVariables_Populated()
        {
            var settings = Setup(FileExistsMock(), "DD_TRACE_PARTIAL_FLUSH_ENABLED:true", "DD_TRACE_PARTIAL_FLUSH_MIN_SPANS:999");
            Assert.True(settings.PartialFlushEnabled);
            Assert.Equal(expected: 999, actual: settings.PartialFlushMinSpans);
        }

        [Fact]
        public void Metrics_SocketFilesExist_NoExplicitConfig_UsesMetricsSocket()
        {
            var settings = Setup(DefaultSocketFilesExist());
            Assert.Equal(expected: MetricsTransportType.UDS, actual: settings.MetricsTransport);
            Assert.Equal(expected: ExporterSettings.DefaultMetricsUnixDomainSocket, actual: settings.MetricsUnixDomainSocketPath);
        }

        [Fact]
        public void Metrics_SocketFilesExist_ExplicitMetricsPort_UsesUdp()
        {
            var expectedPort = 11125;
            var settings = Setup(DefaultSocketFilesExist(), "DD_DOGSTATSD_PORT:11125");
            Assert.Equal(expected: MetricsTransportType.UDP, actual: settings.MetricsTransport);
            Assert.Equal(expected: expectedPort, actual: settings.DogStatsdPort);
        }

        [Fact]
        public void Metrics_SocketFilesExist_ExplicitWindowsPipeConfig_UsesWindowsNamedPipe()
        {
            var settings = Setup(DefaultSocketFilesExist(), "DD_DOGSTATSD_PIPE_NAME:somepipe");
            Assert.Equal(expected: MetricsTransportType.NamedPipe, actual: settings.MetricsTransport);
            Assert.Equal(expected: "somepipe", actual: settings.MetricsPipeName);
        }

        [Fact]
        public void Metrics_SocketFilesExist_ExplicitUdsConfig_UsesExplicitConfig()
        {
            var settings = Setup(DefaultSocketFilesExist(), "DD_DOGSTATSD_SOCKET:somesocket");
            Assert.Equal(expected: MetricsTransportType.UDS, actual: settings.MetricsTransport);
            Assert.Equal(expected: "somesocket", actual: settings.MetricsUnixDomainSocketPath);
        }

        [Fact]
        public void Metrics_SocketFilesExist_ExplicitConfigForAll_UsesDefaultTcp()
        {
            var settings = Setup(DefaultSocketFilesExist(), "DD_AGENT_HOST:someotherhost", "DD_DOGSTATSD_PIPE_NAME:somepipe", "DD_DOGSTATSD_SOCKET:somesocket");
            Assert.Equal(expected: TracesTransportType.Default, actual: settings.TracesTransport);
        }

        private void AssertHttpIsConfigured(ExporterSettings settings, string expectedUri)
        {
            Assert.Equal(expected: TracesTransportType.Default, actual: settings.TracesTransport);
            Assert.Equal(expected: new Uri(expectedUri), actual: settings.AgentUri);
            Assert.False(string.Equals(settings.AgentUri.Host, "localhost", StringComparison.OrdinalIgnoreCase));
        }

        private void AssertUdsIsConfigured(ExporterSettings settings, string socketPath)
        {
            Assert.Equal(expected: TracesTransportType.UnixDomainSocket, actual: settings.TracesTransport);
            Assert.Equal(expected: socketPath, actual: settings.TracesUnixDomainSocketPath);
            Assert.NotNull(settings.AgentUri);
            Assert.False(string.Equals(settings.AgentUri.Host, "localhost", StringComparison.OrdinalIgnoreCase));
        }

        private ExporterSettings Setup(params string[] config)
        {
            return Setup(null, config);
        }

        private ExporterSettings Setup(Func<string, bool> fileExistsMock, params string[] config)
        {
            var configNameValues = new NameValueCollection();

            foreach (var item in config)
            {
                var separatorIndex = item.IndexOf(':');
                configNameValues.Add(item.Substring(0, separatorIndex), item.Substring(separatorIndex + 1));
            }

            var configSource = new NameValueConfigurationSource(configNameValues);

            var exporterSettings = new ExporterSettings(configSource, fileExistsMock);

            return exporterSettings;
        }

        private Func<string, bool> NoFile()
        {
            return (f) => false;
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
    }
}
