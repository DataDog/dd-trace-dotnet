// <copyright file="AzurePlatformDetectionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Serverless;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Serverless;

[Collection(nameof(EnvironmentVariablesTestCollection))]
[EnvironmentRestorer(
    "WEBSITE_SITE_NAME",
    "FUNCTIONS_WORKER_RUNTIME",
    "FUNCTIONS_EXTENSION_VERSION",
    "DD_AZURE_APP_SERVICES",
    "DD_AAS_DOTNET_EXTENSION_VERSION")]
public class AzurePlatformDetectionTests
{
    public AzurePlatformDetectionTests()
    {
        AzurePlatformDetection.Reset();
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
}
