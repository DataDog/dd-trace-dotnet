// <copyright file="RemoteConfigurationSettingsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Configuration
{
    public class RemoteConfigurationSettingsTests : SettingsTestsBase
    {
        [Theory]
        [InlineData(null, null, RemoteConfigurationSettings.DefaultPollIntervalSeconds)]
        [InlineData("", null, RemoteConfigurationSettings.DefaultPollIntervalSeconds)]
        [InlineData("invalid", null, RemoteConfigurationSettings.DefaultPollIntervalSeconds)]
        [InlineData("0.5", "100", 0.5)]
        [InlineData(null, "2", 2)]
        [InlineData("invalid", "0.5", 0.5)]
        [InlineData("0", "1", RemoteConfigurationSettings.DefaultPollIntervalSeconds)]
        [InlineData("-1", "1", RemoteConfigurationSettings.DefaultPollIntervalSeconds)]
        [InlineData("5", "1", 5)]
        [InlineData("5.1", "1", RemoteConfigurationSettings.DefaultPollIntervalSeconds)]
        public void PollInterval(string value, string fallbackValue, double expected)
        {
#pragma warning disable CS0618
            var source = CreateConfigurationSource(
                (ConfigurationKeys.Rcm.PollInterval, value),
                ("DD_INTERNAL_RCM_POLL_INTERVAL", fallbackValue));
#pragma warning restore CS0618

            var settings = new RemoteConfigurationSettings(source, NullConfigurationTelemetry.Instance);

            settings.PollInterval.Should().Be(TimeSpan.FromSeconds(expected));
        }

        [Theory]
        [InlineData(null, true)]
        [InlineData("", true)]
        [InlineData("invalid", true)]
        [InlineData("true", true)]
        [InlineData("false", false)]
        [InlineData("0", false)]
        public void NativeClientEnabled(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.Rcm.RemoteConfigurationNativeClientEnabled, value));

            var settings = new RemoteConfigurationSettings(source, NullConfigurationTelemetry.Instance);

            settings.NativeClientEnabled.Should().Be(expected);
        }
    }
}
