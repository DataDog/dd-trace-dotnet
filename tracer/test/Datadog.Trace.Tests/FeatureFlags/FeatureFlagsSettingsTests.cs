// <copyright file="FeatureFlagsSettingsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Specialized;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.FeatureFlags;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.FeatureFlags;

public class FeatureFlagsSettingsTests
{
    // The source-selection contract is shared across tracers, so these cases mirror the
    // system-tests parametric suite (tests/parametric/test_ffe/test_configuration_sources.py).
    [Theory]
    // Nothing configured: agentless is the default.
    [InlineData(null, null, null, FeatureFlagsSource.Agentless)]
    // The stable kill switch wins over everything, including a legacy opt-in and an explicit source.
    [InlineData("false", null, null, FeatureFlagsSource.Disabled)]
    [InlineData("false", null, "true", FeatureFlagsSource.Disabled)]
    [InlineData("false", "agentless", null, FeatureFlagsSource.Disabled)]
    [InlineData("false", "remote_config", null, FeatureFlagsSource.Disabled)]
    // Enabling explicitly does not imply the historical Remote Configuration source.
    [InlineData("true", null, null, FeatureFlagsSource.Agentless)]
    // An explicit source wins over the legacy key, in both directions.
    [InlineData(null, "agentless", "true", FeatureFlagsSource.Agentless)]
    [InlineData(null, "remote_config", "false", FeatureFlagsSource.RemoteConfig)]
    // The legacy key grandfathers existing adopters, who opted in when RC was the only source.
    [InlineData(null, null, "true", FeatureFlagsSource.RemoteConfig)]
    [InlineData(null, null, "false", FeatureFlagsSource.Disabled)]
    // An explicit new-key value takes precedence over the legacy key, so a stale legacy disable
    // does not silently keep Feature Flags off during migration.
    [InlineData("true", null, "false", FeatureFlagsSource.Agentless)]
    [InlineData("true", null, "true", FeatureFlagsSource.Agentless)]
    // An unrecognised source fails closed rather than guessing a billed delivery path.
    [InlineData(null, "invalid", null, FeatureFlagsSource.Disabled)]
    [InlineData(null, "invalid", "true", FeatureFlagsSource.Disabled)]
    // "offline" is a reserved, recognised fail-closed sentinel (not an unrecognised value).
    [InlineData(null, "offline", null, FeatureFlagsSource.Disabled)]
    [InlineData(null, "offline", "true", FeatureFlagsSource.Disabled)]
    public void ResolvesSource(string? enabled, string? source, string? legacyEnabled, object expected)
    {
        var expectedSource = (FeatureFlagsSource)expected;
        var settings = CreateSettings(enabled, source, legacyEnabled);

        settings.Source.Should().Be(expectedSource);
        settings.Enabled.Should().Be(expectedSource != FeatureFlagsSource.Disabled);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TreatsBlankSourceAsUnset(string source)
    {
        CreateSettings(enabled: null, source: source, legacyEnabled: null)
           .Source.Should().Be(FeatureFlagsSource.Agentless);

        // Being semantically unset, a blank source still lets the legacy key grandfather RC.
        CreateSettings(enabled: null, source: source, legacyEnabled: "true")
           .Source.Should().Be(FeatureFlagsSource.RemoteConfig);
    }

    [Theory]
    [InlineData("AGENTLESS", FeatureFlagsSource.Agentless)]
    [InlineData("  Remote_Config  ", FeatureFlagsSource.RemoteConfig)]
    public void NormalizesSourceCasingAndWhitespace(string source, object expected)
        => CreateSettings(enabled: null, source: source, legacyEnabled: null).Source.Should().Be((FeatureFlagsSource)expected);

    [Fact]
    public void UsesDocumentedDefaults()
    {
        var settings = CreateSettings(null, null, null);

        settings.PollInterval.Should().Be(TimeSpan.FromSeconds(30));
        settings.RequestTimeout.Should().Be(TimeSpan.FromSeconds(5));
        settings.InitializationTimeout.Should().Be(TimeSpan.FromMilliseconds(10_000));
        settings.AgentlessBaseUrl.Should().BeNull();
    }

    [Theory]
    [InlineData("60", 60)]
    [InlineData("3600", 3600)]
    // Out of range values are rejected in favour of the default: a non-positive interval would
    // turn polling into a tight loop, and an implausibly large one is a misconfiguration.
    [InlineData("0", 30)]
    [InlineData("-1", 30)]
    [InlineData("3601", 30)]
    [InlineData("not-a-number", 30)]
    public void ReadsPollInterval(string configured, int expectedSeconds)
    {
        var settings = CreateSettings(
            null,
            null,
            null,
            (ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSourceAgentlessPollIntervalSeconds, configured));

        settings.PollInterval.Should().Be(TimeSpan.FromSeconds(expectedSeconds));
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("0", 5)]
    [InlineData("-2", 5)]
    public void ReadsRequestTimeout(string configured, int expectedSeconds)
    {
        var settings = CreateSettings(
            null,
            null,
            null,
            (ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSourceAgentlessRequestTimeoutSeconds, configured));

        settings.RequestTimeout.Should().Be(TimeSpan.FromSeconds(expectedSeconds));
    }

    [Theory]
    [InlineData("1000", 1000)]
    [InlineData("0", 10_000)]
    [InlineData("-1", 10_000)]
    public void ReadsInitializationTimeout(string configured, int expectedMs)
    {
        var settings = CreateSettings(
            null,
            null,
            null,
            (ConfigurationKeys.FeatureFlags.FlaggingProviderInitializationTimeoutMs, configured));

        settings.InitializationTimeout.Should().Be(TimeSpan.FromMilliseconds(expectedMs));
    }

    [Theory]
    [InlineData("https://flags.example.com/ufc", "https://flags.example.com/ufc")]
    [InlineData("", null)]
    [InlineData("   ", null)]
    public void ReadsAgentlessBaseUrl(string configured, string? expected)
    {
        var settings = CreateSettings(
            null,
            null,
            null,
            (ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSourceAgentlessBaseUrl, configured));

        settings.AgentlessBaseUrl.Should().Be(expected);
    }

    [Fact]
    public void ReadsSiteAndApiKeyFromTheConfigurationSource()
    {
        var settings = CreateSettings(
            null,
            null,
            null,
            (ConfigurationKeys.Site, "datadoghq.eu"),
            (ConfigurationKeys.ApiKey, "an-api-key"));

        settings.Site.Should().Be("datadoghq.eu");
        settings.ApiKey.Should().Be("an-api-key");
    }

    [Theory]
    // A blank site is rejected in favour of the default, so the managed endpoint stays resolvable.
    [InlineData("")]
    [InlineData("   ")]
    public void FallsBackToTheDefaultSite(string configured)
        => CreateSettings(null, null, null, (ConfigurationKeys.Site, configured))
          .Site.Should().Be(FeatureFlagsSettings.DefaultSite);

    private static FeatureFlagsSettings CreateSettings(
        string? enabled,
        string? source,
        string? legacyEnabled,
        params (string Key, string Value)[] extra)
    {
        var collection = new NameValueCollection();

        if (enabled is not null)
        {
            collection[ConfigurationKeys.FeatureFlags.FeatureFlagsEnabled] = enabled;
        }

        if (source is not null)
        {
            collection[ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSource] = source;
        }

        if (legacyEnabled is not null)
        {
#pragma warning disable 618 // superseded, but still honoured for existing adopters
            collection[ConfigurationKeys.FeatureFlags.FlaggingProviderEnabled] = legacyEnabled;
#pragma warning restore 618
        }

        foreach (var (key, value) in extra)
        {
            collection[key] = value;
        }

        return new FeatureFlagsSettings(new NameValueConfigurationSource(collection), NullConfigurationTelemetry.Instance, env: null);
    }
}
