// <copyright file="ManualInstrumentationLegacyConfigurationSourceTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Configuration;

#pragma warning disable CS0618 // Type or member is obsolete
public class ManualInstrumentationLegacyConfigurationSourceTests
{
    public static IEnumerable<object[]> SupportedIds()
        => IntegrationRegistry.Ids.Values
                              .Where(x => x <= (int)IntegrationId.EmailHtmlInjection) // legacy source only supports integrations up to EmailHtmlInjection
                              .Select(x => new object[] { x });

    public static IEnumerable<object[]> UnsupportedIds()
        => IntegrationRegistry.Ids.Values
                              .Where(x => x > (int)IntegrationId.EmailHtmlInjection) // legacy source only supports integrations up to EmailHtmlInjection
                              .Select(x => new object[] { x });

    [Theory]
    [MemberData(nameof(SupportedIds))]
    public void GetIntegrationEnabled_SupportedValues_ReturnsExpectedValues(int id)
    {
        var integrationId = (IntegrationId)id;
        var name = IntegrationRegistry.GetName(integrationId).ToUpperInvariant();
        var enabledKey = string.Format(IntegrationSettings.IntegrationEnabledKey, name);

        var actual = ManualInstrumentationLegacyConfigurationSource.GetIntegrationEnabled(enabledKey);

        actual.Should().Be(integrationId);
    }

    [Theory]
    [MemberData(nameof(SupportedIds))]
    public void GetIntegrationAnalyticsEnabled_SupportedValues_ReturnsExpectedValues(int id)
    {
        var integrationId = (IntegrationId)id;
        var name = IntegrationRegistry.GetName(integrationId).ToUpperInvariant();
        var enabledKey = string.Format(IntegrationSettings.AnalyticsEnabledKey, name);

        var actual = ManualInstrumentationLegacyConfigurationSource.GetIntegrationAnalyticsEnabled(enabledKey);

        actual.Should().Be(integrationId);
    }

    [Theory(Skip = "There are no unsupported IDs yet, and xunit doesn't like that")]
    [MemberData(nameof(UnsupportedIds))]
    public void GetIntegrationEnabled_ForUnsupportedValues_ReturnsNull(int id)
    {
        var integrationId = (IntegrationId)id;
        var name = IntegrationRegistry.GetName(integrationId).ToUpperInvariant();
        var enabledKey = string.Format(IntegrationSettings.IntegrationEnabledKey, name);

        var actual = ManualInstrumentationLegacyConfigurationSource.GetIntegrationEnabled(enabledKey);

        actual.Should().BeNull();
    }

    [Theory(Skip = "There are no unsupported IDs yet, and xunit doesn't like that")]
    [MemberData(nameof(UnsupportedIds))]
    public void GetIntegrationAnalyticsEnabled_ForUnsupportedValues_ReturnsNull(int id)
    {
        var integrationId = (IntegrationId)id;
        var name = IntegrationRegistry.GetName(integrationId).ToUpperInvariant();
        var enabledKey = string.Format(IntegrationSettings.AnalyticsEnabledKey, name);

        var actual = ManualInstrumentationLegacyConfigurationSource.GetIntegrationAnalyticsEnabled(enabledKey);

        actual.Should().BeNull();
    }

    [Theory(Skip = "There are no unsupported IDs yet, and xunit doesn't like that")]
    [MemberData(nameof(UnsupportedIds))]
    public void GetIntegrationAnalyticsSampleRate_ForUnsupportedValues_ReturnsNull(int id)
    {
        var integrationId = (IntegrationId)id;
        var name = IntegrationRegistry.GetName(integrationId).ToUpperInvariant();
        var enabledKey = string.Format(IntegrationSettings.AnalyticsSampleRateKey, name);

        var actual = ManualInstrumentationLegacyConfigurationSource.GetIntegrationAnalyticsSampleRate(enabledKey);

        actual.Should().BeNull();
    }
}
