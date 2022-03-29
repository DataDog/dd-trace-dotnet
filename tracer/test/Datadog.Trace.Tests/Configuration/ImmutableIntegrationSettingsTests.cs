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
        }
    }
}
