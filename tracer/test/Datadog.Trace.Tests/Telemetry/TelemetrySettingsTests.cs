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
        private readonly string _defaultAgentUrl;
        private readonly string _defaultIntakeUrl;

        public TelemetrySettingsTests()
        {
            _tracerSettings = new(new TracerSettings());
            _defaultAgentUrl = $"{_tracerSettings.Exporter.AgentUri}{TelemetryConstants.AgentTelemetryEndpoint}";
            _defaultIntakeUrl = "https://instrumentation-telemetry-intake.datadoghq.com/";
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
                { ConfigurationKeys.ApiKey, "some_key" },
            });

            var settings = new TelemetrySettings(source, _tracerSettings);

            settings.TelemetryUri.Should().Be(expected);
        }

        [Fact]
        public void WhenNoUrlOrApiKeyIsProvided_UsesAgentBasedUrl()
        {
            var source = new NameValueConfigurationSource(new NameValueCollection());

            var settings = new TelemetrySettings(source, _tracerSettings);

            settings.TelemetryUri.Should().Be(_defaultAgentUrl);
        }

        [Fact]
        public void WhenOnlyApiKeyIsProvided_UsesIntakeUrl()
        {
            var source = new NameValueConfigurationSource(new NameValueCollection
            {
                { ConfigurationKeys.ApiKey, "some_key" },
            });

            var settings = new TelemetrySettings(source, _tracerSettings);

            settings.TelemetryUri.Should().Be(_defaultIntakeUrl);
        }

        [Fact]
        public void WhenApiKeyAndDdSiteIsProvided_UsesDdSiteDomain()
        {
            var domain = "my-domain.net";
            var source = new NameValueConfigurationSource(new NameValueCollection
            {
                { ConfigurationKeys.ApiKey, "some_key" },
                { ConfigurationKeys.Site, domain },
            });

            var settings = new TelemetrySettings(source, _tracerSettings);

            settings.TelemetryUri.Should().Be($"https://instrumentation-telemetry-intake.{domain}/");
        }

        [Fact]
        public void WhenInvalidUrlApiIsProvided_AndNoApiKey_UsesDefaultAgentUrl()
        {
            var url = "https://sometest::";
            var source = new NameValueConfigurationSource(new NameValueCollection
            {
                { ConfigurationKeys.Telemetry.Uri, url },
            });

            var settings = new TelemetrySettings(source, _tracerSettings);

            settings.TelemetryUri.Should().Be(_defaultAgentUrl);
        }

        [Theory]
        [InlineData("https://sometest::")]
        [InlineData("https://sometest:-1/")]
        [InlineData("some-path/")]
        [InlineData("nada")]
        public void WhenInvalidUrlIsProvided_AndHasApiKey_UsesDefaultIntakeUrl(string url)
        {
            var source = new NameValueConfigurationSource(new NameValueCollection
            {
                { ConfigurationKeys.Telemetry.Uri, url },
                { ConfigurationKeys.ApiKey, "some_key" },
            });

            var settings = new TelemetrySettings(source, _tracerSettings);

            settings.TelemetryUri.Should().Be(_defaultIntakeUrl);
        }
    }
}
