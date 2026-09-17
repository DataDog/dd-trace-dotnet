// <copyright file="GlobalConfigurationSourceTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.LibDatadog.HandsOffConfiguration;
using FluentAssertions;
using Xunit;
using Result = Datadog.Trace.LibDatadog.HandsOffConfiguration.Result;

namespace Datadog.Trace.Tests.Configuration;

public class GlobalConfigurationSourceTests
{
    [Fact]
    public void TestGlobalConfig()
    {
        // Precedence is as follows:
        // 1. fleet file
        // 2. env var
        // 3. local file

        var result = GlobalConfigurationSource.CreateDefaultConfigurationSource(
            handsOffLocalConfigPath: Path.Combine("Configuration", "HandsOffConfigData", "application_monitoring.yml"),
            handsOffFleetConfigPath: Path.Combine("Configuration", "HandsOffConfigData", "application_monitoring_fleet.yml"),
            isLibdatadogAvailable: true);
        result.Result.Should().Be(Result.Success);
        var sources = result.ConfigurationSource.ToList();
#if NETFRAMEWORK
        sources.Count.Should().Be(4);
        sources[3].Should().BeOfType<NameValueConfigurationSource>();
#else
        sources.Count.Should().Be(3);
#endif
        var fleetConfigSource = sources[0].Should().BeOfType<HandsOffConfigurationSource>().Subject;
        fleetConfigSource.Origin.Should().Be(ConfigurationOrigins.FleetStableConfig);
        fleetConfigSource.GetString("KEY1", NullConfigurationTelemetry.Instance, null, false).Result.Should().Be("fleet_file_env");
        fleetConfigSource.GetString("KEY5", NullConfigurationTelemetry.Instance, null, false).Result.Should().Be("fleet_file_service");
        sources[1].Should().BeOfType<EnvironmentConfigurationSource>();
        var localConfigSource = sources[2].Should().BeOfType<HandsOffConfigurationSource>().Subject;
        localConfigSource.Origin.Should().Be(ConfigurationOrigins.LocalStableConfig);
        // libdatadog already applies precedence so doesnt get these keys already defined by fleet, but we want to account for this in later stages where it should report everything for telemetry
        localConfigSource.GetString("KEY1", NullConfigurationTelemetry.Instance, null, false).IsPresent.Should().BeFalse();
        localConfigSource.GetString("KEY5", NullConfigurationTelemetry.Instance, null, false).IsPresent.Should().BeFalse();
        localConfigSource.GetString("KEY4", NullConfigurationTelemetry.Instance, null, false).Result.Should().Be("true");
        localConfigSource.GetBool("KEY2", NullConfigurationTelemetry.Instance, null).Result.Should().Be(false);
    }

    [Fact]
    public void HandsOffConfigurationEntriesAreExposedForLogging()
    {
        var result = GlobalConfigurationSource.CreateDefaultConfigurationSource(
            handsOffLocalConfigPath: Path.Combine("Configuration", "HandsOffConfigData", "application_monitoring.yml"),
            handsOffFleetConfigPath: Path.Combine("Configuration", "HandsOffConfigData", "application_monitoring_fleet.yml"),
            isLibdatadogAvailable: true);

        result.Result.Should().Be(Result.Success);
        result.HandsOffConfiguration.Should().NotBeNull();
        result.HandsOffConfiguration!.Value.ConfigEntriesFleet.Keys.Should().BeEquivalentTo("KEY1", "KEY5");
        result.HandsOffConfiguration!.Value.ConfigEntriesLocal.Keys.Should().BeEquivalentTo("KEY2", "KEY4");
    }

    [Fact]
    public void HandsOffConfigurationIsNotExposedWhenTheReadFails()
    {
        var result = GlobalConfigurationSource.CreateDefaultConfigurationSource(isLibdatadogAvailable: false);

        result.Result.Should().Be(Result.LibDatadogUnavailable);
        result.HandsOffConfiguration.Should().BeNull();
    }

    [Theory]
    [InlineData(new string[0], "")]
    [InlineData(new[] { "DD_APPSEC_ENABLED" }, "DD_APPSEC_ENABLED")]
    [InlineData(new[] { "DD_TRACE_DEBUG", "DD_APPSEC_ENABLED" }, "DD_APPSEC_ENABLED, DD_TRACE_DEBUG")]
    public void FormatKeysListsKeyNamesInOrder(string[] keys, string expected)
    {
        var entries = keys.ToDictionary(key => key, _ => "some-value");

        ConfigurationSuccessResult.FormatKeys(entries).Should().Be(expected);
    }

    [Fact]
    public void FormatKeysNeverIncludesValues()
    {
        var entries = new Dictionary<string, string> { { "DD_API_KEY", "super-secret" } };

        ConfigurationSuccessResult.FormatKeys(entries).Should().Be("DD_API_KEY").And.NotContain("super-secret");
    }
}
