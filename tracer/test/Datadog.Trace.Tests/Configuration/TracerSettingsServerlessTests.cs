﻿// <copyright file="TracerSettingsServerlessTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Datadog.Trace.ClrProfiler.ServerlessInstrumentation;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Configuration;

[Collection(nameof(EnvironmentVariablesTestCollection))]
[EnvironmentRestorer(LambdaMetadata.FunctionNameEnvVar, LambdaMetadata.HandlerEnvVar, LambdaMetadata.ExtensionPathEnvVar)]
public class TracerSettingsServerlessTests : SettingsTestsBase
{
    // These tests rely on Lambda.Create() which uses environment variables
    // See TracerSettingsTests for tests which don't rely on any environment variables
    [Theory]
    [InlineData("test1,, ,test2", false, new[] { "TEST1", "TEST2" })]
    [InlineData("test1,, ,test2", true, new[] { "TEST1", "TEST2" })]
    [InlineData(null, true, new[] { "/2018-06-01/RUNTIME/INVOCATION/" })] // default value for AWS Lambda, see LambdaMetadata.DefaultHttpClientExclusions
    [InlineData(null, false, new string[0])]                              // empty
    [InlineData("", true, new string[0])]                                 // empty
    public void HttpClientExcludedUrlSubstrings_AwsLambda(string value, bool isRunningInLambda, string[] expected)
    {
        if (isRunningInLambda)
        {
            Environment.SetEnvironmentVariable(LambdaMetadata.FunctionNameEnvVar, "functionName");
            Environment.SetEnvironmentVariable(LambdaMetadata.HandlerEnvVar, "serviceName::handlerName");
            Environment.SetEnvironmentVariable(LambdaMetadata.ExtensionPathEnvVar, GetCallerFilePath());
        }

        var source = CreateConfigurationSource((ConfigurationKeys.HttpClientExcludedUrlSubstrings, value));
        var settings = new TracerSettings(source);

        settings.HttpClientExcludedUrlSubstrings.Should().BeEquivalentTo(expected);
    }

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
            configPairs.Add((ConfigurationKeys.AzureAppService.SiteNameKey, "site-name"));
        }

        var settings = new TracerSettings(CreateConfigurationSource(configPairs.ToArray()));

        settings.HttpClientExcludedUrlSubstrings.Should().BeEquivalentTo(expected);
    }

    private static string GetCallerFilePath([CallerFilePath] string callerFilePath = null)
    {
        // using [CallerFilePath] to ensure we can a file path that exists
        return callerFilePath!;
    }
}
