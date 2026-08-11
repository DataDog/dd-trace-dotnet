// <copyright file="AgentlessConfigurationSourceIntegrationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.FeatureFlags;
using Datadog.Trace.FeatureFlags.Agentless;
using Datadog.Trace.FeatureFlags.Rcm.Model;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

using Environment = Datadog.Trace.FeatureFlags.Rcm.Model.Environment;

namespace Datadog.Trace.IntegrationTests.FeatureFlags;

/// <summary>
/// Exercises the agentless source over a real HTTP transport, so the request the endpoint actually
/// receives — path, headers, gzip, conditional requests — is asserted rather than mocked.
/// </summary>
public class AgentlessConfigurationSourceIntegrationTests
{
    [Fact]
    public async Task PollsTheCanonicalPathWithTheDefaultHeaders()
    {
        using var intake = new MockFeatureFlagsIntake(CreateConfiguration());
        var applied = new List<ServerConfiguration>();

        using var source = CreateSource(intake, applied);
        await source!.PollAsync();

        applied.Should().ContainSingle();
        applied[0].Flags.Should().ContainKey("simple-string");

        var request = intake.Requests.Should().ContainSingle().Subject;

        // The base URL only carries an origin, so the canonical path is appended. dd_env is only
        // added to the managed endpoint, never to a custom one.
        request.PathAndQuery.Should().Be(AgentlessEndpoint.DefaultPath);

        request.Headers["Accept-Encoding"].Should().Contain("gzip");
        request.Headers["DD-Client-Library-Language"].Should().Be(TracerConstants.Language);
        request.Headers["DD-Client-Library-Version"].Should().Be(TracerConstants.ThreePartVersion);

        // Without this the poll instruments itself, producing a span per poll.
        request.Headers["x-datadog-tracing-enabled"].Should().Be("false");

        // A custom endpoint is left to report its own authentication failure rather than having the
        // Datadog credential sent to it.
        request.Headers["DD-API-KEY"].Should().BeNull();
    }

    [Fact]
    public async Task ReusesTheEtagOfTheAppliedConfigurationAndHonoursA304()
    {
        using var intake = new MockFeatureFlagsIntake(CreateConfiguration()) { ETag = "\"v1\"" };
        var applied = new List<ServerConfiguration>();

        using var source = CreateSource(intake, applied);
        await source!.PollAsync();
        await source.PollAsync();

        intake.Requests.Should().HaveCount(2);
        intake.Requests[0].IfNoneMatch.Should().BeNull();
        intake.Requests[1].IfNoneMatch.Should().Be("\"v1\"");

        // The second poll was answered with a 304, which is a no-op.
        applied.Should().ContainSingle();

        // A new document is picked up again once the endpoint's ETag moves on.
        intake.SetConfiguration(CreateConfiguration("staging"), "\"v2\"");
        await source.PollAsync();

        applied.Should().HaveCount(2);
        applied[1].Environment!.Name.Should().Be("staging");
        intake.Requests[2].IfNoneMatch.Should().Be("\"v1\"");
    }

    [Fact]
    public async Task KeepsLastKnownGoodWhenTheEndpointStartsFailing()
    {
        using var intake = new MockFeatureFlagsIntake(CreateConfiguration());
        var applied = new List<ServerConfiguration>();

        using var source = CreateSource(intake, applied);
        await source!.PollAsync();

        // A 404 is not retryable, so the poll ends after one request. The retry schedule itself is
        // covered by unit tests, where it costs no wall-clock time.
        intake.StatusCode = 404;
        await source.PollAsync();

        // Nothing replaced the configuration that was applied.
        applied.Should().ContainSingle();
        intake.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task ReadsAnUncompressedBody()
    {
        using var intake = new MockFeatureFlagsIntake(CreateConfiguration()) { UseGzip = false };
        var applied = new List<ServerConfiguration>();

        using var source = CreateSource(intake, applied);
        await source!.PollAsync();

        applied.Should().ContainSingle();
    }

    [Fact]
    public void IsNotCreatedForTheManagedEndpointWithoutAnApiKey()
    {
        // Polling the managed endpoint unauthenticated would only produce a failure every interval.
        var settings = CreateSettings(baseUrl: null);

        AgentlessConfigurationSource.Create(settings, _ => true).Should().BeNull();
    }

    private static ServerConfiguration CreateConfiguration(string environmentName = "production")
        => new()
        {
            Format = "SERVER",
            CreatedAt = "2025-01-01T00:00:00Z",
            Environment = new Environment { Name = environmentName },
            Flags = FeatureFlagsHelpers.CreateAllFlags(),
        };

    private static AgentlessConfigurationSource? CreateSource(MockFeatureFlagsIntake intake, List<ServerConfiguration> applied)
        => AgentlessConfigurationSource.Create(
            CreateSettings(intake.Origin),
            configuration =>
            {
                applied.Add(configuration);
                return true;
            });

    private static FeatureFlagsSettings CreateSettings(string? baseUrl)
    {
        var collection = new NameValueCollection
        {
            [ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSource] = FeatureFlagsSettings.AgentlessSourceName,

            // These tests drive PollAsync directly, so the interval only bounds retry delays.
            [ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSourceAgentlessPollIntervalSeconds] = "1",
            [ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSourceAgentlessRequestTimeoutSeconds] = "10",
        };

        if (baseUrl is not null)
        {
            collection[ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSourceAgentlessBaseUrl] = baseUrl;
        }

        return new FeatureFlagsSettings(new NameValueConfigurationSource(collection), NullConfigurationTelemetry.Instance);
    }
}
