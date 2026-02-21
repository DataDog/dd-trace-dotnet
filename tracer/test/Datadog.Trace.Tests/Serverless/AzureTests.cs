// <copyright file="AzureTests.cs" company="Datadog">
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
public class AzureTests
{
    [Theory]
    [PairwiseData]
    public void IsAppServices(bool value)
    {
        if (value)
        {
            Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", "test");
        }

        new Azure().IsAppService.Should().Be(value);
    }

    [Theory]
    [PairwiseData]
    public void IsFunctions(bool value)
    {
        if (value)
        {
            Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", "test");
            Environment.SetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME", "dotnet-isolated");
            Environment.SetEnvironmentVariable("FUNCTIONS_EXTENSION_VERSION", "~4");
        }

        new Azure().IsFunction.Should().Be(value);
    }

    [Theory]
    [PairwiseData]
    public void IsUsingSiteExtension(bool value)
    {
        if (value)
        {
            Environment.SetEnvironmentVariable("DD_AZURE_APP_SERVICES", "1");
            Environment.SetEnvironmentVariable("DD_AAS_DOTNET_EXTENSION_VERSION", "3.20.0");
        }

        new Azure().IsUsingSiteExtension.Should().Be(value);
    }
}
