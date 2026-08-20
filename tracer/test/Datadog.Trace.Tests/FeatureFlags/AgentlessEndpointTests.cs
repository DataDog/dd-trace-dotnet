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
        var endpoint = Create(site);

        endpoint.IsManaged.Should().BeTrue();
        endpoint.Uri.ToString().Should().Be(expected);
    }

    [Fact]
    public void EndpointItselfCarriesNoEnvironment()
        => Create("datadoghq.com").Uri.Query.Should().BeEmpty();

    [Fact]
    public void AddsDdEnvWhenEnvIsConfigured()
        => Create("datadoghq.com").BuildRequestUri("production").Query.Should().Be("?dd_env=production");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void DoesNotAddDdEnvWhenEnvIsNotConfigured(string? env)
        => Create("datadoghq.com").BuildRequestUri(env).Query.Should().BeEmpty();

    [Fact]
    public void EscapesDdEnvValue()
        => Create("datadoghq.com").BuildRequestUri("my env&test").Query.Should().Be("?dd_env=my%20env%26test");

    [Fact]
    public void KeepsTheQueryConfiguredOnACustomEndpoint()
    {
        // The query may carry credentials or routing the operator needs, so dd_env is appended to it
        // rather than replacing it.
        var endpoint = Create("datadoghq.com", baseUrl: "https://flags.example.com/ufc?token=abc");

        endpoint.BuildRequestUri("production").Query.Should().Be("?token=abc&dd_env=production");
    }

    [Theory]
    [InlineData("https://flags.example.com/ufc?dd_env=staging")]
    [InlineData("https://flags.example.com/ufc?token=abc&dd_env=staging")]
    public void LeavesACustomEndpointThatAlreadyPinsDdEnvAlone(string baseUrl)
    {
        var endpoint = Create("datadoghq.com", baseUrl: baseUrl);

        endpoint.BuildRequestUri("production").Should().Be(endpoint.Uri);
    }

    [Fact]
    public void AddsDdEnvToACustomEndpointWithoutAQuery()
        => Create("datadoghq.com", baseUrl: "https://flags.example.com/ufc")
          .BuildRequestUri("production").Query.Should().Be("?dd_env=production");

    [Fact]
    public void DoesNotMistakeAQueryValueForAPinnedDdEnv()
        => Create("datadoghq.com", baseUrl: "https://flags.example.com/ufc?next=dd_env")
          .BuildRequestUri("production").Query.Should().Be("?next=dd_env&dd_env=production");

    [Theory]
    [InlineData("https://flags.example.com", "https://flags.example.com" + DefaultPath)]
    [InlineData("https://flags.example.com/", "https://flags.example.com" + DefaultPath)]
    [InlineData("https://flags.example.com/ufc", "https://flags.example.com/ufc")]
    [InlineData("https://flags.example.com/ufc?custom=query", "https://flags.example.com/ufc?custom=query")]
    public void CustomEndpointReceivesCanonicalPathForOriginOnly(string baseUrl, string expected)
    {
        var endpoint = Create("datadoghq.com", baseUrl: baseUrl);

        endpoint.IsManaged.Should().BeFalse();
        endpoint.Uri.ToString().Should().Be(expected);
    }

    [Theory]
    [InlineData("http://localhost:8080/ufc")] // http accepted for custom endpoints
    public void CustomEndpointAcceptsHttp(string baseUrl)
        => Create("datadoghq.com", baseUrl: baseUrl).IsManaged.Should().BeFalse();

    [Theory]
    [InlineData("ftp://flags.example.com", "The configured Feature Flags agentless URL must use HTTP or HTTPS")]
    [InlineData("notaurl", "The configured Feature Flags agentless URL is not a valid absolute URL")]
    [InlineData("https://flags.example.com bad", "The configured Feature Flags agentless URL is not a valid URL")] // internal whitespace
    public void RejectsInvalidBaseUrl(string baseUrl, string expectedError)
    {
        AgentlessEndpoint.TryCreate("datadoghq.com", baseUrl: baseUrl, out var endpoint, out var error)
            .Should().BeFalse();
        error.Should().Be(expectedError);
        endpoint.Should().BeNull();
    }

    [Fact]
    public void RejectsEmptySiteWithoutBaseUrl()
    {
        AgentlessEndpoint.TryCreate(site: null, baseUrl: null, out var endpoint, out var error)
            .Should().BeFalse();
        error.Should().Be("No Datadog site is configured");
        endpoint.Should().BeNull();
    }

    [Fact]
    public void RejectsWhitespaceOnlySiteWithoutBaseUrl()
    {
        AgentlessEndpoint.TryCreate("   ", baseUrl: null, out var endpoint, out var error)
            .Should().BeFalse();
        error.Should().Be("No Datadog site is configured");
        endpoint.Should().BeNull();
    }

    [Theory]
    [InlineData("https://datadoghq.com")] // user accidentally includes the scheme
    [InlineData("data dog hq.com")] // internal spaces
    [InlineData("datadoghq.com:99999")] // invalid port
    public void RejectsMalformedSiteWithoutThrowing(string site)
    {
        AgentlessEndpoint.TryCreate(site, baseUrl: null, out var endpoint, out var error)
            .Should().BeFalse();
        error.Should().Be("The configured Datadog site is not valid");
        endpoint.Should().BeNull();
    }

    [Fact]
    public void ErrorNeverContainsUrl()
    {
        // A URL may carry credentials, so the error must never echo it.
        AgentlessEndpoint.TryCreate("datadoghq.com", baseUrl: "https://user:pass@flags.example.com bad", out _, out var error)
            .Should().BeFalse();
        error.Should().NotContain("user");
        error.Should().NotContain("pass");
    }

    // Builds an endpoint that is expected to be valid, and returns it as non-nullable so the
    // assertions can read it directly. Throwing rather than asserting keeps the compiler's nullable
    // analysis satisfied without a null-forgiving operator, which would let an assertion be
    // silently skipped if the endpoint were ever null.
    private static AgentlessEndpoint Create(string? site, string? baseUrl = null)
    {
        AgentlessEndpoint.TryCreate(site, baseUrl, out var endpoint, out var error)
            .Should().BeTrue();
        error.Should().BeNull();

        return endpoint ?? throw new InvalidOperationException("TryCreate reported success without producing an endpoint.");
    }
}
