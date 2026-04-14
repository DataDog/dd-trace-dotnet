// <copyright file="GlobalConfigurationSourceTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using System.Linq;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources;
using Datadog.Trace.Configuration.Telemetry;
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

#if NETFRAMEWORK
    [Fact]
    public void CreateDefaultConfigurationSource_SetsAndClearsAppSettingsLoadingGuard()
    {
        // The guard must be cleared after CreateDefaultConfigurationSource completes,
        // so that CallTarget integrations resume normally after initialization.
        CallTargetInvoker.IsLoadingConfigurationManagerAppSettings.Should().BeFalse(
            "the guard should not be active outside of ConfigurationManager.AppSettings loading");

        // Verify that after creating the configuration source, the guard is cleared
        var result = GlobalConfigurationSource.CreateDefaultConfigurationSource(
            isLibdatadogAvailable: false);

        result.ConfigurationSource.Should().NotBeNull();
        CallTargetInvoker.IsLoadingConfigurationManagerAppSettings.Should().BeFalse(
            "the guard should be cleared after CreateDefaultConfigurationSource completes");
    }

    [Fact]
    public void AppSettingsLoadingGuard_BlocksCallTargetIntegrations()
    {
        // Verify the guard's default state is false (not blocking)
        CallTargetInvoker.IsLoadingConfigurationManagerAppSettings.Should().BeFalse();

        try
        {
            // When the guard is set, BeginMethod should return the default state
            // (i.e., no instrumentation runs) because CanExecuteCallTargetIntegration returns false
            CallTargetInvoker.IsLoadingConfigurationManagerAppSettings = true;

            // BeginMethod should return default (no-op) when the guard is active
            var result = CallTargetInvoker.BeginMethod<ConfigLoadingGuardTestIntegration, object>(new object());
            result.Should().Be(CallTargetState.GetDefault());
        }
        finally
        {
            CallTargetInvoker.IsLoadingConfigurationManagerAppSettings = false;
        }

        CallTargetInvoker.IsLoadingConfigurationManagerAppSettings.Should().BeFalse();
    }

    /// <summary>
    /// Dummy integration type used only for testing the configuration loading guard.
    /// </summary>
    internal class ConfigLoadingGuardTestIntegration
    {
    }
#endif
}
