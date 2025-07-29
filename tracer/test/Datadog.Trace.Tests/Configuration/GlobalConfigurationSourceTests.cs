// <copyright file="GlobalConfigurationSourceTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using FluentAssertions;
using Xunit;

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

        // Environment: defined in fleet file: this should be ignored
        Environment.SetEnvironmentVariable(ConfigurationKeys.Environment, "local_env_var", EnvironmentVariableTarget.Process);

        // DebugEnabled: defined false in local file: this should be applied over the local file
        Environment.SetEnvironmentVariable(ConfigurationKeys.DebugEnabled, "true", EnvironmentVariableTarget.Process);

        // ApmTracingEnabled: only defined in env var, should prevail
        Environment.SetEnvironmentVariable(ConfigurationKeys.ApmTracingEnabled, "false", EnvironmentVariableTarget.Process);
        var result = GlobalConfigurationSource.CreateDefaultConfigurationSource(
            handsOffLocalConfigPath: Path.Combine("Configuration", "HandsOffConfigData", "application_monitoring.yml"),
            handsOffFleetConfigPath: Path.Combine("Configuration", "HandsOffConfigData", "application_monitoring_fleet.yml"),
            isLibdatadogAvailable: true);

        var source = result.ConfigurationSource;
        // env var
        var apmTracingEnabled = source.GetBool(ConfigurationKeys.ApmTracingEnabled, new NullConfigurationTelemetry(), null);
        apmTracingEnabled.IsPresent.Should().BeTrue();
        apmTracingEnabled.Result.Should().Be(false);

        // fleet file
        var environment = source.GetString(ConfigurationKeys.Environment, new NullConfigurationTelemetry(), null, false);
        environment.IsPresent.Should().BeTrue();
        environment.Result.Should().Be("fleet_file_env");

        // local file
        var dataStreamEnabled = source.GetBool(ConfigurationKeys.DataStreamsMonitoring.Enabled, new NullConfigurationTelemetry(), null);
        dataStreamEnabled.IsPresent.Should().BeTrue();
        dataStreamEnabled.Result.Should().Be(true);

        // fleet file
        var service = source.GetString(ConfigurationKeys.ServiceName, new NullConfigurationTelemetry(), null, false);
        service.IsPresent.Should().BeTrue();
        service.Result.Should().Be("fleet_file_service");
    }

    [Fact]
    public void TestErrorHandsOffConfigFile()
    {
        var handsOffErrorPath = Path.Combine("Configuration", "HandsOffConfigData", "corrupt_file.yml");
        var result = LibDatadog.HandsOffConfiguration.ConfiguratorHelper.GetConfiguration(debugEnabled: true, handsOffLocalConfigPath: handsOffErrorPath, handsOffFleetConfigPath: handsOffErrorPath, isLibdatadogAvailable: true);
        result.ConfigurationSuccessResult.Should().BeNull();
        result.ErrorMessage.Should().NotBeNull();
    }
}
