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
        public void NoSocketFiles_NoExplicitConfiguration_DefaultsMatchExpectation()
        {
            var config = Setup(FileExistsMock());
            Assert.Equal(expected: TracesTransportType.Default, actual: config.TracesTransport);
            Assert.Equal(expected: MetricsTransportType.UDP, actual: config.MetricsTransport);
            Assert.Equal(expected: new Uri($"http://127.0.0.1:8126"), actual: config.AgentUri);
            Assert.Equal(expected: 8125, actual: config.DogStatsdPort);
            Assert.False(config.PartialFlushEnabled);
            Assert.Equal(expected: 500, actual: config.PartialFlushMinSpans);
        }

        [Fact]
        public void PartialFlushVariables_Populated()
        {
            var config = Setup(FileExistsMock(), "DD_TRACE_PARTIAL_FLUSH_ENABLED:true", "DD_TRACE_PARTIAL_FLUSH_MIN_SPANS:999");
            Assert.True(config.PartialFlushEnabled);
            Assert.Equal(expected: 999, actual: config.PartialFlushMinSpans);
        }

        [Fact]
        public void Traces_SocketFilesExist_NoExplicitConfig_UsesTraceSocket()
        {
            var config = Setup(DefaultSocketFilesExist());
            Assert.Equal(expected: TracesTransportType.UnixDomainSocket, actual: config.TracesTransport);
            Assert.Equal(expected: ExporterSettings.DefaultTracesUnixDomainSocket, actual: config.TracesUnixDomainSocketPath);
        }

        [Fact]
        public void Traces_SocketFilesExist_ExplicitAgentHost_UsesDefaultTcp()
        {
            var agentHost = "someotherhost";
            var expectedUri = new Uri($"http://{agentHost}:8126");
            var config = Setup(DefaultSocketFilesExist(), "DD_AGENT_HOST:someotherhost");
            Assert.Equal(expected: TracesTransportType.Default, actual: config.TracesTransport);
            Assert.Equal(expected: expectedUri, actual: config.AgentUri);
        }

        [Fact]
        public void Traces_SocketFilesExist_ExplicitTraceAgentPort_UsesDefaultTcp()
        {
            var expectedUri = new Uri($"http://127.0.0.1:8111");
            var config = Setup(DefaultSocketFilesExist(), "DD_TRACE_AGENT_PORT:8111");
            Assert.Equal(expected: TracesTransportType.Default, actual: config.TracesTransport);
            Assert.Equal(expected: expectedUri, actual: config.AgentUri);
        }

        [Fact]
        public void Traces_SocketFilesExist_ExplicitWindowsPipeConfig_UsesWindowsNamedPipe()
        {
            var config = Setup(DefaultSocketFilesExist(), "DD_TRACE_PIPE_NAME:somepipe");
            Assert.Equal(expected: TracesTransportType.WindowsNamedPipe, actual: config.TracesTransport);
            Assert.Equal(expected: "somepipe", actual: config.TracesPipeName);
        }

        [Fact]
        public void Traces_SocketFilesExist_ExplicitUdsConfig_UsesExplicitConfig()
        {
            var config = Setup(DefaultSocketFilesExist(), "DD_APM_RECEIVER_SOCKET:somesocket");
            Assert.Equal(expected: TracesTransportType.UnixDomainSocket, actual: config.TracesTransport);
            Assert.Equal(expected: "somesocket", actual: config.TracesUnixDomainSocketPath);
        }

        [Fact]
        public void Traces_SocketFilesExist_ExplicitConfigForAll_UsesDefaultTcp()
        {
            var config = Setup(DefaultSocketFilesExist(), "DD_AGENT_HOST:someotherhost", "DD_TRACE_PIPE_NAME:somepipe", "DD_APM_RECEIVER_SOCKET:somesocket");
            Assert.Equal(expected: TracesTransportType.Default, actual: config.TracesTransport);
        }

        [Fact]
        public void Metrics_SocketFilesExist_NoExplicitConfig_UsesMetricsSocket()
        {
            var config = Setup(DefaultSocketFilesExist());
            Assert.Equal(expected: MetricsTransportType.UDS, actual: config.MetricsTransport);
            Assert.Equal(expected: ExporterSettings.DefaultMetricsUnixDomainSocket, actual: config.MetricsUnixDomainSocketPath);
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
            var config = Setup(DefaultSocketFilesExist(), "DD_DOGSTATSD_PIPE_NAME:somepipe");
            Assert.Equal(expected: MetricsTransportType.NamedPipe, actual: config.MetricsTransport);
            Assert.Equal(expected: "somepipe", actual: config.MetricsPipeName);
        }

        [Fact]
        public void Metrics_SocketFilesExist_ExplicitUdsConfig_UsesExplicitConfig()
        {
            var config = Setup(DefaultSocketFilesExist(), "DD_DOGSTATSD_SOCKET:somesocket");
            Assert.Equal(expected: MetricsTransportType.UDS, actual: config.MetricsTransport);
            Assert.Equal(expected: "somesocket", actual: config.MetricsUnixDomainSocketPath);
        }

        [Fact]
        public void Metrics_SocketFilesExist_ExplicitConfigForAll_UsesDefaultTcp()
        {
            var config = Setup(DefaultSocketFilesExist(), "DD_AGENT_HOST:someotherhost", "DD_DOGSTATSD_PIPE_NAME:somepipe", "DD_DOGSTATSD_SOCKET:somesocket");
            Assert.Equal(expected: TracesTransportType.Default, actual: config.TracesTransport);
        }

        private ExporterSettings Setup(Func<string, bool> fileExistsMock, params string[] config)
        {
            var configNameValues = new NameValueCollection();

            foreach (var item in config)
            {
                var parts = item.Split(':');
                configNameValues.Add(parts[0], parts[1]);
            }

            var configSource = new NameValueConfigurationSource(configNameValues);

            var exporterSettings = new ExporterSettings(configSource, fileExistsMock);

            return exporterSettings;
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
