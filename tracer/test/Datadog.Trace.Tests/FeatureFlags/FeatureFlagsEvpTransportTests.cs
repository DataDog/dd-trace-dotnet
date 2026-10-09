// <copyright file="FeatureFlagsEvpTransportTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.FeatureFlags;
using Datadog.Trace.FeatureFlags.Evp;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.Logging;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Telemetry;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.TransportHelpers;
using Datadog.Trace.Tests.Agent;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.FeatureFlags;

[Collection(nameof(WebRequestCollection))]
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
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    public async Task RuntimeEndpointChangeRequiresCapabilitiesFromReplacementAgent(bool discoverBeforeTransportUpdate, bool replacementSupportsIdentity, bool sendBeforeUpdate)
    {
        const string CapableInfo = "{\"endpoints\":[\"evp_proxy/v4\"],\"evp_proxy_allowed_headers\":[\"DD-EVP-ORIGIN\",\"DD-EVP-ORIGIN-VERSION\"]}";
        using var replacement = new HttpListener();
        var replacementUrl = $"http://127.0.0.1:{TcpPortProvider.GetOpenPort()}/";
        replacement.Prefixes.Add(replacementUrl);
        replacement.Start();
        var received = replacement.GetContextAsync();
        var settings = CreateSettings(
            (ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSource, "agentless"),
            (ConfigurationKeys.AgentUri, "http://old-agent:8126/"));
        var factoryA = CreateFactory("http://old-agent:8126/", uri => new TestApiRequest(uri, responseContent: CapableInfo));
        var factoryB = CreateFactory(replacementUrl, uri => new TestApiRequest(uri, responseContent: replacementSupportsIdentity ? CapableInfo : "{\"endpoints\":[\"evp_proxy/v4\"]}"));
        await using var discovery = new DiscoveryService(factoryA, new ServiceRemappingHash(null), 1, 1, 30_000, autoStartLoop: false, exporterSettings: settings.Manager.InitialExporterSettings);
        // Mirrors the production subscription order: shared discovery subscribes before EVP.
        using var discoverySettings = settings.Manager.SubscribeToChanges(changes =>
        {
            if (changes.UpdatedExporter is { } exporter)
            {
                discovery.UpdateRequestFactory(factoryB, exporter);
                if (discoverBeforeTransportUpdate)
                {
                    discovery.RunOneIterationAsync(null).GetAwaiter().GetResult();
                }
            }
        });
        using var transport = new FeatureFlagsEvpTransport(settings, discovery);
        Task? send = null;
        if (sendBeforeUpdate)
        {
            send = transport.SendAsync(new object(), FeatureFlagsEvpTransport.ExposureIntakePath, SerializerSettings);
            send.IsCompleted.Should().BeFalse();
        }
        else
        {
            await discovery.RunOneIterationAsync(null);
        }

        try
        {
            settings.Manager.UpdateManualConfigurationSettings(
                new ManualInstrumentationConfigurationSource(new Dictionary<string, object?> { { TracerSettingKeyConstants.AgentUriKey, new Uri(replacementUrl) } }, useDefaultSources: true),
                NullConfigurationTelemetry.Instance).Should().BeTrue();
            send ??= transport.SendAsync(new object(), FeatureFlagsEvpTransport.ExposureIntakePath, SerializerSettings);
            if (!discoverBeforeTransportUpdate)
            {
                if (sendBeforeUpdate)
                {
                    // Give the waiter released from A a chance to run before publishing B.
                    var stillWaiting = Task.Delay(TimeSpan.FromMilliseconds(100));
                    (await Task.WhenAny(send, stillWaiting)).Should().BeSameAs(stillWaiting, "replacing A must not abandon the current batch while B discovery is pending");
                }

                send.IsCompleted.Should().BeFalse("new endpoint discovery is still pending");
                await discovery.RunOneIterationAsync(null);
            }

            if (replacementSupportsIdentity)
            {
                (await Task.WhenAny(received, Task.Delay(TimeSpan.FromSeconds(5)))).Should().BeSameAs(received);
                var request = await received;
                request.Request.Url!.AbsolutePath.Should().Be("/evp_proxy/v4/api/v2/exposures");
                request.Request.Headers[TelemetryConstants.ApiKeyHeader].Should().BeNull();
                await request.Request.InputStream.CopyToAsync(Stream.Null);
                request.Response.StatusCode = 200;
                request.Response.Close();
            }

            (await Task.WhenAny(send, Task.Delay(TimeSpan.FromSeconds(6)))).Should().BeSameAs(send);
            await send;
            received.IsCompleted.Should().Be(replacementSupportsIdentity, "the old Agent's capabilities cannot authorize an upload to the new Agent");
            factoryB.RequestsSent.Should().ContainSingle("an equal capability body still validates the new endpoint");
        }
        finally
        {
            replacement.Close();
            try
            {
                await received;
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException)
            {
            }
        }
    }

    [Theory]
    [InlineData(200)]
    [InlineData(503)]
    public async Task LateOldAgentResponseDoesNotSuppressReplacementAgent(int oldStatus)
    {
        const string CapableInfo = "{\"endpoints\":[\"evp_proxy/v4\"],\"evp_proxy_allowed_headers\":[\"DD-EVP-ORIGIN\",\"DD-EVP-ORIGIN-VERSION\"]}";
        using var oldAgent = new HttpListener();
        using var replacement = new HttpListener();
        var oldUrl = $"http://127.0.0.1:{TcpPortProvider.GetOpenPort()}/";
        var replacementUrl = $"http://127.0.0.1:{TcpPortProvider.GetOpenPort()}/";
        oldAgent.Prefixes.Add(oldUrl);
        replacement.Prefixes.Add(replacementUrl);
        oldAgent.Start();
        replacement.Start();
        var settings = CreateSettings(
            (ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSource, "agentless"),
            (ConfigurationKeys.AgentUri, oldUrl));
        var factoryA = CreateFactory(oldUrl, uri => new TestApiRequest(uri, responseContent: CapableInfo));
        var factoryB = CreateFactory(replacementUrl, uri => new TestApiRequest(uri, responseContent: CapableInfo));
        await using var discovery = new DiscoveryService(factoryA, new ServiceRemappingHash(null), 1, 1, 30_000, autoStartLoop: false, exporterSettings: settings.Manager.InitialExporterSettings);
        using var discoverySettings = settings.Manager.SubscribeToChanges(changes =>
        {
            if (changes.UpdatedExporter is { } exporter)
            {
                discovery.UpdateRequestFactory(factoryB, exporter);
            }
        });
        using var transport = new FeatureFlagsEvpTransport(settings, discovery);
        await discovery.RunOneIterationAsync(null);
        var receivedOld = oldAgent.GetContextAsync();
        var sendOld = transport.SendAsync(new { Batch = "old" }, FeatureFlagsEvpTransport.ExposureIntakePath, SerializerSettings);
        (await Task.WhenAny(receivedOld, Task.Delay(TimeSpan.FromSeconds(3)))).Should().BeSameAs(receivedOld);
        var oldRequest = await receivedOld;
        await oldRequest.Request.InputStream.CopyToAsync(Stream.Null);
        var receivedReplacement = replacement.GetContextAsync();

        try
        {
            settings.Manager.UpdateManualConfigurationSettings(
                new ManualInstrumentationConfigurationSource(new Dictionary<string, object?> { { TracerSettingKeyConstants.AgentUriKey, new Uri(replacementUrl) } }, useDefaultSources: true),
                NullConfigurationTelemetry.Instance).Should().BeTrue();
            await discovery.RunOneIterationAsync(null);
            oldRequest.Response.StatusCode = oldStatus;
            oldRequest.Response.Close();
            (await Task.WhenAny(sendOld, Task.Delay(TimeSpan.FromSeconds(3)))).Should().BeSameAs(sendOld);
            await sendOld;

            var sendNew = transport.SendAsync(new { Batch = "new" }, FeatureFlagsEvpTransport.ExposureIntakePath, SerializerSettings);
            (await Task.WhenAny(receivedReplacement, Task.Delay(TimeSpan.FromSeconds(3))))
               .Should().BeSameAs(receivedReplacement, "a late response from the old Agent must not suppress the validated replacement");
            var request = await receivedReplacement;
            request.Request.Url!.AbsolutePath.Should().Be("/evp_proxy/v4/api/v2/exposures");
            request.Request.Headers[TelemetryConstants.ApiKeyHeader].Should().BeNull();
            await request.Request.InputStream.CopyToAsync(Stream.Null);
            request.Response.StatusCode = 200;
            request.Response.Close();
            (await Task.WhenAny(sendNew, Task.Delay(TimeSpan.FromSeconds(3)))).Should().BeSameAs(sendNew);
            await sendNew;
        }
        finally
        {
            oldRequest.Response.Close();
            replacement.Close();
            try
            {
                await receivedReplacement;
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException)
            {
            }
        }
    }

    [Theory]
    [InlineData(FeatureFlagsEvpTransport.EventPlatformProxyV4, FeatureFlagsEvpTransport.ExposureIntakePath)]
    [InlineData(FeatureFlagsEvpTransport.EventPlatformProxyV4, FeatureFlagsEvpTransport.FlagEvaluationIntakePath)]
    [InlineData(FeatureFlagsEvpTransport.EventPlatformProxyV2, FeatureFlagsEvpTransport.ExposureIntakePath)]
    [InlineData(FeatureFlagsEvpTransport.EventPlatformProxyV2, FeatureFlagsEvpTransport.FlagEvaluationIntakePath)]
    [InlineData("EVP_PROXY/V4", FeatureFlagsEvpTransport.ExposureIntakePath)]
    [InlineData("Evp_Proxy/V2", FeatureFlagsEvpTransport.FlagEvaluationIntakePath)]
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
    public async Task PotentiallyForwardedOrRetryableHttpFailureChangesOnlyFutureBatchesToDirect(int statusCode)
    {
        var local = CreateFactory(
            "http://agent:8126/",
            uri => new TestApiRequest(uri, statusCode));
        var direct = CreateFactory("https://event-platform-intake.datadoghq.com/");
        using var transport = CreateTransport(local, direct, initialLocalProxyEndpoint: FeatureFlagsEvpTransport.EventPlatformProxyV4);

        await transport.SendAsync(new object(), FeatureFlagsEvpTransport.ExposureIntakePath, SerializerSettings);
        await transport.SendAsync(new object(), FeatureFlagsEvpTransport.FlagEvaluationIntakePath, SerializerSettings);

        local.RequestsSent.Should().ContainSingle();
        direct.RequestsSent.Should().ContainSingle("the failed batch is not replayed, but the next batch uses direct intake");
    }

    [Theory]
    [InlineData(403)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(503)]
    public async Task PotentiallyForwardedOrRetryableHttpFailureWithoutDirectCredentialsEntersCooldown(int statusCode)
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
        await transport.SendAsync(new object(), FeatureFlagsEvpTransport.FlagEvaluationIntakePath, SerializerSettings);

        local.RequestsSent.Should().ContainSingle("the failed route remains unavailable during cooldown");

        now = now.AddMinutes(1);
        await transport.SendAsync(new object(), FeatureFlagsEvpTransport.FlagEvaluationIntakePath, SerializerSettings);

        local.RequestsSent.Should().HaveCount(2, "one post-cooldown local probe restores delivery");
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

    [Fact]
    public void DirectAndLocalHeadersHaveExactIdentityAndCredentialIsolation()
    {
        var directHeaders = FeatureFlagsEvpTransport.GetDirectHeaders("test-api-key").ToDictionary(x => x.Key, x => x.Value);
        var localHeaders = FeatureFlagsEvpHeaderHelper.Instance.DefaultHeaders.ToDictionary(x => x.Key, x => x.Value);

        directHeaders.Should().HaveCount(4);
        directHeaders.Should().Contain(HttpHeaderNames.TracingEnabled, "false");
        directHeaders.Should().Contain(TelemetryConstants.ApiKeyHeader, "test-api-key");
        directHeaders.Should().Contain(FeatureFlagsEvpHeaderHelper.EvpOriginHeader, FeatureFlagsEvpHeaderHelper.EvpOrigin);
        directHeaders.Should().Contain(FeatureFlagsEvpHeaderHelper.EvpOriginVersionHeader, TracerConstants.ThreePartVersion);
        directHeaders.Should().NotContainKey(FeatureFlagsEvpHeaderHelper.EvpSubdomainHeader);

        localHeaders.Should().Contain(FeatureFlagsEvpHeaderHelper.EvpSubdomainHeader, FeatureFlagsEvpHeaderHelper.EvpSubdomain);
        localHeaders.Should().Contain(FeatureFlagsEvpHeaderHelper.EvpOriginHeader, FeatureFlagsEvpHeaderHelper.EvpOrigin);
        localHeaders.Should().Contain(FeatureFlagsEvpHeaderHelper.EvpOriginVersionHeader, TracerConstants.ThreePartVersion);
        localHeaders.Should().NotContainKey(TelemetryConstants.ApiKeyHeader);
        localHeaders.Should().Contain(HttpHeaderNames.TracingEnabled, "false");
    }

    [Fact]
    public async Task DisabledDiscoveryDoesNotDelayDirectDelivery()
    {
        var local = CreateFactory("http://agent:8126/");
        var direct = CreateFactory("https://event-platform-intake.datadoghq.com/");
        using var transport = new FeatureFlagsEvpTransport(
            FeatureFlagsSource.Agentless,
            local,
            direct,
            NullDiscoveryService.Instance,
            initialDiscoveryKnown: false,
            initialDiscoveryWait: TimeSpan.FromMinutes(1));

        var send = transport.SendAsync(new object(), FeatureFlagsEvpTransport.ExposureIntakePath, SerializerSettings);

        send.IsCompleted.Should().BeTrue("disabled discovery cannot produce a callback, and the test sender completes synchronously");
        await send;
        direct.RequestsSent.Should().ContainSingle();
        local.RequestsSent.Should().BeEmpty();
    }

    [Fact]
    public async Task ProductionConstructionDoesNotWaitForDisabledDiscovery()
    {
        var settings = CreateSettings((ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSource, "agentless"));
        using var transport = new FeatureFlagsEvpTransport(settings, NullDiscoveryService.Instance);

        var send = transport.SendAsync(new object(), FeatureFlagsEvpTransport.ExposureIntakePath, SerializerSettings);

        send.IsCompleted.Should().BeTrue("there is no discovery loop or direct credential to wait for");
        await send;
    }

    [Theory]
    [InlineData(400)]
    [InlineData(403)]
    [InlineData(429)]
    [InlineData(500)]
    public async Task DirectTerminalFailureLogsSafeErrorContextWithoutRetry(int status)
    {
        var logger = new Mock<IDatadogLogger>();
        var direct = CreateFactory("https://secret@event-platform-intake.datadoghq.com/", uri => new TestApiRequest(uri, statusCode: status, responseContent: "secret-response"));
        var local = CreateFactory("http://agent:8126/");
        using var transport = new FeatureFlagsEvpTransport(
            FeatureFlagsSource.Agentless, local, direct, NullDiscoveryService.Instance, logger: logger.Object);

        await transport.SendAsync(new { Secret = "secret-payload" }, FeatureFlagsEvpTransport.ExposureIntakePath, SerializerSettings);

        var log = logger.Invocations.Should().ContainSingle().Which;
        log.Method.Name.Should().Be(status == 400 ? "Error" : "ErrorSkipTelemetry");
        log.Arguments[0].Should().BeOfType<string>().Which.Should().Contain("after 1 attempt").And.Contain("will not be replayed").And.NotContain("secret");
        log.Arguments[1].Should().Be(FeatureFlagsEvpTransport.ExposureIntakePath);
        log.Arguments[2].Should().Be(status);
        direct.RequestsSent.Should().ContainSingle();
        local.RequestsSent.Should().BeEmpty();
    }

    [Theory]
    [InlineData("http://localhost:8126/")]
#if NET6_0_OR_GREATER
    [InlineData("unix:///tmp/dd-evp-redirect-test.socket")]
#endif
    public void LocalFactoriesDisableRedirectsWithoutChangingHistoricalDefaults(string agentUrl)
    {
        var exporter = CreateSettings((ConfigurationKeys.AgentUri, agentUrl)).Manager.InitialExporterSettings;
        var agentless = FeatureFlagsEvpTransport.CreateLocalRequestFactory(exporter);
        var historical = FeatureFlagsEvpTransport.CreateLocalRequestFactory(exporter, allowAutoRedirect: true);
#if NETCOREAPP3_1_OR_GREATER
        agentless.Should().BeAssignableTo<HttpClientRequestFactory>().Which.AllowAutoRedirect.Should().BeFalse();
        historical.Should().BeAssignableTo<HttpClientRequestFactory>().Which.AllowAutoRedirect.Should().BeTrue();
#else
        agentless.Should().BeOfType<ApiWebRequestFactory>().Which.AllowAutoRedirect.Should().BeFalse();
        historical.Should().BeOfType<ApiWebRequestFactory>().Which.AllowAutoRedirect.Should().BeTrue();
#endif
    }

    [Fact]
    public async Task LocalRedirectIsNotFollowedOrReplayedAndOnlyFutureBatchUsesDirect()
    {
        using var relay = new HttpListener();
        using var destination = new HttpListener();
        var relayUrl = $"http://127.0.0.1:{TcpPortProvider.GetOpenPort()}/";
        relay.Prefixes.Add(relayUrl);
        relay.Start();
        var destinationUrl = $"http://127.0.0.1:{TcpPortProvider.GetOpenPort()}/";
        destination.Prefixes.Add(destinationUrl);
        destination.Start();
        var receivedAtDestination = destination.GetContextAsync();
        var receivedAtRelay = relay.GetContextAsync();
        var local = FeatureFlagsEvpTransport.CreateLocalRequestFactory(CreateSettings((ConfigurationKeys.AgentUri, relayUrl)).Manager.InitialExporterSettings);
        var direct = CreateFactory("https://event-platform-intake.datadoghq.com/");
        using var transport = new FeatureFlagsEvpTransport(
            FeatureFlagsSource.Agentless,
            local,
            direct,
            new DiscoveryServiceMock(),
            initialLocalProxyEndpoint: FeatureFlagsEvpTransport.EventPlatformProxyV4);

        try
        {
            var send = transport.SendAsync(new object(), FeatureFlagsEvpTransport.ExposureIntakePath, SerializerSettings);
            (await Task.WhenAny(receivedAtRelay, Task.Delay(TimeSpan.FromSeconds(5)))).Should().BeSameAs(receivedAtRelay);
            var request = await receivedAtRelay;
            await request.Request.InputStream.CopyToAsync(Stream.Null);
            request.Request.Headers[TelemetryConstants.ApiKeyHeader].Should().BeNull();
            request.Request.Headers[FeatureFlagsEvpHeaderHelper.EvpOriginHeader].Should().Be("dd-trace-dotnet");
            request.Response.StatusCode = 307;
            request.Response.RedirectLocation = destinationUrl;
            request.Response.Close();

            await send;
            receivedAtDestination.IsCompleted.Should().BeFalse("the selected local route must never follow an HTTP redirect");
            direct.RequestsSent.Should().BeEmpty("a redirect does not prove that the current batch was not accepted");

            await transport.SendAsync(new object(), FeatureFlagsEvpTransport.FlagEvaluationIntakePath, SerializerSettings);
            direct.RequestsSent.Should().ContainSingle();
        }
        finally
        {
            destination.Close();
            try
            {
                await receivedAtDestination;
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException)
            {
            }
        }
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AcceptedNamedPipeEofDoesNotReplayAndChangesFutureRouting(bool hasDirectCredentials)
    {
        var pipeName = "evp" + Guid.NewGuid().ToString("N").Substring(0, 8);
        var settings = CreateSettings((ConfigurationKeys.TracesPipeName, pipeName));
        var local = FeatureFlagsEvpTransport.CreateLocalRequestFactory(settings.Manager.InitialExporterSettings);
        var direct = CreateFactory("https://event-platform-intake.mock-intake.invalid/");
        var now = new DateTimeOffset(2026, 10, 2, 0, 0, 0, TimeSpan.Zero);
        using var transport = new FeatureFlagsEvpTransport(
            FeatureFlagsSource.Agentless,
            local,
            hasDirectCredentials ? direct : null,
            new DiscoveryServiceMock(),
            initialLocalProxyEndpoint: FeatureFlagsEvpTransport.EventPlatformProxyV4,
            routeRecoveryCooldown: TimeSpan.FromMinutes(1),
            utcNow: () => now);
        using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        var received = ReadPipeRequest(server);
        var firstSend = transport.SendAsync(new { Batch = 1 }, FeatureFlagsEvpTransport.ExposureIntakePath, SerializerSettings);
        (await Task.WhenAny(received, Task.Delay(TimeSpan.FromSeconds(5)))).Should().BeSameAs(received);
        var request = await received;
        request.PathAndQuery.Should().Be("/evp_proxy/v4/api/v2/exposures");
        request.Headers.Contains("DD-API-KEY").Should().BeFalse();
        server.Disconnect();

        await firstSend;
        direct.RequestsSent.Should().BeEmpty("an accepted batch must not be replayed after EOF");

        var nextConnection = server.WaitForConnectionAsync();
        await transport.SendAsync(new { Batch = 2 }, FeatureFlagsEvpTransport.ExposureIntakePath, SerializerSettings);
        nextConnection.IsCompleted.Should().BeFalse("future batches use direct or wait for the unavailable cooldown");
        direct.RequestsSent.Should().HaveCount(hasDirectCredentials ? 1 : 0);

        if (!hasDirectCredentials)
        {
            now = now.AddMinutes(1);
            var recovery = transport.SendAsync(new { Batch = 3 }, FeatureFlagsEvpTransport.ExposureIntakePath, SerializerSettings);
            (await Task.WhenAny(nextConnection, Task.Delay(TimeSpan.FromSeconds(5)))).Should().BeSameAs(nextConnection);
            await nextConnection;
            var recoveredRequest = await MockHttpParser.ReadRequest(server);
            recoveredRequest.ReadStreamBody().Should().NotBeEmpty();
            var response = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n");
            await server.WriteAsync(response, 0, response.Length);
            await recovery;
            direct.RequestsSent.Should().BeEmpty();
        }
        else
        {
            server.Dispose();
            await Record.ExceptionAsync(() => nextConnection);
        }
    }

    [Theory]
    [InlineData(404)]
    [InlineData(405)]
    [InlineData(0)]
    public async Task DisposingTransportPreventsFallbackAfterLateLocalCompletion(int statusCode)
    {
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var local = CreateFactory("http://agent:8126/", uri => new DelayedFailureApiRequest(uri, statusCode, started, release));
        var direct = CreateFactory("https://event-platform-intake.mock-intake.invalid/");
        using var transport = CreateTransport(local, direct, initialLocalProxyEndpoint: FeatureFlagsEvpTransport.EventPlatformProxyV4);
        var send = transport.SendAsync(new object(), FeatureFlagsEvpTransport.ExposureIntakePath, SerializerSettings);
        await started.Task;

        transport.Dispose();
        release.TrySetResult(true);
        await send;

        local.RequestsSent.Should().ContainSingle();
        direct.RequestsSent.Should().BeEmpty("a late response or pre-send failure must not start new egress after disposal");
    }

    [Fact]
    public void ProductionDirectFactoryAttachesHeadersToItsRequest()
    {
        var settings = CreateSettings(
            (ConfigurationKeys.ApiKey, "test-api-key"),
            (ConfigurationKeys.Site, "mock-intake.invalid"));
        var factory = FeatureFlagsEvpTransport.CreateDirectRequestFactory(settings.FeatureFlags)!;
        var endpoint = factory.GetEndpoint(FeatureFlagsEvpTransport.ExposureIntakePath);
        var request = factory.Create(endpoint);

        // Inspect the real request/client, not GetDirectHeaders: omitting factory wiring must fail.
        // No TLS bypass or outbound request is needed for this construction regression.
#if NETCOREAPP3_1_OR_GREATER
        using var client = (HttpClient)typeof(HttpClientRequest).GetField("_client", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(request)!;
        var headers = client.DefaultRequestHeaders.ToDictionary(header => header.Key, header => header.Value.Single(), StringComparer.OrdinalIgnoreCase);
#else
        var webRequest = (HttpWebRequest)typeof(ApiWebRequest).GetField("_request", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(request)!;
        var headers = webRequest.Headers.AllKeys.ToDictionary(name => name!, name => webRequest.Headers[name]!, StringComparer.OrdinalIgnoreCase);
#endif
        endpoint.Should().Be(new Uri("https://event-platform-intake.mock-intake.invalid/api/v2/exposures"));
        headers.Should().Contain("DD-API-KEY", "test-api-key");
        headers.Should().Contain("DD-EVP-ORIGIN", "dd-trace-dotnet");
        headers.Should().Contain("DD-EVP-ORIGIN-VERSION", TracerConstants.ThreePartVersion);
        headers.Should().Contain("x-datadog-tracing-enabled", "false");
        headers.Should().NotContainKey("X-Datadog-EVP-Subdomain");
    }

    private static async Task<MockHttpRequest> ReadPipeRequest(NamedPipeServerStream server)
    {
        await server.WaitForConnectionAsync();
        var request = await MockHttpParser.ReadRequest(server);
        request.ReadStreamBody().Should().NotBeEmpty("the relay has accepted the full serialized batch before disconnecting");
        return request;
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

    private sealed class DelayedFailureApiRequest(
        Uri endpoint,
        int statusCode,
        TaskCompletionSource<bool> started,
        TaskCompletionSource<bool> release) : TestApiRequest(endpoint, statusCode)
    {
        private readonly bool _failBeforeSend = statusCode == 0;

        public override async Task<IApiResponse> PostAsJsonAsync<T>(T payload, MultipartCompression compression, JsonSerializerSettings settings)
        {
            started.TrySetResult(true);
            await release.Task.ConfigureAwait(false);
            if (_failBeforeSend)
            {
                throw new SocketException((int)SocketError.ConnectionRefused);
            }

            return await base.PostAsJsonAsync(payload, compression, settings).ConfigureAwait(false);
        }
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
}
