// <copyright file="DiscoveryServiceTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.TransportHelpers;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Agent;

public class DiscoveryServiceTests
{
    // using shorter retry values for faster tests
    // long recheck to ensure we're not hitting it accidentally
    private const int InitialRetryDelayMs = 50;
    private const int MaxRetryDelayMs = 50;
    private const int RecheckIntervalMs = 300_000;

    [Fact]
    public async Task HandlesFlakyConfiguration()
    {
        var mutex = new ManualResetEventSlim();
        var factory = new TestRequestFactory(
            x => new FaultyApiRequest(x),
            x => new TestApiRequest(x));

        var ds = new DiscoveryService(factory, InitialRetryDelayMs, MaxRetryDelayMs, RecheckIntervalMs);
        ds.SubscribeToChanges(x => mutex.Set());

        mutex.Wait(30_000).Should().BeTrue("Should raise subscription changes");

        await ds.DisposeAsync();
    }

    [Fact]
    public async Task ReturnsDeserializedConfig()
    {
        AgentConfiguration config = null;
        var clientDropP0s = true;
        var version = "1.26.3";
        var evpProxyEndpoint = "evp_proxy/v4";
        var mutex = new ManualResetEventSlim();
        var factory = new TestRequestFactory(
            x => new TestApiRequest(x, responseContent: GetConfig(clientDropP0s, version)));

        var ds = new DiscoveryService(factory, InitialRetryDelayMs, MaxRetryDelayMs, RecheckIntervalMs);
        ds.SubscribeToChanges(
            x =>
            {
                config = x;
                mutex.Set();
            });

        mutex.Wait(30_000).Should().BeTrue("Should raise subscription changes");
        config.Should().NotBeNull();
        config.AgentVersion.Should().Be(version);
        config.ConfigurationEndpoint.Should().NotBeNullOrEmpty();
        config.DebuggerEndpoint.Should().NotBeNullOrEmpty();
        config.DiagnosticsEndpoint.Should().NotBeNullOrEmpty();
        config.SymbolDbEndpoint.Should().NotBeNullOrEmpty();
        config.ClientDropP0s.Should().Be(clientDropP0s);
        config.StatsEndpoint.Should().NotBeNullOrEmpty();
        config.DataStreamsMonitoringEndpoint.Should().NotBeNullOrEmpty();
        config.EventPlatformProxyEndpoint.Should().Be(evpProxyEndpoint);
        await ds.DisposeAsync();
    }

    [Fact]
    public async Task DoesNotFireInitialCallbackIfInitialConfigNotFetched()
    {
        var notificationFired = false;
        var mutex = new ManualResetEventSlim();
        var factory = new TestRequestFactory(
            x =>
            {
                mutex.Wait(10_000).Should().BeTrue("Should make request to api");
                return new TestApiRequest(x, responseContent: GetConfig());
            });

        var ds = new DiscoveryService(factory, InitialRetryDelayMs, MaxRetryDelayMs, RecheckIntervalMs);
        ds.SubscribeToChanges(x => notificationFired = true);

        await Task.Delay(5_000); // should recheck 5 times in this duration
        notificationFired.Should().BeFalse();
        mutex.Set();

        await ds.DisposeAsync();
    }

    [Fact]
    public async Task FiresInitialCallbackIfInitialConfigAlreadyFetched()
    {
        int notificationCount = 0;
        var mutex = new ManualResetEventSlim();
        var factory = new TestRequestFactory(
            x =>
            {
                return new TestApiRequest(x, responseContent: GetConfig());
            },
            y => throw new Exception("Should not make a second request"));

        var ds = new DiscoveryService(factory, InitialRetryDelayMs, MaxRetryDelayMs, RecheckIntervalMs);
        // make sure we have config
        ds.SubscribeToChanges(x => mutex.Set());
        mutex.Wait(30_000).Should().BeTrue("Should make request to api");

        ds.SubscribeToChanges(x => Interlocked.Increment(ref notificationCount));
        Volatile.Read(ref notificationCount).Should().Be(1);

        await ds.DisposeAsync();
    }

    [Fact]
    public async Task DoesNotFireCallbackOnRecheckIfNoChangesToConfig()
    {
        int notificationCount = 0;
        var mutex1 = new ManualResetEventSlim();
        var mutex3 = new ManualResetEventSlim();
        var recheckIntervalMs = 1_000; // ms
        var factory = new TestRequestFactory(
            x =>
            {
                mutex1.Wait(10_000).Should().BeTrue("Should make request to api");
                return new TestApiRequest(x, responseContent: GetConfig());
            },
            x => new TestApiRequest(x, responseContent: GetConfig()),
            x =>
            {
                mutex3.Set();
                return new TestApiRequest(x, responseContent: GetConfig());
            });

        var ds = new DiscoveryService(factory, InitialRetryDelayMs, MaxRetryDelayMs, recheckIntervalMs);
        ds.SubscribeToChanges(x => Interlocked.Increment(ref notificationCount));
        // fire first request
        mutex1.Set();
        // wait for third request
        mutex3.Wait(30_000).Should().BeTrue("Should make third request to api");

        Volatile.Read(ref notificationCount).Should().Be(1); // initial

        await ds.DisposeAsync();
    }

    [Fact]
    public async Task FiresCallbackOnRecheckIfHasChangesToConfig()
    {
        var notificationCount = 0;
        var mutex1 = new ManualResetEventSlim();
        var mutex3 = new ManualResetEventSlim();
        var recheckIntervalMs = 1_000; // ms
        var factory = new TestRequestFactory(
            x =>
            {
                mutex1.Wait(10_000).Should().BeTrue("Should make request to api");
                return new TestApiRequest(x, responseContent: GetConfig(dropP0: true));
            },
            x => new TestApiRequest(x, responseContent: GetConfig(dropP0: false)),
            x =>
            {
                mutex3.Set();
                return new TestApiRequest(x, responseContent: GetConfig(dropP0: false));
            });

        var ds = new DiscoveryService(factory, InitialRetryDelayMs, MaxRetryDelayMs, recheckIntervalMs);
        ds.SubscribeToChanges(x => Interlocked.Increment(ref notificationCount));
        // fire first request
        mutex1.Set();
        // wait for third request
        mutex3.Wait(30_000).Should().BeTrue("Should make third request to api");

        Volatile.Read(ref notificationCount).Should().Be(2); // initial and second

        await ds.DisposeAsync();
    }

    [Fact]
    public async Task DoesNotFireAfterUnsubscribing()
    {
        var notificationCount = 0;
        var mutex1 = new ManualResetEventSlim();
        var mutex3 = new ManualResetEventSlim();

        var recheckIntervalMs = 1_000; // ms
        var factory = new TestRequestFactory(
            x =>
            {
                mutex1.Wait(10_000).Should().BeTrue("Should make request to api");
                return new TestApiRequest(x, responseContent: GetConfig(dropP0: true));
            },
            x => new TestApiRequest(x, responseContent: GetConfig(dropP0: false)),
            x =>
            {
                mutex3.Set();
                return new TestApiRequest(x, responseContent: GetConfig(dropP0: false));
            });

        var ds = new DiscoveryService(factory, InitialRetryDelayMs, MaxRetryDelayMs, recheckIntervalMs);

        ds.SubscribeToChanges(Callback);

        // fire first request
        mutex1.Set();
        // wait for third request
        mutex3.Wait(30_000).Should().BeTrue("Should make third request to api");

        Volatile.Read(ref notificationCount).Should().Be(1); // callback should only run once

        await ds.DisposeAsync();

        void Callback(AgentConfiguration x)
        {
            Interlocked.Increment(ref notificationCount);
            ds.RemoveSubscription(Callback);
        }
    }

    [Fact]
    public async Task DisposesInATimelyManner()
    {
        var mutex = new ManualResetEventSlim();

        var factory = new TestRequestFactory(
            x =>
            {
                mutex.Set();
                return new TestApiRequest(x, responseContent: GetConfig(dropP0: true));
            },
            x => new TestApiRequest(x, responseContent: GetConfig(dropP0: false)));

        var ds = new DiscoveryService(factory, InitialRetryDelayMs, MaxRetryDelayMs, RecheckIntervalMs);

        // should be inside recheck loop
        mutex.Wait(30_000).Should().BeTrue("Should make request to api");

        var dispose = ds.DisposeAsync();

        var task = await Task.WhenAny(dispose, Task.Delay(5_000));

        task.Should().Be(dispose, "Should dispose in a timely manner but took >5s");
    }

    [Fact]
    public void AgentConfigurationComparesByValue()
    {
        var config1 = new AgentConfiguration(
            configurationEndpoint: "ConfigurationEndpoint",
            debuggerEndpoint: "DebuggerEndpoint",
            diagnosticsEndpoint: "DiagnosticsEndpoint",
            symbolDbEndpoint: "symbolDbEndpoint",
            agentVersion: "AgentVersion",
            statsEndpoint: "StatsEndpoint",
            dataStreamsMonitoringEndpoint: "DataStreamsMonitoringEndpoint",
            eventPlatformProxyEndpoint: "eventPlatformProxyEndpoint",
            telemetryProxyEndpoint: "telemetryProxyEndpoint",
            tracerFlareEndpoint: "tracerFlareEndpoint",
            clientDropP0: false,
            spanMetaStructs: true);

        // same config
        var config2 = new AgentConfiguration(
            configurationEndpoint: "ConfigurationEndpoint",
            debuggerEndpoint: "DebuggerEndpoint",
            diagnosticsEndpoint: "DiagnosticsEndpoint",
            symbolDbEndpoint: "symbolDbEndpoint",
            agentVersion: "AgentVersion",
            statsEndpoint: "StatsEndpoint",
            dataStreamsMonitoringEndpoint: "DataStreamsMonitoringEndpoint",
            eventPlatformProxyEndpoint: "eventPlatformProxyEndpoint",
            telemetryProxyEndpoint: "telemetryProxyEndpoint",
            tracerFlareEndpoint: "tracerFlareEndpoint",
            clientDropP0: false,
            spanMetaStructs: true);

        // different
        var config3 = new AgentConfiguration(
            configurationEndpoint: "DIFFERENT",
            debuggerEndpoint: "DebuggerEndpoint",
            diagnosticsEndpoint: "DiagnosticsEndpoint",
            symbolDbEndpoint: "symbolDbEndpoint",
            agentVersion: "AgentVersion",
            statsEndpoint: "StatsEndpoint",
            dataStreamsMonitoringEndpoint: "DataStreamsMonitoringEndpoint",
            eventPlatformProxyEndpoint: "eventPlatformProxyEndpoint",
            telemetryProxyEndpoint: "telemetryProxyEndpoint",
            tracerFlareEndpoint: "tracerFlareEndpoint",
            clientDropP0: false,
            spanMetaStructs: true);

        config1.Equals(config2).Should().BeTrue();
        config1.Equals(config3).Should().BeFalse();
    }

    [Fact]
    public async Task HandlesFailuresInApiWithBackoff()
    {
        var mutex = new ManualResetEventSlim(initialState: false, spinCount: 0);
        var factory = new TestRequestFactory(
            _ => new ThrowingRequest(),
            _ => new ThrowingRequest(),
            _ => new ThrowingRequest(),
            _ => new ThrowingRequest(),
            _ => new ThrowingRequest(),
            _ => new ThrowingRequest(),
            _ => new ThrowingRequest());

        // These are the default values in the other constructor
        // but setting them explicitly here as it's the behaviour we're testing
        // not the exact values we choose later
        var ds = new DiscoveryService(factory, initialRetryDelayMs: 500, maxRetryDelayMs: 5_000, recheckIntervalMs: 30_000);
        ds.SubscribeToChanges(_ => mutex.Set());

        // wait for 0 + 500 + 1000 + 2000 + 4000 + 5000 ms (+ 2500 buffer).
        // should not be set
        mutex.Wait(15_000);

        await ds.DisposeAsync();
        // add some leeway in case of slowness
        factory.RequestsSent.Count.Should().BeInRange(3, 6, "Should make between 3 and 6 retries in 13s");
    }

    private string GetConfig(bool dropP0 = true, string version = null)
        => JsonConvert.SerializeObject(new MockTracerAgent.AgentConfiguration() { ClientDropP0s = dropP0, AgentVersion = version });

    internal class ThrowingRequest : TestApiRequest
    {
        public ThrowingRequest()
            : base(new Uri("http://localhost"))
        {
        }

        public override async Task<IApiResponse> GetAsync()
        {
            await Task.Yield();
            throw new WebException("Error in GetAsync");
        }

        public override async Task<IApiResponse> PostAsync(ArraySegment<byte> bytes, string contentType, string contentEncoding)
        {
            await Task.Yield();
            throw new WebException("Error in PostAsync");
        }
    }
}
