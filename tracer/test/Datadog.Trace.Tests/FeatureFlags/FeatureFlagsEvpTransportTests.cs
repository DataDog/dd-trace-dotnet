// <copyright file="FeatureFlagsEvpTransportTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Configuration;
using Datadog.Trace.FeatureFlags;
using Datadog.Trace.FeatureFlags.Evp;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.TestHelpers.TransportHelpers;
using Datadog.Trace.Tests.Agent;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.FeatureFlags;

public class FeatureFlagsEvpTransportTests
{
    private static readonly JsonSerializerSettings SerializerSettings = new();

    public static IEnumerable<object[]> AmbiguousFailures()
    {
        yield return [new IOException("broken pipe")];
        yield return [new TimeoutException("timeout")];
        yield return [new SocketException((int)SocketError.ConnectionReset)];
        yield return [new WebException("send failed", WebExceptionStatus.SendFailure)];
    }

    [Theory]
    [InlineData(FeatureFlagsEvpTransport.EventPlatformProxyV4)]
    [InlineData(FeatureFlagsEvpTransport.EventPlatformProxyV2)]
    public async Task Discovery_SelectsAdvertisedLocalRoute(string proxyEndpoint)
    {
        var local = CreateFactory("http://agent:8126/");
        var direct = CreateFactory("https://event-platform-intake.datadoghq.com/");
        var discovery = new DiscoveryServiceMock();
        using var transport = CreateTransport(local, direct, discovery);

        discovery.TriggerChange(eventPlatformProxyEndpoint: proxyEndpoint);

        await transport.SendAsync(new object(), SerializerSettings);

        local.RequestsSent.Should().ContainSingle()
             .Which.Endpoint.AbsolutePath.Should().Be($"/{proxyEndpoint}/{FeatureFlagsEvpTransport.ExposureIntakePath}");
        direct.RequestsSent.Should().BeEmpty();
    }

    [Fact]
    public async Task AgentlessWithoutCompatibleLocalRoute_UsesDirectAndStaysDirect()
    {
        var local = CreateFactory("http://agent:8126/");
        var direct = CreateFactory("https://event-platform-intake.datadoghq.com/");
        var discovery = new DiscoveryServiceMock();
        using var transport = CreateTransport(local, direct, discovery);

        discovery.TriggerChange(eventPlatformProxyEndpoint: "v0.4/traces");
        await transport.SendAsync(new object(), SerializerSettings);

        // A late discovery result must not move a writer after it selected direct for a batch.
        discovery.TriggerChange(eventPlatformProxyEndpoint: FeatureFlagsEvpTransport.EventPlatformProxyV4);
        await transport.SendAsync(new object(), SerializerSettings);

        local.RequestsSent.Should().BeEmpty();
        direct.RequestsSent.Should().HaveCount(2);
        direct.RequestsSent.Should().OnlyContain(r => r.Endpoint.AbsolutePath == $"/{FeatureFlagsEvpTransport.ExposureIntakePath}");
    }

    [Fact]
    public async Task RemoteConfigurationNeverUsesDirectIntake()
    {
        var local = CreateFactory("http://agent:8126/");
        var direct = CreateFactory("https://event-platform-intake.datadoghq.com/");
        var discovery = new DiscoveryServiceMock();
        using var transport = new FeatureFlagsEvpTransport(FeatureFlagsSource.RemoteConfig, local, direct, discovery);

        await transport.SendAsync(new object(), SerializerSettings);

        local.RequestsSent.Should().ContainSingle()
             .Which.Endpoint.AbsolutePath.Should().Be($"/{FeatureFlagsEvpTransport.EventPlatformProxyV2}/{FeatureFlagsEvpTransport.ExposureIntakePath}");
        direct.RequestsSent.Should().BeEmpty();
    }

    [Theory]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(405)]
    public async Task DefinitiveLocalHttpFailure_ReplaysCurrentBatchDirectAndStaysDirect(int statusCode)
    {
        var local = CreateFactory("http://agent:8126/", _ => new TestApiRequest(new Uri("http://agent"), statusCode));
        var direct = CreateFactory("https://event-platform-intake.datadoghq.com/");
        using var transport = CreateTransport(local, direct, initialLocalProxyEndpoint: FeatureFlagsEvpTransport.EventPlatformProxyV4);

        await transport.SendAsync(new object(), SerializerSettings);
        await transport.SendAsync(new object(), SerializerSettings);

        local.RequestsSent.Should().ContainSingle();
        direct.RequestsSent.Should().HaveCount(2, "the failed current batch and the next batch both use direct intake");
    }

    [Theory]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(503)]
    public async Task OverloadOrServerFailure_DoesNotFallbackOrChangeFutureRoute(int statusCode)
    {
        var local = CreateFactory(
            "http://agent:8126/",
            _ => new TestApiRequest(new Uri("http://agent"), statusCode),
            uri => new TestApiRequest(uri));
        var direct = CreateFactory("https://event-platform-intake.datadoghq.com/");
        using var transport = CreateTransport(local, direct, initialLocalProxyEndpoint: FeatureFlagsEvpTransport.EventPlatformProxyV4);

        await transport.SendAsync(new object(), SerializerSettings);
        await transport.SendAsync(new object(), SerializerSettings);

        local.RequestsSent.Should().HaveCount(2);
        direct.RequestsSent.Should().BeEmpty();
    }

    [Fact]
    public async Task DefinitiveConnectFailure_ReplaysCurrentBatchDirectAndStaysDirect()
    {
        var failure = new WebException("refused", WebExceptionStatus.ConnectFailure);
        var local = CreateFactory("http://agent:8126/", uri => new ThrowingApiRequest(uri, failure));
        var direct = CreateFactory("https://event-platform-intake.datadoghq.com/");
        using var transport = CreateTransport(local, direct, initialLocalProxyEndpoint: FeatureFlagsEvpTransport.EventPlatformProxyV4);

        await transport.SendAsync(new object(), SerializerSettings);
        await transport.SendAsync(new object(), SerializerSettings);

        local.RequestsSent.Should().ContainSingle();
        direct.RequestsSent.Should().HaveCount(2);
    }

    [Theory]
    [MemberData(nameof(AmbiguousFailures))]
    public async Task AmbiguousLocalFailure_DoesNotReplayCurrentBatchButChangesFutureRoute(Exception failure)
    {
        var local = CreateFactory("http://agent:8126/", uri => new ThrowingApiRequest(uri, failure));
        var direct = CreateFactory("https://event-platform-intake.datadoghq.com/");
        using var transport = CreateTransport(local, direct, initialLocalProxyEndpoint: FeatureFlagsEvpTransport.EventPlatformProxyV4);

        await transport.SendAsync(new object(), SerializerSettings);
        direct.RequestsSent.Should().BeEmpty("an ambiguously failed payload may already have reached the local relay");

        await transport.SendAsync(new object(), SerializerSettings);

        local.RequestsSent.Should().ContainSingle();
        direct.RequestsSent.Should().ContainSingle("only the later payload is safe to send direct");
    }

    [Fact]
    public async Task DirectFailure_DoesNotLoopBackToLocalRoute()
    {
        var local = CreateFactory("http://agent:8126/");
        var direct = CreateFactory(
            "https://event-platform-intake.datadoghq.com/",
            uri => new ThrowingApiRequest(uri, new IOException("direct reset")));
        using var transport = CreateTransport(local, direct);

        await transport.SendAsync(new object(), SerializerSettings);

        direct.RequestsSent.Should().ContainSingle();
        local.RequestsSent.Should().BeEmpty();
    }

    [Fact]
    public void DirectCredentialsAreIsolatedFromLocalHeaders()
    {
        var headers = FeatureFlagsEvpTransport.GetDirectHeaders("test-api-key").ToDictionary(x => x.Key, x => x.Value);

        headers.Should().Contain("DD-API-KEY", "test-api-key");
        EventPlatformHeaderHelper.Instance.DefaultHeaders
                                 .Should().NotContain(pair => pair.Key == "DD-API-KEY");
    }

    [Fact]
    public void DirectFactoryUsesHttpsIntakeAndTheRuntimeDefaultProxy()
    {
        var settings = CreateSettings(
            (ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSource, "agentless"),
            (ConfigurationKeys.ApiKey, "test-api-key"),
            (ConfigurationKeys.Site, "datadoghq.eu"));

        var factory = FeatureFlagsEvpTransport.CreateDirectRequestFactory(settings.FeatureFlags);

        factory.Should().NotBeNull();
        factory!.GetEndpoint(FeatureFlagsEvpTransport.ExposureIntakePath)
                .Should().Be(new Uri("https://event-platform-intake.datadoghq.eu/api/v2/exposures"));
        factory.GetType().Name.Should().BeOneOf("HttpClientRequestFactory", "ApiWebRequestFactory");
    }

    [Fact]
    public async Task DisposeUnsubscribesFromDiscovery()
    {
        var discovery = new DiscoveryServiceMock();
        var transport = CreateTransport(CreateFactory("http://agent:8126/"), CreateFactory("https://direct/"), discovery);

        discovery.Callbacks.Should().ContainSingle();
        transport.Dispose();

        discovery.Callbacks.Should().BeEmpty();
        await transport.SendAsync(new object(), SerializerSettings);
    }

    private static FeatureFlagsEvpTransport CreateTransport(
        TestRequestFactory local,
        TestRequestFactory direct,
        DiscoveryServiceMock? discovery = null,
        string? initialLocalProxyEndpoint = null)
        => new(
            FeatureFlagsSource.Agentless,
            local,
            direct,
            discovery ?? new DiscoveryServiceMock(),
            initialLocalProxyEndpoint);

    private static TestRequestFactory CreateFactory(string baseEndpoint, params Func<Uri, TestApiRequest>[] requests)
        => new(new Uri(baseEndpoint), requests);

    private static TracerSettings CreateSettings(params (string Key, string Value)[] values)
    {
        var source = new NameValueCollection();
        foreach (var (key, value) in values)
        {
            source[key] = value;
        }

        return new TracerSettings(new NameValueConfigurationSource(source));
    }

    private sealed class ThrowingApiRequest(Uri endpoint, Exception exception) : TestApiRequest(endpoint)
    {
        public override Task<IApiResponse> PostAsJsonAsync<T>(T payload, MultipartCompression compression, JsonSerializerSettings settings)
            => Task.FromException<IApiResponse>(exception);
    }
}
