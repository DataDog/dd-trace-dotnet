// <copyright file="HandsOffConfigurationSourceTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.IO;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Configuration;

public class HandsOffConfigurationSourceTests
{
    [Fact]
    public void TestErrorHandsOffConfigFile()
    {
        var handsOffErrorPath = Path.Combine("Configuration", "HandsOffConfigData", "corrupt_file.yml");
        var result = LibDatadog.HandsOffConfiguration.ConfiguratorHelper.GetConfiguration(debugEnabled: false, handsOffLocalConfigPath: handsOffErrorPath, handsOffFleetConfigPath: handsOffErrorPath, isLibdatadogAvailable: true);
        result.ConfigurationSuccessResult.Should().BeNull();
        result.ErrorMessage.Should().NotBeNull();
        result.ErrorMessage.Should().Be("apm_configuration_default: invalid type: string \"DD_TRACE_DEBUG': true, DD_ENV: \", expected struct ConfigMap(HashMap<String, String>) at line 3 column 3");
    }

    [Fact]
    public void TestErrorHandsOffConfigFileWrongInitKey()
    {
        var handsOffErrorPath = Path.Combine("Configuration", "HandsOffConfigData", "corrupt_file2.yml");
        var result = LibDatadog.HandsOffConfiguration.ConfiguratorHelper.GetConfiguration(debugEnabled: false, handsOffLocalConfigPath: handsOffErrorPath, handsOffFleetConfigPath: handsOffErrorPath, isLibdatadogAvailable: true);
        result.ConfigurationSuccessResult.Should().BeNull();
        result.ErrorMessage.Should().NotBeNull();
        result.ErrorMessage.Should().Be("did not find expected key at line 3 column 28, while parsing a block mapping at line 3 column 3");
    }

    [Fact]
    public void TestErrorHandsOffConfigFileWrongTypes()
    {
        var handsOffErrorPath = Path.Combine("Configuration", "HandsOffConfigData", "corrupt_file3.yml");
        var result = LibDatadog.HandsOffConfiguration.ConfiguratorHelper.GetConfiguration(debugEnabled: false, handsOffLocalConfigPath: handsOffErrorPath, handsOffFleetConfigPath: handsOffErrorPath, isLibdatadogAvailable: true);
        result.ConfigurationSuccessResult.Should().NotBeNull();
        result.ConfigurationSuccessResult!.Value.ConfigEntriesFleet.Should().NotBeEmpty();
        result.ConfigurationSuccessResult!.Value.ConfigEntriesFleet.Count.Should().Be(3);
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void TestHandsOffConfigFileDoesntExist()
    {
        var handsOffErrorPath = Path.Combine("Configuration", "HandsOffConfigData", "noexistence.yml");
        var result = LibDatadog.HandsOffConfiguration.ConfiguratorHelper.GetConfiguration(debugEnabled: false, handsOffLocalConfigPath: handsOffErrorPath, handsOffFleetConfigPath: handsOffErrorPath, isLibdatadogAvailable: true);
        result.ConfigurationSuccessResult.Should().NotBeNull();
        result.ConfigurationSuccessResult!.Value.ConfigEntriesFleet.Should().BeEmpty();
        result.ConfigurationSuccessResult.Value.ConfigEntriesLocal.Should().BeEmpty();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void TestHandsOffConfigFileWrongPath()
    {
        var handsOffErrorPath = "path_#/$#@$^%!!DSDS_\\reallywro/ng开儿 艾诶开伊 艾艾 西吉艾艾伊娜伊";
        var result = LibDatadog.HandsOffConfiguration.ConfiguratorHelper.GetConfiguration(debugEnabled: false, handsOffLocalConfigPath: handsOffErrorPath, handsOffFleetConfigPath: handsOffErrorPath, isLibdatadogAvailable: true);
        result.ConfigurationSuccessResult.Should().NotBeNull();
        result.ConfigurationSuccessResult!.Value.ConfigEntriesFleet.Should().BeEmpty();
        result.ConfigurationSuccessResult.Value.ConfigEntriesLocal.Should().BeEmpty();
        result.ErrorMessage.Should().BeNull();
    }
}
