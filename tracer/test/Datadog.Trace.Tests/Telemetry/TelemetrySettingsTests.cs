// <copyright file="TelemetrySettingsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Specialized;
using Datadog.Trace.Configuration;
using Datadog.Trace.Telemetry;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Telemetry
{
    public class TelemetrySettingsTests
    {
        private readonly ImmutableTracerSettings _tracerSettings;
        private readonly string _defaultTelemetryUrl;

        public TelemetrySettingsTests()
        {
            _tracerSettings = new(new TracerSettings());
            _defaultTelemetryUrl = $"{_tracerSettings.Exporter.AgentUri}{TelemetryConstants.AgentTelemetryEndpoint}";
        }

        [Theory]
        [InlineData("https://sometest.com", "https://sometest.com/")]
        [InlineData("https://sometest.com/some-path", "https://sometest.com/some-path")]
        [InlineData("https://sometest.com/some-path/", "https://sometest.com/some-path/")]
        public void WhenValidUrlIsProvided_AppendsSlashToPath(string url, string expected)
        {
            var source = new NameValueConfigurationSource(new NameValueCollection
            {
                { ConfigurationKeys.Telemetry.Uri, url },
            });

            var settings = new TelemetrySettings(source, _tracerSettings);

            settings.TelemetryUri.Should().Be(expected);
        }

        [Fact]
        public void WhenNoUrlIsProvided_UsesAgentBasedUrl()
        {
            var source = new NameValueConfigurationSource(new NameValueCollection());

            var settings = new TelemetrySettings(source, _tracerSettings);

            settings.TelemetryUri.Should().Be(_defaultTelemetryUrl);
        }

        [Theory]
        [InlineData("https://sometest::")]
        [InlineData("https://sometest:-1/")]
        [InlineData("some-path/")]
        [InlineData("nada")]
        public void WhenInvalidUrlIsProvided_UsesDefaultUrl(string url)
        {
            var source = new NameValueConfigurationSource(new NameValueCollection
            {
                { ConfigurationKeys.Telemetry.Uri, url },
            });

            var settings = new TelemetrySettings(source, _tracerSettings);

            settings.TelemetryUri.Should().Be(_defaultTelemetryUrl);
        }
    }
}
