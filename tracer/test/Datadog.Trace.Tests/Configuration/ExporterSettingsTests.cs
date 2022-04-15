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
            AssertHttpIsConfigured(settings, uri);
        }

        [Fact]
        public void InvalidAgentUrlShouldNotThrow()
        {
            var param = "http://Invalid=%Url!!";
            var settingsFromSource = Setup("DD_TRACE_AGENT_URL", param);
            CheckDefaultValues(settingsFromSource);
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
            var param = "/var/path";
            var settings = new ExporterSettings() { TracesPipeName = param };
            var settingsFromSource = Setup("DD_TRACE_PIPE_NAME", param);

            AssertPipeIsConfigured(settingsFromSource, param);
            AssertPipeIsConfigured(settings, param);
        }

        [Fact]
        public void MetricsUnixDomainSocketPath()
        {
            var param = "/var/path";
            var settings = new ExporterSettings() { MetricsUnixDomainSocketPath = param };
            var settingsFromSource = Setup("DD_DOGSTATSD_SOCKET", param);

            AssertMetricsUdsIsConfigured(settingsFromSource, param);
            AssertMetricsUdsIsConfigured(settings, param);
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
            AssertMetricsPipeIsConfigured(settings, param);
        }

        [Fact]
        public void DogStatsdPort()
        {
            var param = 9333;
            var settings = new ExporterSettings() { DogStatsdPort = param };
            var settingsFromSource = Setup("DD_DOGSTATSD_PORT", param.ToString());

            settings.DogStatsdPort.Should().Be(param);
            settingsFromSource.DogStatsdPort.Should().Be(param);

            CheckDefaultValues(settings, paramToIgnore: "DogStatsdPort");
            CheckDefaultValues(settingsFromSource, paramToIgnore: "DogStatsdPort");
        }

        [Fact]
        public void PartialFlushEnabled()
        {
            var param = true;
            var settings = new ExporterSettings() { PartialFlushEnabled = param };
            var settingsFromSource = Setup("DD_TRACE_PARTIAL_FLUSH_ENABLED", param.ToString());

            settings.PartialFlushEnabled.Should().Be(param);
            settingsFromSource.PartialFlushEnabled.Should().Be(param);

            CheckDefaultValues(settings, paramToIgnore: "PartialFlushEnabled");
            CheckDefaultValues(settingsFromSource, paramToIgnore: "PartialFlushEnabled");
        }

        [Fact]
        public void PartialFlushMinSpans()
        {
            var param = 200;
            var settings = new ExporterSettings() { PartialFlushMinSpans = param };
            var settingsFromSource = Setup("DD_TRACE_PARTIAL_FLUSH_MIN_SPANS", param.ToString());

            settings.PartialFlushMinSpans.Should().Be(param);
            settingsFromSource.PartialFlushMinSpans.Should().Be(param);

            CheckDefaultValues(settings, paramToIgnore: "PartialFlushMinSpans");
            CheckDefaultValues(settingsFromSource, paramToIgnore: "PartialFlushMinSpans");
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
            var settings = Setup("DD_TRACE_AGENT_URL", "unix:///var/datadog/myscocket.soc");
            // TODO, Handle the property as well as the URI (ie if we set the URI, and we leave the property, I assume we should set this property when setting the URI
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
        public void Traces_SocketFilesExist_ExplicitConfigForWindowsPipeAndAgent_PrioritizesWindowsPipe()
        {
            var settings = Setup(DefaultTraceSocketFilesExist(), "DD_TRACE_PIPE_NAME:somepipe", "DD_TRACE_AGENT_PORT:8111");
            AssertPipeIsConfigured(settings, "somepipe");
        }

        [Fact]
        public void Traces_SocketFilesExist_ExplicitConfigForAll_UsesDefaultTcp()
        {
            var settings = Setup(DefaultTraceSocketFilesExist(), "DD_TRACE_AGENT_URL:http://toto:1234", "DD_TRACE_PIPE_NAME:somepipe");
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

        [Fact]
        public void UltimateTest()
        {
            // Set everything and check precedence.
            var settings = FullyConfiguredSettings();
            FullyConfiguredAssert(settings);
        }

        [Fact]
        public void PropertyOverrideWithPrecedence()
        {
            // Set everything and check precedence with property override.
            var settings = FullyConfiguredSettings();
            settings.TracesUnixDomainSocketPath = "/wow/bob/amazing";
            FullyConfiguredAssert(settings);
            settings.TracesPipeName = "/wow/bob/amazing";
            FullyConfiguredAssert(settings);
            settings.MetricsPipeName = "/wow/bob/amazing";
            FullyConfiguredAssert(settings);
            settings.MetricsUnixDomainSocketPath = "/wow/bob/amazing";
            FullyConfiguredAssert(settings);
        }

        [Fact]
        public void PropertyOverrideWithoutPrecedence()
        {
            // Set everything and check precedence with property override.
            var settings = Setup(DefaultSocketFilesExist());
            AssertUdsIsConfigured(settings, ExporterSettings.DefaultTracesUnixDomainSocket, checkMetricsValues: false);
            AssertMetricsUdsIsConfigured(settings, ExporterSettings.DefaultMetricsUnixDomainSocket, checkTracesValues: false);

            settings.TracesUnixDomainSocketPath = "/wow/bob/amazing";
            AssertUdsIsConfigured(settings, "/wow/bob/amazing", checkMetricsValues: false);
            AssertMetricsUdsIsConfigured(settings, ExporterSettings.DefaultMetricsUnixDomainSocket, checkTracesValues: false);

            settings.TracesPipeName = "/wow/pipe/amazing";
            AssertBasicPipeIsConfigured(settings, "/wow/pipe/amazing");
            AssertMetricsUdsIsConfigured(settings, ExporterSettings.DefaultMetricsUnixDomainSocket, checkTracesValues: false);

            settings.MetricsUnixDomainSocketPath = "/wow/bobby/amazing";
            AssertBasicPipeIsConfigured(settings, "/wow/pipe/amazing");
            AssertMetricsUdsIsConfigured(settings, "/wow/bobby/amazing", checkTracesValues: false);

            settings.MetricsPipeName = "/wow/pipo/amazing";
            AssertBasicPipeIsConfigured(settings, "/wow/pipe/amazing");
            Assert.Equal(expected: MetricsTransportType.NamedPipe, actual: settings.MetricsTransport);
            Assert.Equal(expected: "/wow/pipo/amazing", actual: settings.MetricsPipeName);
        }

        private ExporterSettings FullyConfiguredSettings()
        {
            #pragma warning disable SA1026 // parameters on the same line
            var args = new []
            {
                "DD_AGENT_HOST:someotherhost", "DD_TRACE_AGENT_URL:http://toto:1234", "DD_TRACE_AGENT_PORT:8111",
                "DD_TRACE_PIPE_NAME:somepipe", "DD_TRACE_PIPE_TIMEOUT_MS:42",
                "DD_DOGSTATSD_PIPE_NAME:somepipe", "DD_DOGSTATSD_SOCKET:somesocket", "DD_DOGSTATSD_PORT:11125", "DD_DOGSTATSD_SOCKET:uds.soc",
                "DD_TRACE_PARTIAL_FLUSH_MIN_SPANS:332", "DD_TRACE_PARTIAL_FLUSH_ENABLED:true"
            };
            return Setup(DefaultSocketFilesExist(), args);
        }

        private void FullyConfiguredAssert(ExporterSettings settings)
        {
            Assert.Equal(new Uri("http://toto:1234"), settings.AgentUri);
            Assert.Equal(TracesTransportType.Default, settings.TracesTransport);
            Assert.Null(settings.TracesPipeName);
            Assert.Equal(42, settings.TracesPipeTimeoutMs);
            Assert.Null(settings.TracesUnixDomainSocketPath);

            Assert.Equal(MetricsTransportType.UDP, settings.MetricsTransport);
            Assert.Equal(11125, settings.DogStatsdPort);
            Assert.Null(settings.MetricsPipeName);
            Assert.Null(settings.MetricsUnixDomainSocketPath);

            Assert.True(settings.PartialFlushEnabled);
            Assert.Equal(332, settings.PartialFlushMinSpans);
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

        private void AssertHttpIsConfigured(ExporterSettings settings, Uri expectedUri, bool checkMetricsValues = true)
        {
            Assert.Equal(expected: TracesTransportType.Default, actual: settings.TracesTransport);
            Assert.Equal(expected: expectedUri, actual: settings.AgentUri);
            Assert.False(string.Equals(settings.AgentUri.Host, "localhost", StringComparison.OrdinalIgnoreCase));
            CheckDefaultValues(settings, checkMetricsValues, checkTracesValues: true, "AgentUri", "TracesTransport");
        }

        private void AssertUdsIsConfigured(ExporterSettings settings, string socketPath, bool checkMetricsValues = true)
        {
            Assert.Equal(expected: TracesTransportType.UnixDomainSocket, actual: settings.TracesTransport);
            Assert.Equal(expected: socketPath, actual: settings.TracesUnixDomainSocketPath);
            Assert.NotNull(settings.AgentUri);
            Assert.False(string.Equals(settings.AgentUri.Host, "localhost", StringComparison.OrdinalIgnoreCase));
            CheckDefaultValues(settings, checkMetricsValues, checkTracesValues: true, "TracesUnixDomainSocketPath", "AgentUri", "TracesTransport");
        }

        private void AssertMetricsUdsIsConfigured(ExporterSettings settings, string socketPath, bool checkTracesValues = true)
        {
            Assert.Equal(expected: MetricsTransportType.UDS, actual: settings.MetricsTransport);
            Assert.Equal(expected: socketPath, actual: settings.MetricsUnixDomainSocketPath);
            CheckDefaultValues(settings, checkMetricsValues: true, checkTracesValues: checkTracesValues, "MetricsUnixDomainSocketPath", "MetricsTransport");
        }

        private void AssertMetricsUdpIsConfigured(ExporterSettings settings, int port, bool checkTracesValues = true)
        {
            Assert.Equal(expected: MetricsTransportType.UDP, actual: settings.MetricsTransport);
            Assert.Equal(expected: port, actual: settings.DogStatsdPort);
            CheckDefaultValues(settings, checkMetricsValues: true, checkTracesValues: checkTracesValues, "MetricsTransport", "DogStatsdPort");
        }

        private void AssertBasicPipeIsConfigured(ExporterSettings settings, string pipeName)
        {
            Assert.Equal(expected: TracesTransportType.WindowsNamedPipe, actual: settings.TracesTransport);
            Assert.Equal(expected: pipeName, actual: settings.TracesPipeName);
            Assert.NotNull(settings.AgentUri);
            Assert.False(string.Equals(settings.AgentUri.Host, "localhost", StringComparison.OrdinalIgnoreCase));
        }

        private void AssertPipeIsConfigured(ExporterSettings settings, string pipeName, bool checkMetricsValues = true)
        {
            AssertBasicPipeIsConfigured(settings, pipeName);
            CheckDefaultValues(settings, checkMetricsValues, checkTracesValues: true, "TracesPipeName", "AgentUri", "TracesTransport");
        }

        private void AssertMetricsPipeIsConfigured(ExporterSettings settings, string pipeName, bool checkTracesValues = true)
        {
            Assert.Equal(expected: MetricsTransportType.NamedPipe, actual: settings.MetricsTransport);
            Assert.Equal(expected: pipeName, actual: settings.MetricsPipeName);
            CheckDefaultValues(settings, checkMetricsValues: true, checkTracesValues: checkTracesValues, "MetricsTransport", "MetricsPipeName");
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

        private void CheckDefaultValues(ExporterSettings settings, bool checkMetricsValues = true, bool checkTracesValues = true, params string[] paramToIgnore)
        {
            if (checkTracesValues)
            {
                if (!paramToIgnore.Contains("AgentUri"))
                {
                    settings.AgentUri.Should().Be("http://127.0.0.1:8126/");
                }

                if (!paramToIgnore.Contains("TracesTransport"))
                {
                    settings.TracesTransport.Should().Be(TracesTransportType.Default);
                }

                if (!paramToIgnore.Contains("TracesPipeName"))
                {
                    settings.TracesPipeName.Should().BeNull();
                }

                if (!paramToIgnore.Contains("TracesPipeTimeoutMs"))
                {
                    settings.TracesPipeTimeoutMs.Should().Be(500);
                }

                if (!paramToIgnore.Contains("TracesUnixDomainSocketPath"))
                {
                    settings.TracesUnixDomainSocketPath.Should().BeNull();
                }
            }

            if (checkMetricsValues)
            {
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
