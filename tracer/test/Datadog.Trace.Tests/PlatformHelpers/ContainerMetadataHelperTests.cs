// <copyright file="ContainerMetadataHelperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.PlatformHelpers;

[Collection(nameof(EnvironmentVariablesTestCollection))]
[EnvironmentRestorer(ConfigurationKeys.ExternalEnv)]
public class ContainerMetadataHelperTests
{
    private const string ExternalEnv = "it-false,cn-nginx-webserver,pu-75a2b6d5-3949-4afb-ad0d-92ff0674e759";

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(ExternalEnv, ExternalEnv)]
    [InlineData("  " + ExternalEnv + "\n", ExternalEnv)]
    [InlineData("it-false,cn-weblog", "it-false,cn-weblog")]
    [InlineData("it-false\r\nX-Injected: true", null)]
    [InlineData("it-false,cn-café", null)]
    public void NormalizeExternalEnv(string value, string expected)
    {
        ContainerMetadataHelper.NormalizeExternalEnv(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData(ExternalEnv, ExternalEnv)]
    public void GetExternalEnvFromEnvironment(string value, string expected)
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.ExternalEnv, value);

        ContainerMetadataHelper.GetExternalEnvFromEnvironment().Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(ExternalEnv)]
    public void AddContainerMetadataHeaders_AddsExternalEnvOnlyWhenSet(string externalEnv)
    {
        var savedHeaders = new Dictionary<string, string>();
        var requestMock = new Mock<IApiRequest>();
        requestMock.Setup(x => x.AddHeader(It.IsAny<string>(), It.IsAny<string>())).Callback((string k, string v) => savedHeaders.Add(k, v));

        requestMock.Object.AddContainerMetadataHeaders(new ContainerMetadata(containerId: null, entityId: null, externalEnv));

        if (string.IsNullOrEmpty(externalEnv))
        {
            savedHeaders.Should().NotContainKey(AgentHttpHeaderNames.ExternalEnv);
        }
        else
        {
            savedHeaders.Should().Contain(AgentHttpHeaderNames.ExternalEnv, externalEnv);
        }
    }
}
