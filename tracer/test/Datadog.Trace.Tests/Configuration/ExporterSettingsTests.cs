// <copyright file="ExporterSettingsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Specialized;
using Datadog.Trace.Configuration;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Configuration
{
    public class ExporterSettingsTests
    {
        [Fact]
        public void DefaultValues()
        {
            var configSource = new NameValueConfigurationSource(new NameValueCollection());
            var settingsFromSource = new ExporterSettings(configSource);
            var settings = new ExporterSettings();

            CheckDefaultValues(settings);
            CheckDefaultValues(settingsFromSource);
        }

        [Fact]
        public void AgentUri()
        {
            var param = "http://someUrl";
            var uri = new Uri(param);
            var settings = new ExporterSettings() { AgentUri = uri };
            var settingsFromSource = Setup("DD_TRACE_AGENT_URL", param);

            settings.AgentUri.Should().Be(uri);
            settingsFromSource.AgentUri.Should().Be(uri);

            CheckDefaultValues(settings, "AgentUri");
            CheckDefaultValues(settingsFromSource, "AgentUri");
        }

        [Fact]
        public void InvalidUrl_Not_Taken_Into_Account()
        {
            var settings = Setup("DD_TRACE_AGENT_URL", "http://Invalid=%Url!!");
            settings.AgentUri.Should().BeNull();
        }

        [Fact]
        public void AgentHost()
        {
            var param = "SomeHost";
            var settingsFromSource = Setup("DD_AGENT_HOST", param);

            settingsFromSource.AgentHost.Should().Be(param);

            CheckDefaultValues(settingsFromSource, "AgentHost");
        }

        [Fact]
        public void AgentPort()
        {
            var param = 9333;
            var settingsFromSource = Setup("DD_TRACE_AGENT_PORT", param.ToString());

            settingsFromSource.AgentPort.Should().Be(param);

            CheckDefaultValues(settingsFromSource, "AgentPort");
        }

        [Fact]
        public void TracesPipeName()
        {
            var param = "/var/path";
            var settings = new ExporterSettings() { TracesPipeName = param };
            var settingsFromSource = Setup("DD_TRACE_PIPE_NAME", param);

            settings.TracesPipeName.Should().Be(param);
            settingsFromSource.TracesPipeName.Should().Be(param);

            CheckDefaultValues(settings, "TracesPipeName");
            CheckDefaultValues(settingsFromSource, "TracesPipeName");
        }

        [Fact]
        public void MetricsUnixDomainSocketPath()
        {
            var param = "/var/path";
            var settings = new ExporterSettings() { MetricsUnixDomainSocketPath = param };
            var settingsFromSource = Setup("DD_DOGSTATSD_SOCKET", param);

            settings.MetricsUnixDomainSocketPath.Should().Be(param);
            settingsFromSource.MetricsUnixDomainSocketPath.Should().Be(param);

            CheckDefaultValues(settings, "MetricsUnixDomainSocketPath");
            CheckDefaultValues(settingsFromSource, "MetricsUnixDomainSocketPath");
        }

        [Fact]
        public void MetricsPipeName()
        {
            var param = "/var/path";
            var settings = new ExporterSettings() { MetricsPipeName = param };
            var settingsFromSource = Setup("DD_DOGSTATSD_PIPE_NAME", param);

            settings.MetricsPipeName.Should().Be(param);
            settingsFromSource.MetricsPipeName.Should().Be(param);

            CheckDefaultValues(settings, "MetricsPipeName");
            CheckDefaultValues(settingsFromSource, "MetricsPipeName");
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
        public void PartialFlushMinSpansIssue()
        {
            var param = -200;
            var settingsFromSource = Setup("DD_TRACE_PARTIAL_FLUSH_MIN_SPANS", param.ToString());
            settingsFromSource.AgentPort.Should().BeNull();
            Assert.Throws<ArgumentException>(() => new ExporterSettings() { PartialFlushMinSpans = param });
        }

        private void CheckDefaultValues(ExporterSettings settings, string paramToIgnore = null)
        {
            if (paramToIgnore != "AgentUri")
            {
                settings.AgentUri.Should().BeNull();
            }

            if (paramToIgnore != "AgentHost")
            {
                settings.AgentHost.Should().BeNull();
            }

            if (paramToIgnore != "AgentPort")
            {
                settings.AgentPort.Should().BeNull();
            }

            if (paramToIgnore != "TracesPipeName")
            {
                settings.TracesPipeName.Should().BeNull();
            }

            if (paramToIgnore != "TracesPipeTimeoutMs")
            {
                settings.TracesPipeTimeoutMs.Should().Be(0);
            }

            if (paramToIgnore != "MetricsPipeName")
            {
                settings.MetricsPipeName.Should().BeNull();
            }

            if (paramToIgnore != "MetricsUnixDomainSocketPath")
            {
                settings.MetricsUnixDomainSocketPath.Should().BeNull();
            }

            if (paramToIgnore != "DogStatsdPort")
            {
                settings.DogStatsdPort.Should().Be(0);
            }

            if (paramToIgnore != "PartialFlushEnabled")
            {
                settings.PartialFlushEnabled.Should().BeFalse();
            }

            if (paramToIgnore != "PartialFlushMinSpans")
            {
                settings.PartialFlushMinSpans.Should().Be(0);
            }
        }

        private ExporterSettings Setup(string key, string value)
        {
            var configNameValues = new NameValueCollection();
            configNameValues.Add(key, value);
            var configSource = new NameValueConfigurationSource(configNameValues);
            return new ExporterSettings(configSource);
        }
    }
}
