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
        public void WhenInvalidUrlIsProvided_AndNoApiKey_AgentlessIsNotEnabled()
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
        [InlineData(null, null, true)]
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
            var expectAgentless = enabled && !string.IsNullOrEmpty(apiKey);

            if (expectAgentless)
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

            settings.TelemetryEnabled.Should().Be(true);

            if (agentless == true && !hasApiKey)
            {
                settings.ConfigurationError.Should().NotBeNullOrEmpty();
            }
            else
            {
                settings.ConfigurationError.Should().BeNullOrEmpty();
            }

            if (agentless != false && hasApiKey)
            {
                settings.Agentless.Should().NotBeNull();
                settings.Agentless.AgentlessUri.Should().Be(DefaultIntakeUrl);
            }
            else
            {
                settings.Agentless.Should().BeNull();
            }
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData(null, true)]
        [InlineData(null, false)]
        [InlineData(false, null)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        [InlineData(true, null)]
        [InlineData(true, true)]
        [InlineData(true, false)]
        public void SetsAgentlessBasedOnEnabledAndAgentlessEnabled(bool? enabled, bool? agentlessEnabled)
        {
            var source = new NameValueConfigurationSource(new NameValueCollection
            {
                { ConfigurationKeys.Telemetry.Enabled, enabled?.ToString() },
                { ConfigurationKeys.Telemetry.AgentlessEnabled, agentlessEnabled?.ToString() },
                { ConfigurationKeys.ApiKey, agentlessEnabled == true ? "SOME_KEY" : null },
            });

            var settings = TelemetrySettings.FromSource(source);

            var expectEnabled = enabled == true || (enabled is null && agentlessEnabled == true);
            var expectAgentless = expectEnabled && agentlessEnabled == true;

            settings.TelemetryEnabled.Should().Be(expectEnabled);
            if (expectAgentless)
            {
                settings.Agentless.Should().NotBeNull();
            }
            else
            {
                settings.Agentless.Should().BeNull();
            }
        }
    }
}
