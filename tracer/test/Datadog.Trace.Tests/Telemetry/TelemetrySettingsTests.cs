// <copyright file="TelemetrySettingsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Specialized;
using Datadog.Trace.Configuration;
using Datadog.Trace.Telemetry;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Datadog.Trace.Tests.Telemetry
{
    public class TelemetrySettingsTests
    {
        private const string DefaultIntakeUrl = "https://instrumentation-telemetry-intake.datadoghq.com/";

        [Theory]
        [InlineData("https://sometest.com", "https://sometest.com/")]
        [InlineData("https://sometest.com/some-path", "https://sometest.com/some-path/")]
        [InlineData("https://sometest.com/some-path/", "https://sometest.com/some-path/")]
        public void WhenValidUrlIsProvided_AndAgentless_AppendsSlashToPath(string url, string expected)
        {
            var source = new NameValueConfigurationSource(new NameValueCollection
            {
                { ConfigurationKeys.Telemetry.Enabled, "1" },
                { ConfigurationKeys.Telemetry.Uri, url },
                { ConfigurationKeys.ApiKey, "some_key" },
            });

            var settings = TelemetrySettings.FromSource(source);
            settings.Agentless.Should().NotBeNull();
            settings.Agentless.AgentlessUri.Should().Be(expected);
            settings.ConfigurationError.Should().BeNullOrEmpty();
        }

        [Fact]
        public void WhenNoUrlOrApiKeyIsProvided_AgentlessIsNotEnabled()
        {
            var source = new NameValueConfigurationSource(new NameValueCollection
            {
                { ConfigurationKeys.Telemetry.Enabled, "1" }
            });

            var settings = TelemetrySettings.FromSource(source);
            settings.Agentless.Should().BeNull();
            settings.ConfigurationError.Should().BeNullOrEmpty();
        }

        [Fact]
        public void WhenOnlyApiKeyIsProvided_UsesIntakeUrl()
        {
            var source = new NameValueConfigurationSource(new NameValueCollection
            {
                { ConfigurationKeys.Telemetry.Enabled, "1" },
                { ConfigurationKeys.ApiKey, "some_key" },
            });

            var settings = TelemetrySettings.FromSource(source);
            settings.Agentless.Should().NotBeNull();
            settings.Agentless.AgentlessUri.Should().Be(DefaultIntakeUrl);
            settings.ConfigurationError.Should().BeNullOrEmpty();
        }

        [Fact]
        public void WhenApiKeyAndDdSiteIsProvided_UsesDdSiteDomain()
        {
            var domain = "my-domain.net";
            var source = new NameValueConfigurationSource(new NameValueCollection
            {
                { ConfigurationKeys.Telemetry.Enabled, "1" },
                { ConfigurationKeys.ApiKey, "some_key" },
                { ConfigurationKeys.Site, domain },
            });

            var settings = TelemetrySettings.FromSource(source);

            settings.Agentless.Should().NotBeNull();
            settings.Agentless.AgentlessUri.Should().Be($"https://instrumentation-telemetry-intake.{domain}/");
            settings.ConfigurationError.Should().BeNullOrEmpty();
        }

        [Fact]
        public void WhenInvalidUrlApiIsProvided_AndNoApiKey_AgentlessIsNotEnabled()
        {
            var url = "https://sometest::";
            var source = new NameValueConfigurationSource(new NameValueCollection
            {
                { ConfigurationKeys.Telemetry.Enabled, "1" },
                { ConfigurationKeys.Telemetry.Uri, url },
            });

            var settings = TelemetrySettings.FromSource(source);

            settings.Agentless.Should().BeNull();
            settings.ConfigurationError.Should().BeNullOrEmpty();
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
                { ConfigurationKeys.Telemetry.Enabled, "1" },
                { ConfigurationKeys.Telemetry.Uri, url },
                { ConfigurationKeys.ApiKey, "some_key" },
            });

            var settings = TelemetrySettings.FromSource(source);

            settings.Agentless.Should().NotBeNull();
            settings.Agentless.AgentlessUri.Should().Be(DefaultIntakeUrl);
            settings.ConfigurationError.Should().NotBeNullOrEmpty();
        }

        [Theory]
        [InlineData(null, null, false)]
        [InlineData("SOMEKEY", null, true)]
        [InlineData(null, "0", false)]
        [InlineData("SOMEKEY", "0", false)]
        [InlineData(null, "1", true)]
        [InlineData("SOMEKEY", "1", true)]
        public void SetsTelemetryEnabledBasedOnApiKeyAndEnabledSettings(string apiKey, string enabledSetting, bool enabled)
        {
            var source = new NameValueConfigurationSource(new NameValueCollection
            {
                { ConfigurationKeys.Telemetry.Enabled, enabledSetting },
                { ConfigurationKeys.ApiKey, apiKey },
            });

            var settings = TelemetrySettings.FromSource(source);
            if (enabled && !string.IsNullOrEmpty(apiKey))
            {
                settings.Agentless.Should().NotBeNull();
            }
            else
            {
                settings.Agentless.Should().BeNull();
            }

            settings.TelemetryEnabled.Should().Be(enabled);
            settings.ConfigurationError.Should().BeNullOrEmpty();
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData(null, true)]
        [InlineData(null, false)]
        [InlineData("SOMEKEY", true)]
        [InlineData("SOMEKEY", false)]
        public void SetsAgentlessBasedOnApiKey(string apiKey, bool? agentless)
        {
            var source = new NameValueConfigurationSource(new NameValueCollection
            {
                { ConfigurationKeys.Telemetry.AgentlessEnabled, agentless?.ToString() },
                { ConfigurationKeys.ApiKey, apiKey },
            });
            var hasApiKey = !string.IsNullOrEmpty(apiKey);

            var settings = TelemetrySettings.FromSource(source);
            using var s = new AssertionScope();

            if (agentless == true)
            {
                settings.TelemetryEnabled.Should().Be(hasApiKey);
                if (hasApiKey)
                {
                    settings.Agentless.Should().NotBeNull();
                    settings.ConfigurationError.Should().BeNullOrEmpty();
                }
                else
                {
                    settings.Agentless.Should().BeNull();
                    settings.ConfigurationError.Should().NotBeNullOrEmpty();
                }
            }
            else if (agentless == false)
            {
                settings.Agentless.Should().BeNull();
                settings.TelemetryEnabled.Should().Be(false);
                settings.ConfigurationError.Should().BeNullOrEmpty();
            }
            else
            {
                if (hasApiKey)
                {
                    settings.Agentless.Should().NotBeNull();
                }
                else
                {
                    settings.Agentless.Should().BeNull();
                }

                settings.TelemetryEnabled.Should().Be(hasApiKey);
                settings.ConfigurationError.Should().BeNullOrEmpty();
            }
        }
    }
}
