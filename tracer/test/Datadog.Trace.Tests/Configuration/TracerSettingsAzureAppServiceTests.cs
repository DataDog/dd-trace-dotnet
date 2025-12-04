// <copyright file="TracerSettingsAzureAppServiceTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.ClrProfiler.ServerlessInstrumentation;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Configuration;

[Collection(nameof(EnvironmentVariablesTestCollection))]
[EnvironmentRestorer(PlatformKeys.Aws.FunctionName, PlatformKeys.Aws.Handler, ConfigurationKeys.Aws.ExtensionPath)]
public class TracerSettingsAzureAppServiceTests : SettingsTestsBase
{
    [Theory]
    [InlineData("test1,, ,test2", false, new[] { "TEST1", "TEST2" })]
    [InlineData("test1,, ,test2", true, new[] { "TEST1", "TEST2" })]
    [InlineData(null, true, new[] { "azuredefault" })] // "azuredefault" means use ImmutableAzureAppServiceSettings.DefaultHttpClientExclusions
    [InlineData(null, false, new string[0])]           // empty
    [InlineData("", true, new string[0])]              // empty
    public void HttpClientExcludedUrlSubstrings_AzureAppServices(string value, bool isRunningInAppService, string[] expected)
    {
        if (expected.Length == 1 && expected[0] == "azuredefault")
        {
            expected = ImmutableAzureAppServiceSettings.DefaultHttpClientExclusions.Split(',').Select(s => s.Trim()).ToArray();
        }

        var configPairs = new List<(string Key, string Value)>
        {
            (ConfigurationKeys.HttpClientExcludedUrlSubstrings, value)
        };

        if (isRunningInAppService)
        {
            configPairs.Add((PlatformKeys.AzureAppService.SiteNameKey, "site-name"));
        }

        var settings = new TracerSettings(CreateConfigurationSource(configPairs.ToArray()));

        settings.HttpClientExcludedUrlSubstrings.Should().BeEquivalentTo(expected);
    }
}
