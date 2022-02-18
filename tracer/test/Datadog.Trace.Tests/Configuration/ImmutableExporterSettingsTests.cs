// <copyright file="ImmutableExporterSettingsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;
using MetricsTransportType = Datadog.Trace.Vendors.StatsdClient.Transport.TransportType;

namespace Datadog.Trace.Tests.Configuration
{
    public class ImmutableExporterSettingsTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;

        // These properties are present on ExporterSettings, but not on ImmutableExporterSettings
        private static readonly string[] ExcludedProperties =
        {
            "AgentHost", "AgentPort"
        };

        [Fact]
        public void OnlyHasReadOnlyProperties()
        {
            var type = typeof(ImmutableExporterSettings);

            using var scope = new AssertionScope();

            var properties = type.GetProperties(Flags);
            foreach (var propertyInfo in properties)
            {
                propertyInfo.CanWrite.Should().BeFalse($"{propertyInfo.Name} should be read only");
            }

            var fields = type.GetFields(Flags);
            foreach (var field in fields)
            {
                field.IsInitOnly.Should().BeTrue($"{field.Name} should be read only");
            }
        }

        [Fact]
        public void HasSamePropertiesAsExporterSettings()
        {
            var mutableProperties = typeof(ExporterSettings)
                                   .GetProperties(Flags)
                                   .Select(x => x.Name)
                                   .Where(x => !ExcludedProperties.Contains(x));

            var immutableProperties = typeof(ImmutableExporterSettings)
                                     .GetProperties(Flags)
                                     .Select(x => x.Name);

            immutableProperties.Should().Contain(mutableProperties);
        }

        [Fact]
        public void UnixDomainSocketPathWellFormed()
        {
            var settings = new ExporterSettings { AgentUri = new Uri("unix:///var/datadog/myscocket.soc") };
            var immutableExporterSettings = Setup(FileExistsMock(), settings);

            AssertUdsIsConfigured(immutableExporterSettings, "/var/datadog/myscocket.soc");
        }

        [Fact]
        public void Traces_UrlShouldBeTheDefaultEvenIfEverythingElseIsSet()
        {
            var settings = new ExporterSettings { AgentUri = new Uri("http://thisIsTheOne"), AgentHost = "someotherhost", TracesPipeName = "somepipe", TracesUnixDomainSocketPath = "somesocket" };
            var immutableExporterSettings = Setup(FileExistsMock(), settings);

            AssertHttpIsConfigured(immutableExporterSettings, "http://thisIsTheOne");
        }

        [Fact]
        public void Traces_UrlWithUnixPathShouldBeTheDefaultEvenIfEverythingElseIsSet()
        {
            var settings = new ExporterSettings { AgentUri = new Uri("unix:///thisIsTheOneSocket"), AgentHost = "someotherhost", TracesPipeName = "somepipe", TracesUnixDomainSocketPath = "somesocket" };
            var immutableExporterSettings = Setup(FileExistsMock(), settings);

            AssertUdsIsConfigured(immutableExporterSettings, "/thisIsTheOneSocket");
        }

        [Fact]
        public void Traces_Uds_Have_Precedence_Over_Http()
        {
            var settings = new ExporterSettings { AgentHost = "someotherhost", TracesPipeName = "somepipe", TracesUnixDomainSocketPath = "somesocket" };
            // Should work even if the file isn't present
            var immutableExporterSettings = Setup(NoFile(), settings);
            AssertUdsIsConfigured(immutableExporterSettings, "somesocket");
        }

        [Fact]
        public void Traces_WindowsPipe_Have_Precedence_Over_Http()
        {
            var settings = new ExporterSettings { AgentHost = "someotherhost", TracesPipeName = "somepipe" };
            // Should not even check if a file exists
            var immutableExporterSettings = Setup(settings);
            Assert.Equal(expected: TracesTransportType.WindowsNamedPipe, actual: immutableExporterSettings.TracesTransport);
            Assert.Equal(expected: "somepipe", actual: immutableExporterSettings.TracesPipeName);
            CheckDefaultValues(immutableExporterSettings, "TracesTransport", "TracesPipeName");
        }

        [Fact]
        public void Traces_ExplicitAgentHost_UsesHttp()
        {
            var settings = new ExporterSettings { AgentHost = "someotherhost" };
            var immutableExporterSettings = Setup(settings);

            AssertHttpIsConfigured(immutableExporterSettings, "http://someotherhost:8126");
        }

        [Fact]
        public void Traces_SocketFilesExist_ExplicitTraceAgentPort_UsesDefaultHttp()
        {
            // AgentPort isn't settable directly, so goinng a config source as a user would
            var configNameValues = new NameValueCollection();
            configNameValues.Add("DD_TRACE_AGENT_PORT", "8111");
            var configSource = new NameValueConfigurationSource(configNameValues);
            var immutableExporterSettings = new ImmutableExporterSettings(configSource);

            AssertHttpIsConfigured(immutableExporterSettings, "http://127.0.0.1:8111");
        }

        [Fact]
        public void Traces_SocketFilesExist_NoExplicitConfig_UsesTraceSocket()
        {
            var immutableExporterSettings = Setup(FileExistsMock(ExporterSettings.DefaultTracesUnixDomainSocket), new ExporterSettings());
            AssertUdsIsConfigured(immutableExporterSettings, ExporterSettings.DefaultTracesUnixDomainSocket);
        }

        [Fact]
        public void NoSocketFiles_NoExplicitConfiguration_DefaultsMatchExpectation()
        {
            var immutableExporterSettings = Setup(NoFile(), new ExporterSettings());
            AssertHttpIsConfigured(immutableExporterSettings, "http://127.0.0.1:8126");
        }

        [Fact]
        public void InvalidHost_Fallbacks_On_Uds_If_File()
        {
            var settings = new ExporterSettings { AgentHost = "DD_AGENT_HOST:Invalid=%Host!!" };
            var immutableExporterSettings = Setup(DefaultSocketFilesExist(), settings);

            AssertUdsIsConfigured(immutableExporterSettings, ExporterSettings.DefaultTracesUnixDomainSocket);
            Assert.Equal(expected: new Uri("http://127.0.0.1:8126"), actual: immutableExporterSettings.AgentUri);
        }

        [Fact]
        public void InvalidHost_Fallbacks_On_Default()
        {
            var settings = new ExporterSettings { AgentHost = "DD_AGENT_HOST:Invalid=%Host!!" };
            var immutableExporterSettings = Setup(NoFile(), settings);
            AssertHttpIsConfigured(immutableExporterSettings, "http://127.0.0.1:8126");
        }

        [Fact]
        public void InvalidHost_And_Uds_Throws()
        {
            var settings = new ExporterSettings { TracesUnixDomainSocketPath = "somesocket", AgentHost = "DD_AGENT_HOST:Invalid=%Host!!" };
            Assert.Throws<UriFormatException>(() => Setup(NoFile(), settings));
        }

        [Fact]
        public void InvalidHost_And_Pipe_Throws()
        {
            var settings = new ExporterSettings { TracesPipeName = "somepipe", AgentHost = "DD_AGENT_HOST:Invalid=%Host!!" };
            Assert.Throws<UriFormatException>(() => Setup(NoFile(), settings));
        }

        [Fact]
        public void PartialFlushVariables_Populated()
        {
            var settings = new ExporterSettings { PartialFlushEnabled = true, PartialFlushMinSpans = 999 };
            var immutableExporterSettings = Setup(FileExistsMock(), settings);
            Assert.True(immutableExporterSettings.PartialFlushEnabled);
            Assert.Equal(expected: 999, actual: immutableExporterSettings.PartialFlushMinSpans);
            CheckDefaultValues(immutableExporterSettings, "PartialFlushEnabled", "PartialFlushMinSpans");
        }

        [Fact]
        public void Metrics_SocketFilesExist_NoExplicitConfig_UsesMetricsSocket()
        {
            var immutableExporterSettings = Setup(DefaultSocketFilesExist(), new ExporterSettings());
            Assert.Equal(expected: MetricsTransportType.UDS, actual: immutableExporterSettings.MetricsTransport);
            Assert.Equal(expected: ExporterSettings.DefaultMetricsUnixDomainSocket, actual: immutableExporterSettings.MetricsUnixDomainSocketPath);
            CheckDefaultValues(immutableExporterSettings, "MetricsTransport", "MetricsUnixDomainSocketPath");
        }

        [Fact]
        public void Metrics_SocketFilesExist_ExplicitMetricsPort_UsesUdp()
        {
            var settings = new ExporterSettings { DogStatsdPort = 11125 };
            var immutableExporterSettings = Setup(DefaultSocketFilesExist(), settings);
            Assert.Equal(expected: MetricsTransportType.UDP, actual: immutableExporterSettings.MetricsTransport);
            Assert.Equal(expected: settings.DogStatsdPort, actual: immutableExporterSettings.DogStatsdPort);
            CheckDefaultValues(immutableExporterSettings, "MetricsTransport", "DogStatsdPort");
        }

        [Fact]
        public void Metrics_SocketFilesExist_ExplicitWindowsPipeConfig_UsesWindowsNamedPipe()
        {
            var settings = new ExporterSettings { MetricsPipeName = "somepipe" };
            var immutableExporterSettings = Setup(DefaultSocketFilesExist(), settings);
            Assert.Equal(expected: MetricsTransportType.NamedPipe, actual: immutableExporterSettings.MetricsTransport);
            Assert.Equal(expected: "somepipe", actual: immutableExporterSettings.MetricsPipeName);
            CheckDefaultValues(immutableExporterSettings, "MetricsTransport", "MetricsPipeName");
        }

        [Fact]
        public void Metrics_SocketFilesExist_ExplicitUdsConfig_UsesExplicitConfig()
        {
            var settings = new ExporterSettings { MetricsUnixDomainSocketPath = "somesocket" };
            var immutableExporterSettings = Setup(DefaultSocketFilesExist(), settings);
            Assert.Equal(expected: MetricsTransportType.UDS, actual: immutableExporterSettings.MetricsTransport);
            Assert.Equal(expected: "somesocket", actual: immutableExporterSettings.MetricsUnixDomainSocketPath);
            CheckDefaultValues(immutableExporterSettings, "MetricsTransport", "MetricsUnixDomainSocketPath");
        }

        [Fact]
        public void Metrics_SocketFilesExist_ExplicitConfigForAll_UsesDefaultUdp()
        {
            var settings = new ExporterSettings { AgentHost = "someotherhost", MetricsPipeName = "somepipe", MetricsUnixDomainSocketPath = "somesocket" };
            // Should work even if the file isn't present
            var immutableExporterSettings = Setup(DefaultSocketFilesExist(), settings);
            Assert.Equal(expected: MetricsTransportType.UDP, actual: immutableExporterSettings.MetricsTransport);
            Assert.Equal(expected: new Uri("http://someotherhost:8126/"), actual: immutableExporterSettings.AgentUri);
            CheckDefaultValues(immutableExporterSettings, "MetricsTransport", "AgentUri");
        }

        [Fact]
        public void DefaultValues()
        {
            var settings = new ImmutableExporterSettings(new ExporterSettings());
            CheckDefaultValues(settings);
        }

        private void AssertHttpIsConfigured(ImmutableExporterSettings settings, string expectedUri)
        {
            Assert.Equal(expected: TracesTransportType.Default, actual: settings.TracesTransport);
            Assert.Equal(expected: new Uri(expectedUri), actual: settings.AgentUri);
            Assert.False(string.Equals(settings.AgentUri.Host, "localhost", StringComparison.OrdinalIgnoreCase));
            CheckDefaultValues(settings, "AgentUri", "TracesTransport");
        }

        private void AssertUdsIsConfigured(ImmutableExporterSettings settings, string socketPath)
        {
            Assert.Equal(expected: TracesTransportType.UnixDomainSocket, actual: settings.TracesTransport);
            Assert.Equal(expected: socketPath, actual: settings.TracesUnixDomainSocketPath);
            Assert.NotNull(settings.AgentUri);
            Assert.False(string.Equals(settings.AgentUri.Host, "localhost", StringComparison.OrdinalIgnoreCase));
            CheckDefaultValues(settings, "TracesUnixDomainSocketPath", "AgentUri", "TracesTransport");
        }

        private ImmutableExporterSettings Setup(ExporterSettings settings)
        {
            return Setup(null, settings);
        }

        private ImmutableExporterSettings Setup(Func<string, bool> fileExistsMock, ExporterSettings settings)
        {
            return new ImmutableExporterSettings(settings, fileExistsMock);
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

        private void CheckDefaultValues(ImmutableExporterSettings settings, params string[] paramToIgnore)
        {
            if (!paramToIgnore.Contains("AgentUri"))
            {
                settings.AgentUri.Should().Be("http://127.0.0.1:8126/");
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
                settings.TracesPipeTimeoutMs.Should().Be(0);
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
