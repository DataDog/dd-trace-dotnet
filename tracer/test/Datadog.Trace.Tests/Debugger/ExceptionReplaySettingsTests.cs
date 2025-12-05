// <copyright file="ExceptionReplaySettingsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Specialized;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Debugger.ExceptionAutoInstrumentation;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Debugger
{
    public class ExceptionReplaySettingsTests
    {
        [Fact]
        public void AgentlessSettings_Defaults()
        {
            var settings = new ExceptionReplaySettings(new NameValueConfigurationSource(new NameValueCollection()), NullConfigurationTelemetry.Instance);

            settings.AgentlessEnabled.Should().BeFalse();
            settings.AgentlessApiKey.Should().BeNull();
            settings.AgentlessUrlOverride.Should().BeNull();
            settings.AgentlessSite.Should().Be("datadoghq.com");
        }

        [Fact]
        public void AgentlessSettings_CustomValues()
        {
            var collection = new NameValueCollection
            {
                { ConfigurationKeys.Debugger.ExceptionReplayAgentlessEnabled, "true" },
                { ConfigurationKeys.Debugger.ExceptionReplayAgentlessUrl, "https://example.com/custom" },
                { ConfigurationKeys.ApiKey, "test-key" },
                { ConfigurationKeys.Site, "datadoghq.eu" }
            };

            var settings = new ExceptionReplaySettings(new NameValueConfigurationSource(collection), NullConfigurationTelemetry.Instance);

            settings.AgentlessEnabled.Should().BeTrue();
            settings.AgentlessApiKey.Should().Be("test-key");
            settings.AgentlessUrlOverride.Should().Be("https://example.com/custom");
            settings.AgentlessSite.Should().Be("datadoghq.eu");
        }
    }
}
