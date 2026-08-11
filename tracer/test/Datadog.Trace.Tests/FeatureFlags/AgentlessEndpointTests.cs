// <copyright file="AgentlessEndpointTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.FeatureFlags.Agentless;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.FeatureFlags;

public class AgentlessEndpointTests
{
    // The endpoint is part of the cross-tracer wire contract asserted by system-tests
    // (tests/ffe/test_agentless_configuration.py), so these are exact-string assertions.
    [Theory]
    [InlineData("datadoghq.com", null, "https://ufc-server.ff-cdn.datadoghq.com/api/v2/feature-flagging/config/rules-based/server")]
    [InlineData("datadoghq.com", "", "https://ufc-server.ff-cdn.datadoghq.com/api/v2/feature-flagging/config/rules-based/server")]
    [InlineData("datadoghq.com", "prod", "https://ufc-server.ff-cdn.datadoghq.com/api/v2/feature-flagging/config/rules-based/server?dd_env=prod")]
    [InlineData("datadoghq.eu", "staging", "https://ufc-server.ff-cdn.datadoghq.eu/api/v2/feature-flagging/config/rules-based/server?dd_env=staging")]
    // The host is derived, not matched against an allowlist, so staging and government sites work too.
    [InlineData("datad0g.com", null, "https://ufc-server.ff-cdn.datad0g.com/api/v2/feature-flagging/config/rules-based/server")]
    [InlineData("ddog-gov.com", null, "https://ufc-server.ff-cdn.ddog-gov.com/api/v2/feature-flagging/config/rules-based/server")]
    [InlineData("DataDogHQ.com", null, "https://ufc-server.ff-cdn.datadoghq.com/api/v2/feature-flagging/config/rules-based/server")]
    [InlineData("  datadoghq.com  ", null, "https://ufc-server.ff-cdn.datadoghq.com/api/v2/feature-flagging/config/rules-based/server")]
    public void DerivesManagedEndpointFromSite(string site, string? env, string expected)
    {
        AgentlessEndpoint.TryCreate(site, env, baseUrl: null, out var endpoint, out var error).Should().BeTrue();

        error.Should().BeNull();
        endpoint.Uri.AbsoluteUri.Should().Be(expected);
        endpoint.IsManaged.Should().BeTrue();
    }

    [Fact]
    public void EscapesEnvironmentInQuery()
    {
        AgentlessEndpoint.TryCreate("datadoghq.com", "my env&x", baseUrl: null, out var endpoint, out _).Should().BeTrue();

        endpoint.Uri.Query.Should().Be("?dd_env=my%20env%26x");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FailsWithoutASite(string? site)
    {
        AgentlessEndpoint.TryCreate(site, env: null, baseUrl: null, out _, out var error).Should().BeFalse();

        error.Should().NotBeNull();
    }

    [Theory]
    // An origin-only base URL gets the canonical path, so operators only have to supply a host.
    [InlineData("https://flags.example.com", "https://flags.example.com/api/v2/feature-flagging/config/rules-based/server")]
    [InlineData("https://flags.example.com/", "https://flags.example.com/api/v2/feature-flagging/config/rules-based/server")]
    [InlineData("  https://flags.example.com  ", "https://flags.example.com/api/v2/feature-flagging/config/rules-based/server")]
    [InlineData("https://flags.example.com:8443", "https://flags.example.com:8443/api/v2/feature-flagging/config/rules-based/server")]
    // http is allowed for a custom endpoint: pointing at one is an operator decision.
    [InlineData("http://localhost:8126", "http://localhost:8126/api/v2/feature-flagging/config/rules-based/server")]
    // Anything with a path of its own is the exact endpoint, and is not rewritten.
    [InlineData("https://flags.example.com/ufc", "https://flags.example.com/ufc")]
    [InlineData("https://flags.example.com/ufc?token=abc", "https://flags.example.com/ufc?token=abc")]
    public void UsesConfiguredBaseUrl(string baseUrl, string expected)
    {
        AgentlessEndpoint.TryCreate("datadoghq.com", "prod", baseUrl, out var endpoint, out var error).Should().BeTrue();

        error.Should().BeNull();
        endpoint.Uri.AbsoluteUri.Should().Be(expected);

        // Only the managed endpoint receives the API key.
        endpoint.IsManaged.Should().BeFalse();
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("/relative/path")]
    [InlineData("ftp://flags.example.com/ufc")]
    [InlineData("file:///etc/flags.json")]
    // Uri parsing is lenient about internal whitespace, so it is rejected explicitly.
    [InlineData("https://flags.example.com/u fc")]
    public void RejectsInvalidBaseUrl(string baseUrl)
    {
        AgentlessEndpoint.TryCreate("datadoghq.com", "prod", baseUrl, out _, out var error).Should().BeFalse();

        // A custom endpoint can carry credentials, so it never reaches a log or an error message.
        error.Should().NotBeNullOrEmpty().And.NotContain(baseUrl);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TreatsBlankBaseUrlAsUnset(string? baseUrl)
    {
        AgentlessEndpoint.TryCreate("datadoghq.com", env: null, baseUrl, out var endpoint, out _).Should().BeTrue();

        endpoint.IsManaged.Should().BeTrue();
    }
}
