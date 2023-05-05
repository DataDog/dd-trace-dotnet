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
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Configuration
{
    public class RemoteConfigurationSettingsTests : SettingsTestsBase
    {
        [Fact]
        public void RuntimeId()
        {
            var source = CreateConfigurationSource();
            var settings = new RemoteConfigurationSettings(source);

            settings.RuntimeId.Should().Be(Datadog.Trace.Util.RuntimeId.Get());
        }

        [Fact]
        public void TracerVersion()
        {
            var source = CreateConfigurationSource();
            var settings = new RemoteConfigurationSettings(source);

            settings.TracerVersion.Should().Be(TracerConstants.ThreePartVersion);
        }

        [Theory]
        [InlineData(null, null, RemoteConfigurationSettings.DefaultPollIntervalMilliseconds)]
        [InlineData("", null, RemoteConfigurationSettings.DefaultPollIntervalMilliseconds)]
        [InlineData("invalid", null, RemoteConfigurationSettings.DefaultPollIntervalMilliseconds)]
        [InlineData("50", "100", 50)]
        [InlineData(null, "100", 100)]
        [InlineData("invalid", "100", 100)]
        [InlineData("0", "100", RemoteConfigurationSettings.DefaultPollIntervalMilliseconds)]
        [InlineData("-1", "100", RemoteConfigurationSettings.DefaultPollIntervalMilliseconds)]
        [InlineData("5000", "100", 5000)]
        [InlineData("5001", "100", RemoteConfigurationSettings.DefaultPollIntervalMilliseconds)]
        public void PollInterval(string value, string fallbackValue, int expected)
        {
#pragma warning disable CS0618
            var source = CreateConfigurationSource(
                (ConfigurationKeys.Rcm.PollInterval, value),
                (ConfigurationKeys.Rcm.PollIntervalInternal, fallbackValue));
#pragma warning restore CS0618

            var settings = new RemoteConfigurationSettings(source);

            settings.PollInterval.Should().Be(TimeSpan.FromMilliseconds(expected));
        }
    }
}
