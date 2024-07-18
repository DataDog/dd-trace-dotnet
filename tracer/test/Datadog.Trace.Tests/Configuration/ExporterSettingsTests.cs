// <copyright file="ExporterSettingsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Specialized;
using System.Linq;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
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
            AssertMetricsUdpIsConfigured(settingsFromSource, "someurl");
            AssertHttpIsConfigured(settings, uri);
            AssertMetricsUdpIsConfigured(settings, "someurl");
        }

#if NETCOREAPP3_1_OR_GREATER
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
            // Without additional settings, metrics defaults to UDP even if DD_TRACE_AGENT_URL is UDS
            AssertMetricsUdpIsConfigured(settingsFromSource);
        }
#else
        [Theory]
        [InlineData(@"C:\temp\someval")]
        public void UnixAgentUriOnWindows_UdsUnsupported_UsesDefaultHttp(string path)
        {
            var settingsFromSource = Setup(FileExistsMock(path.Replace("\\", "/")), $"DD_TRACE_AGENT_URL:unix://{path}");
            var expectedUri = new Uri($"http://127.0.0.1:8126");
            AssertHttpIsConfigured(settingsFromSource, expectedUri);
            settingsFromSource.AgentUri.Should().Be(expectedUri);
            settingsFromSource.ValidationWarnings.Should().NotBeEmpty().And.ContainMatch("*current runtime doesn't support UDS*");
            // Without additional settings, metrics defaults to UDP even if DD_TRACE_AGENT_URL is UDS
            AssertMetricsUdpIsConfigured(settingsFromSource);
        }
#endif

        [Fact]
        public void InvalidAgentUrlShouldNotThrow()
        {
            var param = "http://Invalid=%Url!!";
            var settingsFromSource = Setup("DD_TRACE_AGENT_URL", param);
            CheckDefaultValues(settingsFromSource);
            settingsFromSource.ValidationWarnings.Should().Contain($"The Uri: '{param}' is not valid. It won't be taken into account to send traces. Note that only absolute urls are accepted.");
        }

#if NETCOREAPP3_1_OR_GREATER
        [Theory]
        [InlineData("unix://some/socket.soc", "/socket.soc")]
        [InlineData("unix://./socket.soc", "/socket.soc")]
        public void RelativeAgentUrlShouldWarn(string param, string expectedSocket)
        {
            var settingsFromSource = Setup("DD_TRACE_AGENT_URL", param);
            settingsFromSource.TracesTransport.Should().Be(TracesTransportType.UnixDomainSocket);
            settingsFromSource.TracesUnixDomainSocketPath.Should().Be(expectedSocket);
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

            settingsFromSource.TracesTransport.Should().Be(TracesTransportType.UnixDomainSocket);
            settingsFromSource.TracesUnixDomainSocketPath.Should().Be(expectedSocket);
            settingsFromSource.AgentUri.Should().Be(uri);
            CheckDefaultValues(settingsFromSource, "TracesUnixDomainSocketPath", "AgentUri", "TracesTransport");
            settingsFromSource.ValidationWarnings.Should().Contain($"The provided Uri {uri.AbsoluteUri} contains a relative path which may not work. This is the path to the socket that will be used: {expectedSocket}");
            settingsFromSource.ValidationWarnings.Should().Contain($"The socket provided {expectedSocket} cannot be found. The tracer will still rely on this socket to send traces.");
        }
#endif

        [Fact]
        public void AgentHost()
        {
            var param = "SomeHost";
            var settingsFromSource = Setup("DD_AGENT_HOST", param);

            AssertHttpIsConfigured(settingsFromSource, new Uri("http://SomeHost:8126"));
            AssertMetricsUdpIsConfigured(settingsFromSource, "SomeHost");
        }

        [Fact]
        public void AgentPort()
        {
            var param = 9333;
            var settingsFromSource = Setup("DD_TRACE_AGENT_PORT", param.ToString());

            AssertHttpIsConfigured(settingsFromSource, new Uri("http://127.0.0.1:9333"));
            AssertMetricsUdpIsConfigured(settingsFromSource);
        }

        [Fact]
        public void TracesPipeName()
        {
            var param = @"C:\temp\someval";
            var settings = new ExporterSettings() { TracesPipeName = param };
            var settingsFromSource = Setup("DD_TRACE_PIPE_NAME", param);

            AssertPipeIsConfigured(settingsFromSource, param);
            settings.TracesPipeName.Should().Be(param);
            // metrics default to UDP
            AssertMetricsUdpIsConfigured(settingsFromSource);
        }

#if NETCOREAPP3_1_OR_GREATER
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
#else
        [Fact]
        public void MetricsUnixDomainSocketPath_UdsUnsupported_UsesDefaultUdp()
        {
            var param = "/var/path";
            var settings = new ExporterSettings() { MetricsUnixDomainSocketPath = param };

            AssertMetricsUdpIsConfigured(settings);
            // This is actually not working as we don't recompute the transport when setting the property
            // settings.ValidationWarnings.Should().NotBeEmpty().And.ContainMatch("*current runtime doesn't support UDS*");
        }
#endif

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

#if NETCOREAPP3_1_OR_GREATER
        [Fact]
        public void UnixDomainSocketPathWellFormed()
        {
            var settingsFromSource = Setup("DD_TRACE_AGENT_URL", "unix:///var/datadog/myscocket.soc");
            AssertUdsIsConfigured(settingsFromSource, "/var/datadog/myscocket.soc");
            AssertMetricsUdpIsConfigured(settingsFromSource);

            var settings = new ExporterSettings();
            AssertHttpIsConfigured(settings, new Uri("http://127.0.0.1:8126/"));
            AssertMetricsUdpIsConfigured(settingsFromSource);

            settings.AgentUri = new Uri("unix:///var/datadog/myscocket.soc");
            AssertUdsIsConfigured(settings, "/var/datadog/myscocket.soc");
            // Note that this _doesn't_ switch metrics back to UDS
            AssertMetricsUdpIsConfigured(settingsFromSource);
        }

        [Fact]
        public void Traces_SocketFilesExist_NoExplicitConfig_UsesTraceSocket()
        {
            var settings = Setup(DefaultTraceSocketFilesExist());
            AssertUdsIsConfigured(settings, ExporterSettings.DefaultTracesUnixDomainSocket);
            // Uses UDP by default
            AssertMetricsUdpIsConfigured(settings);
        }
#else
        [Fact]
        public void UnixDomainSocketPathWellFormed_UdsUnsupported_UsesDefaultHttp()
        {
            var settingsFromSource = Setup("DD_TRACE_AGENT_URL", "unix:///var/datadog/myscocket.soc");
            var expectedUri = new Uri("http://127.0.0.1:8126/");
            AssertHttpIsConfigured(settingsFromSource, expectedUri);
            AssertMetricsUdpIsConfigured(settingsFromSource);
            settingsFromSource.ValidationWarnings.Should().NotBeEmpty().And.ContainMatch("*current runtime doesn't support UDS*");

            var settings = new ExporterSettings();
            AssertHttpIsConfigured(settings, expectedUri);
            AssertMetricsUdpIsConfigured(settingsFromSource);

            settings.AgentUri = new Uri("unix:///var/datadog/myscocket.soc");
            AssertHttpIsConfigured(settings, expectedUri);
            AssertMetricsUdpIsConfigured(settingsFromSource);
            settings.ValidationWarnings.Should().NotBeEmpty().And.ContainMatch("*current runtime doesn't support UDS*");
        }

        [Fact]
        public void Traces_SocketFilesExist_NoExplicitConfig_UdsUnsupported_UsesDefaultTcp()
        {
            var expectedUri = new Uri($"http://127.0.0.1:8126");
            var settings = Setup(DefaultTraceSocketFilesExist());
            AssertHttpIsConfigured(settings, expectedUri);
            // Uses UDP by default
            AssertMetricsUdpIsConfigured(settings);
        }
#endif

        [Fact]
        public void Traces_SocketFilesExist_ExplicitAgentHost_UsesDefaultTcp()
        {
            var agentHost = "someotherhost";
            var expectedUri = new Uri($"http://{agentHost}:8126");
            var settings = Setup(DefaultSocketFilesExist(), "DD_AGENT_HOST:someotherhost");
            AssertHttpIsConfigured(settings, expectedUri);
            AssertMetricsUdpIsConfigured(settings, "someotherhost");
        }

        [Fact]
        public void Traces_SocketFilesExist_ExplicitTraceAgentPort_UsesDefaultTcp()
        {
            var expectedUri = new Uri($"http://127.0.0.1:8111");
            var settings = Setup(DefaultTraceSocketFilesExist(), "DD_TRACE_AGENT_PORT:8111");
            AssertHttpIsConfigured(settings, expectedUri);
            AssertMetricsUdpIsConfigured(settings);
        }

        [Fact]
        public void Traces_SocketFilesExist_ExplicitWindowsPipeConfig_UsesWindowsNamedPipe()
        {
            var settings = Setup(DefaultTraceSocketFilesExist(), "DD_TRACE_PIPE_NAME:somepipe");
            AssertPipeIsConfigured(settings, "somepipe");
            // Metrics defaults to UDP
            AssertMetricsUdpIsConfigured(settings);
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
            // Metrics defaults to udp
            AssertMetricsUdpIsConfigured(settings);
        }

        [Fact]
        public void Traces_SocketFilesExist_ExplicitConfigForAll_UsesDefaultTcp()
        {
            var settings = Setup(DefaultTraceSocketFilesExist(), "DD_TRACE_AGENT_URL:http://toto:1234", "DD_TRACE_PIPE_NAME:somepipe", "DD_APM_RECEIVER_SOCKET:somesocket");
            AssertHttpIsConfigured(settings, new Uri("http://toto:1234"));
            AssertMetricsUdpIsConfigured(settings, "toto");
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
        public void Metrics_DogStatsdUrl_UdsUnsupported_UsesDefaultUdsOnLinux()
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

        private ExporterSettings Setup(string key, string value)
        {
            return new ExporterSettings(BuildSource(key + ":" + value), NoFile(), NullConfigurationTelemetry.Instance);
        }

        private ExporterSettings Setup(Func<string, bool> fileExistsMock, params string[] config)
        {
            return new ExporterSettings(BuildSource(config), fileExistsMock, NullConfigurationTelemetry.Instance);
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
            settings.TracesTransport.Should().Be(TracesTransportType.Default);
            settings.AgentUri.Should().Be(expectedUri);
            settings.AgentUri.Host.Should().NotBeEquivalentTo("localhost");
            CheckDefaultValues(settings, "AgentUri", "TracesTransport", nameof(settings.MetricsHostname));
        }

        private void AssertMetricsUdpIsConfigured(ExporterSettings settings, string hostname = ExporterSettings.DefaultDogstatsdHostname, int port = ExporterSettings.DefaultDogstatsdPort)
        {
            settings.MetricsTransport.Should().Be(MetricsTransportType.UDP);
            settings.MetricsHostname.Should().Be(hostname);
            settings.DogStatsdPort.Should().Be(port);
            settings.MetricsHostname.Should().NotBeEquivalentTo("localhost");
        }

        private void AssertUdsIsConfigured(ExporterSettings settings, string socketPath)
        {
            settings.TracesTransport.Should().Be(TracesTransportType.UnixDomainSocket);
            settings.TracesUnixDomainSocketPath.Should().Be(socketPath);
            settings.AgentUri.Host.Should().NotBeEquivalentTo("localhost");
            CheckDefaultValues(settings, "TracesUnixDomainSocketPath", "AgentUri", "TracesTransport");
        }

        private void AssertMetricsUdsIsConfigured(ExporterSettings settings, string socketPath)
        {
            settings.MetricsTransport.Should().Be(MetricsTransportType.UDS);
            settings.MetricsUnixDomainSocketPath.Should().Be(socketPath);
            CheckDefaultValues(settings, "MetricsUnixDomainSocketPath", "MetricsTransport", "DogStatsdPort", "AgentUri");
        }

        private void AssertPipeIsConfigured(ExporterSettings settings, string pipeName)
        {
            settings.TracesTransport.Should().Be(TracesTransportType.WindowsNamedPipe);
            settings.TracesPipeName.Should().Be(pipeName);
            settings.AgentUri.Should().NotBeNull();
            settings.AgentUri.Host.Should().NotBeEquivalentTo("localhost");
            CheckDefaultValues(settings, "TracesPipeName", "AgentUri", "TracesTransport", "TracesPipeTimeoutMs");
        }

        private void AssertMetricsPipeIsConfigured(ExporterSettings settings, string pipeName)
        {
            settings.MetricsTransport.Should().Be(MetricsTransportType.NamedPipe);
            settings.MetricsPipeName.Should().Be(pipeName);
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

            if (!paramToIgnore.Contains(nameof(settings.MetricsHostname)))
            {
                settings.MetricsHostname.Should().Be(ExporterSettings.DefaultDogstatsdHostname);
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
