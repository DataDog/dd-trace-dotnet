// <copyright file="ApplicationTelemetryCollectorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Specialized;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Collectors;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Telemetry.Collectors;

public class ApplicationTelemetryCollectorTests
{
    private const string ServiceName = "serializer-test-app";

    [Fact]
    public void ApplicationDataShouldIncludeExpectedValues()
    {
        const string env = "serializer-tests";
        const string serviceVersion = "1.2.3";
        var configurationTelemetry = new ConfigurationTelemetry();
        var settings = new TracerSettings(
            new NameValueConfigurationSource(
                new NameValueCollection
                {
                    { ConfigurationKeys.ServiceName, ServiceName },
                    { ConfigurationKeys.Environment, env },
                    { ConfigurationKeys.ServiceVersion, serviceVersion },
                    { ConfigurationKeys.GitCommitSha, "mySha" },
                    { ConfigurationKeys.GitRepositoryUrl, "https://github.com/gitOrg/gitRepo" },
                }),
            configurationTelemetry,
            new OverrideErrorLog());

        var collector = new ApplicationTelemetryCollector();

        collector.GetApplicationData().Should().BeNull();

        collector.RecordTracerSettings(new ImmutableTracerSettings(settings), ServiceName);

        // calling twice should give same results
        AssertData(collector.GetApplicationData());
        AssertData(collector.GetApplicationData());

        void AssertData(ApplicationTelemetryData data)
        {
            data.Should().NotBeNull();
            data.ServiceName.Should().Be(ServiceName);
            data.Env.Should().Be(env);
            data.TracerVersion.Should().Be(TracerConstants.AssemblyVersion);
            data.LanguageName.Should().Be("dotnet");
            data.ServiceVersion.Should().Be(serviceVersion);
            data.LanguageVersion.Should().Be(FrameworkDescription.Instance.ProductVersion);
            data.RuntimeName.Should().NotBeNullOrEmpty().And.Be(FrameworkDescription.Instance.Name);
            data.RuntimeVersion.Should().Be(FrameworkDescription.Instance.ProductVersion);
            data.CommitSha.Should().Be("mySha");
            data.RepositoryUrl.Should().Be("https://github.com/gitOrg/gitRepo");
        }
    }

    [Fact]
    public void ApplicationWithNoGitDataShouldIncludeExpectedValues()
    {
        const string env = "serializer-tests";
        const string serviceVersion = "1.2.3";
        var configurationTelemetry = new ConfigurationTelemetry();
        var settings = new TracerSettings(
            new NameValueConfigurationSource(
                new NameValueCollection
                {
                    { ConfigurationKeys.ServiceName, ServiceName },
                    { ConfigurationKeys.Environment, env },
                    { ConfigurationKeys.ServiceVersion, serviceVersion },
                }),
            configurationTelemetry,
            new OverrideErrorLog());

        var collector = new ApplicationTelemetryCollector();

        collector.GetApplicationData().Should().BeNull();

        collector.RecordTracerSettings(new ImmutableTracerSettings(settings), ServiceName);

        // calling twice should give same results
        AssertData(collector.GetApplicationData());
        AssertData(collector.GetApplicationData());

        void AssertData(ApplicationTelemetryData data)
        {
            data.Should().NotBeNull();
            data.ServiceName.Should().Be(ServiceName);
            data.Env.Should().Be(env);
            data.TracerVersion.Should().Be(TracerConstants.AssemblyVersion);
            data.LanguageName.Should().Be("dotnet");
            data.ServiceVersion.Should().Be(serviceVersion);
            data.LanguageVersion.Should().Be(FrameworkDescription.Instance.ProductVersion);
            data.RuntimeName.Should().NotBeNullOrEmpty().And.Be(FrameworkDescription.Instance.Name);
            data.RuntimeVersion.Should().Be(FrameworkDescription.Instance.ProductVersion);
            data.CommitSha.Should().BeNull();
            data.RepositoryUrl.Should().BeNull();
        }
    }

    [Fact]
    public void HostDataShouldIncludeExpectedValues()
    {
        const string env = "serializer-tests";
        const string serviceVersion = "1.2.3";
        var configurationTelemetry = new ConfigurationTelemetry();
        var settings = new TracerSettings(
            new NameValueConfigurationSource(
                new NameValueCollection
                {
                    { ConfigurationKeys.ServiceName, ServiceName },
                    { ConfigurationKeys.Environment, env },
                    { ConfigurationKeys.ServiceVersion, serviceVersion },
                    { ConfigurationKeys.GitCommitSha, "mySha" },
                    { ConfigurationKeys.GitRepositoryUrl, "https://github.com/gitOrg/gitRepo" },
                }),
            configurationTelemetry,
            new OverrideErrorLog());

        var collector = new ApplicationTelemetryCollector();

        collector.GetHostData().Should().BeNull();

        collector.RecordTracerSettings(new ImmutableTracerSettings(settings), ServiceName);

        // calling twice should give same results
        AssertData(collector.GetHostData());
        AssertData(collector.GetHostData());

        static void AssertData(HostTelemetryData data)
        {
            data.Should().NotBeNull();
            data.Hostname.Should().Be(HostMetadata.Instance.Hostname ?? string.Empty);
        }
    }
}
