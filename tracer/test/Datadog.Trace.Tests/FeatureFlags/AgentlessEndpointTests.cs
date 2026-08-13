// <copyright file="AgentlessEndpointTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.FeatureFlags.Agentless;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.FeatureFlags;

public class AgentlessEndpointTests
{
    private const string DefaultPath = "/api/v2/feature-flagging/config/rules-based/server";

    [Theory]
    [InlineData("datadoghq.com", "https://ufc-server.ff-cdn.datadoghq.com" + DefaultPath)]
    [InlineData("DATADOGHQ.COM", "https://ufc-server.ff-cdn.datadoghq.com" + DefaultPath)] // site is lowercased
    [InlineData("datad0g.com", "https://ufc-server.ff-cdn.datad0g.com" + DefaultPath)] // staging
    [InlineData("ddog-gov.com", "https://ufc-server.ff-cdn.ddog-gov.com" + DefaultPath)] // govcloud
    public void DerivesManagedEndpointFromSite(string site, string expected)
    {
        AgentlessEndpoint.TryCreate(site, env: null, baseUrl: null, out var endpoint, out var error)
            .Should().BeTrue();
        error.Should().BeNull();
        endpoint.IsManaged.Should().BeTrue();
        endpoint.Uri.ToString().Should().Be(expected);
    }

    [Fact]
    public void AddsDdEnvWhenEnvIsConfigured()
    {
        AgentlessEndpoint.TryCreate("datadoghq.com", env: "production", baseUrl: null, out var endpoint, out _)
            .Should().BeTrue();
        endpoint.Uri.Query.Should().Be("?dd_env=production");
    }

    [Fact]
    public void DoesNotAddDdEnvWhenEnvIsNull()
    {
        AgentlessEndpoint.TryCreate("datadoghq.com", env: null, baseUrl: null, out var endpoint, out _)
            .Should().BeTrue();
        endpoint.Uri.Query.Should().BeEmpty();
    }

    [Fact]
    public void EscapesDdEnvValue()
    {
        AgentlessEndpoint.TryCreate("datadoghq.com", env: "my env&test", baseUrl: null, out var endpoint, out _)
            .Should().BeTrue();
        endpoint.Uri.Query.Should().Be("?dd_env=my%20env%26test");
    }

    [Theory]
    [InlineData("https://flags.example.com", "https://flags.example.com" + DefaultPath)]
    [InlineData("https://flags.example.com/", "https://flags.example.com" + DefaultPath)]
    [InlineData("https://flags.example.com/ufc", "https://flags.example.com/ufc")]
    [InlineData("https://flags.example.com/ufc?custom=query", "https://flags.example.com/ufc?custom=query")]
    public void CustomEndpointReceivesCanonicalPathForOriginOnly(string baseUrl, string expected)
    {
        AgentlessEndpoint.TryCreate("datadoghq.com", env: null, baseUrl: baseUrl, out var endpoint, out var error)
            .Should().BeTrue();
        error.Should().BeNull();
        endpoint.IsManaged.Should().BeFalse();
        endpoint.Uri.ToString().Should().Be(expected);
    }

    [Theory]
    [InlineData("http://localhost:8080/ufc")] // http accepted for custom endpoints
    public void CustomEndpointAcceptsHttp(string baseUrl)
    {
        AgentlessEndpoint.TryCreate("datadoghq.com", env: null, baseUrl: baseUrl, out var endpoint, out var error)
            .Should().BeTrue();
        endpoint.IsManaged.Should().BeFalse();
    }

    [Theory]
    [InlineData("ftp://flags.example.com", "The configured Feature Flags agentless URL must use HTTP or HTTPS")]
    [InlineData("notaurl", "The configured Feature Flags agentless URL is not a valid absolute URL")]
    [InlineData("https://flags.example.com bad", "The configured Feature Flags agentless URL is not a valid URL")] // internal whitespace
    public void RejectsInvalidBaseUrl(string baseUrl, string expectedError)
    {
        AgentlessEndpoint.TryCreate("datadoghq.com", env: null, baseUrl: baseUrl, out var endpoint, out var error)
            .Should().BeFalse();
        error.Should().Be(expectedError);
    }

    [Fact]
    public void RejectsEmptySiteWithoutBaseUrl()
    {
        AgentlessEndpoint.TryCreate(site: null, env: null, baseUrl: null, out var endpoint, out var error)
            .Should().BeFalse();
        error.Should().Be("No Datadog site is configured");
    }

    [Fact]
    public void RejectsWhitespaceOnlySiteWithoutBaseUrl()
    {
        AgentlessEndpoint.TryCreate("   ", env: null, baseUrl: null, out var endpoint, out var error)
            .Should().BeFalse();
        error.Should().Be("No Datadog site is configured");
    }

    [Fact]
    public void ErrorNeverContainsUrl()
    {
        // A URL may carry credentials, so the error must never echo it.
        AgentlessEndpoint.TryCreate("datadoghq.com", env: null, baseUrl: "https://user:pass@flags.example.com bad", out _, out var error)
            .Should().BeFalse();
        error.Should().NotContain("user");
        error.Should().NotContain("pass");
    }
}
