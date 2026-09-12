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
using Datadog.Trace.Telemetry;
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
        yield return [new TaskCanceledException("HTTP timeout")];
        yield return [new SocketException((int)SocketError.ConnectionReset)];
        yield return [new WebException("send failed", WebExceptionStatus.SendFailure)];
    }

    public static IEnumerable<object[]> DefinitivePreSendFailures()
    {
        yield return [new SocketException((int)SocketError.HostNotFound)];
        yield return [new SocketException((int)SocketError.TryAgain)];
        yield return [new SocketException((int)SocketError.ConnectionRefused)];
        yield return [new SocketException((int)SocketError.NetworkUnreachable)];
        yield return [new SocketException((int)SocketError.HostUnreachable)];
        yield return [new SocketException((int)SocketError.AddressNotAvailable)];
        yield return [new SocketException(10061)]; // Windows WSAECONNREFUSED
        yield return [new FileNotFoundException("missing Unix domain socket")];
        yield return [new WebException("refused", WebExceptionStatus.ConnectFailure)];
    }

    [Theory]
    [InlineData(FeatureFlagsEvpTransport.EventPlatformProxyV4, FeatureFlagsEvpTransport.ExposureIntakePath)]
    [InlineData(FeatureFlagsEvpTransport.EventPlatformProxyV4, FeatureFlagsEvpTransport.FlagEvaluationIntakePath)]
    [InlineData(FeatureFlagsEvpTransport.EventPlatformProxyV2, FeatureFlagsEvpTransport.ExposureIntakePath)]
    [InlineData(FeatureFlagsEvpTransport.EventPlatformProxyV2, FeatureFlagsEvpTransport.FlagEvaluationIntakePath)]
    public async Task DiscoverySelectsAdvertisedLocalRoute(string proxyEndpoint, string intakePath)
    {
        var local = CreateFactory("http://agent:8126/");
        var direct = CreateFactory("https://event-platform-intake.datadoghq.com/");
        var discovery = new DiscoveryServiceMock();
        using var transport = CreateTransport(local, direct, discovery);

        discovery.TriggerChange(eventPlatformProxyEndpoint: proxyEndpoint);
        await transport.SendAsync(new object(), intakePath, SerializerSettings);

        local.RequestsSent.Should().ContainSingle()
             .Which.Endpoint.AbsolutePath.Should().Be($"/{proxyEndpoint}/{intakePath}");
        direct.RequestsSent.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AgentlessLocalSelectionRequiresIdentityHeaderForwarding(bool supportsBothIdentityHeaders)
    {
        var local = CreateFactory("http://agent:8126/");
        var direct = CreateFactory("https://event-platform-intake.datadoghq.com/");
        var discovery = new DiscoveryServiceMock();
        using var transport = CreateTransport(local, direct, discovery);

        discovery.TriggerChange(
            eventPlatformProxyEndpoint: FeatureFlagsEvpTransport.EventPlatformProxyV4,
            eventPlatformProxySupportsEvpOriginHeaders: supportsBothIdentityHeaders);
        await transport.SendAsync(new object(), FeatureFlagsEvpTransport.ExposureIntakePath, SerializerSettings);

        if (supportsBothIdentityHeaders)
        {
            local.RequestsSent.Should().ContainSingle();
            direct.RequestsSent.Should().BeEmpty();
        }
        else
        {
            local.RequestsSent.Should().BeEmpty("the Agent cannot preserve the logical SDK identity");
            direct.RequestsSent.Should().ContainSingle();
        }
    }

    [Fact]
    public async Task ConcurrentInitialSendsShareOneDiscoverySubscription()
    {
        var local = CreateFactory("http://agent:8126/");
        var direct = CreateFactory("https://event-platform-intake.datadoghq.com/");
        var discovery = new DiscoveryServiceMock();
        using var transport = CreateTransport(local, direct, discovery, initialDiscoveryKnown: false);

        var exposure = transport.SendAsync(new object(), FeatureFlagsEvpTransport.ExposureIntakePath, SerializerSettings);
        var evaluation = transport.SendAsync(new object(), FeatureFlagsEvpTransport.FlagEvaluationIntakePath, SerializerSettings);

        discovery.Callbacks.Should().ContainSingle("the transport consumes the tracer's shared /info discovery stream");
        discovery.TriggerChange(eventPlatformProxyEndpoint: FeatureFlagsEvpTransport.EventPlatformProxyV4);
        await Task.WhenAll(exposure, evaluation);

        local.RequestsSent.Should().HaveCount(2);
        direct.RequestsSent.Should().BeEmpty();
    }

    [Fact]
    public async Task DirectRouteIsStickyAfterDiscoveryReportsNoCompatibleProxy()
    {
        var local = CreateFactory("http://agent:8126/");
        var direct = CreateFactory("https://event-platform-intake.datadoghq.com/");
        var discovery = new DiscoveryServiceMock();
        using var transport = CreateTransport(local, direct, discovery);

        discovery.TriggerChange(eventPlatformProxyEndpoint: "v0.4/traces");
        await transport.SendAsync(new object(), FeatureFlagsEvpTransport.ExposureIntakePath, SerializerSettings);

        discovery.TriggerChange(eventPlatformProxyEndpoint: FeatureFlagsEvpTransport.EventPlatformProxyV4);
        await transport.SendAsync(new object(), FeatureFlagsEvpTransport.FlagEvaluationIntakePath, SerializerSettings);

        local.RequestsSent.Should().BeEmpty();
        direct.RequestsSent.Select(r => r.Endpoint.AbsolutePath).Should().Equal(
            $"/{FeatureFlagsEvpTransport.ExposureIntakePath}",
            $"/{FeatureFlagsEvpTransport.FlagEvaluationIntakePath}");
    }

    [Fact]
    public async Task InitialDiscoveryTimeoutSelectsDirectAndStaysDirect()
    {
        var local = CreateFactory("http://agent:8126/");
        var direct = CreateFactory("https://event-platform-intake.datadoghq.com/");
        using var transport = CreateTransport(
            local,
            direct,
            initialDiscoveryKnown: false,
            initialDiscoveryWait: TimeSpan.FromMilliseconds(10));

        await transport.SendAsync(new object(), FeatureFlagsEvpTransport.ExposureIntakePath, SerializerSettings);
        await transport.SendAsync(new object(), FeatureFlagsEvpTransport.FlagEvaluationIntakePath, SerializerSettings);

        local.RequestsSent.Should().BeEmpty();
        direct.RequestsSent.Should().HaveCount(2);
    }

    [Fact]
    public async Task UnavailableRouteWarnsOnceAndRecoversWhenAgentAppears()
    {
        var warnings = new List<string>();
        var local = CreateFactory("http://agent:8126/");
        var discovery = new DiscoveryServiceMock();
        using var transport = CreateTransport(local, direct: null, discovery, warningSink: warnings.Add);

        await transport.SendAsync(new object(), FeatureFlagsEvpTransport.ExposureIntakePath, SerializerSettings);
        await transport.SendAsync(new object(), FeatureFlagsEvpTransport.ExposureIntakePath, SerializerSettings);

        warnings.Should().ContainSingle();
        local.RequestsSent.Should().BeEmpty();

        discovery.TriggerChange(eventPlatformProxyEndpoint: FeatureFlagsEvpTransport.EventPlatformProxyV4);
        await transport.SendAsync(new object(), FeatureFlagsEvpTransport.ExposureIntakePath, SerializerSettings);

        local.RequestsSent.Should().ContainSingle();
    }

    [Fact]
    public async Task FailedLocalRouteWithoutDirectCredentialsRecoversAfterCooldown()
    {
        var now = new DateTimeOffset(2026, 9, 12, 0, 0, 0, TimeSpan.Zero);
        var warnings = new List<string>();
        var local = CreateFactory(
            "http://agent:8126/",
            uri => new ThrowingApiRequest(uri, new IOException("ambiguous reset")),
            uri => new TestApiRequest(uri));
        using var transport = CreateTransport(
            local,
            direct: null,
            initialLocalProxyEndpoint: FeatureFlagsEvpTransport.EventPlatformProxyV4,
            routeRecoveryCooldown: TimeSpan.FromMinutes(1),
            utcNow: () => now,
            warningSink: warnings.Add);

        await transport.SendAsync(new object(), FeatureFlagsEvpTransport.ExposureIntakePath, SerializerSettings);
        await transport.SendAsync(new object(), FeatureFlagsEvpTransport.ExposureIntakePath, SerializerSettings);

        local.RequestsSent.Should().ContainSingle("flushes must not probe a failed route during cooldown");
        warnings.Should().ContainSingle();

        now = now.AddMinutes(1);
        await transport.SendAsync(new object(), FeatureFlagsEvpTransport.ExposureIntakePath, SerializerSettings);
        await transport.SendAsync(new object(), FeatureFlagsEvpTransport.ExposureIntakePath, SerializerSettings);

        local.RequestsSent.Should().HaveCount(3, "one post-cooldown probe restores normal local delivery");
    }

    [Fact]
    public async Task ConcurrentFlushesShareOnePostCooldownRecoveryProbe()
    {
        var now = new DateTimeOffset(2026, 9, 12, 0, 0, 0, TimeSpan.Zero);
        var probeStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseProbe = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var local = CreateFactory(
            "http://agent:8126/",
            uri => new ThrowingApiRequest(uri, new IOException("ambiguous reset")),
            uri => new BlockingApiRequest(uri, probeStarted, releaseProbe));
        using var transport = CreateTransport(
            local,
            direct: null,
            initialLocalProxyEndpoint: FeatureFlagsEvpTransport.EventPlatformProxyV4,
            routeRecoveryCooldown: TimeSpan.FromMinutes(1),
            utcNow: () => now);

        await transport.SendAsync(new object(), FeatureFlagsEvpTransport.ExposureIntakePath, SerializerSettings);
        now = now.AddMinutes(1);

        var probe = transport.SendAsync(new object(), FeatureFlagsEvpTransport.ExposureIntakePath, SerializerSettings);
        (await Task.WhenAny(probeStarted.Task, Task.Delay(TimeSpan.FromSeconds(1))))
           .Should().BeSameAs(probeStarted.Task);

        await transport.SendAsync(new object(), FeatureFlagsEvpTransport.FlagEvaluationIntakePath, SerializerSettings);
        local.RequestsSent.Should().HaveCount(2, "a concurrent flush must not start a second recovery probe");

        releaseProbe.TrySetResult(true);
        await probe;
        await transport.SendAsync(new object(), FeatureFlagsEvpTransport.FlagEvaluationIntakePath, SerializerSettings);

        local.RequestsSent.Should().HaveCount(3, "a successful probe restores normal local delivery");
    }

    [Theory]
    [InlineData(404)]
    [InlineData(405)]
    public async Task UnsupportedLocalRouteWithoutDirectCredentialsEntersCooldown(int statusCode)
    {
        var now = new DateTimeOffset(2026, 9, 12, 0, 0, 0, TimeSpan.Zero);
        var local = CreateFactory(
            "http://agent:8126/",
            uri => new TestApiRequest(uri, statusCode),
            uri => new TestApiRequest(uri));
        using var transport = CreateTransport(
            local,
            direct: null,
            initialLocalProxyEndpoint: FeatureFlagsEvpTransport.EventPlatformProxyV4,
            routeRecoveryCooldown: TimeSpan.FromMinutes(1),
            utcNow: () => now);

        await transport.SendAsync(new object(), FeatureFlagsEvpTransport.ExposureIntakePath, SerializerSettings);
        await transport.SendAsync(new object(), FeatureFlagsEvpTransport.ExposureIntakePath, SerializerSettings);

        local.RequestsSent.Should().ContainSingle();

        now = now.AddMinutes(1);
        await transport.SendAsync(new object(), FeatureFlagsEvpTransport.ExposureIntakePath, SerializerSettings);

        local.RequestsSent.Should().HaveCount(2);
    }

    [Fact]
    public async Task RemoteConfigurationAlwaysUsesHistoricalV2WithoutDiscoveryOrDirectCredentials()
    {
        var local = CreateFactory(
            "http://agent:8126/",
            uri => new TestApiRequest(uri, statusCode: 404),
            uri => new ThrowingApiRequest(uri, new SocketException((int)SocketError.ConnectionRefused)),
            uri => new TestApiRequest(uri));
        var direct = CreateFactory("https://event-platform-intake.datadoghq.com/");
        var discovery = new DiscoveryServiceMock();
        using var transport = new FeatureFlagsEvpTransport(FeatureFlagsSource.RemoteConfig, local, direct, discovery);

        discovery.Callbacks.Should().BeEmpty("Remote Config must not change its historical transport contract");
        discovery.TriggerChange(eventPlatformProxyEndpoint: FeatureFlagsEvpTransport.EventPlatformProxyV4);

        await transport.SendAsync(new object(), FeatureFlagsEvpTransport.ExposureIntakePath, SerializerSettings);
        await transport.SendAsync(new object(), FeatureFlagsEvpTransport.ExposureIntakePath, SerializerSettings);
        await transport.SendAsync(new object(), FeatureFlagsEvpTransport.ExposureIntakePath, SerializerSettings);

        local.RequestsSent.Should().HaveCount(3);
        local.RequestsSent.Should().OnlyContain(
            request => request.Endpoint.AbsolutePath == $"/{FeatureFlagsEvpTransport.EventPlatformProxyV2}/{FeatureFlagsEvpTransport.ExposureIntakePath}");
        direct.RequestsSent.Should().BeEmpty();
    }

    [Fact]
    public void RemoteConfigurationProductionConstructionDoesNotSubscribeToDiscovery()
    {
        var settings = CreateSettings(
            (ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSource, "remote_config"),
            (ConfigurationKeys.ApiKey, "must-not-be-used"));
        var discovery = new DiscoveryServiceMock();

        using var transport = new FeatureFlagsEvpTransport(settings, discovery);

        discovery.Callbacks.Should().BeEmpty();
    }

    [Theory]
    [InlineData(404)]
    [InlineData(405)]
    public async Task UnsupportedLocalRouteReplaysCurrentBatchDirectAndStaysDirect(int statusCode)
    {
        var local = CreateFactory("http://agent:8126/", uri => new TestApiRequest(uri, statusCode));
        var direct = CreateFactory("https://event-platform-intake.datadoghq.com/");
        using var transport = CreateTransport(local, direct, initialLocalProxyEndpoint: FeatureFlagsEvpTransport.EventPlatformProxyV4);

        await transport.SendAsync(new object(), FeatureFlagsEvpTransport.ExposureIntakePath, SerializerSettings);
        await transport.SendAsync(new object(), FeatureFlagsEvpTransport.FlagEvaluationIntakePath, SerializerSettings);

        local.RequestsSent.Should().ContainSingle();
        direct.RequestsSent.Should().HaveCount(2, "the safe-to-replay batch and later batches use direct intake");
    }

    [Theory]
    [InlineData(403)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(503)]
    public async Task PotentiallyForwardedOrRetryableHttpFailureDoesNotFallback(int statusCode)
    {
        var local = CreateFactory(
            "http://agent:8126/",
            uri => new TestApiRequest(uri, statusCode),
            uri => new TestApiRequest(uri));
        var direct = CreateFactory("https://event-platform-intake.datadoghq.com/");
        using var transport = CreateTransport(local, direct, initialLocalProxyEndpoint: FeatureFlagsEvpTransport.EventPlatformProxyV4);

        await transport.SendAsync(new object(), FeatureFlagsEvpTransport.ExposureIntakePath, SerializerSettings);
        await transport.SendAsync(new object(), FeatureFlagsEvpTransport.FlagEvaluationIntakePath, SerializerSettings);

        local.RequestsSent.Should().HaveCount(2);
        direct.RequestsSent.Should().BeEmpty("the SDK must not replay a batch that the relay may have accepted");
    }

    [Theory]
    [MemberData(nameof(DefinitivePreSendFailures))]
    public async Task DefinitivePreSendFailureReplaysCurrentBatchDirectAndStaysDirect(Exception failure)
    {
        var local = CreateFactory("http://agent:8126/", uri => new ThrowingApiRequest(uri, failure));
        var direct = CreateFactory("https://event-platform-intake.datadoghq.com/");
        using var transport = CreateTransport(local, direct, initialLocalProxyEndpoint: FeatureFlagsEvpTransport.EventPlatformProxyV4);

        await transport.SendAsync(new object(), FeatureFlagsEvpTransport.ExposureIntakePath, SerializerSettings);
        await transport.SendAsync(new object(), FeatureFlagsEvpTransport.FlagEvaluationIntakePath, SerializerSettings);

        local.RequestsSent.Should().ContainSingle();
        direct.RequestsSent.Should().HaveCount(2);
    }

    [Theory]
    [MemberData(nameof(AmbiguousFailures))]
    public async Task AmbiguousLocalFailureChangesOnlyFutureBatchesToDirect(Exception failure)
    {
        var local = CreateFactory("http://agent:8126/", uri => new ThrowingApiRequest(uri, failure));
        var direct = CreateFactory("https://event-platform-intake.datadoghq.com/");
        using var transport = CreateTransport(local, direct, initialLocalProxyEndpoint: FeatureFlagsEvpTransport.EventPlatformProxyV4);

        await transport.SendAsync(new object(), FeatureFlagsEvpTransport.ExposureIntakePath, SerializerSettings);
        direct.RequestsSent.Should().BeEmpty("an ambiguously failed batch may already have reached the relay");

        await transport.SendAsync(new object(), FeatureFlagsEvpTransport.FlagEvaluationIntakePath, SerializerSettings);

        local.RequestsSent.Should().ContainSingle();
        direct.RequestsSent.Should().ContainSingle("only a later batch is safe to send direct");
    }

    [Fact]
    public async Task DirectFailureNeverLoopsBackToAgent()
    {
        var local = CreateFactory("http://agent:8126/");
        var direct = CreateFactory(
            "https://event-platform-intake.datadoghq.com/",
            uri => new ThrowingApiRequest(uri, new IOException("direct reset")));
        using var transport = CreateTransport(local, direct);

        await transport.SendAsync(new object(), FeatureFlagsEvpTransport.FlagEvaluationIntakePath, SerializerSettings);

        direct.RequestsSent.Should().ContainSingle();
        local.RequestsSent.Should().BeEmpty();
    }

    [Theory]
    [InlineData(FeatureFlagsEvpTransport.EventPlatformProxyV4, false)]
    [InlineData(FeatureFlagsEvpTransport.EventPlatformProxyV2, false)]
    [InlineData(null, true)]
    public async Task CompressedPayloadUsesTheSelectedRoute(string? proxyEndpoint, bool usesDirect)
    {
        var local = CreateFactory("http://agent:8126/", uri => new RecordingCompressedApiRequest(uri));
        var direct = CreateFactory("https://event-platform-intake.datadoghq.com/", uri => new RecordingCompressedApiRequest(uri));
        var discovery = new DiscoveryServiceMock();
        using var transport = CreateTransport(local, direct, discovery);

        discovery.TriggerChange(eventPlatformProxyEndpoint: proxyEndpoint ?? "v0.4/traces");
        var payload = new byte[] { 1, 2, 3 };
        await transport.SendCompressedAsync(new ArraySegment<byte>(payload), FeatureFlagsEvpTransport.FlagEvaluationIntakePath);

        var selectedFactory = usesDirect ? direct : local;
        var request = selectedFactory.RequestsSent.Should().ContainSingle().Which.Should().BeOfType<RecordingCompressedApiRequest>().Subject;
        request.Endpoint.AbsolutePath.Should().Be(
            usesDirect
                ? $"/{FeatureFlagsEvpTransport.FlagEvaluationIntakePath}"
                : $"/{proxyEndpoint}/{FeatureFlagsEvpTransport.FlagEvaluationIntakePath}");
        request.Payload.Should().Equal(payload);
        request.ContentType.Should().Be(MimeTypes.Json);
        request.ContentEncoding.Should().Be("gzip");
    }

    [Fact]
    public void DirectAndLocalHeadersHaveExactIdentityAndCredentialIsolation()
    {
        var directHeaders = FeatureFlagsEvpTransport.GetDirectHeaders("test-api-key").ToDictionary(x => x.Key, x => x.Value);
        var localHeaders = FeatureFlagsEvpHeaderHelper.Instance.DefaultHeaders.ToDictionary(x => x.Key, x => x.Value);

        directHeaders.Should().HaveCount(3);
        directHeaders.Should().Contain(TelemetryConstants.ApiKeyHeader, "test-api-key");
        directHeaders.Should().Contain(FeatureFlagsEvpHeaderHelper.EvpOriginHeader, FeatureFlagsEvpHeaderHelper.EvpOrigin);
        directHeaders.Should().Contain(FeatureFlagsEvpHeaderHelper.EvpOriginVersionHeader, TracerConstants.ThreePartVersion);
        directHeaders.Should().NotContainKey(FeatureFlagsEvpHeaderHelper.EvpSubdomainHeader);

        localHeaders.Should().Contain(FeatureFlagsEvpHeaderHelper.EvpSubdomainHeader, FeatureFlagsEvpHeaderHelper.EvpSubdomain);
        localHeaders.Should().Contain(FeatureFlagsEvpHeaderHelper.EvpOriginHeader, FeatureFlagsEvpHeaderHelper.EvpOrigin);
        localHeaders.Should().Contain(FeatureFlagsEvpHeaderHelper.EvpOriginVersionHeader, TracerConstants.ThreePartVersion);
        localHeaders.Should().NotContainKey(TelemetryConstants.ApiKeyHeader);
    }

    [Theory]
    [InlineData("http://agent:8126/base/path/", "http://agent:8126/base/path/evp_proxy/v4/api/v2/exposures")]
    [InlineData("https://agent:8126/base/path/", "https://agent:8126/base/path/evp_proxy/v4/api/v2/exposures")]
    public void LocalHttpFactoryPreservesSchemeAndBasePath(string agentUrl, string expectedEndpoint)
    {
        var settings = CreateSettings((ConfigurationKeys.AgentUri, agentUrl));

        var factory = FeatureFlagsEvpTransport.CreateLocalRequestFactory(settings.Manager.InitialExporterSettings);

        factory.GetEndpoint($"{FeatureFlagsEvpTransport.EventPlatformProxyV4}/{FeatureFlagsEvpTransport.ExposureIntakePath}")
               .Should().Be(new Uri(expectedEndpoint));
    }

#if NETCOREAPP3_1_OR_GREATER
    [Fact]
    public void LocalUnixDomainSocketFactoryBuildsEvpRouteOnStreamEndpoint()
    {
        var settings = CreateSettings((ConfigurationKeys.AgentUri, "unix:///tmp/dd-apm-test.socket"));

        var factory = FeatureFlagsEvpTransport.CreateLocalRequestFactory(settings.Manager.InitialExporterSettings);

        factory.GetEndpoint($"{FeatureFlagsEvpTransport.EventPlatformProxyV4}/{FeatureFlagsEvpTransport.ExposureIntakePath}")
               .Should().Be(new Uri("http://localhost/evp_proxy/v4/api/v2/exposures"));
    }
#endif

    [Fact]
    public void LocalNamedPipeFactoryBuildsEvpRouteOnStreamEndpoint()
    {
        var settings = CreateSettings((ConfigurationKeys.TracesPipeName, "dd-apm-test-pipe"));

        var factory = FeatureFlagsEvpTransport.CreateLocalRequestFactory(settings.Manager.InitialExporterSettings);

        factory.GetEndpoint($"{FeatureFlagsEvpTransport.EventPlatformProxyV2}/{FeatureFlagsEvpTransport.FlagEvaluationIntakePath}")
               .Should().Be(new Uri("http://localhost/evp_proxy/v2/api/v2/flagevaluation"));
    }

    [Theory]
    [InlineData("datadoghq.eu", "https://event-platform-intake.datadoghq.eu/")]
    [InlineData(" DATADOGHQ.COM ", "https://event-platform-intake.datadoghq.com/")]
    public void DirectFactoryUsesNormalizedHttpsIntakeAndDisablesRedirects(string site, string expectedBase)
    {
        var settings = CreateSettings(
            (ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSource, "agentless"),
            (ConfigurationKeys.ApiKey, "test-api-key"),
            (ConfigurationKeys.Site, site));

        var factory = FeatureFlagsEvpTransport.CreateDirectRequestFactory(settings.FeatureFlags);

        factory.Should().NotBeNull();
        factory!.GetEndpoint(FeatureFlagsEvpTransport.ExposureIntakePath)
                .Should().Be(new Uri(expectedBase + FeatureFlagsEvpTransport.ExposureIntakePath));
        factory.GetEndpoint(FeatureFlagsEvpTransport.FlagEvaluationIntakePath)
               .Should().Be(new Uri(expectedBase + FeatureFlagsEvpTransport.FlagEvaluationIntakePath));
#if NETCOREAPP3_1_OR_GREATER
        factory.Should().BeOfType<HttpClientRequestFactory>()
               .Which.AllowAutoRedirect.Should().BeFalse("DD-API-KEY must never follow an intake redirect");
#else
        factory.Should().BeOfType<ApiWebRequestFactory>()
               .Which.AllowAutoRedirect.Should().BeFalse("DD-API-KEY must never follow an intake redirect");
#endif
    }

    [Fact]
    public void DirectFactoryUsesDefaultSiteWhenDdSiteIsUnset()
    {
        var settings = CreateSettings(
            (ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSource, "agentless"),
            (ConfigurationKeys.ApiKey, "test-api-key"));

        var factory = FeatureFlagsEvpTransport.CreateDirectRequestFactory(settings.FeatureFlags);

        factory.Should().NotBeNull();
        factory!.GetEndpoint(FeatureFlagsEvpTransport.ExposureIntakePath)
                .Should().Be(new Uri("https://event-platform-intake.datadoghq.com/api/v2/exposures"));
    }

    [Theory]
    [InlineData("datadoghq.com@attacker.example")]
    [InlineData("https://attacker.example")]
    [InlineData("datadoghq.com/path")]
    [InlineData("datadoghq.com?redirect=attacker.example")]
    [InlineData("datadoghq.com#attacker.example")]
    [InlineData("datadoghq.com:443")]
    [InlineData("datadoghq.com\\attacker.example")]
    [InlineData("data doghq.com")]
    [InlineData("-datadoghq.com")]
    [InlineData("datadoghq.com-")]
    [InlineData("datadoghq..com")]
    [InlineData("dátadoghq.com")]
    public void DirectFactoryRejectsSiteThatIsNotAnAsciiDnsSuffix(string site)
    {
        var settings = CreateSettings(
            (ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSource, "agentless"),
            (ConfigurationKeys.ApiKey, "test-api-key"),
            (ConfigurationKeys.Site, site));

        FeatureFlagsEvpTransport.CreateDirectRequestFactory(settings.FeatureFlags).Should().BeNull();
    }

    [Fact]
    public void InvalidSiteWarningIsGenericAndEmittedOnce()
    {
        const string Site = "secret@attacker.example";
        const string ApiKey = "secret-api-key";
        var warnings = new List<string>();
        var settings = CreateSettings(
            (ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSource, "agentless"),
            (ConfigurationKeys.ApiKey, ApiKey),
            (ConfigurationKeys.Site, Site));

        using var transport = new FeatureFlagsEvpTransport(settings, new DiscoveryServiceMock(), warnings.Add);

        warnings.Should().ContainSingle();
        warnings[0].Should().NotContain(Site).And.NotContain(ApiKey);
    }

    [Fact]
    public async Task DisposeUnsubscribesAndUnblocksInitialDiscoveryWait()
    {
        var local = CreateFactory("http://agent:8126/");
        var direct = CreateFactory("https://event-platform-intake.datadoghq.com/");
        var discovery = new DiscoveryServiceMock();
        var transport = CreateTransport(
            local,
            direct,
            discovery,
            initialDiscoveryKnown: false,
            initialDiscoveryWait: TimeSpan.FromMinutes(1));

        var send = transport.SendAsync(new object(), FeatureFlagsEvpTransport.ExposureIntakePath, SerializerSettings);
        discovery.Callbacks.Should().ContainSingle();

        transport.Dispose();

        discovery.Callbacks.Should().BeEmpty();
        (await Task.WhenAny(send, Task.Delay(TimeSpan.FromSeconds(1)))).Should().BeSameAs(send);
        await send;
        local.RequestsSent.Should().BeEmpty();
        direct.RequestsSent.Should().BeEmpty();
    }

    private static FeatureFlagsEvpTransport CreateTransport(
        TestRequestFactory local,
        TestRequestFactory? direct,
        DiscoveryServiceMock? discovery = null,
        string? initialLocalProxyEndpoint = null,
        bool initialDiscoveryKnown = true,
        TimeSpan? initialDiscoveryWait = null,
        TimeSpan? routeRecoveryCooldown = null,
        Func<DateTimeOffset>? utcNow = null,
        Action<string>? warningSink = null)
        => new(
            FeatureFlagsSource.Agentless,
            local,
            direct,
            discovery ?? new DiscoveryServiceMock(),
            initialLocalProxyEndpoint,
            initialDiscoveryKnown,
            initialDiscoveryWait,
            routeRecoveryCooldown,
            utcNow,
            warningSink);

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

    private sealed class BlockingApiRequest(
        Uri endpoint,
        TaskCompletionSource<bool> sendStarted,
        TaskCompletionSource<bool> releaseSend) : TestApiRequest(endpoint)
    {
        public override async Task<IApiResponse> PostAsJsonAsync<T>(T payload, MultipartCompression compression, JsonSerializerSettings settings)
        {
            sendStarted.TrySetResult(true);
            await releaseSend.Task.ConfigureAwait(false);
            return await base.PostAsJsonAsync(payload, compression, settings).ConfigureAwait(false);
        }
    }

    private sealed class RecordingCompressedApiRequest(Uri endpoint) : TestApiRequest(endpoint)
    {
        public byte[] Payload { get; private set; } = [];

        public string ContentEncoding { get; private set; } = string.Empty;

        public override Task<IApiResponse> PostAsync(ArraySegment<byte> bytes, string contentType, string contentEncoding)
        {
            Payload = bytes.ToArray();
            ContentEncoding = contentEncoding;
            return base.PostAsync(bytes, contentType, contentEncoding);
        }
    }
}
