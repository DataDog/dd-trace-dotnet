// <copyright file="TracerSettingsServerlessTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Datadog.Trace.ClrProfiler.ServerlessInstrumentation;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Configuration;

[Collection(nameof(EnvironmentVariablesTestCollection))]
public class TracerSettingsServerlessTests : SettingsTestsBase
{
    // These tests rely on Lambda.Create() which uses environment variables
    // See TracerSettingsTests for tests which don't rely on any environment variables
    [Theory]
    [InlineData("test1,, ,test2", false, false, new[] { "TEST1", "TEST2" })]
    [InlineData("test1,, ,test2", true, true, new[] { "TEST1", "TEST2" })]
    [InlineData(null, true, true, new[] { "azuredefault" })]
    [InlineData(null, false, true, new[] { "/2018-06-01/RUNTIME/INVOCATION/" })]
    [InlineData(null, false, false, new string[0])]
    [InlineData("", true, true, new string[0])]
    public void HttpClientExcludedUrlSubstrings(string value, bool isRunningInAppService, bool isRunningInLambda, string[] expected)
    {
        if (expected.Length == 1 && expected[0] == "azuredefault")
        {
            expected = ImmutableAzureAppServiceSettings.DefaultHttpClientExclusions.Split(',').Select(s => s.Trim()).ToArray();
        }

        var previous = isRunningInLambda
                           ? SetLambdaEnvironmentForTests("functionName", "serviceName::handlerName")
                           : SetLambdaEnvironmentForTests(null, null);
        try
        {
            var source = CreateConfigurationSource(
                (ConfigurationKeys.HttpClientExcludedUrlSubstrings, value),
                (ConfigurationKeys.AzureAppService.AzureAppServicesContextKey, isRunningInAppService ? "1" : "0"));

            var settings = new TracerSettings(source);

            settings.HttpClientExcludedUrlSubstrings.Should().BeEquivalentTo(expected);
        }
        finally
        {
            foreach (var kvp in previous)
            {
                System.Environment.SetEnvironmentVariable(kvp.Key, kvp.Value);
            }
        }
    }

    private Dictionary<string, string> SetLambdaEnvironmentForTests(
        string functionName, string handlerName, [CallerFilePath] string extensionPath = null)
    {
        var previous = new Dictionary<string, string>
        {
            { LambdaMetadata.FunctionNameEnvVar, System.Environment.GetEnvironmentVariable(LambdaMetadata.FunctionNameEnvVar) },
            { LambdaMetadata.HandlerEnvVar, System.Environment.GetEnvironmentVariable(LambdaMetadata.HandlerEnvVar) },
            { LambdaMetadata.ExtensionPathEnvVar, System.Environment.GetEnvironmentVariable(LambdaMetadata.ExtensionPathEnvVar) },
        };

        System.Environment.SetEnvironmentVariable(LambdaMetadata.FunctionNameEnvVar, functionName);
        System.Environment.SetEnvironmentVariable(LambdaMetadata.HandlerEnvVar, handlerName);
        System.Environment.SetEnvironmentVariable(LambdaMetadata.ExtensionPathEnvVar, extensionPath);
        return previous;
    }
}
