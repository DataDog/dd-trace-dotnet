// <copyright file="GlobalConfigurationSourceTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.LibDatadog.HandsOffConfiguration;
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

        // KEY1: defined in fleet file: this should be ignored
        Environment.SetEnvironmentVariable("KEY1", "local_env_var", EnvironmentVariableTarget.Process);

        // KEY2: defined false in local file: this should be applied over the local file
        Environment.SetEnvironmentVariable("KEY2", "true", EnvironmentVariableTarget.Process);

        // KEY2: only defined in env var, should prevail
        Environment.SetEnvironmentVariable("KEY3", "false", EnvironmentVariableTarget.Process);
        var result = GlobalConfigurationSource.CreateDefaultConfigurationSource(
            handsOffLocalConfigPath: Path.Combine("Configuration", "HandsOffConfigData", "application_monitoring.yml"),
            handsOffFleetConfigPath: Path.Combine("Configuration", "HandsOffConfigData", "application_monitoring_fleet.yml"),
            isLibdatadogAvailable: true);

        var source = result.ConfigurationSource;
        // env var
        var key3 = source.GetBool("KEY3", new NullConfigurationTelemetry(), null);
        key3.IsPresent.Should().BeTrue();
        key3.Result.Should().Be(false);

        // fleet file
        var environment = source.GetString("KEY1", new NullConfigurationTelemetry(), null, false);
        environment.IsPresent.Should().BeTrue();
        environment.Result.Should().Be("fleet_file_env");

        // local file
        var dataStreamEnabled = source.GetBool("KEY4", new NullConfigurationTelemetry(), null);
        dataStreamEnabled.IsPresent.Should().BeTrue();
        dataStreamEnabled.Result.Should().Be(true);

        // fleet file
        var service = source.GetString("KEY5", new NullConfigurationTelemetry(), null, false);
        service.IsPresent.Should().BeTrue();
        service.Result.Should().Be("fleet_file_service");

        // KEY1: defined in fleet file: this should be ignored
        Environment.SetEnvironmentVariable("KEY1", null, EnvironmentVariableTarget.Process);

        // KEY2: defined false in local file: this should be applied over the local file
        Environment.SetEnvironmentVariable("KEY2", null, EnvironmentVariableTarget.Process);

        // KEY2: only defined in env var, should prevail
        Environment.SetEnvironmentVariable("KEY2", null, EnvironmentVariableTarget.Process);
    }

    [Fact]
    public void TestErrorHandsOffConfigFile()
    {
        var handsOffErrorPath = Path.Combine("Configuration", "HandsOffConfigData", "corrupt_file.yml");
        var result = LibDatadog.HandsOffConfiguration.ConfiguratorHelper.GetConfiguration(debugEnabled: true, handsOffLocalConfigPath: handsOffErrorPath, handsOffFleetConfigPath: handsOffErrorPath, isLibdatadogAvailable: true);
        result.ConfigurationSuccessResult.Should().BeNull();
        result.ErrorMessage.Should().NotBeNull();
        result.ErrorMessage.Should().Be("apm_configuration_default: invalid type: string \"DD_TRACE_DEBUG': true, DD_ENV: \", expected struct ConfigMap(HashMap<String, String>) at line 3 column 3");
    }
}
