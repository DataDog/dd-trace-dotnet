// <copyright file="DebuggerSettingsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Configuration;
using Datadog.Trace.Debugger;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Debugger
{
    public class DebuggerSettingsTests
    {
        [Fact]
        public void WhenFilePathProvided_UseFileMode()
        {
            var expected = "c:/temp";
            var tracerSettings = new TracerSettings(new NameValueConfigurationSource(new()
            {
                { ConfigurationKeys.Debugger.ProbeFile, expected },
            }));

            tracerSettings.DebuggerSettings.ProbeMode.Should().Be(ProbeMode.File);
            tracerSettings.DebuggerSettings.ProbeConfigurationsPath.Should().Be(expected);
        }

        [Fact]
        public void DefaultMode_BackendMode()
        {
            var tracerSettings = new TracerSettings();

            tracerSettings.DebuggerSettings.ProbeMode.Should().Be(ProbeMode.Backend);
            tracerSettings.DebuggerSettings.ProbeConfigurationsPath.Should().Be("http://datadoghq.com");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("http://localhost:8126")]
        public void WhenAgentModeEnabled_UseAgentMode(string agentUri)
        {
            var tracerSettings = new TracerSettings(new NameValueConfigurationSource(new()
            {
                { ConfigurationKeys.Debugger.AgentMode, "true" },
                { ConfigurationKeys.AgentUri, agentUri },
            }));

            tracerSettings.DebuggerSettings.ProbeMode.Should().Be(ProbeMode.Agent);
            tracerSettings.DebuggerSettings.ProbeConfigurationsPath.Should().Be("http://localhost:8126");
        }

        [Theory]
        [InlineData("-1")]
        [InlineData("0")]
        [InlineData("")]
        [InlineData(null)]
        public void InvalidPollInterval_DefaultUsed(string value)
        {
            var tracerSettings = new TracerSettings(new NameValueConfigurationSource(new()
            {
                { ConfigurationKeys.Debugger.PollInterval, value },
            }));

            tracerSettings.DebuggerSettings.ProbeConfigurationsPollIntervalSeconds.Should().Be(1);
        }

        [Theory]
        [InlineData("-1")]
        [InlineData("0")]
        [InlineData("")]
        [InlineData(null)]
        public void InvalidMaxDepthToSerialize_DefaultUsed(string value)
        {
            var tracerSettings = new TracerSettings(new NameValueConfigurationSource(new()
            {
                { ConfigurationKeys.Debugger.MaxDepthToSerialize, value },
            }));

            tracerSettings.DebuggerSettings.MaxDepthToSerialize.Should().Be(3);
        }

        [Theory]
        [InlineData("-1")]
        [InlineData("0")]
        [InlineData("")]
        [InlineData(null)]
        public void InvalidSerializationTimeThreshold_DefaultUsed(string value)
        {
            var tracerSettings = new TracerSettings(new NameValueConfigurationSource(new()
            {
                { ConfigurationKeys.Debugger.SerializationTimeThreshold, value },
            }));

            tracerSettings.DebuggerSettings.SerializationTimeThreshold.Should().Be(150);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("false")]
        public void DebuggerDisabled(string enabled)
        {
            var tracerSettings = new TracerSettings(new NameValueConfigurationSource(new()
            {
                { ConfigurationKeys.Debugger.DebuggerEnabled, enabled },
            }));

            tracerSettings.DebuggerSettings.Enabled.Should().BeFalse();
        }

        [Fact]
        public void DebuggerSettings_UseSettings()
        {
            var tracerSettings = new TracerSettings(new NameValueConfigurationSource(new()
            {
                { ConfigurationKeys.Debugger.DebuggerEnabled, "true" },
                { ConfigurationKeys.Debugger.PollInterval, "10" },
                { ConfigurationKeys.Debugger.MaxDepthToSerialize, "100" },
                { ConfigurationKeys.Debugger.SerializationTimeThreshold, "1000" },
            }));

            tracerSettings.DebuggerSettings.Enabled.Should().BeTrue();
            tracerSettings.DebuggerSettings.ProbeConfigurationsPollIntervalSeconds.Should().Be(10);
            tracerSettings.DebuggerSettings.MaxDepthToSerialize.Should().Be(100);
            tracerSettings.DebuggerSettings.SerializationTimeThreshold.Should().Be(1000);
        }
    }
}
