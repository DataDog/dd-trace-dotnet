// <copyright file="ImmutableIntegrationSettingsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Configuration;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Configuration
{
    public class ImmutableIntegrationSettingsTests
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ImmutableSettingsRespectsOverride(bool initiallyEnabled)
        {
            var name = nameof(IntegrationId.Kafka);
            var settings = new IntegrationSettings(name, source: null)
            {
                Enabled = initiallyEnabled
            };

            var immutableSettings = new ImmutableIntegrationSettings(settings, isExplicitlyDisabled: true);

            immutableSettings.Enabled.Should().BeFalse();
            immutableSettings.ToString().Should<string>().Be($"Kafka - Disabled - Analytics: Disabled");
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ImmutableSettingsRespectsOriginalIfNotOverridden(bool initiallyEnabled)
        {
            var name = nameof(IntegrationId.Kafka);
            var settings = new IntegrationSettings(name, source: null)
            {
                Enabled = initiallyEnabled
            };

            var immutableSettings = new ImmutableIntegrationSettings(settings, isExplicitlyDisabled: false);

            immutableSettings.Enabled.Should().Be(initiallyEnabled);
            immutableSettings.ToString().Should<string>().Be($"Kafka - {(initiallyEnabled ? "Enabled" : "Disabled")} - Analytics: Disabled");
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ImmutableSettingsRespectsOriginalAnalyticsIfNotOverridden(bool initiallyEnabled)
        {
            var name = nameof(IntegrationId.Kafka);
            var settings = new IntegrationSettings(name, source: null)
            {
                Enabled = true,
                AnalyticsEnabled = initiallyEnabled
            };

            var immutableSettings = new ImmutableIntegrationSettings(settings, isExplicitlyDisabled: false);

            immutableSettings.Enabled.Should().Be(true);
            immutableSettings.AnalyticsEnabled.Should().Be(initiallyEnabled);
            immutableSettings.ToString().Should<string>().Be($"Kafka - Enabled - Analytics: {(initiallyEnabled ? $"Sample Rate 0" : "Disabled")}");
        }
    }
}
