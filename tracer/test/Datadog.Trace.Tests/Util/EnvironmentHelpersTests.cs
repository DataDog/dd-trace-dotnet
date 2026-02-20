// <copyright file="EnvironmentHelpersTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Serverless;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Util;

[Collection(nameof(EnvironmentVariablesTestCollection))]
[EnvironmentRestorer(
    "WEBSITE_SITE_NAME",
    "FUNCTIONS_WORKER_RUNTIME",
    "FUNCTIONS_EXTENSION_VERSION",
    "DD_AZURE_APP_SERVICES",
    "DD_AAS_DOTNET_EXTENSION_VERSION",
    "AWS_LAMBDA_FUNCTION_NAME",
    "FUNCTION_NAME",
    "GCP_PROJECT",
    "FUNCTION_TARGET",
    "K_SERVICE")]
public class EnvironmentHelpersTests
{
    public EnvironmentHelpersTests()
    {
        // Reset cached values before each test since tests modify environment variables
        AzurePlatformDetection.Reset();
        AwsPlatformDetection.Reset();
        GcpPlatformDetection.Reset();
    }

    [Theory]
    [PairwiseData]
    public void IsAzureAppServices(bool value)
    {
        if (value)
        {
            Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", "test");
        }

        AzurePlatformDetection.IsAzureAppServices.Should().Be(value);
    }

    [Theory]
    [PairwiseData]
    public void IsAzureFunctions(bool value)
    {
        if (value)
        {
            Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", "test");
            Environment.SetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME", "dotnet-isolated");
            Environment.SetEnvironmentVariable("FUNCTIONS_EXTENSION_VERSION", "~4");
        }

        AzurePlatformDetection.IsAzureFunctions.Should().Be(value);
    }

    [Theory]
    [PairwiseData]
    public void IsUsingAzureAppServicesSiteExtension(bool value)
    {
        if (value)
        {
            Environment.SetEnvironmentVariable("DD_AZURE_APP_SERVICES", "1");
            Environment.SetEnvironmentVariable("DD_AAS_DOTNET_EXTENSION_VERSION", "3.20.0");
        }

        AzurePlatformDetection.IsUsingAzureAppServicesSiteExtension.Should().Be(value);
    }

    [Theory]
    [PairwiseData]
    public void IsAwsLambda(bool value)
    {
        if (value)
        {
            Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", "test");
        }

        AwsPlatformDetection.IsAwsLambda.Should().Be(value);
    }

    [Theory]
    [PairwiseData]
    public void IsGoogleCloudFunctions(bool value, bool gen1)
    {
        if (value)
        {
            if (gen1)
            {
                Environment.SetEnvironmentVariable("FUNCTION_NAME", "test");
                Environment.SetEnvironmentVariable("GCP_PROJECT", "test");
            }
            else
            {
                // gen2
                Environment.SetEnvironmentVariable("K_SERVICE", "test");
                Environment.SetEnvironmentVariable("FUNCTION_TARGET", "test");
            }
        }

        GcpPlatformDetection.IsGoogleCloudFunctions.Should().Be(value);
    }
}
