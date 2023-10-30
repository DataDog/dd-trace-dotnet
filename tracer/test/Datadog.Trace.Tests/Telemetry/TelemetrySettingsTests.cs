// <copyright file="TelemetrySettingsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Specialized;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Telemetry;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Datadog.Trace.Tests.Telemetry
{
    public class TelemetrySettingsTests : SettingsTestsBase
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

            var settings = TelemetrySettings.FromSource(source, telemetry: new NullConfigurationTelemetry(), isAgentAvailable: true, isServerless: false);
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

            var settings = TelemetrySettings.FromSource(source, telemetry: new NullConfigurationTelemetry(), isAgentAvailable: true, isServerless: false);
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

            var settings = TelemetrySettings.FromSource(source, telemetry: new NullConfigurationTelemetry(), isAgentAvailable: true, isServerless: false);
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

            var settings = TelemetrySettings.FromSource(source, telemetry: new NullConfigurationTelemetry(), isAgentAvailable: true, isServerless: false);

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

            var settings = TelemetrySettings.FromSource(source, telemetry: new NullConfigurationTelemetry(), isAgentAvailable: true, isServerless: false);

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

            var settings = TelemetrySettings.FromSource(source, telemetry: new NullConfigurationTelemetry(), isAgentAvailable: true, isServerless: false);

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

            var settings = TelemetrySettings.FromSource(source, telemetry: new NullConfigurationTelemetry(), isAgentAvailable: true, isServerless: false);
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

            var settings = TelemetrySettings.FromSource(source, telemetry: new NullConfigurationTelemetry(), isAgentAvailable: true, isServerless: false);
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

            var settings = TelemetrySettings.FromSource(source, telemetry: new NullConfigurationTelemetry(), isAgentAvailable: true, isServerless: false);

            var expectEnabled = enabled != false;
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

        [Theory]
        [InlineData(null, null, true)]
        [InlineData(null, true, true)]
        [InlineData(null, false, false)]
        [InlineData(true, null, true)]
        [InlineData(true, true, true)]
        [InlineData(true, false, true)]
        [InlineData(false, null, false)]
        [InlineData(false, true, false)]
        [InlineData(false, false, false)]
        public void SetsAgentProxyEnabledBasedOnConfigAndDelegate(bool? agentProxyEnabled, bool? agentAvailable, bool expected)
        {
            var source = new NameValueConfigurationSource(new NameValueCollection
            {
                { ConfigurationKeys.Telemetry.AgentProxyEnabled, agentProxyEnabled?.ToString() }
            });

            var settings = TelemetrySettings.FromSource(source, telemetry: new NullConfigurationTelemetry(), isAgentAvailable: agentAvailable, isServerless: false);

            settings.AgentProxyEnabled.Should().Be(expected);
        }

        [Theory]
        [InlineData(true, true, true)]
        [InlineData(true, false, true)]
        [InlineData(false, true, true)]
        [InlineData(false, false, false)]
        public void SetsTelemetryEnabledBasedOnAgentlessEnabledAndAgentProxyEnabled(bool agentProxyEnabled, bool agentlessEnabled, bool expected)
        {
            var source = new NameValueConfigurationSource(new NameValueCollection
            {
                { ConfigurationKeys.Telemetry.AgentlessEnabled, agentlessEnabled.ToString() },
                { ConfigurationKeys.Telemetry.AgentProxyEnabled, agentProxyEnabled.ToString() },
                { ConfigurationKeys.ApiKey, "SOME_KEY" },
            });

            var settings = TelemetrySettings.FromSource(source, telemetry: new NullConfigurationTelemetry(), isAgentAvailable: true, isServerless: false);

            using var s = new AssertionScope();
            settings.TelemetryEnabled.Should().Be(expected);
            settings.AgentProxyEnabled.Should().Be(agentProxyEnabled);
            if (agentlessEnabled)
            {
                settings.Agentless.Should().NotBeNull();
            }
            else
            {
                settings.Agentless.Should().BeNull();
            }
        }

        [Theory]
        [InlineData("0", 60)]
        [InlineData(null, 60)]
        [InlineData("", 60)]
        [InlineData("invalid", 60)]
        [InlineData("523.5", 523.5)]
        [InlineData("3600", 3600)]
        [InlineData("3601", 60)]
        public void HeartbeatInterval(string value, double expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.Telemetry.HeartbeatIntervalSeconds, value));
            var settings = TelemetrySettings.FromSource(source, NullConfigurationTelemetry.Instance, isAgentAvailable: true, isServerless: false);

            settings.HeartbeatInterval.Should().Be(TimeSpan.FromSeconds(expected));
        }

        [Theory]
        [InlineData("0", false)]
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData("1", false)]
        public void V2Metrics_DisabledInServerless(string metricsEnabled, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.Telemetry.MetricsEnabled, metricsEnabled));
            var settings = TelemetrySettings.FromSource(source, NullConfigurationTelemetry.Instance, isAgentAvailable: true, isServerless: true);

            settings.MetricsEnabled.Should().Be(expected);
        }

        [Theory]
        [InlineData("0", false)]
        [InlineData(null, true)] // enabled by default if v2 enabled
        [InlineData("",  true)]
        [InlineData("1", true)]
        public void MetricsEnabled_HasCorrectDefaultValue(string metricSettingValue, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.Telemetry.MetricsEnabled, metricSettingValue));
            var settings = TelemetrySettings.FromSource(source, NullConfigurationTelemetry.Instance, isAgentAvailable: true, isServerless: false);

            settings.MetricsEnabled.Should().Be(expected);
            settings.ConfigurationError.Should().BeNullOrEmpty();
        }
    }
}
