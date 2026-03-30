// <copyright file="TelemetrySettingsAgentlessSettingsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Telemetry;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Telemetry;

[Collection(nameof(EnvironmentVariablesTestCollection))]
[EnvironmentRestorer(PlatformKeys.GcpFunction.FunctionNameKey, PlatformKeys.AzureAppService.ContainerAppName, PlatformKeys.AzureAppService.RunFromZipKey, PlatformKeys.AzureAppService.AppServiceApplogsTraceEnabledKey, PlatformKeys.AzureAppService.SiteNameKey)]
public class TelemetrySettingsAgentlessSettingsTests
{
    private const string ApiKey = "some-key";
    private static readonly Uri Uri = new("http://localhost:8080");

    [Fact]
    public void CloudDetection_WhenNoCloudVariables()
    {
        var settings = TelemetrySettings.AgentlessSettings.Create(Uri, ApiKey);

        settings.Cloud.Should().BeNull();
    }

    [Fact]
    public void CloudDetection_WhenGcp()
    {
        var id = "some-identifier";
        Environment.SetEnvironmentVariable("K_SERVICE", id);

        var settings = TelemetrySettings.AgentlessSettings.Create(Uri, ApiKey);

        var cloud = settings.Cloud.Should().NotBeNull();
        settings.Cloud.Provider.Should().Be("GCP");
        settings.Cloud.ResourceType.Should().Be("GCPCloudRun");
        settings.Cloud.ResourceIdentifier.Should().Be(id);
    }

    [Fact]
    public void CloudDetection_WhenAzureContainerApps()
    {
        var id = "some-identifier";
        Environment.SetEnvironmentVariable("CONTAINER_APP_NAME", id);

        var settings = TelemetrySettings.AgentlessSettings.Create(Uri, ApiKey);

        var cloud = settings.Cloud.Should().NotBeNull();
        settings.Cloud.Provider.Should().Be("Azure");
        settings.Cloud.ResourceType.Should().Be("AzureContainerApp");
        settings.Cloud.ResourceIdentifier.Should().Be(id);
    }

    [Theory]
    [InlineData("APPSVC_RUN_ZIP")]
    [InlineData("WEBSITE_APPSERVICEAPPLOGS_TRACE_ENABLED")]
    public void CloudDetection_WhenAzureAppService(string variable)
    {
        var id = "some-identifier";
        Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", id);
        Environment.SetEnvironmentVariable(variable, "anything");

        var settings = TelemetrySettings.AgentlessSettings.Create(Uri, ApiKey);

        var cloud = settings.Cloud.Should().NotBeNull();
        settings.Cloud.Provider.Should().Be("Azure");
        settings.Cloud.ResourceType.Should().Be("AzureAppService");
        settings.Cloud.ResourceIdentifier.Should().Be(id);
    }
}
